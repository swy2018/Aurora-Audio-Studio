using System.Text;
using AuroraAudioStudio.Services;

internal static class OutputIntegrityRegression
{
    public static void Run()
    {
        var root = Path.Combine(Path.GetTempPath(), "Aurora-OutputIntegrity-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var passed = 0;
        void Check(bool condition, string name) { if (!condition) throw new Exception(name); passed++; Console.WriteLine("PASS " + name); }
        void Reject(Action action, string name)
        {
            var rejected = false;
            try { action(); } catch (InvalidDataException) { rejected = true; }
            Check(rejected, name);
        }
        string Wave(ushort bits, ushort channels, ushort format = 1, int? block = null, int? bytes = null)
        {
            var path = Path.Combine(root, Guid.NewGuid() + ".wav");
            var align = block ?? bits / 8 * channels;
            var size = bytes ?? align * 160;
            using var writer = new BinaryWriter(File.Create(path));
            writer.Write("RIFF"u8); writer.Write(36 + size); writer.Write("WAVEfmt "u8); writer.Write(16);
            writer.Write(format); writer.Write(channels); writer.Write(16000); writer.Write(16000 * align);
            writer.Write((ushort)align); writer.Write(bits); writer.Write("data"u8); writer.Write(size); writer.Write(new byte[size]);
            return path;
        }
        foreach (ushort bits in new ushort[] { 8, 16, 24, 32 })
        foreach (ushort channels in new ushort[] { 1, 2, 6 })
        {
            var info = ArtifactValidator.Inspect(Wave(bits, channels));
            Check(info.SampleRate == 16000 && info.Channels == channels && info.DurationSeconds == .01, $"PCM {bits}-bit {channels}-channel roundtrip metadata");
        }
        foreach (ushort bits in new ushort[] { 32, 64 })
            Check(ArtifactValidator.Inspect(Wave(bits, 2, 3)).DurationSeconds == .01, $"float {bits}-bit wave accepted");
        Check(MediaInputPolicy.InspectPcmWave(Wave(24, 2)) is { SampleRate: 16000, Channels: 2, DurationSeconds: .01 }, "PCM WAV preflight remains available without external FFmpeg tools");
        Reject(() => ArtifactValidator.Validate(Wave(0, 1, block: 0, bytes: 16)), "zero bit depth cannot pass wave validation");
        Reject(() => ArtifactValidator.Validate(Wave(16, 1, bytes: 1)), "partial PCM frame cannot pass validation");
        Reject(() => ArtifactValidator.Validate(Wave(16, 1, block: 4)), "incorrect block alignment rejected");
        var truncated = Wave(16, 1); using (var stream = File.OpenWrite(truncated)) stream.SetLength(45);
        Reject(() => ArtifactValidator.Validate(truncated), "truncated RIFF length rejected");
        Reject(() => ArtifactValidator.Collect("transcription", Path.Combine(root, "absent")), "missing output directory cannot succeed");
        var empty = Path.Combine(root, "empty"); Directory.CreateDirectory(empty);
        Reject(() => ArtifactValidator.Collect("separation", empty), "present output directory without files cannot succeed");

        string Midi(byte[] header, byte[] track)
        {
            var path = Path.Combine(root, Guid.NewGuid() + ".mid");
            using var writer = new BinaryWriter(File.Create(path));
            writer.Write("MThd"u8); writer.Write(new byte[] { 0, 0, 0, 6 }); writer.Write(header);
            writer.Write("MTrk"u8); writer.Write(new byte[] { 0, 0, 0, (byte)track.Length }); writer.Write(track);
            return path;
        }
        byte[] header = [0, 0, 0, 1, 0, 96], note = [0, 0x90, 60, 100, 96, 0x80, 60, 0, 0, 0xff, 0x2f, 0];
        var midi = ArtifactValidator.Inspect(Midi(header, note));
        Check(midi.Notes == 1 && midi.Tracks == 1 && midi.DurationSeconds == .5, "MIDI note count, track count and tempo-derived duration");
        foreach (byte invalidFormat in new byte[] { 3, 4, 127, 255 })
            Reject(() => ArtifactValidator.Validate(Midi([0, invalidFormat, 0, 1, 0, 96], note)), "unsupported MIDI format " + invalidFormat);
        Reject(() => ArtifactValidator.Validate(Midi([0, 0, 0, 1, 0, 0], note)), "MIDI zero time division rejected");
        Reject(() => ArtifactValidator.Validate(Midi(header, [0, 0x90, 60, 100])), "MIDI missing end event rejected");
        Reject(() => ArtifactValidator.Validate(Midi(header, [0, 0x90, 60, 100, 0, 0xff, 1, 127])), "MIDI metadata cannot seek beyond a track");
        Check(ArtifactValidator.Inspect(Midi(header, [0, 0x90, 60, 100, 0, 0xff, 0x2f, 0])).Warnings!.Count == 1,
            "unclosed MIDI notes carry an explicit usability warning");

        const string valid = "1\n00:00:00,100 --> 00:00:01,200\n字幕\n";
        ArtifactValidator.ValidateSubtitleText(valid);
        foreach (var text in new[] { "00:00:00,100 --> 00:00:01,200\n", valid + "\n2\nbroken time\ntext",
            "1\n00:00:01,000 --> 00:00:01,000\ntext", valid + "\n1\n00:00:02,000 --> 00:00:03,000\nDuplicate" })
            Reject(() => ArtifactValidator.ValidateSubtitleText(text), "invalid subtitle block cannot hide behind one valid timestamp");
        var subtitle = Path.Combine(root, "captions.srt"); File.WriteAllText(subtitle, valid, Encoding.UTF8);
        Check(ArtifactValidator.Inspect(subtitle) is { Subtitles: 1, DurationSeconds: 1.2 }, "SRT summary includes full cue count and end time");
        var silence = Path.Combine(root, "silence.srt"); File.WriteAllText(silence, ""); File.WriteAllText(Path.ChangeExtension(silence, ".json"), "{\"segments\":[]}");
        Check(ArtifactValidator.Inspect(silence).Subtitles == 0, "documented silence remains a valid empty subtitle result");
        Console.WriteLine($"Output integrity checks passed: {passed}. Fixtures retained: {root}");
    }
}
