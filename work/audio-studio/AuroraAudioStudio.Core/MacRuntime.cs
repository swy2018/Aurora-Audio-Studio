using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using System.Security.Cryptography;
using AuroraAudioStudio.Models;
using AuroraAudioStudio.Services;

namespace AuroraAudioStudio.Core;

public sealed record MacModelInfo(string Family, double Gb, bool DownloadOnly = false, string[]? Required = null);
public sealed record MacModelStatus(bool Installed, string Version, string Health, bool HasUpdate, string UpdateMessage);

public sealed class MacRuntime : IDisposable
{
    private readonly SettingsService settings;
    public string Scripts { get; }
    public Dictionary<string, MacModelInfo> Models { get; } = [];
    private readonly HashSet<Process> processes = [];
    private readonly SemaphoreSlim maintenanceGate = new(1, 1);
    public string? BusyModel { get; private set; }
    public string LastMessage { get; private set; } = "";
    public event Action<string>? Log;
    public string Root => settings.Current.LocalAiRoot;
    public string Current(string id) => Path.Combine(Root, "models", id, "current");
    public string Python(string family) => Path.Combine(Root, "envs", family, "bin", "python");

    public MacRuntime(SettingsService settings, string? scripts = null)
    {
        this.settings = settings;
        Scripts = scripts ?? Path.Combine(AppContext.BaseDirectory, "Runtime");
        var catalog = Path.Combine(Scripts, "catalog.json");
        if (!File.Exists(catalog)) return;
        using var doc = JsonDocument.Parse(File.ReadAllText(catalog));
        foreach (var item in doc.RootElement.EnumerateObject())
            Models[item.Name] = new(item.Value.GetProperty("family").GetString()!, item.Value.GetProperty("gb").GetDouble(),
                item.Value.TryGetProperty("download_only", out var downloadOnly) && downloadOnly.GetBoolean(),
                item.Value.TryGetProperty("required", out var required) ? required.EnumerateArray().Select(x => x.GetString()!).ToArray() : []);
    }

    public bool IsReady(string id)
        => Models.TryGetValue(id, out var model) && !model.DownloadOnly && BusyModel != id && IsInstalled(id) && !HasFailedHealth(id);

    public bool IsInstalled(string id)
    {
        if (!Models.TryGetValue(id, out var model)) return false;
        try
        {
            var receipt = Path.Combine(Current(id), "receipt.json");
            if (!File.Exists(receipt)) return false;
            if (!model.DownloadOnly && (!File.Exists(Python(model.Family)) || !File.Exists(Path.Combine(Root, "envs", model.Family, "aurora-runtime.json")))) return false;
            if ((model.Required ?? []).Any(name => !File.Exists(Path.Combine(Current(id), name)) || new FileInfo(Path.Combine(Current(id), name)).Length == 0)) return false;
            using var doc = JsonDocument.Parse(File.ReadAllText(receipt));
            if (doc.RootElement.GetProperty("files").GetArrayLength() == 0) return false;
            return doc.RootElement.GetProperty("files").EnumerateArray().All(f =>
            {
                var path = Path.GetFullPath(Path.Combine(Current(id), f.GetProperty("path").GetString()!));
                if (!path.StartsWith(Path.GetFullPath(Current(id)) + Path.DirectorySeparatorChar, StringComparison.Ordinal)) return false;
                var file = new FileInfo(path);
                return file.Exists && file.Length == f.GetProperty("size").GetInt64();
            });
        }
        catch { return false; }
    }

    private bool HasFailedHealth(string id)
    {
        var path = Path.Combine(Current(id), "health.json");
        if (!File.Exists(path)) return false;
        try { using var document = JsonDocument.Parse(File.ReadAllText(path)); return document.RootElement.TryGetProperty("failed", out var failed) && failed.GetBoolean(); }
        catch { return true; }
    }

    public MacModelStatus ModelStatus(string id)
    {
        var installed = IsInstalled(id);
        var downloadOnly = Models.TryGetValue(id, out var model) && model.DownloadOnly;
        string version = "—", health = installed ? "文件齐全 · 待运行验证" : "未安装或需要修复", message = "";
        if (installed && downloadOnly) health = id == "subtitle-edit" ? "外部字幕编辑器" : "仅模型管理 · 尚未接入工作台";
        var available = false;
        try
        {
            using var receipt = JsonDocument.Parse(File.ReadAllText(Path.Combine(Current(id), "receipt.json")));
            version = string.Join(" / ", receipt.RootElement.GetProperty("assets").EnumerateArray().Select(a => a.GetProperty("revision").GetString()!).Distinct().Select(r => r[..Math.Min(8, r.Length)]));
            if (version.Length == 0) version = DateTimeOffset.FromUnixTimeSeconds((long)receipt.RootElement.GetProperty("installed_at").GetDouble()).ToLocalTime().ToString("yyyy.MM.dd");
            var healthFile = Path.Combine(Current(id), "health.json");
            if (installed && File.Exists(healthFile))
            {
                using var check = JsonDocument.Parse(File.ReadAllText(healthFile));
                health = (downloadOnly ? "模型文件已校验" : "文件与运行环境已校验") + " · " + DateTimeOffset.FromUnixTimeSeconds((long)check.RootElement.GetProperty("at").GetDouble()).ToLocalTime().ToString("MM-dd HH:mm");
            }
            var runFile = Path.Combine(Current(id), "run-verification.json");
            if (installed && !downloadOnly && File.Exists(runFile))
            {
                using var run = JsonDocument.Parse(File.ReadAllText(runFile));
                if (run.RootElement.GetProperty("signature").GetString() == VerificationSignature(id))
                    health = "短任务已验证 · " + run.RootElement.GetProperty("device").GetString() + " · " + run.RootElement.GetProperty("at").GetString();
            }
            var updateFile = Path.Combine(Current(id), "update-status.json");
            if (File.Exists(updateFile))
            {
                using var check = JsonDocument.Parse(File.ReadAllText(updateFile));
                available = check.RootElement.GetProperty("available").GetBoolean();
                message = check.RootElement.GetProperty("message").GetString() ?? "";
            }
        }
        catch (Exception ex) when (ex is IOException or JsonException or InvalidOperationException or KeyNotFoundException) { }
        if (installed && HasFailedHealth(id)) health = "需要修复 · 上次校验未通过";
        return new(installed, version, health, available, message);
    }

    private string VerificationSignature(string id)
    {
        var receipt = File.ReadAllText(Path.Combine(Current(id), "receipt.json"));
        var environment = File.ReadAllText(Path.Combine(Root, "envs", Models[id].Family, "aurora-runtime.json"));
        return Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(receipt + environment)));
    }

    public void RecordSuccessfulRun(string id, string device, string? expectedSignature = null)
    {
        if (!IsInstalled(id) || Models[id].DownloadOnly) return;
        if (expectedSignature is not null && expectedSignature != VerificationSignature(id)) return;
        File.WriteAllText(Path.Combine(Current(id), "run-verification.json"), JsonSerializer.Serialize(new { signature = VerificationSignature(id), device, at = DateTime.Now.ToString("yyyy-MM-dd HH:mm") }));
    }

    public async Task ManageAsync(string id, string action, IProgress<string> progress, CancellationToken token)
    {
        if (!Models.ContainsKey(id)) throw new InvalidOperationException("此模型尚未提供 Mac 适配器。");
        if (settings.Current.SafeMode && action != "check" && action != "updates") throw new InvalidOperationException("安全模式已启用。");
        await maintenanceGate.WaitAsync(token);
        try
        {
            BusyModel = id;
            var python = Path.Combine(Root, ".manager", "bin", "python");
            if (!File.Exists(python))
            {
                var uv = new[] { Path.Combine(Scripts, "bin", "uv"), "/opt/homebrew/bin/uv", Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local/bin/uv"), "/usr/local/bin/uv" }.FirstOrDefault(File.Exists)
                    ?? throw new InvalidOperationException("未找到 uv。请先安装 uv，或在维护页面查看诊断说明。");
                await RunAsync(uv, ["venv", "--python", "3.11", Path.Combine(Root, ".manager")], progress, token);
                await RunAsync(uv, ["pip", "install", "--python", python, "huggingface_hub>=0.34,<2", "filelock"], progress, token);
            }
            await RunAsync(python, [Path.Combine(Scripts, "manage.py"), "--root", Root, "--model", id, "--action", action], progress, token);
        }
        finally { BusyModel = null; maintenanceGate.Release(); }
    }

    public Process Start(string executable, IEnumerable<string> arguments, IReadOnlyDictionary<string, string>? environment = null)
    {
        var info = new ProcessStartInfo(executable) { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true, WorkingDirectory = Root };
        Directory.CreateDirectory(Root);
        foreach (var arg in arguments) info.ArgumentList.Add(arg);
        info.Environment["PATH"] = Path.Combine(Scripts, "bin") + ":/opt/homebrew/bin:/usr/local/bin:/usr/bin:/bin:" + Environment.GetEnvironmentVariable("PATH");
        info.Environment["PYTHONUNBUFFERED"] = "1";
        // Imported manager modules live in the signed app; keep that bundle immutable.
        info.Environment["PYTHONDONTWRITEBYTECODE"] = "1";
        info.Environment["PYTORCH_ENABLE_MPS_FALLBACK"] = "1";
        info.Environment["HF_HOME"] = Path.Combine(Root, "cache", "huggingface");
        info.Environment["TORCH_HOME"] = Path.Combine(Root, "cache", "torch");
        info.Environment["HF_HUB_DISABLE_TELEMETRY"] = "1";
        info.Environment["GRADIO_ANALYTICS_ENABLED"] = "False";
        info.Environment["DO_NOT_TRACK"] = "1";
        info.Environment["TOKENIZERS_PARALLELISM"] = "false";
        if (environment is not null) foreach (var item in environment) info.Environment[item.Key] = item.Value;
        var process = Process.Start(info) ?? throw new IOException("无法启动本地引擎。");
        lock (processes) processes.Add(process);
        return process;
    }

    public void Stop(Process process)
    {
        try { if (!process.HasExited) process.Kill(entireProcessTree: true); } catch (InvalidOperationException) { }
        lock (processes) processes.Remove(process);
    }

    public async Task RunAsync(string executable, IEnumerable<string> arguments, IProgress<string> progress, CancellationToken token)
    {
        using var process = Start(executable, arguments);
        using var registration = token.Register(() => Stop(process));
        var errors = new Queue<string>();
        async Task Read(StreamReader reader)
        {
            while (await reader.ReadLineAsync() is { } line)
            {
                lock (errors) { errors.Enqueue(line); while (errors.Count > 15) errors.Dequeue(); }
                progress.Report(line);
            }
        }
        try
        {
            await Task.WhenAll(Read(process.StandardOutput), Read(process.StandardError), process.WaitForExitAsync(token));
            token.ThrowIfCancellationRequested();
            if (process.ExitCode != 0) throw new IOException(string.Join(Environment.NewLine, errors));
        }
        finally { Stop(process); }
    }

    public void Report(string line)
    {
        LastMessage = line;
        Log?.Invoke(line);
    }

    public async Task<ModelWorkbenchConnection> OpenWorkbenchAsync(string id, string language, CancellationToken token)
    {
        if (settings.Current.SafeMode) throw new InvalidOperationException("安全模式已启用。");
        if (!IsReady(id)) throw new InvalidOperationException("模型或运行环境未完整安装，请先安装或修复。");
        var listener = new TcpListener(IPAddress.Loopback, 0); listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port; listener.Stop();
        var process = Start(Python(Models[id].Family), [Path.Combine(Scripts, "workbench.py"), "--root", Root, "--model", id, "--output", settings.Current.OutputRoot, "--port", port.ToString(), "--language", language],
            new Dictionary<string, string> {
                ["AURORA_RESULT_RECEIPTS"] = Path.Combine(settings.AppDataRoot, "WorkbenchReceipts"),
                ["AURORA_MODEL_VERSION"] = ModelStatus(id).Version,
                ["AURORA_RUNTIME_SIGNATURE"] = VerificationSignature(id)
            });
        var logDir = Path.Combine(settings.AppDataRoot, "EngineLogs"); Directory.CreateDirectory(logDir);
        var logfile = Path.Combine(logDir, id + "-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".log");
        var logGate = new object();
        async Task Read(StreamReader reader)
        {
            try
            {
                while (await reader.ReadLineAsync() is { } line)
                {
                    lock (logGate) File.AppendAllText(logfile, line + Environment.NewLine);
                    Report(line);
                }
            }
            catch (Exception ex) { Report(ex.Message); }
        }
        _ = Read(process.StandardOutput); _ = Read(process.StandardError);
        using var canceled = token.Register(() => Stop(process));
        var uri = new Uri($"http://127.0.0.1:{port}/");
        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(3) };
        try
        {
            while (true)
            {
                token.ThrowIfCancellationRequested();
                if (process.HasExited) throw new IOException("引擎启动失败。日志：" + logfile + "\n" + LastMessage);
                try { using var response = await client.GetAsync(uri, token); if (response.IsSuccessStatusCode) break; }
                catch (HttpRequestException) { }
                catch (TaskCanceledException) when (!token.IsCancellationRequested) { }
                await Task.Delay(700, token);
            }
            return new ModelWorkbenchConnection(uri, () => Stop(process));
        }
        catch { Stop(process); throw; }
    }

    public void Dispose() { Process[] running; lock (processes) running = processes.ToArray(); foreach (var process in running) Stop(process); }
}

public sealed class MacWorkbenchAdapter(MacRuntime runtime, string id) : IModelWorkbench
{
    public string ModelId => id;
    public bool IsReady => runtime.IsReady(id);
    public Task<ModelWorkbenchConnection> StartAsync(string language, CancellationToken cancellationToken) => runtime.OpenWorkbenchAsync(id, language, cancellationToken);
}

public sealed class MacUtilityAdapter(MacRuntime runtime, SettingsService settings, string id) : IStudioEngine
{
    public string ModelId => id;
    public bool IsReady => runtime.IsReady(id);
    public static string OutputPath(string root, string feature, string source)
    {
        var group = feature switch { "separation" => "AI分轨", "transcription" => "AI扒谱", "subtitles" => "AI字幕", _ => throw new ArgumentException("Unsupported utility feature.", nameof(feature)) };
        var name = string.Concat(Path.GetFileNameWithoutExtension(source).Where(c => !Path.GetInvalidFileNameChars().Contains(c)));
        if (name.Length > 60) name = name[..60];
        // Match ArtifactValidator.CreateRunDirectory without creating it: Python owns creation.
        return Path.Combine(root, group, $"{DateTime.Now:yyyyMMdd-HHmmss}-{name}-{Guid.NewGuid():N}");
    }
    public async Task<OperationResult> ExecuteAsync(StudioDraft draft, IProgress<TaskExecutionProgress> progress, CancellationToken token)
    {
        if (!IsReady) throw new InvalidOperationException("请先安装或修复选中的模型。");
        if (!File.Exists(draft.Source)) throw new FileNotFoundException("素材文件不存在。", draft.Source);
        var output = OutputPath(settings.Current.OutputRoot, draft.Feature, draft.Source);
        string[]? outputs = null;
        var message = "处理完成";
        var reporter = new InlineProgress<string>(line =>
        {
            if (line.StartsWith('{'))
            {
                try
                {
                    using var doc = JsonDocument.Parse(line); var value = doc.RootElement;
                    if (value.TryGetProperty("aurora", out var kind))
                    {
                        if (kind.GetString() == "result")
                        {
                            outputs = value.GetProperty("outputs").EnumerateArray().Select(x => x.GetString()!).ToArray();
                            if (value.TryGetProperty("message", out var resultMessage)) message = resultMessage.GetString() ?? message;
                        }
                        else if (kind.GetString() == "progress") progress.Report(new(value.GetProperty("progress").GetDouble(), value.GetProperty("stage").GetString()!, line));
                        return;
                    }
                }
                catch (JsonException) { }
            }
            progress.Report(new(null, "正在处理", line));
        });
        await runtime.RunAsync(runtime.Python(runtime.Models[id].Family), [Path.Combine(runtime.Scripts, "utility.py"), "--root", runtime.Root, "--model", id, "--source", draft.Source, "--output", output, "--language", draft.SourceLanguage], reporter, token);
        if (outputs is null || outputs.Length == 0) throw new InvalidDataException("引擎没有返回有效结果。");
        foreach (var file in outputs) ArtifactValidator.Validate(file);
        runtime.RecordSuccessfulRun(id, "CPU");
        return new OperationResult(true, message, output, Outputs: outputs, Device: "CPU");
    }
}

public sealed class InlineProgress<T>(Action<T> report) : IProgress<T> { public void Report(T value) => report(value); }
