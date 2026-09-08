using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using Avalonia.Controls;
using AuroraAudioStudio.Core;
using AuroraAudioStudio.Models;
using AuroraAudioStudio.Services;

namespace AuroraAudioStudio.Mac;

public sealed partial class MainWindow
{
    private readonly UpdateFlowGuard appUpdateFlow = new();
    private readonly HttpClient appUpdateClient = new() { Timeout = TimeSpan.FromMinutes(30) };
    private CancellationTokenSource? appUpdateCancellation;
    private static string CurrentMacVersion => typeof(MainWindow).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()!.InformationalVersion.Split('+')[0];

    private async Task CheckAppUpdateAsync(bool manual)
    {
        if (!appUpdateFlow.TryBegin()) { status.Text = workspace.Localization.Get("updateAlreadyRunning"); return; }
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMinutes(30));
        appUpdateCancellation = cancellation;
        try
        {
            status.Text = workspace.Localization.Get("updateChecking");
            var updater = new MacAppUpdater(appUpdateClient, workspace.Settings.UpdatesRoot, CurrentMacVersion, L, workspace.Settings.Current.AppUpdateChannel);
            var update = await updater.CheckAsync(cancellation.Token);
            status.Text = update.Message;
            if (!update.UpdateAvailable || update.InstallerUrl is null || update.ChecksumUrl is null)
            {
                if (manual) await MessageAsync(L("检查更新"), update.Message);
                return;
            }
            if (!await ConfirmModelChangeAsync(workspace.Localization.Get("updateDialogTitle"),
                workspace.Localization.Format("updateDialogBody", update.CurrentVersion, update.LatestVersion) + "\n\n" + L("将保留旧应用备份，模型、设置和作品不动。"))) return;
            if (modelOperation is not null || utilityRunning.Count > 0 || workspace.Queue.Items.Any(t => t.CanCancel)
                || modelStudios.Values.Any(s => s.Connection is not null || s.Startup is not null))
                throw new InvalidOperationException(L("请先结束正在运行的任务和模型工作台，再安装更新。"));
            var dmg = await updater.DownloadAsync(update, new Progress<AppUpdateProgress>(p => status.Text = p.Message + (p.IsIndeterminate ? "" : $" {p.Percentage:0}%")), cancellation.Token);
            // Prevent operations begun while the download was in progress from being interrupted.
            if (modelOperation is not null || utilityRunning.Count > 0 || workspace.Queue.Items.Any(t => t.CanCancel)
                || modelStudios.Values.Any(s => s.Connection is not null || s.Startup is not null))
                throw new InvalidOperationException(L("安装包已下载；请结束当前任务后再次检查更新。"));
            workspace.SaveDrafts();
            var bundle = Directory.GetParent(AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar))?.Parent?.FullName;
            if (bundle is null || Path.GetExtension(bundle) != ".app") throw new InvalidOperationException("请从已安装的 Aurora 应用中执行更新。");
            var report = Path.Combine(Path.GetDirectoryName(dmg)!, "install-" + Guid.NewGuid().ToString("N") + ".json");
            var info = new ProcessStartInfo(Path.Combine(AppContext.BaseDirectory, "Runtime", "install-update")) { UseShellExecute = false };
            foreach (var argument in new[] { dmg, bundle, Environment.ProcessId.ToString(), report }) info.ArgumentList.Add(argument);
            using var installer = Process.Start(info) ?? throw new IOException("无法启动 Mac 更新助手。");
            try
            {
                using var preparing = CancellationTokenSource.CreateLinkedTokenSource(cancellation.Token);
                preparing.CancelAfter(TimeSpan.FromMinutes(3));
                while (!installer.HasExited)
                {
                    preparing.Token.ThrowIfCancellationRequested();
                    if (File.Exists(report))
                    {
                        using var result = JsonDocument.Parse(await File.ReadAllTextAsync(report, preparing.Token));
                        var state = result.RootElement.GetProperty("state").GetString();
                        if (state == "failed") throw new IOException(result.RootElement.GetProperty("message").GetString());
                        if (state == "ready")
                        {
                            if (modelOperation is not null || utilityRunning.Count > 0 || workspace.Queue.Items.Any(t => t.CanCancel)
                                || modelStudios.Values.Any(s => s.Connection is not null || s.Startup is not null))
                                throw new InvalidOperationException(L("安装包已下载；请结束当前任务后再次检查更新。"));
                            await File.WriteAllTextAsync(Path.Combine(workspace.Settings.UpdatesRoot, "pending-install.txt"), report, preparing.Token);
                            status.Text = workspace.Localization.Get("updateInstallerHandoff"); Close();
                            if (IsVisible) throw new IOException("当前工作未能保存，已停止更新。");
                            return;
                        }
                    }
                    await Task.Delay(200, preparing.Token);
                }
                var details = File.Exists(report) ? await File.ReadAllTextAsync(report, cancellation.Token) : "未能校验或准备替换应用。";
                throw new IOException(details);
            }
            catch { if (!installer.HasExited) installer.Kill(entireProcessTree: true); throw; }
        }
        catch (OperationCanceledException) { status.Text = L("更新已取消；当前应用保持不变。"); }
        catch (Exception ex) { status.Text = ex.Message; if (manual) await MessageAsync(L("更新失败"), ex.Message); }
        finally { appUpdateCancellation = null; appUpdateFlow.End(); }
    }

    private async Task DailyAppUpdateAsync()
    {
        var today = DateOnly.FromDateTime(DateTime.Now);
        if (!workspace.Settings.Current.AutoCheckAppUpdates || !UpdateFlowGuard.ShouldRunDailyCheck(workspace.Settings.Current.LastAppUpdateCheckDate, today)) return;
        workspace.Settings.Current.LastAppUpdateCheckDate = UpdateFlowGuard.DateKey(today);
        if (!workspace.Settings.TrySave(workspace.Settings.Current, out var error)) throw new IOException(error);
        settingsDraft.LastAppUpdateCheckDate = workspace.Settings.Current.LastAppUpdateCheckDate;
        await CheckAppUpdateAsync(false);
    }

    private async Task ShowPreviousAppUpdateAsync()
    {
        var pointer = Path.Combine(workspace.Settings.UpdatesRoot, "pending-install.txt");
        if (!File.Exists(pointer)) return;
        var report = Path.GetFullPath((await File.ReadAllTextAsync(pointer)).Trim());
        if (!report.StartsWith(Path.GetFullPath(workspace.Settings.UpdatesRoot) + Path.DirectorySeparatorChar, StringComparison.Ordinal) || !File.Exists(report))
            throw new IOException("更新结果记录无效，请检查 Updates 目录。");
        using var result = JsonDocument.Parse(await File.ReadAllTextAsync(report));
        var state = result.RootElement.GetProperty("state").GetString();
        if (state == "ready") return;
        File.Move(pointer, pointer + ".handled-" + Guid.NewGuid().ToString("N"));
        await MessageAsync(L("程序更新"), state == "installed"
            ? L("更新已完成，旧版备份位于：") + "\n" + result.RootElement.GetProperty("backup").GetString()
            : L("更新未完成：") + result.RootElement.GetProperty("message").GetString());
    }
}
