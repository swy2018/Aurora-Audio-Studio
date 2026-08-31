using AuroraAudioStudio.Services;
using AuroraAudioStudio.Models;
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
Directory.Delete(transactionRoot, true);

var legacyRecord = ProjectDocumentMigrator.Read("{\"Name\":\"Legacy record\",\"Feature\":\"subtitles\"}");
Require(legacyRecord.SchemaVersion == ProjectDocumentMigrator.CurrentSchemaVersion, "Legacy processing records without a schema number must migrate to the current schema.");
var rejectedFutureRecord = false;
try { ProjectDocumentMigrator.Read("{\"SchemaVersion\":99}"); }
catch (InvalidDataException) { rejectedFutureRecord = true; }
Require(rejectedFutureRecord, "A processing record from a newer unsupported schema must be preserved for recovery instead of being misread.");

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

var qwen = new ModelDefinition("qwen3-tts-06b-base", "Qwen3-TTS 0.6B", "voice", @"Qwen3-TTS\models\Qwen3-TTS-12Hz-0.6B-Base", "model.safetensors", "Qwen", "huggingface", "Qwen/Qwen3-TTS-12Hz-0.6B-Base");
var plan = ModelInstallPlanner.Create(qwen, @"D:\AuroraModels");
Require(plan.TargetPath == @"D:\AuroraModels\Qwen3-TTS\models\Qwen3-TTS-12Hz-0.6B-Base", "The model plan must show the exact target directory.");
Require(plan.EstimatedDownload == "≈ 1.5 GB", "The Qwen 0.6B plan must show its estimated download size.");
Require(plan.RecommendedFreeSpace == "≈ 3 GB", "The Qwen 0.6B plan must show recommended free disk space.");
var minimax = new ModelDefinition("minimax-music3", "MiniMax-Music3", "music", "MiniMax-Music3", "modular_model_index.json", "MiniMax", "minimax-music3", "MiniMaxAI/MiniMax-Music3");
var minimaxPlan = ModelInstallPlanner.Create(minimax, @"D:\AuroraModels");
Require(minimaxPlan.EstimatedDownload == "≈ 27 GB" && minimaxPlan.RecommendedFreeSpace == "≈ 55 GB", "MiniMax-Music3 must disclose its large on-demand download and staging space.");
var transkun = new ModelDefinition("transkun", "TransKun V2", "transcription", @"AudioTools\transkun-env", @"Scripts\transkun.exe", "PyPI", "uv-package", "transkun", true);
var transkunPlan = ModelInstallPlanner.Create(transkun, @"D:\AuroraModels");
Require(transkunPlan.TargetPath.EndsWith(@"AudioTools\transkun-env", StringComparison.OrdinalIgnoreCase), "TransKun must use an isolated model environment.");

var catalogSource = File.ReadAllText(Path.Combine(audioStudioRoot, "AuroraAudioStudio", "Services", "ModelCatalogService.cs"));
Require(catalogSource.Contains("new(\"minimax-music3\"", StringComparison.Ordinal), "Model Management must expose MiniMax-Music3.");
Require(catalogSource.Contains("new(\"transkun\"", StringComparison.Ordinal), "Model Management must expose TransKun V2.");
var mainPageSource = File.ReadAllText(Path.Combine(audioStudioRoot, "AuroraAudioStudio", "MainPage.xaml.cs"));
Require(mainPageSource.Contains("(\"transcription\", _, _) => \"transkun\"", StringComparison.Ordinal), "TransKun must be the default recommended piano transcription engine.");
Require(mainPageSource.Contains("OpenWorkbenchButton.Content = localization.Get(installed ? \"openWorkbench\" : \"installModel\")", StringComparison.Ordinal), "An uninstalled workbench model must expose an Install button.");
Require(mainPageSource.Contains("!await InstallSelectedModelAsync(model)", StringComparison.Ordinal), "Opening an uninstalled workbench model must start the on-demand installer.");
Require(mainPageSource.Contains("(\"separation\", \"two-stem\", _) => \"roformer-vocals\"", StringComparison.Ordinal), "Two-stem separation must select the dedicated vocals/instrumental model.");
Require(mainPageSource.Contains("(\"separation\", \"multi-stem\", \"fast\") => \"demucs\"", StringComparison.Ordinal), "Fast multi-stem separation must retain Demucs.");
Require(catalogSource.Contains("new(\"roformer-vocals\"", StringComparison.Ordinal), "Model Management must expose the dedicated two-stem BS-RoFormer model.");
Require(!catalogSource.Contains("\"python-tool\"", StringComparison.Ordinal) && !catalogSource.Contains("\"direct\"", StringComparison.Ordinal), "All catalog components must use an automatic update or repair adapter.");
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
var notes141 = ReleaseNotesCatalog.CurrentAndRecent("1.4.1", "zh-CN");
Require(notes141.Count == 5 && notes141[0].Version == "1.4.1" && notes141[^1].Version == "1.2.0", "Version 1.4.1 must show itself and its four previous releases.");
Require(notes141.All(x => !string.IsNullOrWhiteSpace(x.Body)), "Every 1.4.1 release note must have localized content.");
var notes140 = ReleaseNotesCatalog.CurrentAndRecent("1.4.0", "zh-CN");
Require(notes140.Count == 5 && notes140[0].Version == "1.4.0" && notes140[^1].Version == "1.1.0", "Version 1.4.0 must show itself and its four previous releases.");
Require(notes140.All(x => !string.IsNullOrWhiteSpace(x.Body)), "Every 1.4.0 release note must have localized content.");
var validationNotes = ReleaseNotesCatalog.CurrentAndRecent("0.9.8.9", "zh-CN");
Require(validationNotes[0].Version == "0.9.8" && validationNotes[0].IsCurrent, "The validation build must identify its nearest public release history as current.");

Console.WriteLine("Update flow regression checks passed.");
