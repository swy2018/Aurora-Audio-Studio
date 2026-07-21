# Aurora Audio Studio 0.8.0 使用说明

给学音乐、做翻唱、做配音、做视频字幕的人用的本地 AI 音频工作台。

界面支持中文 / English 切换。右上角按钮可以切换语言。

你不需要会编程。这个软件的目标是把常用的本地 AI 音频工具放到一个窗口里：写歌、配音、歌声克隆、去人声、扒谱、视频字幕，都从左边菜单进入。

## 这个软件能做什么

### 1. 音乐创作

用文字描述你想要的音乐，然后生成歌曲、纯音乐、旋律或带人声风格的音乐。

适合：

- 写 demo 灵感
- 生成伴奏草稿
- 做旋律参考
- 尝试不同曲风、情绪、速度和人声风格

默认模型是 `ACE-Step 1.5`。如果你选择了其他音乐模型但还没安装，按钮会变成“安装此模型”。

模型下拉里也预留了 YuE、Stable Audio Open 等入口；没检测到时也可以选中并进入安装流程。

### 2. AI 配音与声音克隆

用参考声音生成旁白、角色对白或短句配音。

适合：

- 视频旁白
- 角色台词
- 中文配音
- 口播样音

默认模型是 `IndexTTS2`。模型下拉里也预留了 GPT-SoVITS、CosyVoice 等入口。

### 3. 歌声克隆

把一段歌声转换成另一个参考音色，尽量保留原来的旋律和唱法。

适合：

- 翻唱音色实验
- 声线参考
- 歌声转换测试

默认模型是 `Seed-VC 44.1k`。模型下拉里也预留了 RVC WebUI、DiffSinger 等入口。

注意：歌声克隆和普通配音不是一回事。配音主要适合“说话”，歌声克隆才更适合“唱歌”。

### 4. 去人声 / AI 分轨

把一首歌分离成两个主要结果：

- `Vocal`：纯人声
- `Instrumental / Accompaniment`：纯伴奏

适合：

- 做翻唱伴奏
- 提取人声参考
- 混音练习
- 后续 AI 扒谱或人声处理

### 5. AI 扒谱（MIDI）

把独唱、单乐器或旋律清晰的音频分析成 MIDI 和音符事件表。

适合：

- 听写旋律
- 快速扒主旋律
- 导入 DAW 继续编辑

提示：音频越干净、乐器越少，扒谱效果越好。复杂混音里很多乐器同时响，AI 会更容易听错。

### 6. 视频 AI 字幕

打开 Subtitle Edit 字幕工具，识别视频语音并生成带时间轴的字幕。

适合：

- 给视频自动打字幕
- 校对字幕时间轴
- 导出 SRT 字幕

从 Aurora 打开 Subtitle Edit 时，会自动切换为简体中文界面。

## 第一次使用

1. 双击 `Aurora Audio Studio.exe`。
2. 在左侧选择你要用的功能。
3. 如果模型没安装，选择模型后主按钮会变成“安装此模型”。
4. 点击“安装此模型”。
5. 选择一个安装位置。
6. 软件会在你选择的位置下面创建 `LocalAI` 文件夹。

例如你选择 `D:\AI工具`，实际模型会装到：

```text
D:\AI工具\LocalAI
```

不建议装到快满的系统盘。AI 模型通常很大，几十 GB 很正常。

## 设置输出位置

左下角有“设置输出位置”。

点击后选择一个文件夹，之后所有成品会自动分门别类放进去，例如：

```text
AI音乐
AI配音
AI歌声克隆
AI分轨
AI扒谱
AI字幕
```

如果不设置，默认会放到桌面的 `AI工作流` 文件夹里。

## 模型怎么选

每个功能页都有模型下拉框。

- 已安装的模型可以直接启动。
- 未安装的模型会显示“未安装”。
- 选中未安装模型后，主按钮会变成“安装此模型”。
- 下拉菜单里也可以点“安装当前选择模型...”。

安装模型可能很慢，因为模型文件很大，速度取决于网络。

## 使用建议

### 做音乐创作

提示词尽量写清楚：

```text
中文流行抒情歌，女声，慢速，钢琴和弦乐，情绪伤感，副歌有爆发力
```

比只写“来一首好听的歌”效果更稳定。

### 做配音

参考音频尽量：

- 没有背景音乐
- 没有混响
- 没有多人说话
- 音量清楚

参考音频越干净，克隆越像。

### 做歌声克隆

源歌声和参考人声都尽量干净。不要用杂音太多、伴奏太重的音频。

如果原曲有伴奏，建议先用“去人声 / AI分轨”提取人声，再做歌声克隆。

### 做 AI 扒谱

最好用：

- 清唱
- 单乐器旋律
- 主旋律很清楚的音频

不适合直接扒完整复杂编曲。

### 做字幕

打开字幕工作台后：

1. 打开视频文件。
2. 找到语音转文字 / 音频转文字功能。
3. 生成字幕。
4. 手动检查错字和时间轴。
5. 导出 SRT。

AI 字幕一定要校对，尤其是人名、术语、歌词和口音。

## 显存和性能

本地 AI 只有在你启动模型时才会占用显存。

不用的时候，可以点左下角：

```text
释放模型显存
```

这样会停止正在运行的模型服务。

软件不会开机自启动。

## 常见问题

### 为什么第一次启动很慢？

模型第一次加载、第一次下载、第一次生成都会比较慢。之后通常会快一些。

### 为什么安装模型很久？

AI 模型文件非常大，下载几十 GB 很常见。网络慢时可能需要很久。

### 为什么生成结果和我想的不完全一样？

AI 音频模型不是传统乐器或 DAW，它更像一个“创作助手”。提示词越清楚、素材越干净，结果越稳定。

### 为什么有些模型装了但还不能直接启动？

Aurora 会先提供安装入口。部分额外模型可能还需要后续接入专门启动器。默认模型优先保证可用。

### 可以把软件发给朋友吗？

可以把单文件版 `Aurora Audio Studio.exe` 发给朋友。

朋友第一次使用时：

1. 打开软件。
2. 选择功能。
3. 选择模型。
4. 点击“安装此模型”。
5. 选择安装位置。
6. 软件会在她选择的位置里创建 `LocalAI` 文件夹，不会默认塞到 C 盘。

不要把你电脑上的大模型文件夹随便混在桌面上。模型应该统一放在软件创建的 `LocalAI` 文件夹里。

## For English users

Aurora Audio Studio is a local-first Windows launcher for AI music and audio workflows.

- Use the language button in the top-right corner to switch between Chinese and English.
- Pick a feature from the left sidebar.
- Pick a model from the dropdown.
- If the model is missing, the main button becomes “Install this model”.
- Choose an install location. Aurora creates a `LocalAI` folder inside it.
- Choose an output location if needed. Otherwise outputs go to `Desktop\AI工作流`.

Supported workflow entries in version `0.8.0`:

- Music generation: ACE-Step 1.5 default, with installable entries for YuE and Stable Audio Open.
- AI voice and speaking voice cloning: IndexTTS2 default, with entries for GPT-SoVITS and CosyVoice.
- Singing voice clone: Seed-VC 44.1k default, with entries for RVC WebUI and DiffSinger.
- Vocal removal / stem separation: creates Vocal and Instrumental outputs.
- AI transcription: exports MIDI and note-event data.
- Video subtitles: opens Subtitle Edit / Faster-Whisper style workflow.

## 版权和素材提醒

请确认你有权使用输入的声音、歌曲、视频和参考素材。

如果用于公开发布或商业用途，尤其要注意：

- 歌曲版权
- 人声音色授权
- 视频版权
- 平台规则

AI 工具能提高效率，但不自动解决版权问题。
