// Use the existing Playwright library when the optional CLI is unavailable.
// The browser is headless, has its own profile, and cannot contact the network.
const fs = require('node:fs');
const path = require('node:path');
const { chromium } = require(process.env.PLAYWRIGHT_MODULE || 'playwright');
(async () => {
  const [source, output, profile] = process.argv.slice(2);
  fs.mkdirSync(path.dirname(output), { recursive: true });
  const browser = await chromium.launchPersistentContext(path.resolve(profile), { channel: 'msedge', headless: true, viewport: { width: 1260, height: 1782 } });
  try {
    await browser.route('**/*', route => route.abort());
    const page = browser.pages()[0];
    const svg = fs.readFileSync(source, 'utf8');
    await page.setContent('<!doctype html><meta charset="utf-8"><meta http-equiv="Content-Security-Policy" content="default-src \'none\'; font-src data:; style-src \'unsafe-inline\'"><style>body{margin:0;background:white}svg{width:1260px;height:auto;display:block}</style>' + svg);
    await page.evaluate(() => document.fonts.ready);
    await page.screenshot({ path: output, fullPage: true });
    console.log(JSON.stringify({ source, screenshot: output, network: 'blocked', headless: true }));
  } finally { await browser.close(); }
})().catch(error => { console.error(error); process.exitCode = 1; });
