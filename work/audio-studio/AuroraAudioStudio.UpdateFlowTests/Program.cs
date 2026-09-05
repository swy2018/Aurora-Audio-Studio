using AuroraAudioStudio.Services;
using AuroraAudioStudio.Models;
using System.Net;
using System.Text.RegularExpressions;

static void Require(bool condition, string message)
{
    if (condition) return;
    Console.Error.WriteLine(message);
    Environment.Exit(1);
}

var today = new DateOnly(2026, 8, 8);
Require(UpdateFlowGuard.ShouldRunDailyCheck(null, today), "A first-ever launch must check for updates.");
Require(!UpdateFlowGuard.ShouldRunDailyCheck("2026-08-08", today), "A second launch on the same day must not auto-check again.");
Require(UpdateFlowGuard.ShouldRunDailyCheck("2026-08-07", today), "The first launch on a new day must auto-check again.");
Require(UpdateFlowGuard.ShouldRunDailyCheck("invalid", today), "An invalid saved date must fail safe and run a check.");

var guard = new UpdateFlowGuard();
Require(guard.TryBegin(), "The first update flow must acquire the guard.");
Require(!guard.TryBegin(), "A concurrent update flow must be rejected.");
guard.End();
Require(guard.TryBegin(), "The guard must be reusable after the active flow ends.");
guard.End();

Require(DownloadResumeGuard.CanPromotePartial(HttpStatusCode.RequestedRangeNotSatisfiable, 109_471_580, 109_471_580), "A complete saved package rejected with HTTP 416 must proceed to the caller's integrity check.");
Require(!DownloadResumeGuard.CanPromotePartial(HttpStatusCode.RequestedRangeNotSatisfiable, 109_471_579, 109_471_580), "A partial package whose size differs from the official asset must restart instead of being promoted.");

var instanceName = @"Local\AuroraAudioStudio.Tests." + Guid.NewGuid().ToString("N");
using (var primary = new SingleInstanceGuard(instanceName))
using (var secondary = new SingleInstanceGuard(instanceName))
{
    Require(primary.IsPrimary, "The first Aurora process must own the single-instance guard.");
    Require(!secondary.IsPrimary, "A second Aurora process must not enter the application workspace.");
}

var transactionRoot = Path.Combine(Path.GetTempPath(), "aurora-model-transaction-" + Guid.NewGuid().ToString("N"));
var modelTarget = Path.Combine(transactionRoot, "model");
Directory.CreateDirectory(modelTarget);
File.WriteAllText(Path.Combine(modelTarget, "version.txt"), "old");
ModelInstallTransaction.Prepare(modelTarget);
File.WriteAllText(Path.Combine(ModelInstallTransaction.StagingPath(modelTarget), "version.txt"), "new");
ModelInstallTransaction.Commit(modelTarget);
Require(File.ReadAllText(Path.Combine(modelTarget, "version.txt")) == "new", "A verified model staging directory must become the active model atomically.");
Require(File.ReadAllText(Path.Combine(ModelInstallTransaction.PreviousPath(modelTarget), "version.txt")) == "old", "The previous model version must remain recoverable.");
Require(ModelInstallTransaction.RestorePrevious(modelTarget), "The previous model version must be restorable without downloading it again.");
Require(File.ReadAllText(Path.Combine(modelTarget, "version.txt")) == "old", "Rollback must restore the previous model directory.");

var legacyRecord = ProjectDocumentMigrator.Read("{\"Name\":\"Legacy record\",\"Feature\":\"subtitles\"}");
Require(legacyRecord.SchemaVersion == ProjectDocumentMigrator.CurrentSchemaVersion, "Legacy processing records without a schema number must migrate to the current schema.");
var rejectedFutureRecord = false;
try { ProjectDocumentMigrator.Read("{\"SchemaVersion\":99}"); }
catch (InvalidDataException) { rejectedFutureRecord = true; }
Require(rejectedFutureRecord, "A processing record from a newer unsupported schema must be preserved for recovery instead of being misread.");

var currentProcessRoot = Path.GetDirectoryName(Environment.ProcessPath!)!;
Require(!string.IsNullOrWhiteSpace(RunningProcessGuard.FindInRoot(currentProcessRoot)), "The process guard must detect a running executable inside the component root.");
var arguments = UpdateFlowGuard.BuildInstallerArguments(321, @"C:\Logs\update.log");
Require(arguments.Contains("/SILENT", StringComparison.Ordinal), "Automatic updates must show only Inno Setup's standard installation progress window.");
Require(!arguments.Contains("/VERYSILENT", StringComparison.Ordinal), "Automatic updates must not hide the standard installer progress window.");
Require(arguments.Contains("/UPDATE", StringComparison.Ordinal), "The installer must use update mode.");
Require(arguments.Contains("/KEEPUSERDATA", StringComparison.Ordinal), "Automatic updates must preserve personal settings.");
Require(arguments.Contains("/NORESTART", StringComparison.Ordinal), "The installer must never reboot Windows.");
Require(arguments.Contains("/UPDATEPID=321", StringComparison.Ordinal), "The installer must know which Aurora process is exiting.");
Require(!arguments.Contains("/SUPPRESSMSGBOXES", StringComparison.Ordinal), "Fatal installer errors must remain visible to the user.");

Require(args.Length == 1 && File.Exists(args[0]), "Pass the Aurora Inno Setup script to verify the restart handoff.");
var installerScript = File.ReadAllText(args[0]);
Require(installerScript.Contains("RestartApplications=no", StringComparison.Ordinal), "Restart Manager must not race the explicit Aurora relaunch.");
Require(installerScript.Contains("UsePreviousAppDir=yes", StringComparison.Ordinal), "Automatic updates must preserve a user-selected installation directory.");
Require(installerScript.Contains("SetupMutex=AuroraAudioStudioInstaller", StringComparison.Ordinal), "Only one Aurora installer may run at a time.");
Require(!installerScript.Contains("UpdateForm := CreateCustomForm(", StringComparison.Ordinal), "Automatic updates must not display a second custom updater window.");
Require(!installerScript.Contains("procedure CurInstallProgressChanged", StringComparison.Ordinal), "Only Inno Setup should own installation progress.");
Require(installerScript.Contains("AppIcon-{#MyAppVersion}.ico", StringComparison.Ordinal), "Each release must use a versioned icon path to bypass stale Windows icon caches.");
Require(!installerScript.Contains("function PrepareToInstall", StringComparison.Ordinal), "Updates must use Inno Setup's in-place upgrade instead of launching the old uninstaller.");
Require(!installerScript.Contains("UninstallString", StringComparison.Ordinal), "The installer must not parse and execute a registry uninstall command during an upgrade.");
Require(installerScript.Contains("Flags: nowait runascurrentuser; Check: IsAutomaticUpdate", StringComparison.Ordinal), "A successful automatic update must relaunch Aurora as the signed-in user.");
Require(installerScript.Contains("Result := HasCommandLineParam('/UPDATE')", StringComparison.Ordinal), "The installer must recognize automatic update mode.");

var audioStudioRoot = Path.GetDirectoryName(args[0])!;
var mainPageXaml = File.ReadAllText(Path.Combine(audioStudioRoot, "AuroraAudioStudio", "MainPage.xaml"));
Require(Regex.Matches(mainPageXaml, "AccessKey=\\\"").Count >= 6, "All six Home workflows must expose keyboard access keys.");
Require(mainPageXaml.Contains("AutomationProperties.LiveSetting=\"Polite\"", StringComparison.Ordinal), "Progress and status changes must be announced to assistive technologies.");
Require(mainPageXaml.Contains("AutomationProperties.Name=\"本地 AI 创作工作台\"", StringComparison.Ordinal), "The embedded workbench must have a screen-reader name.");

var repositoryRoot = Path.GetFullPath(Path.Combine(audioStudioRoot, "..", ".."));
var workflow = File.ReadAllText(Path.Combine(repositoryRoot, ".github", "workflows", "build.yml"));
Require(workflow.Contains("AuroraAudioStudio.UpdateFlowTests", StringComparison.Ordinal), "CI must run the regression program before packaging.");
using var releaseMetadata = System.Text.Json.JsonDocument.Parse(File.ReadAllText(Path.Combine(repositoryRoot, "docs", "release.json")));
var currentVersion = releaseMetadata.RootElement.GetProperty("version").GetString()!;
Require(File.ReadAllText(Path.Combine(audioStudioRoot, "AuroraAudioStudio", "AuroraAudioStudio.csproj")).Contains($"<Version>{currentVersion}</Version>", StringComparison.Ordinal), "The application version must match release metadata.");
Require(installerScript.Contains($"MyAppVersion \"{currentVersion}\"", StringComparison.Ordinal), "The installer version must match release metadata.");
Require(File.ReadAllText(Path.Combine(repositoryRoot, "README.md")).Contains($"Aurora-Audio-Studio-{currentVersion}-Setup-x64.exe", StringComparison.Ordinal), "README downloads must match release metadata.");
Require(File.ReadAllText(Path.Combine(repositoryRoot, "docs", "index.html")).Contains($"Download {currentVersion}", StringComparison.Ordinal), "Website downloads must match release metadata.");
foreach (var badge in new[] { "download", "changelog" }) Require(File.ReadAllText(Path.Combine(repositoryRoot, "docs", "assets", $"readme-button-{badge}.svg")).Contains($">{currentVersion}</text>", StringComparison.Ordinal), "README badge version must match release metadata.");

var qwen = new ModelDefinition("qwen3-tts-06b-base", "Qwen3-TTS 0.6B", "voice", @"Qwen3-TTS\models\Qwen3-TTS-12Hz-0.6B-Base", "model.safetensors", "Qwen", "huggingface", "Qwen/Qwen3-TTS-12Hz-0.6B-Base");
var plan = ModelInstallPlanner.Create(qwen, @"D:\AuroraModels");
Require(plan.TargetPath == @"D:\AuroraModels\Qwen3-TTS\models\Qwen3-TTS-12Hz-0.6B-Base", "The model plan must show the exact target directory.");
Require(plan.EstimatedDownload == "≈ 1.5 GB", "The Qwen 0.6B plan must show its estimated download size.");
Require(plan.RecommendedFreeSpace == "≈ 3 GB", "The Qwen 0.6B plan must show recommended free disk space.");
var minimax = new ModelDefinition("minimax-music3", "MiniMax-Music3", "music", "MiniMax-Music3", "modular_model_index.json", "MiniMax", "minimax-music3", "MiniMaxAI/MiniMax-Music3");
var minimaxPlan = ModelInstallPlanner.Create(minimax, @"D:\AuroraModels");
Require(minimaxPlan.EstimatedDownload == "≈ 27 GB" && minimaxPlan.RecommendedFreeSpace == "≈ 55 GB", "MiniMax-Music3 must disclose its large on-demand download and staging space.");
var heartmula = new ModelDefinition("heartmula-3b", "HeartMuLa 3B", "music", @"AudioTools\heartmula-models\HeartMuLa-oss-3B-happy-new-year", "model.safetensors.index.json", "HeartMuLa", "huggingface", "HeartMuLa/HeartMuLa-oss-3B-happy-new-year");
var heartmulaPlan = ModelInstallPlanner.Create(heartmula, @"D:\AuroraModels");
Require(heartmulaPlan.EstimatedDownload == "≈ 15.8 GB" && heartmulaPlan.RecommendedFreeSpace == "≈ 32 GB", "HeartMuLa must disclose its full official checkpoint and staging space.");
var qwenAsr = new ModelDefinition("qwen3-asr-17b", "Qwen3-ASR 1.7B", "subtitles", @"AudioTools\qwen3-asr-models\Qwen3-ASR-1.7B-hf", "model.safetensors", "Qwen", "huggingface", "Qwen/Qwen3-ASR-1.7B-hf");
var qwenAsrPlan = ModelInstallPlanner.Create(qwenAsr, @"D:\AuroraModels");
Require(qwenAsrPlan.EstimatedDownload == "≈ 4.1 GB" && qwenAsrPlan.RecommendedFreeSpace == "≈ 9 GB", "Qwen3-ASR 1.7B must disclose its official checkpoint and staging space.");
var transkun = new ModelDefinition("transkun", "TransKun V2", "transcription", @"AudioTools\transkun-env", @"Scripts\transkun.exe", "PyPI", "uv-package", "transkun", true);
var transkunPlan = ModelInstallPlanner.Create(transkun, @"D:\AuroraModels");
Require(transkunPlan.TargetPath.EndsWith(@"AudioTools\transkun-env", StringComparison.OrdinalIgnoreCase), "TransKun must use an isolated model environment.");
var seed = new ModelDefinition("seed-vc", "Seed-VC 44.1k", "singing", "Seed-VC", "app_svc_local.py", "GitHub Release + Hugging Face", "github-release-git", "https://github.com/Plachtaa/seed-vc.git", true);
var seedHealthRoot = Path.Combine(Path.GetTempPath(), "aurora-seed-health-" + Guid.NewGuid().ToString("N"));
var seedRoot = Path.Combine(seedHealthRoot, "Seed-VC");
foreach (var directory in new[]
{
    Path.Combine(seedRoot, ".venv", "Scripts"),
    Path.Combine(seedRoot, "checkpoints", "manual")
}) Directory.CreateDirectory(directory);
foreach (var file in new[]
{
    Path.Combine(seedRoot, "app_svc_local.py"),
    Path.Combine(seedRoot, ".venv", "Scripts", "python.exe"),
    Path.Combine(seedRoot, "checkpoints", "manual", "DiT_seed_v2_uvit_whisper_base_f0_44k_bigvgan_pruned_ft_ema_v2.pth"),
    Path.Combine(seedRoot, "checkpoints", "manual", "config_dit_mel_seed_uvit_whisper_base_f0_44k.yml")
}) File.WriteAllText(file, "fixture");
var missingSeedDependencies = ModelHealthPolicy.MissingRequirements(seed, seedHealthRoot);
Require(missingSeedDependencies.Any(item => item.Contains("BigVGAN", StringComparison.Ordinal))
    && missingSeedDependencies.Any(item => item.Contains("Whisper", StringComparison.Ordinal))
    && missingSeedDependencies.Any(item => item.Contains("CAMPPlus", StringComparison.Ordinal))
    && missingSeedDependencies.Any(item => item.Contains("RMVPE", StringComparison.Ordinal)), "Seed-VC must not be reported ready when an auxiliary model cache is absent.");
foreach (var cache in new[]
{
    (Path.Combine(seedRoot, "checkpoints"), "models--funasr--campplus", new[] { "campplus_cn_common.bin" }),
    (Path.Combine(seedRoot, "checkpoints"), "models--lj1995--VoiceConversionWebUI", new[] { "rmvpe.pt" }),
    (Path.Combine(seedRoot, "checkpoints", "hf_cache"), "models--nvidia--bigvgan_v2_44khz_128band_512x", new[] { "bigvgan_generator.pt", "config.json" }),
    (Path.Combine(seedRoot, "checkpoints", "hf_cache"), "models--openai--whisper-small", new[] { "model.safetensors", "config.json", "preprocessor_config.json" })
})
{
    const string revision = "verified-snapshot";
    var repositoryCache = Path.Combine(cache.Item1, cache.Item2);
    Directory.CreateDirectory(Path.Combine(repositoryCache, "refs"));
    Directory.CreateDirectory(Path.Combine(repositoryCache, "snapshots", revision));
    File.WriteAllText(Path.Combine(repositoryCache, "refs", "main"), revision);
    foreach (var file in cache.Item3) File.WriteAllText(Path.Combine(repositoryCache, "snapshots", revision, file), "fixture");
}
Require(ModelHealthPolicy.IsReady(seed, seedHealthRoot), "Seed-VC must become ready only after every offline auxiliary model snapshot is complete.");

var catalogSource = File.ReadAllText(Path.Combine(audioStudioRoot, "AuroraAudioStudio", "Services", "ModelCatalogService.cs"));
Require(catalogSource.Contains("new(\"minimax-music3\"", StringComparison.Ordinal), "Model Management must expose MiniMax-Music3.");
Require(catalogSource.Contains("new(\"transkun\"", StringComparison.Ordinal), "Model Management must expose TransKun V2.");
foreach (var candidate in new[] { "heartmula-3b", "indextts-2-5", "soulx-singer-svc", "qwen3-asr-06b", "qwen3-asr-17b", "qwen3-forced-aligner" })
    Require(catalogSource.Contains($"new(\"{candidate}\"", StringComparison.Ordinal), $"Model Management must expose optional candidate {candidate}.");
var mainPageSource = File.ReadAllText(Path.Combine(audioStudioRoot, "AuroraAudioStudio", "MainPage.xaml.cs"));
var modelUpdateSource = File.ReadAllText(Path.Combine(audioStudioRoot, "AuroraAudioStudio", "Services", "ModelUpdateService.cs"));
var taskQueueSource = File.ReadAllText(Path.Combine(audioStudioRoot, "AuroraAudioStudio", "Services", "TaskQueueService.cs"));
var settingsSource = File.ReadAllText(Path.Combine(audioStudioRoot, "AuroraAudioStudio", "Services", "SettingsService.cs"));
Require(mainPageXaml.Contains("x:Name=\"UpdateAllModelsButton\"", StringComparison.Ordinal), "Model Management must expose an Update all button after checking updates.");
Require(mainPageXaml.Contains("x:Name=\"InstallWorkbenchModelButton\"", StringComparison.Ordinal), "The workbench must expose a dedicated install button for the selected missing model.");
Require(mainPageSource.Contains("ModelPicker.SelectionChanged += ModelPicker_SelectionChanged", StringComparison.Ordinal), "The workbench model picker must refresh its actions after page initialization.");
Require(mainPageSource.Contains("x.Feature == tag && x.IsRunnable", StringComparison.Ordinal), "Model-management-only candidates must not appear in a workflow before an execution adapter exists.");
Require(mainPageXaml.Contains("AutomationProperties.Name=\"{Binding Name}\"", StringComparison.Ordinal), "Every model row must expose its model name to assistive technologies.");
Require(mainPageXaml.Contains("ToolTipService.ToolTip=\"{Binding LocalPath}\"", StringComparison.Ordinal), "A truncated model path must remain discoverable.");
Require(mainPageXaml.Contains("Content=\"{Binding RollbackAction}\"", StringComparison.Ordinal) && mainPageXaml.Contains("Content=\"{Binding UninstallAction}\"", StringComparison.Ordinal), "Virtualized model actions must use localized data instead of fixed Chinese labels.");
Require(!mainPageXaml.Contains("<ColumnDefinition Width=\"260\"/><ColumnDefinition Width=\"210\"/>", StringComparison.Ordinal), "Model rows must not rely on the old fixed-width four-column table layout.");
Require(mainPageSource.Contains("modelUpdateChecks", StringComparison.Ordinal), "Model update check results must remain available to drive update actions.");
Require(mainPageSource.Contains("UpdateAllModelsButton_Click", StringComparison.Ordinal), "The Update all button must execute available model updates.");
Require(mainPageSource.Contains("private readonly UpdateFlowGuard modelInstallFlow = new();", StringComparison.Ordinal)
    && mainPageSource.Contains("if (!modelInstallFlow.TryBegin())", StringComparison.Ordinal)
    && mainPageSource.Contains("modelInstallFlow.End();", StringComparison.Ordinal), "Model installation must reject a second concurrent operation and release its guard afterward.");
Require(mainPageSource.Contains("$\"{catalog.DisplayName(model)} · {displayProgress.Detail}\"", StringComparison.Ordinal)
    && mainPageSource.Contains("Stage = localization.Translate(value.Stage)", StringComparison.Ordinal), "Model installation progress must identify the localized model and stage while retaining transfer details.");
Require(modelUpdateSource.Contains("PyTorch 组件较大，请耐心等待", StringComparison.Ordinal), "Large CUDA dependency installs must explain that an indeterminate wait can still be active.");
Require(modelUpdateSource.Contains("IsProgressNoise", StringComparison.Ordinal)
    && modelUpdateSource.Contains("Using Python", StringComparison.Ordinal), "uv environment headers must not replace the active installation stage in the progress UI.");
Require(taskQueueSource.Contains("if (task.Status == AuroraTaskStates.Canceled)", StringComparison.Ordinal), "A queued task canceled before execution must never be reset to waiting and run later.");
var cancelHandler = Regex.Match(mainPageSource, @"private void CancelTaskButton_Click[\s\S]*?\n    }").Value;
Require(!cancelHandler.Contains("backend.StopAll()", StringComparison.Ordinal), "Canceling one task must not stop an unrelated workbench or another queued task.");
Require(mainPageSource.Contains("entry.Task.Feature, entry.Task.InputPath, entry.Task.ModelId", StringComparison.Ordinal), "Queued work must execute its immutable task feature and model even after navigation changes.");
Require(mainPageXaml.Contains("IsEnabled=\"{Binding CanCancel}\"", StringComparison.Ordinal)
    && mainPageXaml.Contains("IsEnabled=\"{Binding CanRetry}\"", StringComparison.Ordinal), "Task actions must reflect whether each task can be canceled or retried.");
Require(modelUpdateSource.Contains("EnsureHuggingFaceBootstrapAsync", StringComparison.Ordinal), "Hugging Face downloads must bootstrap independently instead of depending on an installed Qwen runtime.");
Require(catalogSource.Contains("ModelHealthPolicy", StringComparison.Ordinal), "Model readiness must verify the runtime files required by the backend, not only one marker file.");
Require(settingsSource.Contains("TrySave", StringComparison.Ordinal) && settingsSource.Contains("SettingsPathValidator", StringComparison.Ordinal), "Settings must validate all storage paths before replacing the active configuration.");
Require(modelUpdateSource.Contains("SemaphoreSlim(4", StringComparison.Ordinal)
    && modelUpdateSource.Contains("ModelCheckProgress", StringComparison.Ordinal)
    && modelUpdateSource.Contains("CancellationToken cancellationToken", StringComparison.Ordinal), "Check all must use bounded concurrency, progress, and cancellation.");
Require(mainPageXaml.Contains("x:Name=\"UpdateLogText\"", StringComparison.Ordinal)
    && mainPageXaml.Contains("TextWrapping=\"Wrap\"", StringComparison.Ordinal), "Long model installation activity must remain readable and expose recent details.");
Require(MediaInputPolicy.IsSupported("subtitles", "clip.mp4") && MediaInputPolicy.IsSupported("separation", "song.flac"), "Media intake must accept supported feature-specific formats.");
Require(MediaInputPolicy.IsSupported("transcription", "clip.exe") == false, "Media intake must reject unsupported executable files.");
Require(SettingsPathValidator.TryValidate(@"C:\", @"C:\Output", @"C:\Projects", out _) == false
    && SettingsPathValidator.TryValidate(@"C:\LocalAI", @"C:\Output", @"C:\Projects", out _), "Settings paths must reject a drive root while accepting scoped absolute folders.");
var missingRuntimeModel = new ModelDefinition("qwen3-tts-06b-base", "Qwen", "voice", @"Qwen3-TTS\models\Qwen3-TTS-12Hz-0.6B-Base", "model.safetensors", "test", "huggingface");
Require(ModelHealthPolicy.IsReady(missingRuntimeModel, Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"))) == false, "A model without its required runtime and marker must not be reported as ready.");
var healthRoot = Path.Combine(Path.GetTempPath(), "aurora-health-" + Guid.NewGuid().ToString("N"));
var aceRoot = Path.Combine(healthRoot, "ACE-Step-1.5");
var aceModel = new ModelDefinition("ace-step", "ACE-Step", "music", "ACE-Step-1.5", @"acestep\acestep_v15_pipeline.py", "test", "github-release-git", "test", true);
var aceFiles = new[]
{
    Path.Combine(aceRoot, "acestep", "acestep_v15_pipeline.py"),
    Path.Combine(aceRoot, "python_embeded", "python.exe"),
    Path.Combine(aceRoot, "checkpoints", "acestep-v15-turbo", "model.safetensors"),
    Path.Combine(aceRoot, "checkpoints", "acestep-v15-xl-turbo", "model-00001-of-00004.safetensors"),
    Path.Combine(aceRoot, "checkpoints", "acestep-5Hz-lm-1.7B", "model.safetensors"),
    Path.Combine(aceRoot, "checkpoints", "Qwen3-Embedding-0.6B", "model.safetensors"),
    Path.Combine(aceRoot, "checkpoints", "vae", "diffusion_pytorch_model.safetensors")
};
foreach (var path in aceFiles.Take(2)) { Directory.CreateDirectory(Path.GetDirectoryName(path)!); File.WriteAllBytes(path, [1]); }
foreach (var path in aceFiles.Skip(2)) Directory.CreateDirectory(Path.GetDirectoryName(path)!);
Require(!ModelHealthPolicy.IsReady(aceModel, healthRoot), "ACE-Step folders without real checkpoint files must never be reported as ready.");
foreach (var path in aceFiles.Skip(2)) File.WriteAllBytes(path, [1]);
Require(ModelHealthPolicy.IsReady(aceModel, healthRoot), "ACE-Step must become ready when every official runtime checkpoint is present.");
var transkunRoot = Path.Combine(healthRoot, "AudioTools", "transkun-env");
var transkunHealthModel = new ModelDefinition("transkun", "TransKun", "transcription", @"AudioTools\transkun-env", @"Scripts\transkun.exe", "test", "uv-package", "transkun", true);
var transkunExe = Path.Combine(transkunRoot, "Scripts", "transkun.exe");
Directory.CreateDirectory(Path.GetDirectoryName(transkunExe)!); File.WriteAllBytes(transkunExe, [1]);
Require(!ModelHealthPolicy.IsReady(transkunHealthModel, healthRoot), "TransKun without pkg_resources must be reported as broken instead of ready.");
var pkgResources = Path.Combine(transkunRoot, "Lib", "site-packages", "pkg_resources", "__init__.py");
Directory.CreateDirectory(Path.GetDirectoryName(pkgResources)!); File.WriteAllBytes(pkgResources, [1]);
var torchAudioLibrary = Path.Combine(transkunRoot, "Lib", "site-packages", "torchaudio", "lib", "libtorchaudio.pyd");
Directory.CreateDirectory(Path.GetDirectoryName(torchAudioLibrary)!); File.WriteAllBytes(torchAudioLibrary, [1]);
var torchVersion = Path.Combine(transkunRoot, "Lib", "site-packages", "torch-2.13.0.dist-info");
var torchAudioVersion = Path.Combine(transkunRoot, "Lib", "site-packages", "torchaudio-2.11.0+cu128.dist-info");
Directory.CreateDirectory(torchVersion); Directory.CreateDirectory(torchAudioVersion);
Require(!ModelHealthPolicy.IsReady(transkunHealthModel, healthRoot), "TransKun with mismatched torch and torchaudio versions must be reported as broken.");
var matchingTorchVersion = Path.Combine(transkunRoot, "Lib", "site-packages", "torch-2.11.0+cu128.dist-info");
Directory.Move(torchVersion, matchingTorchVersion);
Require(ModelHealthPolicy.IsReady(transkunHealthModel, healthRoot), "TransKun must become ready after setuptools and a matching torch audio runtime are present.");
Require(mainPageSource.Contains("PrimaryAction = localization.Translate(\"更新\")", StringComparison.Ordinal), "A detected model update must replace the generic card action with Update.");
Require(mainPageSource.Contains("new OperationResult(false, result.Message, \"available\")", StringComparison.Ordinal), "A failed batch update must remain available for retry.");
Require(modelUpdateSource.Contains("RunningProcessGuard.FindInRoot", StringComparison.Ordinal), "Model updates must detect a running component before replacing its files.");
Require(modelUpdateSource.Contains("CheckGitHubRepositoryReleaseAsync", StringComparison.Ordinal)
    && modelUpdateSource.Contains("/releases/latest", StringComparison.Ordinal)
    && modelUpdateSource.Contains("checkout --detach refs/tags/", StringComparison.Ordinal), "ACE updates must follow GitHub formal releases instead of the development branch.");
Require(modelUpdateSource.Contains("default_branch", StringComparison.Ordinal)
    && modelUpdateSource.Contains("/commits/", StringComparison.Ordinal)
    && modelUpdateSource.Contains("refs/heads/", StringComparison.Ordinal)
    && modelUpdateSource.Contains("日期版", StringComparison.Ordinal), "A Git repository without formal Releases must fall back to its official default-branch date version and exact commit.");
Require(modelUpdateSource.Contains("lastModified", StringComparison.Ordinal)
    && modelUpdateSource.Contains("HuggingFaceVersion", StringComparison.Ordinal)
    && modelUpdateSource.Contains(".aurora-version", StringComparison.Ordinal), "Hugging Face models must display the official date version while retaining an exact snapshot revision.");
Require(catalogSource.Contains("\"github-release-git\"", StringComparison.Ordinal), "ACE must use the formal GitHub Release update policy.");
Require(catalogSource.Contains("\"seed-vc\"", StringComparison.Ordinal)
    && Regex.Matches(catalogSource, "\"github-release-git\"").Count == 2, "Every Git-backed model must prefer formal GitHub Releases before the date-version fallback.");
Require(!catalogSource.Contains("\"git-hf\"", StringComparison.Ordinal), "No model may continue tracking Git development branches.");
Require(mainPageXaml.Contains("x:Name=\"ModelGroupsSource\"", StringComparison.Ordinal)
    && mainPageXaml.Contains("<ListView.GroupStyle>", StringComparison.Ordinal)
    && mainPageSource.Contains("ModelFeatureOrder = [\"music\", \"voice\", \"singing\", \"separation\", \"transcription\", \"subtitles\"]", StringComparison.Ordinal), "Model Management must group models in the six product-feature categories.");
Require(modelUpdateSource.Contains("DownloadResumeGuard.CanPromotePartial", StringComparison.Ordinal) && modelUpdateSource.Contains("cancellationToken, release.Size", StringComparison.Ordinal), "GitHub Release downloads must route HTTP 416 through the verified partial-package guard.");
Require(mainPageSource.Contains("InstallWorkbenchModelButton_Click", StringComparison.Ordinal), "The dedicated workbench install button must invoke the on-demand installer.");
Require(mainPageSource.Contains("ShowModelInUseDialogAsync", StringComparison.Ordinal), "A blocked model update must explain how to close the component and retry safely.");
Require(modelUpdateSource.Contains("catch (IOException", StringComparison.Ordinal), "A late file-lock race must return an actionable retry state instead of a raw exception.");
Require(mainPageSource.Contains("(\"transcription\", _, _) => \"transkun\"", StringComparison.Ordinal), "TransKun must be the default recommended piano transcription engine.");
Require(mainPageSource.Contains("InstallWorkbenchModelButton.Visibility = installed ? Visibility.Collapsed : Visibility.Visible", StringComparison.Ordinal) && mainPageSource.Contains("OpenWorkbenchButton.Visibility = installed ? Visibility.Visible : Visibility.Collapsed", StringComparison.Ordinal), "An uninstalled workbench model must expose a dedicated Install button instead of the Open button.");
Require(mainPageSource.Contains("!await InstallSelectedModelAsync(model)", StringComparison.Ordinal), "Opening an uninstalled workbench model must start the on-demand installer.");
Require(mainPageSource.Contains("(\"separation\", \"two-stem\", _) => \"roformer-vocals\"", StringComparison.Ordinal), "Two-stem separation must select the dedicated vocals/instrumental model.");
Require(mainPageSource.Contains("(\"separation\", \"multi-stem\", \"fast\") => \"demucs\"", StringComparison.Ordinal), "Fast multi-stem separation must retain Demucs.");
Require(catalogSource.Contains("new(\"roformer-vocals\"", StringComparison.Ordinal), "Model Management must expose the dedicated two-stem BS-RoFormer model.");
Require(!catalogSource.Contains("\"python-tool\"", StringComparison.Ordinal) && !catalogSource.Contains("\"direct\"", StringComparison.Ordinal), "All catalog components must use an automatic update or repair adapter.");
var backendSource = File.ReadAllText(Path.Combine(audioStudioRoot, "AuroraAudioStudio", "Services", "BackendService.cs"));
Require(backendSource.Contains("[\"HF_HUB_OFFLINE\"] = \"1\"", StringComparison.Ordinal), "ACE-Step must start offline so the workbench can never trigger a hidden multi-gigabyte download.");
Require(backendSource.Contains("process.HasExited", StringComparison.Ordinal) && backendSource.Contains("ReadLogTail", StringComparison.Ordinal), "Workbench startup must stop promptly when its child process exits and surface the diagnostic log tail.");
Require(!backendSource.Contains("_ => \"medium\"", StringComparison.Ordinal), "Unknown subtitle components must fail explicitly instead of silently routing to Whisper medium.");
Require(modelUpdateSource.Contains("setuptools", StringComparison.Ordinal), "TransKun installation must include pkg_resources through setuptools.");
Require(backendSource.Contains("DetectTorchDeviceAsync", StringComparison.Ordinal)
    && backendSource.Contains("transkunInfo.ArgumentList.Add(device)", StringComparison.Ordinal), "TransKun must detect CUDA availability at runtime and fall back to CPU instead of blindly requesting CUDA.");
var transkunPackageInstall = modelUpdateSource.IndexOf("正在部署 {model.Name}", StringComparison.Ordinal);
var transkunCudaInstall = modelUpdateSource.IndexOf("正在配置 {model.Name} 的配套 CUDA 运行环境", StringComparison.Ordinal);
Require(transkunPackageInstall >= 0 && transkunCudaInstall > transkunPackageInstall, "TransKun must install its package first and apply the matching CUDA torch/torchaudio pair last.");
Require(modelUpdateSource.Contains("正在下载 ACE-Step 基础权重", StringComparison.Ordinal) && modelUpdateSource.Contains("acestep-v15-xl-turbo", StringComparison.Ordinal), "ACE-Step installation must download both the official base components and XL checkpoint.");
Require(catalogSource.Contains("\"faster-whisper\"", StringComparison.Ordinal) && catalogSource.Contains("\"subtitle-edit\"", StringComparison.Ordinal)
    && Regex.IsMatch(catalogSource, "new\\(\\\"faster-whisper\\\"[^\\n]+true, false\\)")
    && Regex.IsMatch(catalogSource, "new\\(\\\"subtitle-edit\\\"[^\\n]+false, false\\)"), "Runtime-only Faster-Whisper and external Subtitle Edit must stay in Model Management but never enter the runnable workflow picker.");
Require(catalogSource.Contains(@"Models\faster-whisper-large-v3-turbo", StringComparison.Ordinal), "Whisper models must use the directory names recognized by Faster-Whisper XXL.");
Require(backendSource.Contains("--model_dir", StringComparison.Ordinal)
    && backendSource.Contains("--compute_type", StringComparison.Ordinal)
    && backendSource.Contains("float32", StringComparison.Ordinal)
    && backendSource.Contains("[\"HF_HUB_OFFLINE\"] = \"1\"", StringComparison.Ordinal), "Whisper must load the explicit local model directory in offline GPU-compatible mode.");
Require(backendSource.Contains("EnsureWhisperModelLayout", StringComparison.Ordinal), "Whisper must migrate the legacy Aurora model folder without requiring a redownload.");
Require(backendSource.Contains("WhisperOutputIsFresh", StringComparison.Ordinal), "Whisper exit code zero must not count as success unless fresh subtitle outputs exist.");
Require(backendSource.Contains("subtitles-cpu", StringComparison.Ordinal), "Whisper must retry on CPU when compatible GPU inference still fails.");
Require(modelUpdateSource.Contains("ResolveExistingModelRoot", StringComparison.Ordinal) && modelUpdateSource.Contains("MigrateLegacyWhisperRoot", StringComparison.Ordinal),
    "Whisper update checks must recognize legacy folders and migrate them before updating instead of downloading a duplicate model.");
foreach (var dependency in new[] { "funasr/campplus", "campplus_cn_common.bin", "lj1995/VoiceConversionWebUI", "rmvpe.pt", "nvidia/bigvgan_v2_44khz_128band_512x", "openai/whisper-small" })
    Require(modelUpdateSource.Contains(dependency, StringComparison.Ordinal), $"Seed-VC installation must explicitly download auxiliary dependency {dependency}.");
Require(modelUpdateSource.Contains("--cache-dir", StringComparison.Ordinal), "Seed-VC auxiliary dependencies must be written to the cache layout used by its upstream loaders.");
Require(backendSource.Contains("ModelHealthPolicy.MissingRequirements", StringComparison.Ordinal)
    && backendSource.Contains("[\"TRANSFORMERS_OFFLINE\"] = \"1\"", StringComparison.Ordinal), "Seed-VC startup must validate every local dependency and enforce offline loading.");
Require(modelUpdateSource.Contains("Distinct(StringComparer.OrdinalIgnoreCase)", StringComparison.Ordinal), "Refreshing PATH must remain idempotent instead of duplicating every entry after each model action.");
Require(mainPageXaml.Contains("x:Name=\"UtilityScrollViewer\"", StringComparison.Ordinal), "The utility workspace must scroll instead of clipping controls at supported window sizes.");
Require(mainPageXaml.Contains("ui:LocalizedText.NameKey=\"处理引擎选择\"", StringComparison.Ordinal)
    && mainPageXaml.Contains("ui:LocalizedText.NameKey=\"创作引擎选择\"", StringComparison.Ordinal), "Model selectors must expose localized screen-reader names.");
Require(mainPageXaml.Contains("x:Name=\"RunUtilityButton\"", StringComparison.Ordinal) && mainPageXaml.Contains("IsEnabled=\"False\"", StringComparison.Ordinal), "Utility processing must start disabled until valid source material and a ready engine are selected.");
Require(mainPageSource.Contains("UpdateUtilityRunState", StringComparison.Ordinal), "Utility intake and model selection must continuously refresh whether processing can start.");
Require(mainPageSource.Contains("SetStatus(localization.Get(\"ready\"))", StringComparison.Ordinal), "A no-update result must restore the persistent footer to a stable ready state.");
Require(mainPageSource.Contains("await Workbench.EnsureCoreWebView2Async", StringComparison.Ordinal), "The embedded workbench must initialize WebView2 before assigning its source.");
var makeWorkbenchLoadable = mainPageSource.IndexOf("Workbench.Visibility = Visibility.Visible;", StringComparison.Ordinal);
var initializeWorkbench = mainPageSource.IndexOf("await Workbench.EnsureCoreWebView2Async", StringComparison.Ordinal);
Require(mainPageSource.Contains("CoreWebView2Environment.CreateWithOptionsAsync", StringComparison.Ordinal)
    && mainPageSource.Contains("Path.Combine(settings.AppDataRoot, \"WebView2\")", StringComparison.Ordinal)
    && !mainPageSource.Contains("CoreWebView2Environment.CreateAsync().AsTask()", StringComparison.Ordinal),
    "Installed builds must put the WebView2 user data folder under writable LocalAppData instead of beside the executable in Program Files.");

Require(makeWorkbenchLoadable >= 0 && initializeWorkbench >= 0 && makeWorkbenchLoadable < initializeWorkbench,
    "The WebView2 control must be visible/loadable before EnsureCoreWebView2Async so installed builds cannot wait forever on a collapsed control.");
Require(mainPageSource.Contains("WaitAsync(TimeSpan.FromSeconds(30)", StringComparison.Ordinal), "WebView2 initialization and navigation must have a bounded timeout.");
Require(mainPageSource.Contains("workbenchStartupCancellation?.Cancel()", StringComparison.Ordinal)
    && mainPageSource.Contains("backend.StopWorkbench", StringComparison.Ordinal),
    "Canceling or leaving a starting workbench must cancel UI initialization and stop only its creative engine.");
Require(mainPageSource.Contains("using Microsoft.Windows.Storage.Pickers;", StringComparison.Ordinal)
    && !mainPageSource.Contains("using Windows.Storage.Pickers;", StringComparison.Ordinal), "All file and folder actions must use the current Windows App SDK picker API.");
Require(mainPageSource.Contains("new FileOpenPicker(App.MainWindow.AppWindow.Id)", StringComparison.Ordinal)
    && Regex.Matches(mainPageSource, @"new (?:FolderPicker|FileOpenPicker)\(([^)]*)\)").Cast<Match>().All(match => match.Groups[1].Value == "App.MainWindow.AppWindow.Id"), "Every picker must be owned by the Aurora AppWindow without legacy HWND initialization.");
foreach (var automationId in new[] { "MaintenanceScanButton", "MaintenanceDiagnosticsButton", "OpenLogsButton", "SaveSettingsButton", "OpenOutputButton", "ReleaseEngineButton" })
    Require(mainPageXaml.Contains($"x:Name=\"{automationId}\"", StringComparison.Ordinal), $"Primary action {automationId} must expose a stable UI Automation id.");
var localizationSource = File.ReadAllText(Path.Combine(audioStudioRoot, "AuroraAudioStudio", "Services", "LocalizationService.cs"));
Require(mainPageSource.Contains("RefreshCurrentPageHeading();", StringComparison.Ordinal), "Changing language must immediately refresh the current page title and subtitle.");
Require(localizationSource.Contains("设置已保存。", StringComparison.Ordinal)
    && mainPageSource.Contains("localization.Translate(\"设置已保存。\")", StringComparison.Ordinal), "The settings-saved status must use the newly selected language immediately.");
Require(mainPageSource.Contains("LocalizedText.Refresh(localization)", StringComparison.Ordinal) && mainPageXaml.Contains("ui:LocalizedText.Key", StringComparison.Ordinal), "Authored localization keys must apply before a popup is opened without rewriting framework template children.");
foreach (var phrase in new[] { "浅色", "深色", "跟随系统" })
    Require(localizationSource.Contains($"[\"{phrase}\"]", StringComparison.Ordinal), $"Theme option {phrase} must have translations.");
Require(mainPageSource.Contains("LocalizeTree(target);", StringComparison.Ordinal), "A page that was collapsed during startup must be localized when first shown.");
Require(mainPageSource.Contains("DispatcherQueue.TryEnqueue(() => LocalizeTree(target));", StringComparison.Ordinal), "Localization of a newly shown page must run again after WinUI creates its visual tree.");
Require(mainPageSource.Contains("element.LayoutUpdated += handler", StringComparison.Ordinal)
    && mainPageSource.Contains("element.LayoutUpdated -= handler", StringComparison.Ordinal), "A newly shown ScrollViewer must receive one final localization pass after layout and release the one-shot handler.");
foreach (var phrase in new[] { "浏览…", "仅查看任务与诊断" })
    Require(localizationSource.Contains($"[\"{phrase}\"]", StringComparison.Ordinal), $"Visible settings and safe-mode phrase {phrase} must have translations.");
var minimaxTool = File.ReadAllText(Path.Combine(audioStudioRoot, "AuroraAudioStudio", "Tools", "minimax_music3_webui.py"));
Require(minimaxTool.Contains("MiniMax-Music3 Community License", StringComparison.Ordinal), "The MiniMax-Music3 workbench must display the upstream model and license name.");

var notes098 = ReleaseNotesCatalog.CurrentAndRecent("0.9.8", "zh-CN");
Require(notes098.Count == 5 && notes098[0].Version == "0.9.8" && notes098[^1].Version == "0.7.0", "Version 0.9.8 must show itself and its four previous releases.");
var notes099 = ReleaseNotesCatalog.CurrentAndRecent("0.9.9", "en-US");
Require(notes099.Count == 5 && notes099[0].Version == "0.9.9" && notes099[^1].Version == "0.9.5", "Version 0.9.9 must show itself and its four previous releases.");
Require(notes099.All(x => !string.IsNullOrWhiteSpace(x.Body)), "Every displayed release must have localized content.");
var notes100 = ReleaseNotesCatalog.CurrentAndRecent("1.0.0", "zh-TW");
Require(notes100.Count == 5 && notes100[0].Version == "1.0.0" && notes100[^1].Version == "0.9.6", "Version 1.0.0 must show itself and its four previous releases.");
Require(notes100.All(x => !string.IsNullOrWhiteSpace(x.Body)), "Every 1.0.0 release note must have localized content.");
var notes101 = ReleaseNotesCatalog.CurrentAndRecent("1.0.1", "zh-CN");
Require(notes101.Count == 5 && notes101[0].Version == "1.0.1" && notes101[^1].Version == "0.9.7", "Version 1.0.1 must show itself and its four previous releases.");
Require(notes101.All(x => !string.IsNullOrWhiteSpace(x.Body)), "Every 1.0.1 release note must have localized content.");
var notes110 = ReleaseNotesCatalog.CurrentAndRecent("1.1.0", "en-US");
Require(notes110.Count == 5 && notes110[0].Version == "1.1.0" && notes110[^1].Version == "0.9.8", "Version 1.1.0 must show itself and its four previous releases.");
Require(notes110.All(x => !string.IsNullOrWhiteSpace(x.Body)), "Every 1.1.0 release note must have localized content.");
var notes120 = ReleaseNotesCatalog.CurrentAndRecent("1.2.0", "zh-CN");
Require(notes120.Count == 5 && notes120[0].Version == "1.2.0" && notes120[^1].Version == "0.9.9", "Version 1.2.0 must show itself and its four previous releases.");
Require(notes120.All(x => !string.IsNullOrWhiteSpace(x.Body)), "Every 1.2.0 release note must have localized content.");
var notes125 = ReleaseNotesCatalog.CurrentAndRecent("1.2.5", "zh-CN");
Require(notes125.Count == 5 && notes125[0].Version == "1.2.5" && notes125[^1].Version == "1.0.0", "Version 1.2.5 must show itself and its four previous releases.");
Require(notes125.All(x => !string.IsNullOrWhiteSpace(x.Body)), "Every 1.2.5 release note must have localized content.");
var notes130 = ReleaseNotesCatalog.CurrentAndRecent("1.3.0", "ja-JP");
Require(notes130.Count == 5 && notes130[0].Version == "1.3.0" && notes130[^1].Version == "1.0.1", "Version 1.3.0 must show itself and its four previous releases.");
Require(notes130.All(x => !string.IsNullOrWhiteSpace(x.Body)), "Every 1.3.0 release note must have localized content.");
var notes160 = ReleaseNotesCatalog.CurrentAndRecent("1.6.0", "en-US");
Require(notes160.Count == 5 && notes160[0].Version == "1.6.0" && notes160[^1].Version == "1.4.0", "Version 1.6.0 must show itself and its four previous releases.");
Require(notes160.All(x => !string.IsNullOrWhiteSpace(x.Body)), "Every 1.6.0 release note must have localized content.");
var notes161 = ReleaseNotesCatalog.CurrentAndRecent("1.6.1", "zh-TW");
Require(notes161.Count == 5 && notes161[0].Version == "1.6.1" && notes161[^1].Version == "1.4.1", "Version 1.6.1 must show itself and its four previous releases.");
Require(notes161.All(x => !string.IsNullOrWhiteSpace(x.Body)), "Every 1.6.1 release note must have localized content.");
var notes170 = ReleaseNotesCatalog.CurrentAndRecent("1.7.0", "zh-CN");
Require(notes170.Count == 5 && notes170[0].Version == "1.7.0" && notes170[^1].Version == "1.5.0", "Version 1.7.0 must show itself and its four previous releases.");
Require(notes170.All(x => !string.IsNullOrWhiteSpace(x.Body)), "Every 1.7.0 release note must have localized content.");
var notes151 = ReleaseNotesCatalog.CurrentAndRecent("1.5.1", "en-US");
var notes181 = ReleaseNotesCatalog.CurrentAndRecent("1.8.1", "en-US");
Require(notes181.Count == 5 && notes181[0].Version == "1.8.1" && notes181[^1].Version == "1.6.0", "Version 1.8.1 must show itself and its four previous releases.");
Require(notes181.All(x => !string.IsNullOrWhiteSpace(x.Body)), "Every 1.8.1 release note must have localized content.");
var notes180 = ReleaseNotesCatalog.CurrentAndRecent("1.8.0", "ja-JP");
Require(notes180.Count == 5 && notes180[0].Version == "1.8.0" && notes180[^1].Version == "1.5.1", "Version 1.8.0 must show itself and its four previous releases.");
Require(notes180.All(x => !string.IsNullOrWhiteSpace(x.Body)), "Every 1.8.0 release note must have localized content.");
Require(notes151.Count == 5 && notes151[0].Version == "1.5.1" && notes151[^1].Version == "1.3.0", "Version 1.5.1 must show itself and its four previous releases.");
Require(notes151.All(x => !string.IsNullOrWhiteSpace(x.Body)), "Every 1.5.1 release note must have localized content.");
var notes150 = ReleaseNotesCatalog.CurrentAndRecent("1.5.0", "en-US");
Require(notes150.Count == 5 && notes150[0].Version == "1.5.0" && notes150[^1].Version == "1.2.5", "Version 1.5.0 must show itself and its four previous releases.");
Require(notes150.All(x => !string.IsNullOrWhiteSpace(x.Body)), "Every 1.5.0 release note must have localized content.");
var notes141 = ReleaseNotesCatalog.CurrentAndRecent("1.4.1", "zh-CN");
Require(notes141.Count == 5 && notes141[0].Version == "1.4.1" && notes141[^1].Version == "1.2.0", "Version 1.4.1 must show itself and its four previous releases.");
Require(notes141.All(x => !string.IsNullOrWhiteSpace(x.Body)), "Every 1.4.1 release note must have localized content.");
var notes140 = ReleaseNotesCatalog.CurrentAndRecent("1.4.0", "zh-CN");
Require(notes140.Count == 5 && notes140[0].Version == "1.4.0" && notes140[^1].Version == "1.1.0", "Version 1.4.0 must show itself and its four previous releases.");
Require(notes140.All(x => !string.IsNullOrWhiteSpace(x.Body)), "Every 1.4.0 release note must have localized content.");
var validationNotes = ReleaseNotesCatalog.CurrentAndRecent("0.9.8.9", "zh-CN");
Require(validationNotes[0].Version == "0.9.8" && validationNotes[0].IsCurrent, "The validation build must identify its nearest public release history as current.");

Console.WriteLine("Update flow regression checks passed.");
