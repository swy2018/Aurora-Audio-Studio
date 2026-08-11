using System.Diagnostics;
using AuroraAudioStudio.Models;

namespace AuroraAudioStudio.Services;

public sealed class MaintenanceService(SettingsService settings, ModelCatalogService catalog)
{
    public IReadOnlyList<HealthCheckItem> Scan()
    {
        var checks = new List<HealthCheckItem>();
        var models = catalog.GetDefaultStates();
        checks.Add(new("创作引擎", models.All(x => x.Installed) ? "良好" : "需要检查", $"{models.Count(x => x.Installed)}/{models.Count} 个组件可用", "\uE896"));
        checks.Add(new("模型存储", Directory.Exists(settings.Current.LocalAiRoot) ? "良好" : "不可用", settings.Current.LocalAiRoot, "\uEDA2"));
        checks.Add(new("成品存储", Directory.Exists(settings.Current.OutputRoot) ? "良好" : "需要创建", DiskDetail(settings.Current.OutputRoot), "\uE8B7"));
        checks.Add(new("任务恢复", "已启用", "异常退出后保留任务、处理记录与诊断记录", "\uE777"));
        checks.Add(new("安全模式", settings.Current.SafeMode ? "已启用" : "未启用", settings.Current.SafeMode ? "第三方创作引擎不会自动启动" : "所有已安装引擎均可启动", "\uE72E"));
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

    private static string DiskDetail(string path)
    {
        try
        {
            var root = Path.GetPathRoot(Path.GetFullPath(path));
            var drive = new DriveInfo(root!);
            return $"{path} · 可用 {drive.AvailableFreeSpace / 1024d / 1024 / 1024:0.0} GB";
        }
        catch { return path; }
    }
}
