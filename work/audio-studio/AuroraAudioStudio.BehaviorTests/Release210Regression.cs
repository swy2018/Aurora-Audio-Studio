using AuroraAudioStudio.Core;
using AuroraAudioStudio.Models;
using AuroraAudioStudio.Services;

internal static class Release210Regression
{
    public static async Task RunAsync()
    {
        var root = Path.Combine(Path.GetTempPath(), "Aurora-210-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        void Check(bool condition, string name)
        {
            if (!condition) throw new Exception("FAIL " + name);
            Console.WriteLine("PASS " + name);
        }

        var settingsRoot = Path.Combine(root, "settings");
        Directory.CreateDirectory(settingsRoot);
        var path = Path.Combine(settingsRoot, "settings.json");
        await File.WriteAllTextAsync(path, "{broken settings");
        var settings = new SettingsService(settingsRoot);
        Check(settings.NeedsRecovery && !settings.TrySetLanguage("en-US", out _), "corrupt settings remain write protected");
        Check(settings.TryRecoverSettings(out var backup, out _) && File.ReadAllText(backup) == "{broken settings", "explicit settings recovery preserves exact original");
        Check(settings.TrySetLanguage("ja-JP", out _) && new SettingsService(settingsRoot).Current.Language == "ja-JP", "recovered settings can be saved and reloaded");
        Check(settings.TryRecoverSettings(out var unnecessaryBackup, out _) && unnecessaryBackup == "", "healthy settings recovery is a no-op");

        var source = Path.Combine(root, "silence.srt");
        await File.WriteAllTextAsync(source, "");
        await File.WriteAllTextAsync(Path.ChangeExtension(source, ".json"), "{\"segments\":[]}");
        var output = Path.Combine(root, "exports");
        Directory.CreateDirectory(output);
        var exported = await ArtifactExport.CopyAsync(source, output, "");
        Check(ArtifactValidator.Inspect(exported).Subtitles == 0 && File.Exists(Path.ChangeExtension(exported, ".json")), "unchanged empty subtitle keeps its silence evidence on edit export");
        var count = Directory.GetFiles(output).Length;
        var rejected = false;
        try { await ArtifactExport.CopyAsync(source, output, "invalid subtitle"); }
        catch (InvalidDataException) { rejected = true; }
        Check(rejected && Directory.GetFiles(output).Length == count, "invalid subtitle never creates a final export");
        var second = await ArtifactExport.CopyAsync(source, output);
        Check(second != exported && File.ReadAllText(source) == "", "exports never overwrite source or previous output");
        var stems = new List<string>();
        foreach (var stem in new[] { "vocals", "drums", "bass", "guitar", "piano", "other" })
        {
            var wave = Path.Combine(root, "song_" + stem + ".wav");
            using var writer = new BinaryWriter(File.Create(wave));
            writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF")); writer.Write(16036);
            writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt ")); writer.Write(16);
            writer.Write((short)1); writer.Write((short)1); writer.Write(8000); writer.Write(16000); writer.Write((short)2); writer.Write((short)16);
            writer.Write(System.Text.Encoding.ASCII.GetBytes("data")); writer.Write(16000); writer.Write(new byte[16000]);
            stems.Add(wave);
        }
        StemOutputValidation.Validate("roformer", stems, 1);
        Check(true, "all six named stems with matching durations pass");
        rejected = false;
        try { StemOutputValidation.Validate("roformer", stems.Take(5).ToArray()); } catch (InvalidDataException) { rejected = true; }
        Check(rejected, "five valid WAV files cannot masquerade as a complete six-stem result");
        rejected = false;
        try { StemOutputValidation.Validate("roformer", stems, 9); } catch (InvalidDataException) { rejected = true; }
        Check(rejected, "truncated stem group cannot masquerade as full-source separation");
        Check(!UpdateFlowGuard.SupportsSafeRestart("2.0.1") && UpdateFlowGuard.SupportsSafeRestart("2.1.0-beta.1"), "legacy releases are excluded from automatic elevated restart");
        var legacyArguments = UpdateFlowGuard.BuildInstallerArguments(123, "test.log", false);
        Check(!legacyArguments.Contains("/UPDATE", StringComparison.Ordinal) && legacyArguments.Contains("/SILENT", StringComparison.Ordinal)
            && legacyArguments.Contains("/KEEPUSERDATA", StringComparison.Ordinal), "legacy rollback skips automatic Run while preserving personal data");
        Console.WriteLine("2.1.0 regression evidence: " + root);
    }
}
