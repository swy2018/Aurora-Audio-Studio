using AuroraAudioStudio.Services;
using System.Xml.Linq;

internal static class ScoreRegression
{
    public static void Run(string? evidenceRoot = null)
    {
        var root = Path.Combine(evidenceRoot is null ? Path.GetTempPath() : Path.GetFullPath(evidenceRoot), "Aurora-ScoreTests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        void Check(bool value, string label) { if (!value) throw new Exception("FAIL " + label); Console.WriteLine("PASS " + label); }
        var inputs = new List<string>();
        for (var i = 0; i < 6; i++)
        {
            var instrument = i == 5 ? MidiScore.Instruments[^1] : MidiScore.Instruments[i];
            var notes = new ArrangedNote[] { new(37, 471, i == 5 ? 36 : 60 + i, 90, 1), new(480, 2520, i == 5 ? 38 : 64 + i, 80, 1), new(480, 960, i == 5 ? 42 : 67 + i, 75, 1) };
            var input = Path.Combine(root, "输入 日本語 " + i + ".mid");
            MidiScore.WriteMidi(new([new("Part " + i, instrument, notes, 0, 0)], [new(0, 500000), new(4, 600000)], new()), input);
            inputs.Add(input);
        }
        var originals = inputs.Select(File.ReadAllBytes).ToArray();
        var doc = MidiScore.Read(inputs);
        Check(doc.Parts.Count == 6 && doc.Parts.Sum(part => part.Notes.Count) == 18, "six MIDI sources merge without losing or duplicating note events");
        Check(doc.Tempos.Count == 2 && doc.Parts.Last().Instrument.Percussion, "tempo map and percussion channel are retained");
        foreach (var grid in new[] { 4, 8, 12, 16, 24, 32 })
        foreach (var meter in new[] { (3, 4), (4, 4), (6, 8) })
        {
            var arrangement = MidiScore.Prepare(doc, new(Grid: grid, Beats: meter.Item1, BeatType: meter.Item2));
            var xml = MidiScore.MusicXml(arrangement, "测试 & 日本語");
            var bar = MidiScore.Division * 4 * meter.Item1 / meter.Item2;
            foreach (var measure in xml.Descendants("measure"))
            {
                var cursor = 0;
                foreach (var element in measure.Elements())
                {
                    if (element.Name == "backup") cursor -= (int)element.Element("duration")!;
                    if (element.Name == "note" && element.Element("chord") is null) cursor += (int)element.Element("duration")!;
                    CheckCursor(cursor >= 0 && cursor <= bar);
                }
                CheckCursor(cursor == bar);
            }
            Check(true, $"MusicXML voices fill legal measures at grid {grid}, meter {meter.Item1}/{meter.Item2}");
        }
        var exported = MidiScore.Export(doc, new(), root);
        Check(ArtifactValidator.Inspect(exported.Midi).Notes == 18, "arranged MIDI remains valid with all original notes in preserve mode");
        Check(ArtifactValidator.Inspect(exported.Audio).DurationSeconds > 2, "review audio contains the arranged timeline");
        var scoreXml = XDocument.Load(exported.MusicXml);
        Check(scoreXml.Root?.Attribute("version")?.Value == "4.0" && scoreXml.Descendants("part").Count() == 6, "export is MusicXML 4.0 with six named parts");
        Check(scoreXml.Descendants("tie").Any(tie => tie.Attribute("type")?.Value == "start") && scoreXml.Descendants("tie").Any(tie => tie.Attribute("type")?.Value == "stop"), "notes crossing barlines have both sound and notation ties");
        Check(scoreXml.Descendants("unpitched").Any() && scoreXml.Descendants("midi-unpitched").Any(), "drums use unpitched notation and GM percussion mapping");
        Check(scoreXml.Descendants("accidental").Any(note => note.Value == "sharp"), "chromatic pitches include visible accidentals, not just hidden playback alterations");
        var rhythmic = new ScoreArrangement([new("Rhythm", MidiScore.Instruments[1], Enumerable.Range(0, 12).Select(i => new ArrangedNote(i * 160, (i + 1) * 160, 60 + i % 7, 80, 1)).ToArray(), 0, 0)], [new(0, 500000)], new(Grid:12));
        var rhythmXml = MidiScore.MusicXml(rhythmic, "Triplets");
        Check(rhythmXml.Descendants("beam").Any() && rhythmXml.Descendants("tuplet").Count() == 8, "triplet groups have visible beams and start/stop tuplet notation");
        rhythmXml.Save(Path.Combine(root, "triplets.musicxml"));
        Check(inputs.Select((path, i) => File.ReadAllBytes(path).SequenceEqual(originals[i])).All(value => value), "source MIDI bytes are unchanged after complete export");
        doc.Parts[0].Simplify = 1;
        var simplified = MidiScore.Prepare(doc, new());
        Check(simplified.Parts[0].Removed == 1 && simplified.Parts[0].Notes.Select(note => note.Voice).Distinct().Count() == 1, "explicit melody simplification reports removed chord notes and produces one voice");
        doc.Parts[0].Instrument = MidiScore.Instruments.First(item => item.Key == "scoreBass");
        doc.Parts[0].Notes.Add(new(3, 4, 100, 90));
        Check(MidiScore.Prepare(doc, new()).Parts[0].OutOfRange > 0, "instrument-range warnings never silently transpose or delete notes");
        var corrupt = Path.Combine(root, "truncated.mid");
        File.WriteAllBytes(corrupt, System.Text.Encoding.ASCII.GetBytes("MThd\0"));
        var rejected = false;
        try { MidiScore.Read(new[] { corrupt }); } catch (InvalidDataException error) { rejected = error.Message == "scoreInvalidMidi"; }
        Check(rejected, "truncated MIDI headers produce a localizable error instead of an index exception");
        var canceledFolder = Path.Combine(root, "canceled"); Directory.CreateDirectory(canceledFolder);
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        rejected = false;
        try { MidiScore.Export(doc, new(), canceledFolder, cancellation.Token); } catch (OperationCanceledException) { rejected = true; }
        Check(rejected && Directory.GetDirectories(canceledFolder).All(path => path.EndsWith(".partial", StringComparison.Ordinal)), "canceled score export never leaves a completed-result folder");
        Console.WriteLine("Score acceptance files: " + exported.Folder);
        static void CheckCursor(bool condition) { if (!condition) throw new Exception("Invalid MusicXML measure timing"); }
    }
}
