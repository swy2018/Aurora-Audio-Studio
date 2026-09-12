using System.Net;
using System.Runtime.InteropServices;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input.Platform;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Platform.Storage;
using AuroraAudioStudio.Core;
using AuroraAudioStudio.Models;
using AuroraAudioStudio.Services;

namespace AuroraAudioStudio.Mac;

public sealed partial class MainWindow
{
    private Control TasksPage()
    {
        var content = VStack();
        content.Children.Add(Row(ActionButton(text[workspace.Queue.IsPaused ? "resume" : "pause"], () =>
        { if (workspace.Queue.IsPaused) workspace.Queue.Resume(); else workspace.Queue.Pause(); RenderPage(); return Task.CompletedTask; }),
            ActionButton(text["openFolder"], () => OpenPathAsync(workspace.Settings.Current.OutputRoot)),
            ActionButton(text["refresh"], () => { RenderPage(); return Task.CompletedTask; })));
        if (workspace.Queue.Items.Count == 0) content.Children.Add(Panel(VStack(Txt(text["noTasks"], 22, true), Txt(text["noTasksHint"]))));
        foreach (var task in workspace.Queue.Items.ToArray())
        {
            var stateKey = task.Status switch
            {
                "waiting" => "taskWaiting", "preparing" => "taskPreparing", "running" => "taskRunning", "completed" => "taskCompleted",
                "failed" => "taskFailed", "canceled" => "taskCanceled", _ => "taskInterrupted"
            };
            var item = VStack(Txt(task.Title, 16, true), Txt(text[stateKey] + "  ·  " + task.CreatedDisplay));
            if (task.Status == "running") item.Children.Add(new ProgressBar { Minimum = 0, Maximum = 1, Value = task.Progress });
            item.Children.Add(Txt(task.Status is "running" or "preparing" or "waiting" ? task.Stage : string.Join(" · ", new[] { task.Stage, task.Message }.Where(s => !string.IsNullOrWhiteSpace(s)).Distinct()), 12));
            var taskActions = Row();
            taskActions.Children.Add(ActionButton(text["openFolder"], () => OpenPathAsync(
                Directory.Exists(task.OutputPath) ? task.OutputPath : Path.GetDirectoryName(File.Exists(task.OutputPath) ? task.OutputPath : task.InputPath)!)));
            taskActions.Children.Add(ActionButton(L("查看日志"), () => File.Exists(task.LogPath)
                ? OpenPathAsync(task.LogPath) : MessageAsync(task.Title, task.Message)));
            if (task.CanRetry && workspace.Engines.IsAvailable(task.ModelId)) taskActions.Children.Add(ActionButton("重试", async () =>
            {
                if (modelOperation is not null) throw new InvalidOperationException("请先等待模型维护完成。");
                ReleaseOtherStudios();
                var result = await workspace.RetryTaskAsync(task);
                if (!result.Success) status.Text = workspace.Localization.Translate(result.Message);
                RenderPage();
            }));
            if (task.CanCancel) taskActions.Children.Add(ActionButton(workspace.Localization.Translate("取消"), () => { workspace.Queue.Cancel(task.Id); RenderPage(); return Task.CompletedTask; }));
            if (taskActions.Children.Count > 0) item.Children.Add(taskActions);
            content.Children.Add(Panel(item));
        }
        return Scroll(content);
    }

    private Control ResultsPage()
    {
        var content = VStack(Row(ActionButton(text["importResult"], async () =>
        {
            var path = await PickAsync([new FilePickerFileType("WAV / MIDI / SRT") { Patterns = ["*.wav", "*.mid", "*.midi", "*.srt"], AppleUniformTypeIdentifiers = ["com.microsoft.waveform-audio", "public.midi-audio", "public.plain-text"] }, FilePickerFileTypes.All]);
            if (path is null) return;
            await workspace.ImportArtifactAsync(path); RenderPage(); status.Text = text["imported"];
        }, "import-result"), ActionButton(text["openFolder"], () => OpenPathAsync(workspace.Settings.Current.OutputRoot))));
        var artifacts = workspace.Projects.Artifacts();
        if (artifacts.Count == 0) content.Children.Add(Panel(VStack(Txt(text["noResults"], 22, true), Txt(text["noResultsHint"]))));
        foreach (var artifact in artifacts)
        {
            var actions = Row(ActionButton(text["previewFile"], () => PreviewAsync(artifact.Path)), ActionButton(text["export"], () => ExportAsync(artifact.Path)),
                ActionButton(L("复制路径"), () => CopyArtifactPathAsync(artifact.Path)),
                ActionButton(L("打开位置"), () => OpenPathAsync(Path.GetDirectoryName(artifact.Path)!)));
            if (Path.GetExtension(artifact.Path).Equals(".srt", StringComparison.OrdinalIgnoreCase)) actions.Children.Add(ActionButton("编辑字幕", () => EditSubtitlesAsync(artifact.Path)));
            content.Children.Add(Panel(VStack(Txt(artifact.Name, 16, true), Txt(text[artifact.Kind] + "  ·  " + artifact.ProjectName + "  ·  " + artifact.CreatedDisplay, 12), actions)));
        }
        return Scroll(content);
    }

    private async Task CopyArtifactPathAsync(string path)
    {
        // Avalonia 12 clipboard API: https://docs.avaloniaui.net/docs/how-to/clipboard-how-to
        var clipboard = TopLevel.GetTopLevel(this)?.Clipboard ?? throw new InvalidOperationException("剪贴板暂不可用。");
        await clipboard.SetTextAsync(path);
        status.Text = L("成品路径已复制。");
    }

    private async Task EditSubtitlesAsync(string source)
    {
        ArtifactValidator.Validate(source);
        if (new FileInfo(source).Length > 2 * 1024 * 1024) throw new InvalidDataException(L("字幕超过 2 MB，请使用外部编辑器打开。"));
        var editor = new TextBox { Text = await File.ReadAllTextAsync(source), AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, MinHeight = 320 };
        var dialog = new Window { Title = "编辑字幕", Width = 820, Height = 650, WindowStartupLocation = WindowStartupLocation.CenterOwner };
        var message = Txt("可修改字幕文字和时间码，保存前会检查 SRT 格式。", 12);
        var save = ActionButton("保存副本…", async () =>
        {
            try
            {
                ArtifactValidator.ValidateSubtitleText(editor.Text ?? "");
                var folders = await dialog.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions { Title = L("保存副本"), AllowMultiple = false });
                if (folders.FirstOrDefault()?.TryGetLocalPath() is not { } folder) return;
                var destination = await ArtifactExport.CopyAsync(source, folder, editor.Text);
                message.Text = workspace.Localization.Format("artifactExported", destination);
            }
            catch (Exception ex) { message.Text = ex.Message; }
        });
        var layout = new Grid { Margin = new Thickness(20), RowDefinitions = new RowDefinitions("Auto,*,Auto,Auto"), RowSpacing = 12 };
        layout.Children.Add(Txt(Path.GetFileName(source), 20, true)); Grid.SetRow(editor, 1); layout.Children.Add(editor);
        var actions = Row(save);
        if (workspace.Runtime.IsInstalled("subtitle-edit")) actions.Children.Add(ActionButton("Subtitle Edit", async () =>
        {
            await OpenSubtitleEditAsync(source);
            dialog.Close();
        }));
        Grid.SetRow(message, 2); layout.Children.Add(message); Grid.SetRow(actions, 3); layout.Children.Add(actions);
        dialog.Content = layout; await dialog.ShowDialog(this);
    }

    private async Task OpenSubtitleEditAsync(string source)
    {
        var app = Path.Combine(workspace.Runtime.Current("subtitle-edit"), "Subtitle Edit.app");
        var info = new System.Diagnostics.ProcessStartInfo("/usr/bin/open") { UseShellExecute = false };
        foreach (var argument in new[] { "-a", app, source }) info.ArgumentList.Add(argument);
        using var process = System.Diagnostics.Process.Start(info) ?? throw new IOException("无法打开 Subtitle Edit。");
        await process.WaitForExitAsync();
        if (process.ExitCode != 0) throw new IOException("Subtitle Edit 启动失败，请在模型中心检查或修复。");
    }

    private async Task ExportAsync(string source)
    {
        ArtifactValidator.Validate(source);
        var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions { Title = text["export"], AllowMultiple = false });
        if (folders.FirstOrDefault()?.TryGetLocalPath() is not { } folder) return;
        var destination = await ArtifactExport.CopyAsync(source, folder);
        status.Text = workspace.Localization.Format("artifactExported", destination);
    }

    private async Task PreviewAsync(string path, string? displayName = null)
    {
        ArtifactValidator.Validate(path);
        if (Path.GetExtension(path).Equals(".srt", StringComparison.OrdinalIgnoreCase))
        { await EditSubtitlesAsync(path); return; }
        if (Path.GetExtension(path).ToLowerInvariant() is ".mid" or ".midi")
        {
            var midiDialog = new Window { Title = text["previewFile"], Width = 560, Height = 280, WindowStartupLocation = WindowStartupLocation.CenterOwner };
            var close = ActionButton(text["ok"], () => { midiDialog.Close(); return Task.CompletedTask; });
            midiDialog.Content = new StackPanel { Margin = new Thickness(24), Spacing = 18, Children = {
                Txt(Path.GetFileName(path), 20, true),
                Txt(workspace.Localization.Format("midiPreviewHint", ArtifactValidator.MidiNoteCount(path))),
                Row(ActionButton(L("用默认程序打开"), () => OpenPathAsync(path)), close)
            } };
            await midiDialog.ShowDialog(this);
            return;
        }
        using var server = new LocalWorkbenchServer();
        var audio = server.AddFile(path, "audio/wav");
        var html = "<!doctype html><meta charset='utf-8'><style>body{font:16px system-ui;padding:26px;color:#17241f}h3{overflow-wrap:anywhere}audio{width:100%}</style><h3>" + WebUtility.HtmlEncode(displayName ?? Path.GetFileName(path)) + "</h3><audio controls src='" + audio.AbsoluteUri + "'></audio>";
        var view = new StudioWebView { Source = server.AddPage(html) };
        view.NavigationStarted += (_, e) => e.Cancel = !server.Owns(e.Request);
        view.NewWindowRequested += (_, e) => e.Handled = true;
        var dialog = new Window { Title = text["previewFile"], Width = 580, Height = 250, Content = view, WindowStartupLocation = WindowStartupLocation.CenterOwner };
        await dialog.ShowDialog(this);
    }

    private Control MaintenancePage()
    {
        var content = VStack(Txt(text["health"], 20, true));
        content.Children.Add(Panel(VStack(Txt(RuntimeInformation.OSDescription + " · " + RuntimeInformation.ProcessArchitecture),
            Txt(".NET " + Environment.Version), Txt(text["healthEngines"] + " · " + workspace.Runtime.Models.Keys.Count(workspace.Runtime.IsReady)), Txt(workspace.Settings.AppDataRoot, 12))));
        content.Children.Add(Panel(VStack(Txt(text["workbenchCheck"], 20, true), Txt(text["checkHint"]),
            ActionButton(text["checkOpen"], () => { Navigate("workbench"); return Task.CompletedTask; }, "workbench-check"))));
        content.Children.Add(Row(ActionButton("打开引擎日志", () => OpenPathAsync(Path.Combine(workspace.Settings.AppDataRoot, "EngineLogs"))),
        ActionButton("检查全部已安装模型", async () =>
        {
            await ModelBatchAsync("check");
        }), ActionButton(text["diagnostics"], ExportDiagnosticsAsync)));
        if (workspace.RecoveryWarning is not null) content.Children.Add(Txt(text["error"] + ": " + workspace.RecoveryWarning));
        return Scroll(content);
    }

    private async Task ExportDiagnosticsAsync()
    {
        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions { Title = text["diagnostics"], SuggestedFileName = "Aurora-Mac-diagnostics.json", ShowOverwritePrompt = true });
        if (file is null) return;
        var payload = new
        {
            app = "Aurora", version = CurrentMacVersion, platform = RuntimeInformation.OSDescription,
            architecture = RuntimeInformation.ProcessArchitecture.ToString(), dotnet = Environment.Version.ToString(),
            language = workspace.Settings.EffectiveLanguage(), modelDownloadsEnabled = true, installedEngines = workspace.Runtime.Models.Keys.Count(workspace.Runtime.IsReady),
            catalogCount = workspace.Catalog.Definitions.Count, tasks = workspace.Queue.Items.Count,
            embeddedWorkbenchLoaded = workbenchReady
        };
        await using var stream = await file.OpenWriteAsync(); stream.SetLength(0);
        await JsonSerializer.SerializeAsync(stream, payload, new JsonSerializerOptions { WriteIndented = true });
        status.Text = workspace.Localization.Format("artifactExported", file.Name);
    }

    private Control AboutPage() => Scroll(Panel(VStack(Txt("Aurora Audio Studio", 30, true), Txt(CurrentMacVersion + " · Apple Silicon", 14),
        Txt(text["aboutBody"]), Rule(), Txt(text["aboutScope"]), Txt("GPL-3.0-only · Noto Sans JP / SIL OFL 1.1", 12),
        ActionButton(L("检查程序更新"), () => CheckAppUpdateAsync(true)),
        ActionButton(L("rollbackApp"), () => CheckAppUpdateAsync(true, rollback: true), "rollback-app"),
        Txt(L("rollbackHint"), 12),
        ActionButton(text["openFolder"] + " · Licenses", () => OpenPathAsync(Path.Combine(AppContext.BaseDirectory, "Licenses"))))));

    private void ShowWorkbench()
    {
        if (workbenchHost is not null) return;
        webServer = new LocalWorkbenchServer();
        using var font = AssetLoader.Open(new Uri("avares://Aurora/Assets/Fonts/NotoSansJP.ttf"));
        using var fontBytes = new MemoryStream(); font.CopyTo(fontBytes);
        var fontUri = webServer.AddResource(fontBytes.ToArray(), "font/ttf");
        var html = """
            <!doctype html><html lang="zh-CN"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width, initial-scale=1">
            <style>
            @font-face{font-family:Noto;src:url('FONT_URI')}*{box-sizing:border-box}body{margin:0;padding:32px;color:#17241f;background:#fff;font:15px -apple-system,'PingFang SC',sans-serif}html:lang(ja-JP) body{font-family:Noto,sans-serif}h1{font-size:25px;margin:0 0 12px}p{color:#5f7169;line-height:1.7}label{display:block;margin:22px 0 9px;font-weight:600}textarea{width:100%;min-height:145px;border:1px solid #cfe0d8;border-radius:9px;padding:14px;font:inherit;resize:vertical}button{padding:12px 20px;background:#165f4d;color:white;border:0;border-radius:8px;font:inherit;margin:14px 0}audio{display:block;width:100%;margin:20px 0}input{font:inherit}#reply{padding:16px;background:#eaf6f1;border-radius:8px;white-space:pre-wrap;overflow-wrap:anywhere}
            </style></head><body><h1 id="heading"></h1><p id="hint"></p><label id="inputLabel" for="draft"></label><textarea id="draft"></textarea><button id="send"></button><div id="reply" role="status"></div><label id="audioLabel" for="file"></label><button id="file"></button><span id="fileName"></span><audio id="audio" controls></audio>
            <script>
            window.setLanguage=function(t){document.documentElement.lang=t.language;heading.textContent=t.heading;hint.textContent=t.hint;inputLabel.textContent=t.input;send.textContent=t.send;audioLabel.textContent=t.audio;file.textContent=t.choose};
            window.hostReady=function(){invokeCSharpAction(JSON.stringify({kind:'ready'}))};
            send.onclick=()=>invokeCSharpAction(JSON.stringify({kind:'echo',value:draft.value}));
            window.receive=function(value){reply.textContent=value};
            file.onclick=()=>invokeCSharpAction(JSON.stringify({kind:'pick-audio'}));window.loadAudio=function(url,name){audio.pause();audio.src=url;fileName.textContent=name};
            </script></body></html>
            """.Replace("FONT_URI", fontUri.AbsoluteUri, StringComparison.Ordinal);
        workbench = new NativeWebView();
        var hostStatus = Txt(text["webLoading"], 12);
        Uri? previousAudio = null;
        var failed = false;
        var timeout = new Avalonia.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(15) };
        timeout.Tick += (_, _) => { timeout.Stop(); if (!workbenchReady) { failed = true; hostStatus.Text = text["webFailure"]; } };
        Closed += (_, _) => timeout.Stop();
        workbench.NavigationStarted += (_, e) =>
        {
            if (!webServer.Owns(e.Request)) { e.Cancel = true; return; }
            workbenchLoaded = false; workbenchReady = false; failed = false; timeout.Start(); hostStatus.Text = text["webLoading"];
        };
        workbench.NewWindowRequested += (_, e) => e.Handled = true;
        workbench.NavigationCompleted += async (_, e) =>
        {
            if (!e.IsSuccess) { timeout.Stop(); failed = true; hostStatus.Text = text["webFailure"]; return; }
            workbenchLoaded = true;
            await SafeAsync(async () => { await UpdateWorkbenchLanguageAsync(); await workbench.InvokeScript("window.hostReady()"); });
        };
        workbench.WebMessageReceived += async (_, e) =>
        {
            await SafeAsync(async () =>
            {
                if (workbench.Source is not { } uri || !webServer.Owns(uri) || e.Body is null || e.Body.Length > 65536) return;
                using var message = JsonDocument.Parse(e.Body);
                if (message.RootElement.GetProperty("kind").GetString() == "ready") { timeout.Stop(); failed = false; workbenchReady = true; hostStatus.Text = text["webReady"]; }
                else if (message.RootElement.GetProperty("kind").GetString() == "pick-audio")
                {
                    var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
                    { Title = text["webAudio"], AllowMultiple = false, FileTypeFilter = [new FilePickerFileType("WAV") { Patterns = ["*.wav"], AppleUniformTypeIdentifiers = ["com.microsoft.waveform-audio"] }] });
                    var path = files.FirstOrDefault()?.TryGetLocalPath();
                    if (path is null) return;
                    ArtifactValidator.Validate(path);
                    var audio = webServer.AddFile(path, "audio/wav");
                    await workbench.InvokeScript("window.loadAudio(" + JsonSerializer.Serialize(audio.AbsoluteUri) + "," + JsonSerializer.Serialize(Path.GetFileName(path)) + ")");
                    webServer.Remove(previousAudio); previousAudio = audio;
                }
                else if (message.RootElement.GetProperty("kind").GetString() == "echo")
                {
                    var reply = text["received"] + message.RootElement.GetProperty("value").GetString();
                    await workbench.InvokeScript("window.receive(" + JsonSerializer.Serialize(reply) + ")");
                    hostStatus.Text = text["webReady"];
                }
            });
        };
        var container = new Grid { RowDefinitions = new RowDefinitions("Auto,*") };
        var retry = ActionButton(text["retryWeb"], () => { workbench.Source = webServer.AddPage(html); return Task.CompletedTask; });
        var toolbar = Row(hostStatus, retry); toolbar.Margin = new Thickness(8, 0, 8, 10);
        container.Children.Add(toolbar);
        Grid.SetRow(workbench, 1); container.Children.Add(workbench);
        workbenchHost = new Border { Margin = new Thickness(28, 24), Child = container, Background = Surface };
        body.Children.Add(workbenchHost);
        workbench.Source = webServer.AddPage(html);
        languageUpdates.Add(() => { retry.Content = text["retryWeb"]; hostStatus.Text = text[workbenchReady ? "webReady" : failed ? "webFailure" : "webLoading"]; _ = SafeAsync(UpdateWorkbenchLanguageAsync); });
    }

    private async Task UpdateWorkbenchLanguageAsync()
    {
        if (!workbenchLoaded || workbench?.Source is not { } uri || webServer is null || !webServer.Owns(uri)) return;
        var labels = new { language = workspace.Settings.EffectiveLanguage(), heading = text["workbenchCheck"], hint = text["checkHint"], input = text["webInput"], send = text["sendHost"], audio = text["webAudio"], choose = text["chooseWav"] };
        await workbench.InvokeScript("window.setLanguage(" + JsonSerializer.Serialize(labels) + ")");
    }
}
