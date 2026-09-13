using System.Diagnostics;
using System.Reflection;
using System.Text;
using AuroraAudioStudio.Models;
using AuroraAudioStudio.Services;

internal static class RuntimeCopyRegression
{
    private sealed class EmptyManifest : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.RequestUri?.AbsoluteUri != "https://raw.githubusercontent.com/swy2018/Aurora-Audio-Studio/main/model-manifest.json") throw new InvalidOperationException("Unexpected network request");
            return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK) { Content = new StringContent("{\"Models\":[]}") });
        }
    }

    public static async Task RunAsync()
    {
        var root = Path.Combine(Path.GetTempPath(), "Aurora-RuntimeCopy-" + Guid.NewGuid().ToString("N"));
        var passed = 0;
        void Check(bool ok, string name) { if (!ok) throw new Exception(name); passed++; Console.WriteLine("PASS " + name); }
        foreach (var language in new[] { "zh-CN", "zh-TW", "en-US", "ja-JP" })
        {
            var settings = new SettingsService(Path.Combine(root, language));
            settings.Current.Language = language;
            var text = new LocalizationService(settings);
            Check(text.Translations.All(row => row.Length == 4 && row.All(value => !string.IsNullOrWhiteSpace(value))), language + " complete four-language rows");
            string[] Placeholders(string value) => System.Text.RegularExpressions.Regex.Matches(value, @"\{\d+(?:[^}]*)\}").Select(m => m.Value).Order().ToArray();
            Check(text.Translations.All(row => row.All(value => Placeholders(value).SequenceEqual(Placeholders(row[0])))), language + " translation placeholders preserved");
            if (language == "en-US")
            {
                string[] stages = ["正在校验官方模型文件", "正在校验 GitHub Release 摘要", "正在检查模型部署组件", "正在创建隔离运行环境", "正在安装模型部署组件 uv", "正在校验模型包完整性", "正在安装模型文件", "正在读取模型版本", "正在配置独立模型下载组件", "正在下载模型包", "正在配置 ACE-Step 隔离运行环境", "正在下载 ACE-Step 基础权重", "正在下载 ACE-Step 1.5 XL Turbo 官方权重", "正在配置 Seed-VC Python 3.10 隔离环境", "正在下载 Seed-VC 官方权重与配置", "正在下载 BS-RoFormer-SW 多轨权重", "正在下载 YourMT3+ 多乐器权重", "正在下载并配置 MiniMax-Music3 CUDA 运行环境（PyTorch 组件较大，请耐心等待）", "正在下载 MiniMax-Music3 官方模型（约 27 GB）", "正在创建 Qwen3-TTS Python 3.12 隔离环境", "正在安装 Qwen3-TTS 所需的 SoX 音频组件", "正在安装 FFmpeg 音频组件"];
                Check(stages.All(stage => !System.Text.RegularExpressions.Regex.IsMatch(text.Translate(stage), @"[\u4e00-\u9fff]")), "all authored maintenance stages translated");
            }
            var backend = new BackendService(settings);
            settings.Current.LocalAiRoot = Path.Combine(root, language, "models");
            using var metadata = new HttpClient(new EmptyManifest());
            var maintenance = new ModelUpdateService(new ModelCatalogService(settings), settings, metadataTransport: metadata);
            var model = new ModelDefinition("copy-fixture", "Log fixture", "fixture", "fixture", "missing.bin", "fixture", "fixture", IsRunnable: false);
            var maintenanceResult = await maintenance.UpdateAsync(model);
            var maintenanceLog = await File.ReadAllTextAsync(Directory.GetFiles(settings.LogsRoot, "model-maintenance-*.log").Single());
            Check(!maintenanceResult.Success && maintenanceLog.Contains(text.Get("logOperationFailed")) && !maintenanceLog.Contains("Success=False"), language + " model maintenance status localized");
            Check(maintenanceResult.Message == text.Translate("此引擎需要通过 Aurora 安装程序添加运行环境。"), language + " maintenance result translated");
            Check(!Directory.Exists(settings.Current.LocalAiRoot), language + " maintenance fixture does not install models");
            var statuses = new List<string>();
            backend.StatusChanged += (_, status) => statuses.Add(status);
            var method = typeof(BackendService).GetMethod("RunCapturedAsync", BindingFlags.Instance | BindingFlags.NonPublic)!;
            var output = Path.Combine(root, language, "output");
            Directory.CreateDirectory(output);
            using (var writer = new BinaryWriter(File.Create(Path.Combine(output, "sample.wav"))))
            {
                writer.Write(Encoding.ASCII.GetBytes("RIFF")); writer.Write(38);
                writer.Write(Encoding.ASCII.GetBytes("WAVEfmt ")); writer.Write(16);
                writer.Write((short)1); writer.Write((short)1); writer.Write(8000); writer.Write(16000);
                writer.Write((short)2); writer.Write((short)16); writer.Write(Encoding.ASCII.GetBytes("data")); writer.Write(2); writer.Write((short)100);
            }
            ProcessStartInfo Fixture(string mode)
            {
                var info = new ProcessStartInfo(Environment.ProcessPath!) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true, StandardOutputEncoding = Encoding.UTF8, StandardErrorEncoding = Encoding.UTF8 };
                if (Path.GetFileNameWithoutExtension(info.FileName).Equals("dotnet", StringComparison.OrdinalIgnoreCase)) info.ArgumentList.Add(Assembly.GetExecutingAssembly().Location);
                info.ArgumentList.Add("--log-fixture"); info.ArgumentList.Add(mode);
                return info;
            }
            async Task<OperationResult> Run(string mode, string prefix, CancellationToken token = default)
                => await (Task<OperationResult>)method.Invoke(backend, ["utility", Fixture(mode), prefix, output, null, token])!;

            var failed = await Run("fail", "copy-fail");
            Check(!failed.Success && failed.Message == text.Format("logTaskExit", 23), language + " failed exit is localized");
            var raw = await File.ReadAllTextAsync(failed.Path!);
            Check(raw.Contains("Starting upstream fixture Ω {0}") && raw.Contains(@"Traceback fixture C:\源音频\voice.wav"), language + " upstream stdout and stderr unchanged");
            Check(raw.Contains("[Aurora]") && raw.Contains(text.Get("logEngineOutput")) && raw.Contains(failed.Message), language + " log has localized source and terminal labels");
            Check(statuses.Contains("failed:copy-fail"), language + " machine status protocol unchanged");

            var complete = await Run("ok", "copy-ok");
            Check(complete.Success && complete.Message == text.Get("logTaskComplete") && complete.Outputs?.Count == 1, language + " successful output still collected");
            var successLog = Directory.GetFiles(settings.LogsRoot, "copy-ok-*.log").Single();
            Check((await File.ReadAllTextAsync(successLog)).Contains(text.Get("logTaskComplete")), language + " success log localized");

            using var cancellation = new CancellationTokenSource();
            var running = Run("wait", "copy-cancel", cancellation.Token);
            cancellation.CancelAfter(300);
            try { await running; throw new Exception("Cancellation not propagated"); }
            catch (OperationCanceledException) { Check(true, language + " cancellation propagated"); }
            var cancelLog = Directory.GetFiles(settings.LogsRoot, "copy-cancel-*.log").Single();
            Check((await File.ReadAllTextAsync(cancelLog)).Contains(text.Get("logTaskCanceled")) && !backend.HasActiveOperations, language + " canceled log and process ownership");
            var detail = @"C:\素材\voice {0}.wav";
            Check(text.Format("engineStartFailed", 23, detail).Contains(detail), language + " error paths and braces retained");
            Check(text.Get("logEngineStarting") != "logEngineStarting", language + " log key resolves");
        }
        Console.WriteLine($"Runtime copy checks passed: {passed}. Evidence: {root}");
    }
}
