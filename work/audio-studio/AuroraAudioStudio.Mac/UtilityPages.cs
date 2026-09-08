using System.Net;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using AuroraAudioStudio.Core;

namespace AuroraAudioStudio.Mac;

public sealed partial class MainWindow
{
    private LocalWorkbenchServer? utilityPreviewServer;
    private readonly Dictionary<string, string> utilitySelection = [];
    private readonly Dictionary<string, List<string>> utilityLogs = [];

    private Control UtilityPage(string feature)
    {
        var draft = workspace.Drafts[feature];
        if (!utilityLogs.ContainsKey(feature)) utilityLogs[feature] = [];
        var logs = utilityLogs[feature];
        var titleKey = feature switch { "separation" => "新建分轨任务", "transcription" => "新建扒谱任务", _ => "新建字幕任务" };
        var outputHint = feature switch { "separation" => "输出为可继续混音的独立 WAV 音轨。", "transcription" => "处理结果会保存为标准 MIDI 文件。", _ => "处理结果会保存到视频字幕成品目录。" };
        var form = VStack(Txt(L(titleKey), 22, true), Txt(text[feature + "Desc"]));
        var sourceList = new ListBox { MinHeight = 112, MaxHeight = 180, HorizontalAlignment = HorizontalAlignment.Stretch };
        AutomationProperties.SetAutomationId(sourceList, "utility-sources");
        sourceList.ItemsSource = draft.Sources.Select(p => Path.GetFileName(p) + (File.Exists(p) ? "  ·  " + Path.GetExtension(p).TrimStart('.').ToUpperInvariant() : "  ·  " + text["missingSource"])).ToArray();
        var preview = new Grid { MinHeight = 140 };
        var emptyPreview = VStack(Txt("♫", 26, true), Txt(L("选择素材后可在此预览"), 12));
        foreach (var label in emptyPreview.Children.OfType<TextBlock>()) label.TextAlignment = TextAlignment.Center;
        emptyPreview.VerticalAlignment = VerticalAlignment.Center; emptyPreview.HorizontalAlignment = HorizontalAlignment.Center;
        preview.Children.Add(emptyPreview);
        void Preview(string? path)
        {
            utilityPreviewServer?.Dispose(); utilityPreviewServer = null;
            preview.Children.Clear();
            if (path is null || !File.Exists(path)) { preview.Children.Add(emptyPreview); return; }
            var mime = Path.GetExtension(path).ToLowerInvariant() switch
            { ".wav" => "audio/wav", ".mp3" => "audio/mpeg", ".m4a" => "audio/mp4", ".mp4" => "video/mp4", ".mov" => "video/quicktime", _ => "" };
            if (mime.Length == 0) { preview.Children.Add(Txt(text["previewUnsupported"], 12)); return; }
            utilityPreviewServer = new LocalWorkbenchServer();
            var server = utilityPreviewServer;
            var media = server.AddFile(path, mime);
            var tag = mime.StartsWith("video") ? "video" : "audio";
            var html = "<!doctype html><meta charset='utf-8'><style>body{margin:0;padding:12px;background:#eff7f3;color:#17241f;font:13px system-ui}audio,video{width:100%;max-height:180px}p{overflow-wrap:anywhere}</style><p>" + WebUtility.HtmlEncode(Path.GetFileName(path)) + "</p><" + tag + " controls preload='metadata' src='" + media.AbsoluteUri + "'></" + tag + ">";
            html = html.Replace("#eff7f3", CssColor(Pane)).Replace("#17241f", CssColor(Ink));
            var view = new StudioWebView { MinHeight = tag == "video" ? 210 : 140 };
            view.NavigationStarted += (_, e) => e.Cancel = !server.Owns(e.Request);
            view.NewWindowRequested += (_, e) => e.Handled = true;
            preview.Children.Add(view); view.Source = server.AddPage(html);
        }
        sourceList.SelectionChanged += (_, _) =>
        {
            if (sourceList.SelectedIndex < 0 || sourceList.SelectedIndex >= draft.Sources.Count) return;
            draft.Source = draft.Sources[sourceList.SelectedIndex]; utilitySelection[feature] = draft.Source; Preview(draft.Source);
        };
        var add = ActionButton(L("添加素材"), async () =>
        {
            var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions { Title = L("添加素材"), AllowMultiple = true,
                FileTypeFilter = [new FilePickerFileType("Audio / Video") { Patterns = ["*.wav", "*.flac", "*.mp3", "*.m4a", "*.mp4", "*.mov", "*.aac", "*.ogg"], AppleUniformTypeIdentifiers = ["public.audio", "public.movie"] }] });
            var added = 0;
            foreach (var file in files)
            {
                var path = file.TryGetLocalPath();
                if (path is null || draft.Sources.Contains(path, StringComparer.Ordinal)) continue;
                draft.Sources.Add(path); added++;
            }
            if (added == 0) return;
            draft.Source = draft.Sources[0]; logs.Add(DateTime.Now.ToString("HH:mm:ss") + "  " + string.Format(text["sourcesAdded"], added));
            workspace.SaveDrafts(); RenderPage();
        }, "utility-add-sources");
        var clear = ActionButton(L("清空"), () => { draft.Sources.Clear(); draft.Source = ""; utilitySelection.Remove(feature); workspace.SaveDrafts(); RenderPage(); return Task.CompletedTask; }, "utility-clear-sources");
        var sourceHeading = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto,Auto"), ColumnSpacing = 10, MinHeight = 40 };
        var sourceTitle = Txt(L("源文件与批处理"), 15, true); sourceTitle.VerticalAlignment = VerticalAlignment.Center;
        sourceHeading.Children.Add(sourceTitle);
        add.VerticalAlignment = VerticalAlignment.Center; clear.VerticalAlignment = VerticalAlignment.Center;
        Grid.SetColumn(add, 1); Grid.SetColumn(clear, 2); sourceHeading.Children.Add(add); sourceHeading.Children.Add(clear);
        var sources = VStack(sourceHeading, sourceList, Txt(text["batchSourcesHint"], 12));
        var sourceGrid = new Grid { ColumnDefinitions = new ColumnDefinitions("3*,2*"), RowDefinitions = new RowDefinitions("Auto,Auto"), ColumnSpacing = 18, RowSpacing = 12 };
        sourceGrid.Children.Add(sources);
        var previewPanel = Panel(preview, "#EFF7F3"); previewPanel.Padding = new Thickness(10); Grid.SetColumn(previewPanel, 1); sourceGrid.Children.Add(previewPanel);
        form.Children.Add(sourceGrid);
        form.Children.Add(Rule()); form.Children.Add(Txt(L("处理引擎"), 15, true));
        var models = workspace.Catalog.Definitions.Where(m => m.Feature == feature && m.IsRunnable && workspace.Runtime.Models.ContainsKey(m.Id)).ToArray();
        var model = new ComboBox { ItemsSource = models.Select(workspace.Catalog.DisplayName).ToArray(), SelectedIndex = Math.Max(0, Array.FindIndex(models, m => m.Id == draft.ModelId)), HorizontalAlignment = HorizontalAlignment.Stretch };
        AutomationProperties.SetAutomationId(model, "utility-model");
        model.SelectionChanged += (_, _) => { if (model.SelectedIndex >= 0) draft.ModelId = models[model.SelectedIndex].Id; };
        var presetKeys = new[] { "fast", "recommended", "quality" };
        var preset = new ComboBox { ItemsSource = new[] { L("快速草稿"), L("推荐质量"), L("高质量") }, SelectedIndex = Math.Max(0, Array.IndexOf(presetKeys, draft.Option)), HorizontalAlignment = HorizontalAlignment.Stretch };
        AutomationProperties.SetAutomationId(preset, "utility-preset");
        var track = new ComboBox { ItemsSource = new[] { L("二轨：人声 + 伴奏"), L("多轨：完整分轨") }, SelectedIndex = draft.TrackMode == "multi-stem" ? 1 : 0, HorizontalAlignment = HorizontalAlignment.Stretch };
        AutomationProperties.SetAutomationId(track, "utility-track-mode");
        void ApplyPreset()
        {
            if (preset.SelectedIndex < 0 || track.SelectedIndex < 0) return;
            draft.Option = presetKeys[preset.SelectedIndex]; draft.TrackMode = track.SelectedIndex == 1 ? "multi-stem" : "two-stem";
            draft.ModelId = UtilityPresets.Model(feature, draft.TrackMode, draft.Option);
            model.SelectedIndex = Array.FindIndex(models, m => m.Id == draft.ModelId);
        }
        preset.SelectionChanged += (_, _) => ApplyPreset(); track.SelectionChanged += (_, _) => ApplyPreset();
        var modelRow = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), ColumnSpacing = 12 };
        model.VerticalAlignment = VerticalAlignment.Center; modelRow.Children.Add(model);
        var manageModel = ActionButton(L("模型管理"), () => { Navigate("models"); return Task.CompletedTask; });
        manageModel.VerticalAlignment = VerticalAlignment.Center; Grid.SetColumn(manageModel, 1); modelRow.Children.Add(manageModel);
        form.Children.Add(modelRow);
        var options = new Grid { ColumnDefinitions = new ColumnDefinitions(feature == "separation" ? "*,*" : "*"), ColumnSpacing = 12 };
        if (feature == "separation") options.Children.Add(Field(L("分轨模式"), track));
        var presetField = Field(L("处理预设"), preset); if (feature == "separation") Grid.SetColumn(presetField, 1); options.Children.Add(presetField); form.Children.Add(options);
        if (feature == "subtitles")
        {
            var keys = new[] { "auto", "zh", "en", "ja", "ko", "fr", "de", "es" };
            var language = new ComboBox { ItemsSource = new[] { L("自动识别"), L("中文"), "English", "日本語", "한국어", "Français", "Deutsch", "Español" }, SelectedIndex = Math.Max(0, Array.IndexOf(keys, draft.SourceLanguage)), HorizontalAlignment = HorizontalAlignment.Stretch };
            AutomationProperties.SetAutomationId(language, "utility-source-language");
            language.SelectionChanged += (_, _) => { if (language.SelectedIndex >= 0) draft.SourceLanguage = keys[language.SelectedIndex]; };
            form.Children.Add(Field(L("素材语言"), language));
        }
        form.Children.Add(Txt(L(outputHint), 12));
        var run = ActionButton(L("开始处理"), () => RunUtilityAsync(feature)); run.Classes.Add("primary");
        void UpdateRun() => run.IsEnabled = draft.Sources.Count > 0 && workspace.Engines.IsAvailable(draft.ModelId) && !utilityRunning.Contains(feature) && modelOperation is null && !workspace.Settings.Current.SafeMode;
        model.SelectionChanged += (_, _) => UpdateRun(); UpdateRun();
        AutomationProperties.SetAutomationId(run, "utility-run");
        var buttons = new WrapPanel();
        foreach (var item in new Control[] { run, ActionButton(L("打开成品目录"), () => OpenPathAsync(workspace.Settings.Current.OutputRoot)), ActionButton(text["saveProject"], async () => { await workspace.SaveProjectAsync(draft); status.Text = text["saved"]; }, "save-utility-project") })
        { item.Margin = new Thickness(0, 0, 10, 6); buttons.Children.Add(item); }
        form.Children.Add(buttons);
        if (!workspace.Engines.IsAvailable(draft.ModelId)) form.Children.Add(Txt(L("请先在模型管理中安装所选模型。"), 12));
        form.Children.Add(Panel(VStack(Txt(L("在 Aurora 中完成"), 15, true), Txt(L("素材、处理过程与结果路径都留在同一个工作区；关闭应用时，Aurora 会安全结束由它启动的任务。"))), "#EFF7F3"));
        var logPanel = VStack(Txt(L("任务动态"), 18, true), Txt(L("当前会话的状态与处理记录"), 12),
            Panel(VStack(Txt(L("当前状态"), 12), Txt(utilityRunning.Contains(feature) ? "正在处理 · 可在任务中心查看进度或取消" : draft.Sources.Count == 0 ? L("等待添加素材") : text["waitingForModel"], 15, true))),
            Txt(L("输出位置"), 14, true), Txt(workspace.Settings.Current.OutputRoot, 12), Rule());
        logPanel.Children.Add(Txt(logs.Count == 0 ? text["utilityReady"] : string.Join(Environment.NewLine, logs), 12));
        logPanel.Children.Add(ActionButton(L("清空记录"), () => { logs.Clear(); RenderPage(); return Task.CompletedTask; }));
        var layout = new Grid { ColumnDefinitions = new ColumnDefinitions("3*,2*"), RowDefinitions = new RowDefinitions("Auto,Auto"), ColumnSpacing = 14, RowSpacing = 14 };
        var main = Panel(form); var side = Panel(logPanel, "#EFF7F3");
        layout.Children.Add(main); Grid.SetColumn(side, 1); layout.Children.Add(side);
        layout.SizeChanged += (_, e) =>
        {
            var stacked = e.NewSize.Width < 1180;
            layout.ColumnDefinitions[0].Width = new GridLength(stacked ? 1 : 3, GridUnitType.Star);
            layout.ColumnDefinitions[1].Width = stacked ? new GridLength(0) : new GridLength(2, GridUnitType.Star);
            Grid.SetColumn(side, stacked ? 0 : 1); Grid.SetRow(side, stacked ? 1 : 0);
        };
        sourceGrid.SizeChanged += (_, e) =>
        {
            var stacked = e.NewSize.Width < 700;
            sourceGrid.ColumnDefinitions[0].Width = new GridLength(stacked ? 1 : 3, GridUnitType.Star);
            sourceGrid.ColumnDefinitions[1].Width = stacked ? new GridLength(0) : new GridLength(2, GridUnitType.Star);
            Grid.SetColumn(previewPanel, stacked ? 0 : 1); Grid.SetRow(previewPanel, stacked ? 1 : 0);
        };
        if (draft.Sources.Count > 0)
            sourceList.SelectedIndex = Math.Max(0, draft.Sources.IndexOf(utilitySelection.GetValueOrDefault(feature, draft.Source)));
        return Scroll(layout);
    }
}
