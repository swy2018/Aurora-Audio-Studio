using System.Text.Json;
using Avalonia.Controls;
using Avalonia.Threading;
using AuroraAudioStudio.Core;
using AuroraAudioStudio.Models;

namespace AuroraAudioStudio.Mac;

public sealed partial class MainWindow
{
    private readonly HashSet<string> utilityRunning = [];
    private CancellationTokenSource? modelOperation;
    private string modelLog = "";

    private async Task<bool> InstallFromFeatureAsync(string id)
    {
        if (modelOperation is not null || utilityRunning.Count > 0 || workspace.Queue.Items.Any(t => t.CanCancel)
            || modelStudios.Values.Any(s => s.Connection is not null || s.Startup is not null))
            throw new InvalidOperationException(L("请先结束正在运行的任务和模型工作台，再安装更新。"));
        if (workspace.Settings.Current.SafeMode) throw new InvalidOperationException(L("安全模式已启用。"));
        if (!workspace.Runtime.Models.TryGetValue(id, out var spec)) throw new InvalidOperationException(L("尚无 Mac 适配器"));
        var caption = L("installModel") + " · " + workspace.Catalog.Find(id)!.Name;
        var size = spec.Gb < .01 ? $"{spec.Gb * 1000:0.##} MB" : $"{spec.Gb:0.##} GB";
        if (!await ConfirmModelChangeAsync(caption, L("featureInstallNotice") + "\n\n" + L("modelDownloadSize") + ": " + size
            + "\n" + L("modelInstallLocation") + "\n" + workspace.Runtime.Current(id))) return false;
        var progress = Txt(workspace.Localization.Format("modelInstalling", workspace.Catalog.Find(id)!.Name), 13);
        var dialog = new Window { Title = caption, Width = 580, Height = 320, WindowStartupLocation = WindowStartupLocation.CenterOwner };
        dialog.Content = Panel(VStack(Txt(caption, 20, true), new ProgressBar { IsIndeterminate = true }, progress,
            ActionButton(L("取消"), () => { modelOperation?.Cancel(); return Task.CompletedTask; })));
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
        timer.Tick += (_, _) => progress.Text = modelLog;
        string? error = null;
        dialog.Closing += (_, e) => { if (modelOperation is not null) { modelOperation.Cancel(); e.Cancel = true; } };
        dialog.Opened += async (_, _) =>
        {
            timer.Start();
            try
            {
                var action = workspace.Runtime.ModelStatus(id).Health.StartsWith("需要修复") ? "repair" : "install";
                var success = await ModelActionAsync(id, action);
                dialog.Close(success && workspace.Runtime.IsInstalled(id));
            }
            catch (Exception ex) { error = ex.Message; dialog.Close(false); }
            finally { timer.Stop(); }
        };
        var installed = await dialog.ShowDialog<bool>(this);
        if (error is not null) await MessageAsync(text["error"], error);
        return installed;
    }

    private void ReleaseOtherStudios(string? keep = null)
    {
        // Windows keeps music and voice independent; singing is exclusive with both.
        foreach (var pair in modelStudios.Where(p => p.Key != keep &&
            (keep is null or "singing" || p.Key == "singing")))
        {
            pair.Value.Dispose(); pair.Value.Reset();
        }
    }

    private async Task RunUtilityAsync(string feature)
    {
        if (!workspace.Engines.IsAvailable(workspace.Drafts[feature].ModelId)
            && !await InstallFromFeatureAsync(workspace.Drafts[feature].ModelId)) return;
        if (!utilityRunning.Add(feature)) return;
        ReleaseOtherStudios();
        var draft = JsonSerializer.Deserialize<StudioDraft>(JsonSerializer.Serialize(workspace.Drafts[feature]))!;
        var logs = utilityLogs[feature];
        try
        {
            var project = await workspace.SaveProjectAsync(draft);
            workspace.Drafts[feature].ProjectId = project.Id;
            var jobs = new List<Task>();
            foreach (var source in draft.Sources.ToArray())
            {
                var item = JsonSerializer.Deserialize<StudioDraft>(JsonSerializer.Serialize(draft))!; item.Source = source;
                var task = workspace.Queue.Create(project.Id, Path.GetFileName(source), feature, source, draft.ModelId, draft.Option, draft.SourceLanguage, draft.TrackMode);
                await workspace.Projects.AddTaskAsync(project, task);
                jobs.Add(RunOne(task, item));
            }
            if (current == feature) RenderPage();
            await Task.WhenAll(jobs);
        }
        finally { utilityRunning.Remove(feature); if (current == feature) RenderPage(); }

        async Task RunOne(AuroraTaskRecord task, StudioDraft item)
        {
            var result = await workspace.Queue.RunAsync(task, (progress, token) => workspace.Engines.ExecuteAsync(item, progress, token));
            logs.Add(DateTime.Now.ToString("HH:mm:ss") + "  " + task.Title + " · " + result.Message);
            if (result.Success) await workspace.Projects.CompleteTaskAsync(task.ProjectId, task);
            status.Text = result.Message;
        }
    }

    private async Task<bool> ModelActionAsync(string id, string action)
    {
        if (modelOperation is not null) return false;
        if (action == "uninstall" && !await ConfirmModelRemovalAsync(id)) return false;
        if (action == "rollback" && !await ConfirmModelChangeAsync(L("回退"), "将恢复上一个已记录版本。当前版本将保留为可恢复版本，成品与任务记录保持不变。")) return false;
        return await RunModelActionsAsync([id], action);
    }

    private async Task ModelBatchAsync(string action)
    {
        var ids = workspace.Runtime.Models.Keys.Where(workspace.Runtime.IsInstalled).ToArray();
        if (ids.Length == 0) { modelLog = "没有可检查的已安装模型。"; RenderPage(); return; }
        await RunModelActionsAsync(ids, action);
        if (action == "updates")
        {
            modelLog += $" 另有 {workspace.Catalog.Definitions.Count - ids.Length} 个组件未安装或未适配。";
            if (current is "models" or "maintenance") RenderPage();
            status.Text = modelLog;
        }
    }

    private async Task UpdateAllModelsAsync()
    {
        var ids = workspace.Runtime.Models.Keys.Where(id => workspace.Runtime.ModelStatus(id) is { Installed: true, HasUpdate: true }).ToArray();
        if (ids.Length == 0) { RenderPage(); return; }
        if (!await ConfirmModelChangeAsync(L("更新全部"), "将按顺序更新以下组件，保留可回退版本。失败项会保留更新入口，可重试。\n\n" + string.Join("\n", ids.Select(id => workspace.Catalog.Find(id)!.Name)))) return;
        await RunModelActionsAsync(ids, "update");
    }

    private async Task ModelPrimaryAsync(string id)
    {
        var state = workspace.Runtime.ModelStatus(id);
        if (state.Health.StartsWith("需要修复"))
        {
            if (await ConfirmModelChangeAsync("修复模型", "上次校验未通过。将重新安装运行环境并修复缺失或损坏的模型文件，成品与项目保留。")) await ModelActionAsync(id, "repair");
            return;
        }
        if (state.Installed && !state.HasUpdate)
        {
            if (!await ModelActionAsync(id, "updates")) return;
            state = workspace.Runtime.ModelStatus(id);
            if (!state.HasUpdate)
            {
                if (!await ModelActionAsync(id, "check") && await ConfirmModelChangeAsync("修复模型", modelLog + "\n\n是否重新安装运行环境并修复模型文件？")) await ModelActionAsync(id, "repair");
                return;
            }
        }
        var action = state.HasUpdate ? "update" : "install";
        var spec = workspace.Runtime.Models[id];
        var caption = state.HasUpdate ? L("更新") : L("安装");
        var modelSize = spec.Gb < .01 ? $"{spec.Gb * 1000:0.##} MB" : $"{spec.Gb:0.##} GB";
        if ((!workspace.Settings.Current.ConfirmLargeModelDownloads && action == "install") || await ConfirmModelChangeAsync(caption + " · " + workspace.Catalog.Find(id)!.Name,
            $"将从模型声明的上游来源下载并校验文件。模型约 {modelSize}，运行环境和缓存另计。\n\n位置：{workspace.Runtime.Current(id)}\n\n" + (state.HasUpdate ? "当前版本会保留以便回退。" : "已有完整文件会复用，未完成下载可以继续。"))) await ModelActionAsync(id, action);
    }

    private async Task<bool> RunModelActionsAsync(IReadOnlyList<string> ids, string action)
    {
        if (modelOperation is not null) return false;
        var readOnly = action is "updates" or "check";
        if (!readOnly && (utilityRunning.Count > 0 || workspace.Queue.Items.Any(t => t.Status is "running" or "preparing" or "waiting") || modelStudios.Values.Any(s => s.Connection is not null || s.Startup is not null)))
            throw new InvalidOperationException("组件正在使用。请先保存工作、结束当前引擎或取消处理任务，再执行模型维护；Aurora 不会强制关闭工作台。");
        using var cancellation = new CancellationTokenSource(); modelOperation = cancellation;
        modelCompleted = 0; modelTotal = ids.Count; modelLog = "正在准备…";
        var failures = new List<string>(); var canceled = false;
        var label = action == "updates" ? "检查模型更新" : action == "check" ? "校验模型" : action == "update" ? "更新模型" : "维护模型";
        var logRoot = Path.Combine(workspace.Settings.AppDataRoot, "EngineLogs"); Directory.CreateDirectory(logRoot);
        var logPath = Path.Combine(logRoot, "maintenance-" + DateTime.Now.ToString("yyyyMMdd-HHmmss-fff") + ".log");
        var lastRender = DateTimeOffset.MinValue;
        try
        {
            foreach (var id in ids)
            {
                cancellation.Token.ThrowIfCancellationRequested();
                modelActivity = $"{label} {modelCompleted}/{modelTotal} · {workspace.Catalog.Find(id)!.Name}";
                if (current is "models" or "maintenance") RenderPage();
                try
                {
                    await workspace.Runtime.ManageAsync(id, action, new Progress<string>(line =>
                    {
                        File.AppendAllText(logPath, id + " " + line + Environment.NewLine);
                        try
                        {
                            if (line.StartsWith('{'))
                            {
                                using var doc = JsonDocument.Parse(line);
                                if (doc.RootElement.TryGetProperty("message", out var message)) line = message.GetString() ?? line;
                                else if (doc.RootElement.TryGetProperty("stage", out var stage))
                                {
                                    line = stage.GetString() ?? line;
                                    if (doc.RootElement.TryGetProperty("completed", out var completed) && doc.RootElement.TryGetProperty("total", out var total) && total.GetDouble() > 0)
                                        line += $" · {completed.GetDouble() / total.GetDouble():P0} ({completed.GetDouble() / 1e9:F2} / {total.GetDouble() / 1e9:F2} GB)";
                                }
                            }
                        }
                        catch (JsonException) { }
                        if (modelOperation != cancellation) return;
                        modelLog = line; status.Text = line;
                        if (DateTimeOffset.Now - lastRender > TimeSpan.FromSeconds(1) && current is "models" or "maintenance") { lastRender = DateTimeOffset.Now; RenderPage(); }
                    }), cancellation.Token);
                }
                catch (OperationCanceledException) { throw; }
                catch (Exception ex) { failures.Add(id + "：" + ex.Message); File.AppendAllText(logPath, ex + Environment.NewLine); }
                modelCompleted++;
            }
        }
        catch (OperationCanceledException) { canceled = true; }
        finally
        {
            modelOperation = null;
            var updates = workspace.Runtime.Models.Keys.Count(id => workspace.Runtime.ModelStatus(id).HasUpdate);
            modelLog = canceled ? $"已取消，完成 {modelCompleted}/{modelTotal} 项；已完成检查结果和下载文件保留。"
                : failures.Count > 0 ? $"完成 {modelCompleted - failures.Count}/{modelTotal} 项，{failures.Count} 项失败；可重试。\n" + string.Join("\n", failures.Select(f => f.Split('\n')[0]))
                : action == "updates" ? $"检查完成：{modelCompleted} 项已检查，发现 {updates} 个可更新模型。" : $"完成：{modelCompleted}/{modelTotal} 项。";
            if (current is "models" or "maintenance") RenderPage();
            status.Text = modelLog;
        }
        return !canceled && failures.Count == 0;
    }

    private async Task<bool> ConfirmModelChangeAsync(string heading, string message)
    {
        var dialog = new Window { Title = heading, Width = 580, Height = 390, MinWidth = 420, WindowStartupLocation = WindowStartupLocation.CenterOwner };
        dialog.Content = new ScrollViewer { Content = Panel(VStack(Txt(heading, 20, true), Txt(message),
            Row(ActionButton(L("取消"), () => { dialog.Close(false); return Task.CompletedTask; }), ActionButton(L("继续"), () => { dialog.Close(true); return Task.CompletedTask; })))) };
        return await dialog.ShowDialog<bool>(this);
    }

    private Task<bool> ConfirmModelRemovalAsync(string id) => ConfirmModelChangeAsync(L("卸载模型"),
        "将以下目录整体移到 macOS 废纸篓，可从访达恢复：\n" + Path.Combine(workspace.Runtime.Root, "models", id) +
        "\n\n包括：此模型的权重、未完成下载与历史版本。\n保留：其他模型、共享运行环境、项目、任务记录和创作成品。");

    private async Task ImportWorkbenchResultsAsync()
    {
        var count = await workspace.WorkbenchResults.ImportAsync();
        if (count == 0) return;
        status.Text = "生成完成，已保存 " + count + " 个结果。";
        if (current is "results" or "tasks" or "home") RenderPage();
    }
}
