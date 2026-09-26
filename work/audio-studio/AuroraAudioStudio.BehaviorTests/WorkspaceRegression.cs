using System.Diagnostics;
using System.Text.Json;
using AuroraAudioStudio.Models;
using AuroraAudioStudio.Services;

internal static class WorkspaceRegression
{
    public static async Task RunAsync()
    {
        var count = 0;
        void Check(bool value, string name) { if (!value) throw new Exception(name); count++; Console.WriteLine("PASS " + name); }
        foreach (var rate in new[] { 16000, 44100, 48000, 96000 })
            foreach (var channels in new[] { 1, 2, 6 })
            {
                var json = JsonSerializer.Serialize(new { streams = new[] { new { sample_rate = rate.ToString(), channels, duration = "N/A", codec_name = "pcm_s16le" } }, format = new { duration = "1.25" } });
                var info = MediaInputPolicy.ParseProbe(json, 100);
                Check(info.SampleRate == rate && info.Channels == channels && info.DurationSeconds == 1.25 && info.Bytes == 100, "stream metadata " + rate + "/" + channels);
            }
        foreach (var invalid in new[] { "{}", "{\"streams\":[]}", "{\"streams\":[{\"sample_rate\":\"0\",\"channels\":2}]}", "{\"streams\":[{\"sample_rate\":\"48000\",\"channels\":0}]}" })
        {
            var rejected = false; try { MediaInputPolicy.ParseProbe(invalid, 10); } catch (InvalidDataException) { rejected = true; }
            Check(rejected, "missing or invalid audio stream is rejected");
        }
        var settings = new SettingsService(Path.Combine(Path.GetTempPath(), "Aurora-Workspace-" + Guid.NewGuid().ToString("N")));
        var projects = new ProjectService(settings, appVersion: "2.0.1-beta.1");
        var origin = await projects.CreateAsync("separation", "input.wav", "roformer");
        origin.Artifacts.Add(new AuroraArtifact { Path = "stem.wav", Kind = "separation" });
        await projects.SaveAsync(origin);
        var next = await projects.CreateAsync("transcription", "stem.wav", "yourmt3");
        projects.LinkSource(next, "stem.wav"); await projects.SaveAsync(next);
        Check(projects.Find(next.Id)!.Parameters["sourceProjectId"] == origin.Id && next.Parameters["sourceArtifactId"] == origin.Artifacts[0].Id, "MIDI retains exact originating project and stem identity");
        Check(!new ArtifactDisplay { Path = "out.mid", Kind = "transcription" }.CanTranscribe && new ArtifactDisplay { Path = "stem.wav", Kind = "separation" }.IsStem, "result handoff accepts only source audio");
        var stems = new[] { "bass", "drums", "guitar", "piano", "other", "vocals", "instrumental" }.Select(name => new AuroraArtifact { Path = Path.Combine("output", "song_" + name + ".wav"), Kind = "separation" }).ToArray();
        Check(ProjectService.SelectStemSources(stems).Count == 6 && !ProjectService.SelectStemSources(stems).Any(path => path.EndsWith("_instrumental.wav")), "six-stem MIDI handoff excludes the duplicate accompaniment mix");
        Check(ProjectService.SelectStemSources(stems.TakeLast(2)).Count == 2, "two-stem MIDI handoff retains both vocals and accompaniment");
        var before = projects.Find(origin.Id)!;
        before.Name = "not yet saved";
        Check(projects.Find(origin.Id)!.Name != before.Name, "editing a record cannot mutate the cached on-disk snapshot");
        await projects.SaveAsync(before);
        Check(projects.Find(origin.Id)!.Name == before.Name, "successful save invalidates the cached record");
        before.Name = "external change with different length";
        await File.WriteAllTextAsync(before.FilePath, JsonSerializer.Serialize(before));
        Check(projects.Find(origin.Id)!.Name == before.Name, "external edits are visible on the next library query");
        File.Move(before.FilePath, before.FilePath + ".retained-fixture");
        Check(projects.Find(origin.Id) is null, "moved records cannot remain as cache ghosts");
        Console.WriteLine($"Workspace checks passed: {count}. Evidence: {settings.AppDataRoot}");
    }

    public static async Task BenchmarkAsync(string root)
    {
        root = Path.GetFullPath(root);
        Directory.CreateDirectory(root);
        foreach (var count in new[] { 100, 1000, 10000 })
        {
            var settings = new SettingsService(Path.Combine(root, count.ToString()));
            for (var index = 0; index < count; index++)
            {
                var path = Path.Combine(settings.Current.ProjectsRoot, index + ".arr");
                if (File.Exists(path)) continue;
                var item = new AuroraProject { Id = index.ToString(), Name = "历史记录 " + index, UpdatedAt = DateTimeOffset.UnixEpoch.AddSeconds(index), Artifacts = [new() { Path = "fixture.wav", Kind = "music" }] };
                await File.WriteAllTextAsync(path, JsonSerializer.Serialize(item));
            }
            var service = new ProjectService(settings);
            var samples = new List<double>();
            for (var pass = 0; pass < 6; pass++)
            {
                var clock = Stopwatch.StartNew();
                var recent = service.Recent(); var artifacts = service.Artifacts();
                if (recent.Count != 8 || recent[0].Id != (count - 1).ToString()) throw new Exception("Benchmark ordering regressed");
                clock.Stop(); samples.Add(clock.Elapsed.TotalMilliseconds);
            }
            Console.WriteLine(JsonSerializer.Serialize(new { count, milliseconds = samples, warmMedian = samples.Skip(1).Order().ElementAt(2) }));
        }
    }

    public static async Task ProbeRealAsync(string path)
    {
        var info = await MediaInputPolicy.InspectAsync(path, @"C:\LocalAI");
        Console.WriteLine(JsonSerializer.Serialize(info));
        var missing = false; try { await MediaInputPolicy.InspectAsync(path + ".missing", @"C:\LocalAI"); } catch (InvalidDataException) { missing = true; }
        if (!missing || info.DurationSeconds is not > 0) throw new Exception("Real input inspection failed");
        Console.WriteLine("PASS real FFprobe metadata and FFmpeg decode");
    }
}
