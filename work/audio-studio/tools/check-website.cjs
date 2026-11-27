// Headless local-site regression. Uses installed Playwright; no external traffic.
const fs = require('node:fs');
const path = require('node:path');
const http = require('node:http');
const { chromium } = require(process.env.PLAYWRIGHT_MODULE || 'playwright');
(async () => {
  const root = path.resolve('docs');
  const out = path.resolve('output/playwright/aurora210-website');
  fs.mkdirSync(out, { recursive: true });
  const server = http.createServer((request, response) => {
    const name = new URL(request.url, 'http://localhost').pathname;
    const file = path.resolve(root, '.' + decodeURIComponent(name === '/' ? '/index.html' : name));
    if (!file.startsWith(root + path.sep) || !fs.existsSync(file) || !fs.statSync(file).isFile()) { response.writeHead(404).end(); return; }
    const types = { '.html':'text/html', '.js':'text/javascript', '.mjs':'text/javascript', '.json':'application/json', '.css':'text/css', '.jpg':'image/jpeg', '.png':'image/png', '.svg':'image/svg+xml' };
    response.setHeader('Content-Type', types[path.extname(file)] || 'application/octet-stream');
    fs.createReadStream(file).pipe(response);
  });
  await new Promise(resolve => server.listen(0, '127.0.0.1', resolve));
  const origin = 'http://127.0.0.1:' + server.address().port;
  let browser;
  const results = [];
  try {
    browser = await chromium.launch({ channel:'msedge', headless:true });
    for (const [width, height] of [[1440,1000], [390,844]]) {
      const context = await browser.newContext({ viewport: { width, height } });
      await context.route('**/*', route => new URL(route.request().url()).origin === origin ? route.continue() : route.abort());
      const page = await context.newPage(); const errors = [];
      page.on('pageerror', error => errors.push(error.message));
      await page.goto(origin, { waitUntil:'networkidle' });
      for (const language of ['zh', 'en']) {
        if (language === 'en') await page.locator('#language').click();
        await page.locator('#tab-transcription').click();
        await page.locator('#workflow-description').waitFor();
        const text = await page.locator('#workflow-description').innerText();
        if (!text.includes('MusicXML') || !text.includes('Windows 2.1 Beta')) throw Error('Score copy is absent');
        const overflow = await page.evaluate(() => document.documentElement.scrollWidth > innerWidth + 1);
        if (overflow) throw Error('Horizontal page overflow at ' + width);
        await page.locator('#workflow-panel').scrollIntoViewIfNeeded();
        const screenshot = path.join(out, `${width}-${language}.png`);
        await page.screenshot({ path:screenshot });
        await page.locator('#download-channel').selectOption('stable');
        for (const platform of ['windows','mac']) {
          const href = await page.locator('#download-' + platform).getAttribute('href');
          if (!href.includes('/v2.0.1/')) throw Error('Stable download changed: ' + href);
        }
        await page.locator('#download-channel').selectOption('beta');
        for (const platform of ['windows','mac']) {
          if (await page.locator('#checksum-' + platform).isHidden()) throw Error('Missing beta checksum');
        }
        results.push({ width, language, screenshot, horizontalOverflow:false, stable:'2.0.1', betaSource:'existing verified local snapshot' });
      }
      if (errors.length) throw Error(errors.join('\n'));
      await context.close();
    }
    fs.writeFileSync(path.join(out,'results.json'), JSON.stringify({passed:true, scope:'Local rendering and snapshot fallback; not live release acceptance', results}, null, 2));
    console.log(JSON.stringify({passed:true, checks:results.length, evidence:out}));
  } finally {
    if (browser) await browser.close();
    await new Promise(resolve => server.close(resolve));
  }
})().catch(error => { console.error(error); process.exitCode=1; });
