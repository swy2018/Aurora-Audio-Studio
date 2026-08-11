using System.Diagnostics;
using AuroraAudioStudio.Models;

namespace AuroraAudioStudio.Services;

public sealed class MaintenanceService(SettingsService settings, ModelCatalogService catalog, ProjectService? projects = null, LocalizationService? localization = null)
{
    private string Text(string key, string fallback) => localization?.Get(key) ?? fallback;

    public IReadOnlyList<HealthCheckItem> Scan()
    {
        var checks = new List<HealthCheckItem>();
        var models = catalog.GetDefaultStates();
        checks.Add(new(Text("healthEngines", "创作引擎"), Text(models.All(x => x.Installed) ? "healthGood" : "healthCheck", models.All(x => x.Installed) ? "良好" : "需要检查"), localization?.Format("healthComponents", models.Count(x => x.Installed), models.Count) ?? $"{models.Count(x => x.Installed)}/{models.Count} 个组件可用", "\uE896"));
        checks.Add(new(Text("healthModelStorage", "模型存储"), Text(Directory.Exists(settings.Current.LocalAiRoot) ? "healthGood" : "healthUnavailable", Directory.Exists(settings.Current.LocalAiRoot) ? "良好" : "不可用"), settings.Current.LocalAiRoot, "\uEDA2"));
        checks.Add(new(Text("healthOutputStorage", "成品存储"), Text(Directory.Exists(settings.Current.OutputRoot) ? "healthGood" : "healthCreate", Directory.Exists(settings.Current.OutputRoot) ? "良好" : "需要创建"), DiskDetail(settings.Current.OutputRoot), "\uE8B7"));
        checks.Add(new(Text("healthTaskRecovery", "任务恢复"), Text("healthEnabled", "已启用"), Text("healthTaskRecoveryDetail", "异常退出后保留任务、处理记录与诊断记录"), "\uE777"));
        if (projects is not null)
            checks.Add(new(Text("healthRecords", "处理记录"), Text(projects.RecoveryCount == 0 ? "healthGood" : "healthRecover", projects.RecoveryCount == 0 ? "良好" : "需要恢复"), projects.RecoveryCount == 0 ? Text("healthRecordsGood", "未发现损坏或不兼容的处理记录") : localization?.Format("healthRecordsRecover", projects.RecoveryCount) ?? $"{projects.RecoveryCount} 个记录已保留恢复副本", "\uE7C3"));
        checks.Add(new(Text("healthSafeMode", "安全模式"), Text(settings.Current.SafeMode ? "healthEnabled" : "healthDisabled", settings.Current.SafeMode ? "已启用" : "未启用"), Text(settings.Current.SafeMode ? "healthSafeOn" : "healthSafeOff", settings.Current.SafeMode ? "第三方创作引擎不会自动启动" : "所有已安装引擎均可启动"), "\uE72E"));
        return checks;
    }

    public string GpuSummary()
    {
        try
        {
            var info = new ProcessStartInfo("nvidia-smi", "--query-gpu=name,memory.total,driver_version --format=csv,noheader") { UseShellExecute = false, RedirectStandardOutput = true, CreateNoWindow = true };
            using var process = Process.Start(info);
            if (process is null) return "未检测到 NVIDIA GPU 信息";
            var text = process.StandardOutput.ReadToEnd(); process.WaitForExit(3000);
            return string.IsNullOrWhiteSpace(text) ? "未检测到 NVIDIA GPU 信息" : text.Trim();
        }
        catch { return "GPU 信息不可用"; }
    }

    private string DiskDetail(string path)
    {
        try
        {
            var root = Path.GetPathRoot(Path.GetFullPath(path));
            var drive = new DriveInfo(root!);
            return localization?.Format("healthDiskAvailable", path, drive.AvailableFreeSpace / 1024d / 1024 / 1024) ?? $"{path} · 可用 {drive.AvailableFreeSpace / 1024d / 1024 / 1024:0.0} GB";
        }
        catch { return path; }
    }
}
