(() => {
  if (location.hostname !== '127.0.0.1' || !['7860','7861','7862'].includes(location.port)) return;
  window.auroraLocalizationObserver?.disconnect();
  const token = {};
  window.auroraLocalizationToken = token;
  const style = document.getElementById('aurora-workbench-style') || document.createElement('style');
  style.id = 'aurora-workbench-style';
  style.textContent = __AURORA_STYLE__;
  if (!style.isConnected) document.head.appendChild(style);
  const data = __AURORA_TRANSLATIONS__;
  const index = __AURORA_LANGUAGE_INDEX__;
  const lookup = new Map();
  const patterns = [];
  for (const [source, translations] of Object.entries(data)) {
    lookup.set(source.trim(), translations[index]);
    for (const text of translations) if (text) lookup.set(text.trim(), translations[index]);
    for (const text of [source, ...translations]) if (text.includes('{0}')) {
      const escape = part => part.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
      patterns.push([new RegExp('^' + text.trim().split('{0}').map(escape).join('(.+?)') + '$'), translations[index]]);
    }
  }
  function translate(text) {
    const key = text.trim();
    let value = lookup.get(key);
    if (!value) for (const [pattern, target] of patterns) {
      const match = pattern.exec(key);
      if (match) { value = target.replaceAll('{0}', lookup.get(match[1]) || match[1]); break; }
    }
    return value && value !== key ? text.replace(key, value) : text;
  }
  function apply(root) {
    // Upstream's "50~100" text is accidentally parsed as Markdown strikethrough.
    for (const paragraph of root.querySelectorAll('p')) if (paragraph.querySelector('del') && lookup.has(paragraph.textContent.trim())) paragraph.textContent = translate(paragraph.textContent);
    const walker = document.createTreeWalker(root, NodeFilter.SHOW_TEXT);
    while (walker.nextNode()) {
      const node = walker.currentNode;
      if (node.parentElement?.closest('textarea,input,pre,code,[contenteditable=true]')) continue;
      const value = translate(node.textContent);
      if (value !== node.textContent) node.textContent = value;
    }
    for (const element of root.querySelectorAll('[placeholder],[aria-label],[title],[alt]'))
      for (const attribute of ['placeholder','aria-label','title','alt']) {
        const value = element.getAttribute(attribute);
        if (value) { const translated = translate(value); if (translated !== value) element.setAttribute(attribute, translated); }
      }
    // Gradio's output-only status fields are UI messages, not editable user text.
    for (const field of root.querySelectorAll('textarea[readonly],input[readonly],textarea[disabled],input[role=listbox]')) {
      if (field === document.activeElement) continue;
      const translated = translate(field.value);
      if (translated !== field.value) field.value = translated;
    }
    // Upstream Qwen prints the same disclaimer twice, once in each language.
    // Keep one translated paragraph without altering any form option or user data.
    for (const item of root.querySelectorAll('.prose li')) {
      const previous = item.previousElementSibling;
      if (previous?.tagName === 'LI' && item.textContent.trim().length > 200 &&
          previous.textContent.trim() === item.textContent.trim() && lookup.has(item.textContent.trim())) item.hidden = true;
    }
  }
  function start() {
    if (!document.body) return;
    apply(document.body);
    document.title = translate(document.title);
    let scheduled = false;
    const observer = new MutationObserver(() => {
      if (scheduled) return;
      scheduled = true;
      setTimeout(() => { if (window.auroraLocalizationToken !== token) return; observer.disconnect(); apply(document.body); scheduled = false; observer.observe(document.body, {childList:true,subtree:true,characterData:true}); }, 80);
    });
    window.auroraLocalizationObserver = observer;
    observer.observe(document.body, {childList:true,subtree:true,characterData:true});
    document.documentElement.lang = ['zh-CN','zh-TW','en','ja'][index];
  }
  if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', start, {once:true}); else start();
})();
