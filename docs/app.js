const root = document.documentElement;
const languageButton = document.querySelector('.language-button');
const menuButton = document.querySelector('.menu-button');
const navLinks = document.querySelector('.nav-links');
const lightbox = document.querySelector('.lightbox');
const lightboxImage = lightbox.querySelector('img');

function setLanguage(language) {
  const english = language === 'en';
  root.lang = english ? 'en' : 'zh-CN';
  document.title = english ? 'Aurora Audio Studio | Local audio creation workspace' : 'Aurora Audio Studio | 本地音频创作工作台';
  languageButton.textContent = english ? '中文' : 'EN';
  try { localStorage.setItem('aurora-language', english ? 'en' : 'zh'); } catch { /* Private mode can disable storage. */ }
  document.querySelectorAll('[data-label-zh]').forEach(element => element.setAttribute('aria-label', english ? element.dataset.labelEn : element.dataset.labelZh));
  document.querySelectorAll('[data-text-zh]').forEach(element => { element.textContent = english ? element.dataset.textEn : element.dataset.textZh; });
  document.querySelectorAll('[data-alt-zh]').forEach(element => { element.alt = english ? element.dataset.altEn : element.dataset.altZh; });
  const selected = document.querySelector('.workspace-tabs [aria-selected="true"]');
  if (selected) updateWorkspaceAlt(selected);
}

let savedLanguage;
try { savedLanguage = localStorage.getItem('aurora-language'); } catch { /* Keep browser-language default. */ }
setLanguage(savedLanguage || (navigator.language.startsWith('zh') ? 'zh' : 'en'));
languageButton.addEventListener('click', () => setLanguage(root.lang === 'en' ? 'zh' : 'en'));

menuButton.addEventListener('click', () => {
  const open = !navLinks.classList.contains('open');
  navLinks.classList.toggle('open', open);
  menuButton.setAttribute('aria-expanded', String(open));
});
navLinks.querySelectorAll('a').forEach(link => link.addEventListener('click', () => {
  navLinks.classList.remove('open');
  menuButton.setAttribute('aria-expanded', 'false');
}));

const tabs = [...document.querySelectorAll('.workspace-tabs [role="tab"]')];
const workspaceImage = document.querySelector('.workspace-image img');
const workspaceButton = document.querySelector('.workspace-image');
const workspaceDescription = document.querySelector('.workspace-description');

function selectTab(tab) {
  tabs.forEach(item => { item.setAttribute('aria-selected', String(item === tab)); item.tabIndex = item === tab ? 0 : -1; });
  workspaceImage.src = tab.dataset.shot;
  updateWorkspaceAlt(tab);
  document.querySelector('#workspace-panel').setAttribute('aria-labelledby', tab.id);
  workspaceButton.dataset.lightbox = tab.dataset.shot;
  workspaceDescription.querySelector('.zh').textContent = tab.dataset.zh;
  workspaceDescription.querySelector('.en').textContent = tab.dataset.en;
}

function updateWorkspaceAlt(tab) {
  const english = root.lang === 'en';
  const label = tab.querySelector(english ? '.en' : '.zh').textContent.trim();
  document.querySelector('.workspace-image img').alt = english ? `Aurora ${label} workspace (earlier interface screenshot)` : `Aurora ${label}工作台（历史界面截图）`;
}

tabs.forEach((tab, index) => {
  tab.addEventListener('click', () => selectTab(tab));
  tab.addEventListener('keydown', event => {
    let next = index;
    if (event.key === 'ArrowRight') next = (index + 1) % tabs.length;
    else if (event.key === 'ArrowLeft') next = (index - 1 + tabs.length) % tabs.length;
    else if (event.key === 'Home') next = 0;
    else if (event.key === 'End') next = tabs.length - 1;
    else return;
    event.preventDefault();
    tabs[next].focus();
    selectTab(tabs[next]);
  });
});

document.querySelectorAll('[data-lightbox]').forEach(button => button.addEventListener('click', () => {
  lightboxImage.src = button.dataset.lightbox;
  lightboxImage.alt = button.querySelector('img')?.alt || 'Aurora Audio Studio';
  lightbox.showModal();
}));
document.querySelector('.lightbox-close').addEventListener('click', () => lightbox.close());
lightbox.addEventListener('click', event => { if (event.target === lightbox) lightbox.close(); });

async function loadCapabilities() {
  const body = document.querySelector('#model-capabilities');
  const features = { music: ['音乐创作', 'Music'], voice: ['配音与声音克隆', 'Voice'], singing: ['歌声转换', 'Singing'], separation: ['分轨', 'Separation'], transcription: ['MIDI 扒谱', 'Transcription'], subtitles: ['视频字幕', 'Subtitles'] };
  const modes = { 'embedded-workbench': ['嵌入式工作台', 'Embedded workbench'], 'native-task': ['原生任务', 'Native task'], 'download-only': ['仅下载管理', 'Download only'], 'external-editor': ['外部编辑器', 'External editor'], 'shared-runtime': ['共享运行组件', 'Shared runtime'] };
  function bilingual(cell, values) {
    ['zh', 'en'].forEach((language, index) => { const span = document.createElement('span'); span.className = language; span.textContent = values[index]; cell.append(span); });
  }
  try {
    const response = await fetch('capabilities.json');
    if (!response.ok) throw new Error('Capabilities unavailable');
    const data = await response.json();
    for (const model of data.models) {
      const row = body.insertRow();
      bilingual(row.insertCell(), [model.name, model.nameEn]);
      bilingual(row.insertCell(), features[model.feature]);
      bilingual(row.insertCell(), modes[model.mode]);
      const license = row.insertCell();
      const link = document.createElement('a');
      link.textContent = model.license;
      link.href = /^https:\/\//.test(model.source) ? model.source.replace(/\.git$/, '') : model.source?.includes('/') ? `https://huggingface.co/${model.source}` : `https://pypi.org/project/${model.source}/`;
      license.append(link);
    }
  } catch { /* Keep the static data link visible if the table cannot load. */ }
}
loadCapabilities();

if ('IntersectionObserver' in window && !matchMedia('(prefers-reduced-motion: reduce)').matches) {
  const observer = new IntersectionObserver(entries => {
    entries.forEach(entry => {
      if (!entry.isIntersecting) return;
      entry.target.classList.add('visible');
      entry.target.classList.remove('pending');
      observer.unobserve(entry.target);
    });
  }, { threshold: .12 });
  document.querySelectorAll('.reveal').forEach(element => {
    element.classList.add('pending');
    observer.observe(element);
  });
}
