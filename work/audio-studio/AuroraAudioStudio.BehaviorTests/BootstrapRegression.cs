using System.IO.Compression;
using System.Diagnostics;
using System.Net;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using AuroraAudioStudio.Models;
using AuroraAudioStudio.Services;

internal static class BootstrapRegression
{
    public static async Task RunAsync()
    {
        var root = Path.Combine(Path.GetTempPath(), "Aurora-bootstrap-regression-" + Guid.NewGuid().ToString("N"));
        var settings = new SettingsService(root);
        var catalog = new ModelCatalogService(settings);
        var candidate = RuntimeEnvironment.CreateCandidate(root);
        if (candidate.Length > 170 || !candidate.StartsWith(Path.Combine(root, ".aurora", "envs") + Path.DirectorySeparatorChar))
            throw new Exception("New native runtime path is not compact");
        var excessiveRoot = Path.Combine(root, new string('x', 150));
        var rejectedPath = false;
        try { RuntimeEnvironment.CreateCandidate(excessiveRoot); }
        catch (PathTooLongException) { rejectedPath = true; }
        if (!rejectedPath || Directory.Exists(excessiveRoot)) throw new Exception("Excessive model paths must fail before creating an environment or downloading dependencies");
        Console.WriteLine("PASS compact native runtime paths and early long-path rejection");
        var linkedRoot = Path.Combine(root, "linked");
        Directory.CreateDirectory(linkedRoot);
        await File.WriteAllTextAsync(Path.Combine(linkedRoot, "blob"), "real weight fixture");
        File.CreateSymbolicLink(Path.Combine(linkedRoot, "model.bin"), "blob");
        File.CreateSymbolicLink(Path.Combine(linkedRoot, "missing.bin"), "not-downloaded");
        await File.WriteAllTextAsync(Path.Combine(linkedRoot, "empty.bin"), "");
        var linkedModel = new ModelDefinition("linked", "Linked", "voice", "linked", "model.bin", "fixture", "huggingface", "fixture/model");
        if (!ModelHealthPolicy.IsReady(linkedModel, root) || ModelHealthPolicy.HasNonemptyFile(Path.Combine(linkedRoot, "missing.bin"))
            || ModelHealthPolicy.HasNonemptyFile(Path.Combine(linkedRoot, "empty.bin")))
            throw new Exception("Hugging Face links must follow their blob while dangling links and empty files remain invalid");
        Console.WriteLine("PASS model health follows valid cache links but rejects missing and empty targets");
        var hubFiles = ModelUpdateService.HubDownloadCommand("hf.exe", "repo/model", "local target", "revision", files: ["weights.pth", "config.yml"]);
        var hubPatterns = ModelUpdateService.HubDownloadCommand("hf.exe", "repo/model", includes: ["index.json", "transformer/*", "vocoder/*"]);
        if (hubFiles.ArgumentList.Contains("--include") || !hubFiles.ArgumentList.Take(4).SequenceEqual(new[] { "download", "repo/model", "weights.pth", "config.yml" })
            || hubPatterns.ArgumentList.Count(value => value == "--include") != 3 || hubPatterns.ArgumentList[4] != "--include")
            throw new Exception("HF filenames must be positional; each wildcard needs its own repeatable include option");
        Console.WriteLine("PASS Hub commands keep multiple filenames and wildcard filters distinct");
        var aceRoot = Path.Combine(root, "ace");
        var aceWeights = Path.Combine(aceRoot, "checkpoints", "acestep-v15-xl-turbo");
        Directory.CreateDirectory(aceWeights);
        await File.WriteAllTextAsync(Path.Combine(aceWeights, "silence_latent.pt"), "auxiliary data, not model weights");
        await File.WriteAllTextAsync(Path.Combine(aceWeights, "model.safetensors.index.json"), "{\"weight_map\":{\"one\":\"shard1.safetensors\",\"two\":\"shard2.safetensors\"}}");
        await File.WriteAllTextAsync(Path.Combine(aceWeights, "shard1.safetensors"), "first shard");
        if (ModelHealthPolicy.HasCompleteModelWeights(aceWeights) || !ModelUpdateService.AceDownloadCommand("download.exe", aceRoot, true).ArgumentList.Contains("--force"))
            throw new Exception("An interrupted ACE download must resume despite an existing folder, index and auxiliary tensor");
        await File.WriteAllTextAsync(Path.Combine(aceWeights, "shard2.safetensors"), "second shard");
        if (!ModelHealthPolicy.HasCompleteModelWeights(aceWeights) || ModelUpdateService.AceDownloadCommand("download.exe", aceRoot, true).ArgumentList.Contains("--force"))
            throw new Exception("A complete ACE checkpoint must not be forced through another download");
        Console.WriteLine("PASS ACE interrupted sharded downloads resume and complete checkpoints remain reusable");
        var git = new ProcessStartInfo("git.exe");
        git.Environment["GIT_CONFIG_COUNT"] = "1";
        git.Environment["GIT_CONFIG_KEY_0"] = "http.version";
        git.Environment["GIT_CONFIG_VALUE_0"] = "HTTP/1.1";
        git.Environment["GIT_SSL_NO_VERIFY"] = "1";
        var parentGitConfig = Environment.GetEnvironmentVariable("GIT_CONFIG_COUNT");
        BundledTools.Configure(git);
        BundledTools.Configure(git);
        if (git.Environment["GIT_CONFIG_COUNT"] != "4" || git.Environment["GIT_CONFIG_VALUE_0"] != "HTTP/1.1"
            || git.Environment["GIT_CONFIG_KEY_1"] != "http.sslBackend" || git.Environment["GIT_CONFIG_VALUE_1"] != "schannel"
            || git.Environment["GIT_CONFIG_VALUE_2"] != "true" || git.Environment.ContainsKey("GIT_SSL_NO_VERIFY")
            || git.Environment["GIT_CONFIG_KEY_3"] != "core.longpaths" || git.Environment["GIT_CONFIG_VALUE_3"] != "true"
            || Environment.GetEnvironmentVariable("GIT_CONFIG_COUNT") != parentGitConfig)
            throw new Exception("Git must use verified Windows trust without duplicating configuration or changing its parent");
        Console.WriteLine("PASS Git TLS overrides are child-only, idempotent and preserve unrelated settings");
        using var zip = new MemoryStream();
        using (var archive = new ZipArchive(zip, ZipArchiveMode.Create, true))
        {
            using var file = new StreamWriter(archive.CreateEntry("faster-whisper-xxl.exe").Open());
            file.Write("fixture, never executed");
        }
        var bytes = zip.ToArray();
        var downloads = 0;
        using var client = new HttpClient(new Handler(request =>
        {
            if (request.RequestUri!.Host == "example.invalid")
            {
                downloads++;
                return new(HttpStatusCode.OK) { Content = new ByteArrayContent(bytes) };
            }
            var asset = new { name = "Faster-Whisper-XXL_r245.2_windows.7z", browser_download_url = "https://example.invalid/runtime", size = bytes.Length, digest = "sha256:" + Convert.ToHexString(SHA256.HashData(bytes)) };
            return new(HttpStatusCode.OK) { Content = new StringContent(JsonSerializer.Serialize(new { assets = new[] { asset } })) };
        }));
        var updater = new ModelUpdateService(catalog, settings, client, client);
        var basic = updater.PackageInstallCommand("uv.exe", catalog.Find("basic-pitch")!, "python.exe", "0.4.0");
        var tensorflowWheel = Path.Combine(settings.Current.LocalAiRoot, ".aurora", "packages", "tensorflow_intel-2.15.0-cp311-cp311-win_amd64.whl");
        if (!basic.ArgumentList.Contains("basic-pitch==0.4.0") || basic.ArgumentList.Count(value => value == tensorflowWheel) != 1
            || !basic.ArgumentList.Contains("setuptools<81") || basic.ArgumentList.Contains("--no-deps"))
            throw new Exception("Basic Pitch must resolve its verified resumable TensorFlow wheel with all other dependencies");
        Console.WriteLine("PASS Basic Pitch resolves the verified local TensorFlow wheel without skipping dependency checks");
        Directory.CreateDirectory(Path.GetDirectoryName(tensorflowWheel)!);
        await File.WriteAllTextAsync(tensorflowWheel, "unverified cache, never activate");
        var tensorflowRequests = new List<HttpRequestMessage>();
        using var incompleteTensorflow = new HttpClient(new Handler(request =>
        {
            tensorflowRequests.Add(request);
            var response = new HttpResponseMessage(tensorflowRequests.Count == 1 ? HttpStatusCode.OK : HttpStatusCode.PartialContent)
                { Content = new ByteArrayContent([1, 2, 3]) };
            if (tensorflowRequests.Count == 2) response.Content.Headers.ContentRange = new(3, 5, 300919984);
            return response;
        }));
        var tensorflowUpdater = new ModelUpdateService(catalog, settings, incompleteTensorflow, incompleteTensorflow);
        var rejectedTensorflow = false;
        try { await tensorflowUpdater.EnsureBasicPitchTensorflowAsync(null, CancellationToken.None); }
        catch (IOException) { rejectedTensorflow = true; }
        if (!rejectedTensorflow || tensorflowRequests.Count != 2
            || tensorflowRequests[0].RequestUri!.Host != "files.pythonhosted.org"
            || tensorflowRequests[1].RequestUri!.Host != "pypi.tuna.tsinghua.edu.cn"
            || tensorflowRequests[1].Headers.Range?.Ranges.Single().From != 3
            || new FileInfo(tensorflowWheel + ".download.part").Length != 6
            || await File.ReadAllTextAsync(tensorflowWheel) != "unverified cache, never activate")
            throw new Exception("Basic Pitch must resume partial bytes and reject incomplete dependencies without replacing its cache");
        Console.WriteLine("PASS Basic Pitch retries the same pinned file with Range and preserves cache on failed verification");
        using var tensorflowCancel = new CancellationTokenSource();
        var canceledRequests = 0;
        using var canceledTensorflowClient = new HttpClient(new Handler(request =>
        {
            canceledRequests++;
            tensorflowCancel.Cancel();
            throw new OperationCanceledException(tensorflowCancel.Token);
        }));
        var canceledTensorflowUpdater = new ModelUpdateService(catalog, settings, canceledTensorflowClient, canceledTensorflowClient);
        var tensorflowCanceled = false;
        try { await canceledTensorflowUpdater.EnsureBasicPitchTensorflowAsync(null, tensorflowCancel.Token); }
        catch (OperationCanceledException) { tensorflowCanceled = true; }
        if (!tensorflowCanceled || canceledRequests != 1)
            throw new Exception("User cancellation must stop Basic Pitch download without requesting the alternate server");
        Console.WriteLine("PASS Basic Pitch user cancellation never starts a fallback download");
        foreach (var id in new[] { "transkun", "roformer", "yourmt3", "demucs", "f5-tts" })
        {
            var definition = catalog.Find(id)!;
            var command = updater.PackageInstallCommand("uv.exe", definition, "python.exe", "1.0");
            if (!command.ArgumentList.Contains(definition.Repository + "==1.0") || !command.ArgumentList.Contains("--torch-backend=cu128")
                || command.ArgumentList.Count(value => value == "torch==2.8.0") != 1 || !command.ArgumentList.Contains("torchaudio==2.8.0"))
                throw new Exception("Model and CUDA dependencies must resolve in one command: " + id);
            if (id == "transkun" && new[] { "ncls==0.0.68", "numpy<2", "--only-binary=ncls" }.Any(value => !command.ArgumentList.Contains(value)))
                throw new Exception("TransKun must use the official Windows NCLS wheel with its compatible NumPy ABI, never a user-side C compiler");
            if (id == "yourmt3" && new[] { "transformers==4.44.2", "numpy<2", "setuptools<81" }.Any(value => !command.ArgumentList.Contains(value)))
                throw new Exception("YourMT3 must keep the compatible T5 and native dependency interfaces");
            if (id == "demucs" && !command.ArgumentList.Contains("numpy<2"))
                throw new Exception("Demucs must supply NumPy even when upstream metadata omits its runtime import");
            if (id == "f5-tts" && (!command.ArgumentList.Contains("datasets>=3")
                || !command.ArgumentList.Any(value => value.StartsWith("torchcodec @ https://files.pythonhosted.org/", StringComparison.Ordinal)
                && value.Contains("torchcodec-0.7.0-cp311-cp311-win_amd64.whl#sha256=", StringComparison.Ordinal))))
                throw new Exception("F5 must use the compatible, hash-pinned Windows TorchCodec wheel instead of the Linux-only CUDA index");
        }
        Console.WriteLine("PASS model packages and matching CUDA dependencies are installed in one resolution");
        var parentJit = Environment.GetEnvironmentVariable("NUMBA_DISABLE_JIT");
        var piano = BackendService.PianoCommand("python.exe", "source.wav", "output.mid", "weights.pth", "cuda", root);
        if (piano.Environment["NUMBA_DISABLE_JIT"] != "0" || Environment.GetEnvironmentVariable("NUMBA_DISABLE_JIT") != parentJit
            || !piano.ArgumentList.Skip(2).SequenceEqual(new[] { "source.wav", "output.mid", "weights.pth", "cuda" }))
            throw new Exception("Piano must preserve its input, checkpoint and GPU selection without disabling librosa's JIT dependencies");
        Console.WriteLine("PASS Piano keeps Numba JIT available without changing the parent environment");
        var weight = ModelHealthPolicy.YourMt3CheckpointPath(settings.Current.LocalAiRoot);
        Directory.CreateDirectory(Path.GetDirectoryName(weight)!);
        await File.WriteAllTextAsync(weight, "existing checkpoint, must survive a failed replacement");
        var requestedCheckpoint = false;
        using var incompleteCheckpoint = new HttpClient(new Handler(request =>
        {
            requestedCheckpoint = request.RequestUri!.Host == "huggingface.co"
                && request.RequestUri.AbsolutePath.Contains("/resolve/5e66c1ea173a8186e0d20432b841d3180cc015b5/", StringComparison.Ordinal);
            return new(HttpStatusCode.OK) { Content = new ByteArrayContent([1, 2, 3]) };
        }));
        var checkedDownloader = new ModelUpdateService(catalog, settings, incompleteCheckpoint, incompleteCheckpoint);
        var rejectedCheckpoint = false;
        try { await checkedDownloader.EnsureYourMt3WeightsAsync(null, CancellationToken.None); }
        catch (IOException) { rejectedCheckpoint = true; }
        if (!requestedCheckpoint || !rejectedCheckpoint || await File.ReadAllTextAsync(weight) != "existing checkpoint, must survive a failed replacement")
            throw new Exception("Pinned YourMT3 download must preserve the original when verification fails");
        if ((await checkedDownloader.InspectRepairAsync(catalog.Find("yourmt3")!)).Kind != ModelRepairKind.FullRedeploy)
            throw new Exception("A corrupt YourMT3 checkpoint cannot be labeled healthy or repaired only by reinstalling Python");
        Console.WriteLine("PASS YourMT3 uses a pinned official file and preserves existing weights on an incomplete download");
        var miniMax = updater.MiniMaxInstallCommand("uv.exe", "python.exe");
        if (!miniMax.ArgumentList.Contains("torch==2.8.0") || !miniMax.ArgumentList.Contains("torchaudio==2.8.0")
            || !miniMax.ArgumentList.Contains("--torch-backend=cu128") || miniMax.ArgumentList.Contains("--upgrade"))
            throw new Exception("MiniMax dependencies must not replace CUDA torch in a later unconstrained upgrade");
        Console.WriteLine("PASS MiniMax resolves its model dependencies and CUDA pair together");
        var ensure = typeof(ModelUpdateService).GetMethod("EnsureWhisperRuntimeAsync", BindingFlags.NonPublic | BindingFlags.Instance)!;
        async Task<OperationResult> Run() => await (Task<OperationResult>)ensure.Invoke(updater, [null, CancellationToken.None])!;
        var result = await Run();
        if (!result.Success || !catalog.IsInstalled(catalog.Find("faster-whisper")!) || downloads != 1) throw new Exception("Whisper shared runtime was not provisioned: " + result.Message);
        Console.WriteLine("PASS missing Whisper shared runtime is downloaded, verified and installed before weights");
        if (!(await Run()).Success || downloads != 1) throw new Exception("Existing Whisper runtime was unnecessarily downloaded");
        Console.WriteLine("PASS existing Whisper shared runtime is retained without another download");
        var messages = new System.Collections.Concurrent.ConcurrentQueue<ModelInstallProgress>();
        var info = new ProcessStartInfo(Environment.ProcessPath!) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
        if (Path.GetFileNameWithoutExtension(info.FileName).Equals("dotnet", StringComparison.OrdinalIgnoreCase)) info.ArgumentList.Add(Assembly.GetExecutingAssembly().Location);
        info.ArgumentList.Add("--log-fixture"); info.ArgumentList.Add("wait");
        var runner = typeof(ModelUpdateService).GetMethod("RunProcessAsync", BindingFlags.Instance | BindingFlags.NonPublic)!;
        using var canceled = new CancellationTokenSource(TimeSpan.FromSeconds(11));
        var stopped = false;
        try { await (Task<(int, string, string)>)runner.Invoke(updater, [info, canceled.Token, new Reporter(messages.Enqueue)])!; }
        catch (OperationCanceledException) { stopped = true; }
        var waitingPrefix = new LocalizationService(settings).Get("maintenanceWaiting").Split("{0}")[0];
        if (!stopped || !messages.Any(item => item.LogLine?.StartsWith(waitingPrefix, StringComparison.Ordinal) == true)) throw new Exception("Long installation has no elapsed-time feedback or does not cancel");
        var count = messages.Count;
        await Task.Delay(200);
        if (count != messages.Count) throw new Exception("Canceled installation still reports progress");
        Console.WriteLine("PASS waiting step reports elapsed time, cancels and stops reporting after completion");
        Console.WriteLine("Fixtures retained: " + root);
    }
    private sealed class Handler(Func<HttpRequestMessage, HttpResponseMessage> handler) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => Task.FromResult(handler(request));
    }
    private sealed class Reporter(Action<ModelInstallProgress> report) : IProgress<ModelInstallProgress>
    {
        public void Report(ModelInstallProgress value) => report(value);
    }
}
