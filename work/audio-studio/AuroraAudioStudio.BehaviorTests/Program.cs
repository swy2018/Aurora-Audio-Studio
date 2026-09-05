using System.Reflection;
using System.Text.Json;
using AuroraAudioStudio.Models;
using AuroraAudioStudio.Services;

if (args.FirstOrDefault() == "--engine") { await EngineIntegration.RunAsync(args.Skip(1).ToArray()); return; }
if (args.FirstOrDefault() == "--catalog") { CatalogExport.Run(args[1], args.Contains("--check")); return; }
if (args.FirstOrDefault() == "--strings") { LocalizationAudit.Run(args[1]); return; }
if (args.FirstOrDefault() == "--provision") { await ProvisionIntegration.RunAsync(args[1], args[2], args[3]); return; }

var root = Path.Combine(Path.GetTempPath(), "Aurora-BehaviorTests-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(root);
var passed = 0;
void Check(bool value, string name) { if (!value) throw new Exception(name); passed++; Console.WriteLine("PASS " + name); }
void Fixture(string path, string content = "fixture") { Directory.CreateDirectory(Path.GetDirectoryName(path)!); File.WriteAllText(path, content); }
var settings = new SettingsService(Path.Combine(root, "state"));
var queue = new TaskQueueService(settings);
var waiting = queue.Create("project", "waiting", "subtitles", "sample.wav", "whisper-small", sourceLanguage: "ja");
var recovered = new TaskQueueService(settings);
Check(recovered.Items.Single().Status == AuroraTaskStates.Interrupted && recovered.Items.Single().CanRetry, "waiting task becomes recoverable after restart");
Check(recovered.Items.Single().SourceLanguage == "ja", "source language survives restart");
settings.Current.TaskHistoryLimit = 20;
for (var i = 0; i < 25; i++) queue.Create("project", "queued " + i, "subtitles", "sample.wav", "whisper-small");
Check(queue.Items.Count == 26 && queue.Items.Any(x => x.Id == waiting.Id), "active tasks never trimmed by history limit");
var complete = new AuroraTaskRecord { Status = AuroraTaskStates.Completed, Progress = 1, Stage = "complete" };
typeof(TaskQueueService).GetMethod("Report", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(queue, [complete, new TaskExecutionProgress(.7, "stale")]);
Check(complete.Progress == 1 && complete.Stage == "complete", "late progress cannot mutate a completed task");
settings.Current.SafeMode = true;
var called = false;
var safeTask = queue.Create("p", "safe retry", "subtitles", "sample.wav", "whisper-small");
var safeResult = await queue.RunAsync(safeTask, (_, _) => { called = true; return Task.FromResult(new OperationResult(true, "unexpected")); });
Check(!safeResult.Success && !called, "safe mode rejects queued work and retries");
var backend = new BackendService(settings);
Check(!(await backend.StartWorkbenchAsync("voice", "qwen3-tts-base", "zh-CN")).Success, "safe mode is enforced at backend entry");
settings.Current.SafeMode = false;
queue.Pause();
var canceled = queue.Create("p", "cancel while paused", "subtitles", "sample.wav", "whisper-small");
var pending = queue.RunAsync(canceled, (_, _) => { called = true; return Task.FromResult(new OperationResult(true, "unexpected")); });
queue.Cancel(canceled.Id);
Check(!(await pending).Success && canceled.Status == AuroraTaskStates.Canceled && !called, "cancel paused task never runs delegate");
queue.Resume();
var outputs = new[] { Path.Combine(root, "only-this.mid") };
var finished = queue.Create("p", "finish", "transcription", "sample.wav", "transkun", "quality", "auto", "multi-stem");
await queue.RunAsync(finished, (_, _) => Task.FromResult(new OperationResult(true, "ok", root, Outputs: outputs, Device: "cuda")));
Check(finished.OutputFiles.SequenceEqual(outputs) && finished.Device == "cuda" && finished.TrackMode == "multi-stem", "task stores explicit output manifest and execution parameters");

var target = Path.Combine(root, "downloads", "model");
ModelInstallTransaction.Prepare(target, "model:revision-a");
var partial = Path.Combine(ModelInstallTransaction.StagingPath(target), "weights.partial"); Fixture(partial);
ModelInstallTransaction.Prepare(target, "model:revision-a");
Check(File.Exists(partial), "same revision resumes without discarding partial data");
ModelInstallTransaction.Prepare(target, "model:revision-b");
Check(!File.Exists(partial) && Directory.EnumerateDirectories(Path.GetDirectoryName(target)!, "*.abandoned-*").Any(), "different revision is isolated while retaining old partial data");
Fixture(Path.Combine(target, "version"), "old"); Fixture(Path.Combine(ModelInstallTransaction.StagingPath(target), "version"), "new");
ModelInstallTransaction.Commit(target);
Check(File.ReadAllText(Path.Combine(target, "version")) == "new", "commit promotes verified candidate");
Check(ModelInstallTransaction.RestorePrevious(target) && File.ReadAllText(Path.Combine(target, "version")) == "old", "rollback restores old candidate");

var logical = Path.Combine(root, "logical-env"); var runtime = RuntimeEnvironment.CreateCandidate(root, "sample");
Fixture(Path.Combine(logical, "Scripts", "python.exe")); Fixture(Path.Combine(runtime, "Scripts", "python.exe"));
RuntimeEnvironment.Activate(logical, runtime);
Check(RuntimeEnvironment.Resolve(logical) == runtime && File.Exists(Path.Combine(logical, "Scripts", "python.exe")), "environment activation leaves original files and candidate paths unchanged");
Check(RuntimeEnvironment.Rollback(logical) && RuntimeEnvironment.Resolve(logical) == logical, "environment rollback restores original path without moving Python");
var ace = new ModelDefinition("ace-step", "ACE", "music", "ACE-Step-1.5", @"acestep\acestep_v15_pipeline.py", "GitHub", "github-release-git");
var staging = Path.Combine(root, "new-install.aurora-staging"); Fixture(Path.Combine(staging, ace.Marker)); Fixture(Path.Combine(staging, ".venv", "Scripts", "python.exe"));
foreach (var folder in new[] { "acestep-v15-turbo", "acestep-v15-xl-turbo", "acestep-5Hz-lm-1.7B", "Qwen3-Embedding-0.6B", "vae" }) Fixture(Path.Combine(staging, "checkpoints", folder, "weights.bin"));
Check(ModelHealthPolicy.MissingRequirements(ace, root, staging).Count == 0 && !ModelHealthPolicy.IsReady(ace, root), "first-install validation evaluates explicit staging root");
Check(!SettingsPathValidator.TryValidate("relative", "relative", "relative", out _), "relative settings paths rejected");

var runA = ArtifactValidator.CreateRunDirectory(root, "outputs", "same.wav"); var runB = ArtifactValidator.CreateRunDirectory(root, "outputs", "same.wav");
Check(runA != runB, "identical source names get separate output directories");
var noResultRejected = false; try { ArtifactValidator.Collect("transcription", runA); } catch (InvalidDataException) { noResultRejected = true; }
Check(noResultRejected, "exit without output cannot produce a successful artifact collection");
Fixture(Path.Combine(root, "failed.log"));
var resolve = typeof(ProjectService).GetMethod("ResolveArtifacts", BindingFlags.Static | BindingFlags.NonPublic)!;
Check(((IReadOnlyList<string>)resolve.Invoke(null, [new AuroraTaskRecord { Status = AuroraTaskStates.Failed, OutputPath = Path.Combine(root, "failed.log") }])!).Count == 0, "failure logs never enter the results library");
Fixture(Path.Combine(runA, "ours.mid")); Fixture(Path.Combine(runA, "unrelated.mid"));
var owned = new AuroraTaskRecord { Status = AuroraTaskStates.Completed, OutputPath = runA, OutputFiles = [Path.Combine(runA, "ours.mid")] };
Check(((IReadOnlyList<string>)resolve.Invoke(null, [owned])!).SequenceEqual(owned.OutputFiles), "only explicit task-owned files are registered");
var emptyMidi = Path.Combine(root, "empty.mid");
File.WriteAllBytes(emptyMidi, Convert.FromHexString("4D546864000000060000000100604D54726B0000000400FF2F00"));
var rejectedEmptyMidi = false; try { ArtifactValidator.Validate(emptyMidi); } catch (InvalidDataException) { rejectedEmptyMidi = true; }
Check(rejectedEmptyMidi, "empty MIDI is not a successful transcription");
var noteMidi = Path.Combine(root, "note.mid");
File.WriteAllBytes(noteMidi, Convert.FromHexString("4D546864000000060000000100604D54726B0000000C00903C6460803C0000FF2F00"));
ArtifactValidator.Validate(noteMidi);
Check(ArtifactValidator.MidiNoteCount(noteMidi) == 1, "MIDI validation counts real note events");
Fixture(Path.Combine(root, "bad.srt"), "1\n00:00:03,000 --> 00:00:01,000\nBad\n");
var rejectedTimeline = false; try { ArtifactValidator.Validate(Path.Combine(root, "bad.srt")); } catch (InvalidDataException) { rejectedTimeline = true; }
Check(rejectedTimeline, "subtitle edits reject reversed timestamps");
var progressTask = new AuroraTaskRecord { Status = AuroraTaskStates.Running };
var changes = 0; queue.Changed += (_, _) => changes++;
for (var i = 0; i < 1000; i++) typeof(TaskQueueService).GetMethod("Report", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(queue, [progressTask, new TaskExecutionProgress(.3, "progress")]);
Check(changes == 0, "frequent progress does not rebuild the task and project lists");
Check(!RuntimeEnvironment.CanRollback(Path.Combine(root, "no-backup")), "rollback unavailable without a retained version");
settings.Current.Language = "en-US";
var catalog = new ModelCatalogService(settings);
Check(catalog.Definitions.Where(x => !x.IsRunnable).All(x => catalog.GetStates().First(s => s.Id == x.Id).Status.Contains("workbench", StringComparison.OrdinalIgnoreCase) || x.Id is "subtitle-edit" or "faster-whisper"), "download-only components cannot advertise a workbench");
var localization = new LocalizationService(settings);
var originalOutput = settings.Current.OutputRoot;
Check(settings.TrySetLanguage("ja-JP", out _) && new SettingsService(settings.AppDataRoot).Current.Language == "ja-JP", "language selection persists immediately without the full settings form");
Check(settings.TrySetLanguage("zh-CN", out _) && new SettingsService(settings.AppDataRoot).Current.Language == "zh-CN" && settings.Current.OutputRoot == originalOutput, "Japanese returns to Chinese without changing storage settings");
Check(!settings.TrySetLanguage("unsupported", out _) && settings.Current.Language == "zh-CN", "invalid language cannot replace the current selection");
settings.TrySetLanguage("en-US", out _);
Check(localization.Translations.All(x => x.Length == 4 && x.All(text => !string.IsNullOrWhiteSpace(text))), "every native translation supplies all four languages");
Check(catalog.GetStates().All(x => !System.Text.RegularExpressions.Regex.IsMatch(x.License, "[\\p{IsCJKUnifiedIdeographs}]")) && catalog.GetStates().Single(x => x.Id == "f5-tts").License.Contains("CC-BY-NC-4.0"), "license labels are localized and F5 model weights retain their noncommercial condition");
Check(localization.Translate("已连接 Qwen3-TTS · 声音克隆") == "Connected to Qwen3-TTS · Voice cloning", "dynamic model status translates descriptors without changing engine names");
Check(localization.Translate("已是最新日期版 2026-09-05") == "Latest dated version: 2026-09-05", "dated update status uses localized template");
settings.Current.LocalAiRoot = Path.Combine(root, "model-health");
var piano = catalog.Find("piano")!;
Fixture(Path.Combine(settings.Current.LocalAiRoot, piano.RelativeRoot, piano.Marker));
Check(!catalog.IsInstalled(piano), "piano weights alone cannot advertise a runnable engine");
var pianoRuntime = Path.Combine(settings.Current.LocalAiRoot, "AudioTools", "piano-env");
Fixture(Path.Combine(pianoRuntime, "Scripts", "python.exe"));
catalog.RecordSuccessfulRun("piano", "cuda");
Check(catalog.GetStates().Single(x => x.Id == "piano").Status == "Short task verified", "successful run records its runtime identity");
Directory.CreateDirectory(Path.Combine(pianoRuntime, "Lib", "site-packages", "torch-2.8.0.dist-info"));
Check(catalog.GetStates().Single(x => x.Id == "piano").Status == "Files present · not yet verified", "changing a shared runtime invalidates prior execution verification");
var vocals = catalog.Find("roformer-vocals")!;
Fixture(Path.Combine(settings.Current.LocalAiRoot, vocals.RelativeRoot, vocals.Marker));
Check(!catalog.IsInstalled(vocals), "vocals weights require the shared separation launcher");
var ffmpeg = Path.Combine(settings.Current.LocalAiRoot, "Faster-Whisper-XXL", "Faster-Whisper-XXL", "ffmpeg.exe");
Fixture(ffmpeg);
Check(AudioRuntime.FindFfmpeg(settings.Current.LocalAiRoot) == ffmpeg, "bundled FFmpeg has deterministic precedence over PATH");
Console.WriteLine($"Behavior checks passed: {passed}. Isolated evidence: {root}");
