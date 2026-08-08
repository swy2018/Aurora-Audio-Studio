const root = document.documentElement;
const languageButton = document.querySelector('.language-button');
const menuButton = document.querySelector('.menu-button');
const navLinks = document.querySelector('.nav-links');
const lightbox = document.querySelector('.lightbox');
const lightboxImage = lightbox.querySelector('img');

function setLanguage(language) {
  const english = language === 'en';
  root.lang = english ? 'en' : 'zh-CN';
  languageButton.textContent = english ? '中文' : 'EN';
  localStorage.setItem('aurora-language', english ? 'en' : 'zh');
}

setLanguage(localStorage.getItem('aurora-language') || (navigator.language.startsWith('zh') ? 'zh' : 'en'));
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
  tabs.forEach(item => item.setAttribute('aria-selected', String(item === tab)));
  workspaceImage.src = tab.dataset.shot;
  workspaceImage.alt = root.lang === 'en' ? `${tab.textContent.trim()} workspace in Aurora Audio Studio` : `Aurora Audio Studio ${tab.textContent.trim()}工作台`;
  workspaceButton.dataset.lightbox = tab.dataset.shot;
  workspaceDescription.querySelector('.zh').textContent = tab.dataset.zh;
  workspaceDescription.querySelector('.en').textContent = tab.dataset.en;
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
  lightbox.showModal();
}));
document.querySelector('.lightbox-close').addEventListener('click', () => lightbox.close());
lightbox.addEventListener('click', event => { if (event.target === lightbox) lightbox.close(); });

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
