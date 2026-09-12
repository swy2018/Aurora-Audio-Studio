import { chooseDownload } from './release-policy.mjs';
const $ = selector => document.querySelector(selector);
const featureData = {
music: [['从文字到完整歌曲','From words to a complete song'],['输入创作描述、歌词与风格，在本地模型工作台中完成创作。','Describe the piece, add lyrics and style, and create in a local model workbench.'],['文字、歌词与风格','Text, lyrics, and style'],'ACE-Step 1.5 XL Turbo',['音频文件','Audio files']],
voice: [['给文字一个声音','Give your words a voice'],['选择专业音色、设计新声音，或使用有授权的参考音频进行声音克隆。','Choose a preset voice, design a voice, or clone from an authorized reference recording.'],['文字与可选参考音频','Text and optional reference audio'],'Qwen3-TTS / F5-TTS',['配音音频','Speech audio']],
singing: [['保留演唱，转换音色','Keep the performance. Change the voice.'],['准备源歌曲与有授权的参考音频，在歌声工作台中进行音色转换。','Use a source performance and an authorized reference to convert the singing voice.'],['源歌曲与参考音频','Song and reference audio'],'Seed-VC 44.1k',['转换后的音频','Converted audio']],
separation: [['把混音拆开，继续创作','Separate the mix. Keep creating.'],['从人声与伴奏二轨到多乐器分轨，按任务选择引擎，在成品库导出独立音轨。','Choose vocals and accompaniment or multiple instrument stems, then export the separate tracks.'],['音频或视频素材','Audio or video'],'BS-RoFormer / Demucs',['独立 WAV 音轨','Separate WAV stems']],
transcription: [['从演奏到 MIDI','From a performance to MIDI'],['选择适合钢琴、旋律或多乐器的模型，生成 MIDI，再用你的音乐软件继续编辑。','Choose piano, melody, or multi-instrument transcription, then edit the MIDI in your music software.'],['演奏录音','Recorded performance'],'TransKun / YourMT3+ / Piano / Basic Pitch',['标准 MIDI','Standard MIDI']],
subtitles: [['把语音变成时间轴','Turn speech into a timeline'],['识别音视频里的语音，生成字幕；预览后保存副本，或交给 Subtitle Edit 校对。','Transcribe audio or video into subtitles, then save a reviewed copy or edit in Subtitle Edit.'],['音频或视频素材','Audio or video'],'Whisper / Subtitle Edit',['SRT 与转写数据','SRT and transcription data']]
};
let language = 'zh', feature = 'music', releases = null, releaseError = false, models = [];
try { language = localStorage.getItem('aurora-language') === 'en' ? 'en' : 'zh'; } catch { /* Browser storage is optional. */ }
const t = (zh,en) => language === 'en' ? en : zh;
function renderFeature() {
const row = featureData[feature], index = language === 'en' ? 1 : 0;
$('#workflow-title').textContent = row[0][index]; $('#workflow-description').textContent = row[1][index];
$('#workflow-input').textContent = row[2][index]; $('#workflow-engine').textContent = row[3]; $('#workflow-output').textContent = row[4][index];
$('#workflow-image').src = 'assets/mac-2.0-' + feature + '.png';
$('#workflow-image').alt = t('Aurora Mac 实机界面：','Real Aurora Mac screen: ') + $('#tab-' + feature).textContent;
$('#workflow-image-link').href = $('#workflow-image').getAttribute('src');
$('#workflow-panel').setAttribute('aria-labelledby','tab-' + feature);
document.querySelectorAll('[role=tab]').forEach(tab => { const selected = tab.dataset.feature === feature; tab.setAttribute('aria-selected',String(selected)); tab.tabIndex = selected ? 0 : -1; });
}
function renderDownloads() {
const channel = $('#download-channel').value;
for (const platform of ['windows','mac']) {
const link = $('#download-' + platform), checksum = $('#checksum-' + platform), status = $('#' + platform + '-version');
link.href = 'https://github.com/swy2018/Aurora-Audio-Studio/releases'; checksum.hidden = true;
link.textContent = t('查看 ' + (platform === 'mac' ? 'Mac' : 'Windows') + ' 发布包','View ' + (platform === 'mac' ? 'Mac' : 'Windows') + ' releases');
if (!releases) { status.textContent = releaseError ? t('暂时无法读取发布信息，请打开 GitHub 查看。','Release information is unavailable. Open GitHub to check.') : t('正在读取 GitHub 发布信息…','Loading GitHub releases…'); continue; }
const selected = chooseDownload(releases, platform, channel);
if (!selected) { status.textContent = t('此通道暂未提供完整的平台安装包。','No complete package is available for this platform and channel.'); continue; }
link.href = selected.url; link.textContent = t('下载 ','Download ') + selected.version;
checksum.href = selected.checksum; checksum.hidden = false;
status.textContent = (selected.beta ? 'Beta · ' : t('正式版 · ','Stable · ')) + selected.name;
}
}
function renderModels() {
const body = $('#models-body'); body.replaceChildren();
const mode = value => ({'embedded-workbench':t('内嵌工作台','Workbench'),'native-task':t('内置处理工具','Built-in tools'),'download-only':t('仅模型管理','Management only'),'shared-runtime':t('运行组件','Runtime component'),'external-editor':t('外部编辑器','External editor')})[value] || value;
for (const model of models) {
const row = document.createElement('tr');
const macMode = ['minimax-music3','faster-whisper'].includes(model.id) ? t('暂不支持 Mac','Not available on Mac') : mode(model.mode);
for (const text of [language === 'en' ? model.nameEn : model.name, mode(model.mode), macMode, model.license]) { const cell = document.createElement('td'); cell.textContent = text; row.append(cell); }
body.append(row);
}
}
function setLanguage(value) {
language = value; document.documentElement.lang = value === 'en' ? 'en' : 'zh-CN';
document.querySelectorAll('[data-zh]').forEach(el => el.textContent = el.dataset[value]);
document.querySelectorAll('[data-alt-zh]').forEach(el => el.alt = value === 'en' ? el.dataset.altEn : el.dataset.altZh);
$('#language').textContent = value === 'en' ? '中文' : 'EN';
try { localStorage.setItem('aurora-language',value); } catch { /* No persistent storage required. */ }
renderFeature(); renderDownloads(); renderModels();
}
$('#language').addEventListener('click',() => setLanguage(language === 'zh' ? 'en' : 'zh'));
const tabs = [...document.querySelectorAll('[role=tab]')];
tabs.forEach((tab,index) => {
tab.addEventListener('click',() => { feature = tab.dataset.feature; renderFeature(); });
tab.addEventListener('keydown',event => { let next; if(event.key === 'ArrowRight') next=(index+1)%tabs.length; if(event.key === 'ArrowLeft') next=(index+tabs.length-1)%tabs.length; if(event.key === 'Home') next=0; if(event.key === 'End') next=tabs.length-1; if(next!==undefined){event.preventDefault();feature=tabs[next].dataset.feature;renderFeature();tabs[next].focus();} });
});
$('#download-channel').addEventListener('change',renderDownloads);
setLanguage(language);
fetch('https://api.github.com/repos/swy2018/Aurora-Audio-Studio/releases?per_page=100',{signal:AbortSignal.timeout(15000)})
.then(response => {if(!response.ok) throw new Error('Release request failed');return response.json();})
.then(data => { if(!Array.isArray(data)) throw new Error('Invalid release response'); releases=data;renderDownloads(); })
.catch(() => {releaseError=true;renderDownloads();});
fetch('capabilities.json').then(response => {if(!response.ok) throw new Error('Catalog request failed'); return response.json();})
.then(data => {models=data.models;renderModels();}).catch(() => {$('#model-status').append(document.createTextNode(t(' · 读取失败，可直接打开模型数据。',' · Could not load; open the data link directly.')));});
