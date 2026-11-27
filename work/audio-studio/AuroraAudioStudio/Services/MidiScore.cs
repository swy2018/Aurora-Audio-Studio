using System.Buffers.Binary;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;

namespace AuroraAudioStudio.Services;

public sealed record ScoreInstrument(string Key, int Program, int Low, int High, bool GrandStaff = false, bool Percussion = false);
public sealed record ScoreNote(double Start, double End, int Pitch, int Velocity);
public sealed record ScoreTempo(double Beat, int Microseconds);
public sealed class ScorePart
{
    public string Name { get; set; } = "";
    public ScoreInstrument Instrument { get; set; } = MidiScore.Instruments[0];
    public int Simplify { get; set; }
    public List<ScoreNote> Notes { get; } = [];
}
public sealed class ScoreDocument
{
    public List<string> Sources { get; } = [];
    public List<ScorePart> Parts { get; } = [];
    public List<ScoreTempo> Tempos { get; } = [new(0, 500000)];
    public int Beats { get; set; } = 4;
    public int BeatType { get; set; } = 4;
    public int Fifths { get; set; }
    public bool MeterChanges { get; set; }
    public bool ExpressiveControls { get; set; }
}
public sealed record ScoreOptions(int Grid = 16, int Beats = 4, int BeatType = 4, double? Tempo = null, int Fifths = 0, double OffsetSeconds = 0);
public sealed record ArrangedNote(int Start, int End, int Pitch, int Velocity, int Staff, int Voice = 1);
public sealed record ArrangedPart(string Name, ScoreInstrument Instrument, IReadOnlyList<ArrangedNote> Notes, int Removed, int OutOfRange);
public sealed record ScoreArrangement(IReadOnlyList<ArrangedPart> Parts, IReadOnlyList<ScoreTempo> Tempos, ScoreOptions Options);
public sealed record ScoreExport(string Folder, string MusicXml, string Midi, string Audio, int RemovedNotes, int RangeWarnings);

// Notation preparation, not acoustic re-transcription. Sources are read only;
// all timing/voice simplifications are visible options and separate outputs.
public static class MidiScore
{
    public const int Division = 480;
    public static readonly ScoreInstrument[] Instruments =
    [
        new("scorePiano", 0, 21, 108, true), new("scoreGuitar", 24, 40, 88),
        new("scoreBass", 32, 28, 67), new("scoreViolin", 40, 55, 103),
        new("scoreViola", 41, 48, 91), new("scoreCello", 42, 36, 84),
        new("scoreVoice", 52, 48, 84), new("scoreTrumpet", 56, 54, 82),
        new("scoreClarinet", 71, 50, 94), new("scoreFlute", 73, 60, 96),
        new("scorePercussion", 0, 35, 81, Percussion: true)
    ];

    public static ScoreDocument Read(IEnumerable<string> paths)
    {
        var document = new ScoreDocument();
        foreach (var path in paths.Select(Path.GetFullPath).Distinct(OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal))
        {
            ScoreDocument parsed;
            try { parsed = ReadFile(path); }
            catch (Exception error) when (error is EndOfStreamException or ArgumentOutOfRangeException or OverflowException)
            { throw new InvalidDataException("scoreInvalidMidi", error); }
            if (document.Sources.Count == 0)
            {
                document.Tempos.Clear(); document.Tempos.AddRange(parsed.Tempos);
                document.Beats = parsed.Beats; document.BeatType = parsed.BeatType; document.Fifths = parsed.Fifths;
            }
            document.Sources.Add(path); document.Parts.AddRange(parsed.Parts);
            document.MeterChanges |= parsed.MeterChanges;
            document.ExpressiveControls |= parsed.ExpressiveControls;
        }
        if (document.Parts.Count == 0 || document.Parts.All(part => part.Notes.Count == 0)) throw new InvalidDataException("scoreNoNotes");
        if (document.Parts.Count > 128 || document.Parts.Sum(part => part.Notes.Count) > 100000) throw new InvalidDataException("scoreTooLarge");
        return document;
    }

    private static ScoreDocument ReadFile(string path)
    {
        if (new FileInfo(path).Length > 32 * 1024 * 1024) throw new InvalidDataException("scoreTooLarge");
        using var reader = new BinaryReader(File.OpenRead(path));
        byte[] ReadExact(int count) { var bytes = reader.ReadBytes(count); if (bytes.Length != count) throw new InvalidDataException("scoreInvalidMidi"); return bytes; }
        int Read16() => BinaryPrimitives.ReadUInt16BigEndian(ReadExact(2));
        int Read32() => checked((int)BinaryPrimitives.ReadUInt32BigEndian(ReadExact(4)));
        if (Encoding.ASCII.GetString(reader.ReadBytes(4)) != "MThd") throw new InvalidDataException("scoreInvalidMidi");
        var header = Read32();
        if (header < 6 || header > reader.BaseStream.Length - reader.BaseStream.Position) throw new InvalidDataException("scoreInvalidMidi");
        var format = Read16(); var tracks = Read16(); var ticks = Read16();
        if (format > 1 || ticks == 0 || (ticks & 0x8000) != 0 || tracks == 0) throw new InvalidDataException("scoreUnsupportedMidi");
        reader.BaseStream.Position += header - 6;
        var doc = new ScoreDocument();
        var tempos = new List<(long Tick, int Value)> { (0, 500000) };
        var notes = new List<(ScorePart Part, long Start, long End, int Pitch, int Velocity)>();
        var meters = new List<(long Tick, int Beats, int Type)>();
        for (var track = 0; track < tracks; track++)
        {
            if (Encoding.ASCII.GetString(reader.ReadBytes(4)) != "MTrk") throw new InvalidDataException("scoreInvalidMidi");
            var length = Read32(); var end = reader.BaseStream.Position + length;
            if (end > reader.BaseStream.Length) throw new InvalidDataException("scoreInvalidMidi");
            long time = 0;
            var running = 0; var port = 0;
            var programs = new int[16];
            var name = Path.GetFileNameWithoutExtension(path) + " · " + (track + 1);
            var parts = new Dictionary<(int Port, int Channel, int Program), ScorePart>();
            var active = new Dictionary<(int Channel, int Pitch), Queue<(ScorePart Part, long Start, int Velocity)>>();
            byte Byte()
            {
                if (reader.BaseStream.Position >= end) throw new InvalidDataException("scoreInvalidMidi");
                return reader.ReadByte();
            }
            int Variable()
            {
                var value = 0;
                for (var i = 0; i < 4; i++) { var next = Byte(); value = (value << 7) | (next & 127); if (next < 128) return value; }
                throw new InvalidDataException("scoreInvalidMidi");
            }
            while (reader.BaseStream.Position < end)
            {
                time = checked(time + Variable());
                var first = Byte();
                var status = first >= 128 ? first : running;
                if (status < 128) throw new InvalidDataException("scoreInvalidMidi");
                if (status == 255)
                {
                    running = 0;
                    var type = Byte(); var count = Variable();
                    if (reader.BaseStream.Position + count > end) throw new InvalidDataException("scoreInvalidMidi");
                    var data = reader.ReadBytes(count);
                    if (type == 3) name = Encoding.UTF8.GetString(data).Trim('\0');
                    if (type == 0x21 && count == 1) port = data[0];
                    if (type == 0x51 && count == 3)
                    {
                        var value = (data[0] << 16) | (data[1] << 8) | data[2];
                        if (value == 0) throw new InvalidDataException("scoreInvalidMidi");
                        tempos.Add((time, value));
                    }
                    if (type == 0x58 && count >= 2 && data[0] > 0 && data[1] < 6) meters.Add((time, data[0], 1 << data[1]));
                    if (type == 0x59 && count >= 2 && time == 0) doc.Fifths = Math.Clamp((int)(sbyte)data[0], -7, 7);
                    if (type == 0x2F) break;
                    continue;
                }
                if (status is 0xF0 or 0xF7)
                {
                    running = 0; var count = Variable();
                    if (reader.BaseStream.Position + count > end) throw new InvalidDataException("scoreInvalidMidi");
                    reader.BaseStream.Position += count; doc.ExpressiveControls = true; continue;
                }
                if (status >= 0xF0) throw new InvalidDataException("scoreUnsupportedMidi");
                running = status;
                var kind = status & 0xF0; var channel = status & 15;
                var a = first < 128 ? first : Byte(); var b = kind is 0xC0 or 0xD0 ? 0 : Byte();
                if (a >= 128 || b >= 128) throw new InvalidDataException("scoreInvalidMidi");
                if (kind == 0xC0) programs[channel] = a;
                else if (kind is 0xB0 or 0xE0 or 0xD0 or 0xA0) doc.ExpressiveControls = true;
                else if (kind == 0x90 && b > 0)
                {
                    var key = (port, channel, programs[channel]);
                    if (!parts.TryGetValue(key, out var part))
                    {
                        var instrument = channel == 9 ? Instruments[^1] : Instruments.FirstOrDefault(item => !item.Percussion && item.Program == programs[channel])
                            ?? new ScoreInstrument("GM " + (programs[channel] + 1), programs[channel], 0, 127);
                        parts[key] = part = new ScorePart { Name = name, Instrument = instrument };
                        doc.Parts.Add(part);
                    }
                    if (!active.TryGetValue((channel, a), out var queue)) active[(channel, a)] = queue = new();
                    queue.Enqueue((part, time, b));
                }
                else if (kind == 0x80 || kind == 0x90 && b == 0)
                {
                    if (active.TryGetValue((channel, a), out var queue) && queue.TryDequeue(out var note) && time > note.Start)
                        notes.Add((note.Part, note.Start, time, a, note.Velocity));
                }
                if (notes.Count > 100000) throw new InvalidDataException("scoreTooLarge");
            }
            if (active.Values.Any(queue => queue.Count > 0)) throw new InvalidDataException("scoreOpenNotes");
            reader.BaseStream.Position = end;
        }
        doc.Tempos.Clear();
        doc.Tempos.AddRange(tempos.GroupBy(item => item.Tick).OrderBy(group => group.Key).Select(group => new ScoreTempo((double)group.Key / ticks, group.Last().Value)));
        foreach (var note in notes)
            note.Part.Notes.Add(new(ToSeconds((double)note.Start / ticks, doc.Tempos), ToSeconds((double)note.End / ticks, doc.Tempos), note.Pitch, note.Velocity));
        if (meters.Count > 0)
        {
            var meter = meters.OrderBy(item => item.Tick).First(); doc.Beats = meter.Beats; doc.BeatType = meter.Type;
            doc.MeterChanges = meters.Any(item => item.Beats != meter.Beats || item.Type != meter.Type);
        }
        return doc;
    }

    public static double ToSeconds(double beat, IReadOnlyList<ScoreTempo> tempos)
    {
        var seconds = 0d; var previous = 0d; var value = 500000;
        foreach (var tempo in tempos)
        {
            if (tempo.Beat > beat) break;
            seconds += (tempo.Beat - previous) * value / 1000000; previous = tempo.Beat; value = tempo.Microseconds;
        }
        return seconds + (beat - previous) * value / 1000000;
    }

    private static double ToBeat(double seconds, IReadOnlyList<ScoreTempo> tempos)
    {
        var previous = 0d; var time = 0d; var value = 500000;
        foreach (var tempo in tempos)
        {
            var next = time + (tempo.Beat - previous) * value / 1000000;
            if (next > seconds) break;
            time = next; previous = tempo.Beat; value = tempo.Microseconds;
        }
        return previous + (seconds - time) * 1000000 / value;
    }

    public static ScoreArrangement Prepare(ScoreDocument document, ScoreOptions options)
    {
        if (options.Grid is not (4 or 8 or 16 or 32 or 12 or 24) || options.Beats is < 1 or > 12
            || options.BeatType is not (2 or 4 or 8 or 16) || options.Fifths is < -7 or > 7
            || options.Tempo is not null && (!double.IsFinite(options.Tempo.Value) || options.Tempo is < 20 or > 400)
            || !double.IsFinite(options.OffsetSeconds) || options.OffsetSeconds is < 0 or > 60) throw new ArgumentException("scoreInvalidOptions");
        IReadOnlyList<ScoreTempo> tempos = options.Tempo is { } bpm ? [new ScoreTempo(0, (int)Math.Round(60000000 / bpm))] : document.Tempos;
        var grid = Division * 4 / options.Grid;
        if (options.Grid is 12 or 24 && (Division * 4 * options.Beats / options.BeatType) % (grid * 3) != 0)
            throw new ArgumentException("scoreTripletMeter");
        int Quantize(double seconds) => checked((int)Math.Round(ToBeat(seconds - options.OffsetSeconds, tempos) * Division / grid, MidpointRounding.AwayFromZero) * grid);
        var parts = new List<ArrangedPart>();
        foreach (var source in document.Parts.Where(part => part.Notes.Count > 0))
        {
            if (source.Simplify is not (0 or 1 or 4)) throw new ArgumentException("scoreInvalidOptions");
            var notes = source.Notes.Where(note => note.End > options.OffsetSeconds).Select(note =>
            {
                var start = Math.Max(0, Quantize(note.Start)); var end = Math.Max(start + grid, Quantize(note.End));
                return new ArrangedNote(start, end, note.Pitch, note.Velocity, source.Instrument.GrandStaff && note.Pitch < 60 ? 2 : 1);
            }).GroupBy(note => (note.Start, note.Pitch)).Select(group => group.First() with { End = group.Max(note => note.End), Velocity = group.Max(note => note.Velocity) }).ToList();
            if (source.Simplify > 0)
                notes = notes.GroupBy(note => note.Start).SelectMany(group => source.Simplify == 1
                    ? group.OrderByDescending(note => note.Pitch).Take(1) : group.OrderByDescending(note => note.Velocity).ThenByDescending(note => note.Pitch).Take(source.Simplify)).OrderBy(note => note.Start).ToList();
            if (source.Simplify == 1)
                for (var i = 0; i + 1 < notes.Count; i++) notes[i] = notes[i] with { End = Math.Min(notes[i].End, notes[i + 1].Start) };
            var assigned = new List<ArrangedNote>();
            foreach (var staff in notes.GroupBy(note => note.Staff))
            {
                var ends = new List<int>();
                foreach (var chord in staff.GroupBy(note => (note.Start, note.End)).OrderBy(group => group.Key.Start).ThenBy(group => group.Key.End))
                {
                    var voice = ends.FindIndex(end => end <= chord.Key.Start);
                    if (voice < 0) { voice = ends.Count; ends.Add(0); }
                    ends[voice] = chord.Key.End;
                    assigned.AddRange(chord.Select(note => note with { Voice = (staff.Key - 1) * 100 + voice + 1 }));
                }
            }
            parts.Add(new(source.Name, source.Instrument, assigned, source.Notes.Count - assigned.Count,
                assigned.Count(note => !source.Instrument.Percussion && (note.Pitch < source.Instrument.Low || note.Pitch > source.Instrument.High))));
        }
        if (parts.All(part => part.Notes.Count == 0)) throw new InvalidDataException("scoreNoNotes");
        if (parts.SelectMany(part => part.Notes).Max(note => note.End) > Division * 400 * 60) throw new InvalidDataException("scoreTooLarge");
        return new(parts.Where(part => part.Notes.Count > 0).ToArray(), tempos, options);
    }

    public static ScoreExport Export(ScoreDocument document, ScoreOptions options, string folder, CancellationToken token = default)
    {
        var score = Prepare(document, options);
        var output = Path.Combine(folder, "Aurora-Score-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N")[..8]);
        var staging = output + ".partial";
        Directory.CreateDirectory(staging);
        var xml = Path.Combine(staging, "score.musicxml"); var midi = Path.Combine(staging, "score-arranged.mid"); var audio = Path.Combine(staging, "score-review.wav");
        MusicXml(score, Path.GetFileNameWithoutExtension(document.Sources[0]), token).Save(xml);
        token.ThrowIfCancellationRequested();
        WriteMidi(score, midi);
        RenderAudio(score, audio, token);
        token.ThrowIfCancellationRequested();
        ArtifactValidator.Validate(midi); ArtifactValidator.Validate(audio);
        var result = new ScoreExport(output, Path.Combine(output, "score.musicxml"), Path.Combine(output, "score-arranged.mid"), Path.Combine(output, "score-review.wav"), score.Parts.Sum(part => part.Removed), score.Parts.Sum(part => part.OutOfRange));
        File.WriteAllText(Path.Combine(staging, "score-report.json"), JsonSerializer.Serialize(new
        {
            sourceFiles = document.Sources, sourceFilesModified = false, options,
            parts = score.Parts.Select(part => new { part.Name, part.Instrument, notes = part.Notes.Count, part.Removed, part.OutOfRange }),
            document.MeterChanges, document.ExpressiveControls,
            note = "Notation draft for review, not a verified transcription. Playback uses synthesized reference tones, not sampled instruments. Pitch bends, pedals and other expressive MIDI controls remain in the original sources only."
        }, new JsonSerializerOptions { WriteIndented = true }));
        token.ThrowIfCancellationRequested();
        Directory.Move(staging, output);
        return result;
    }

    public static XDocument MusicXml(ScoreArrangement score, string title, CancellationToken token = default)
    {
        var root = new XElement("score-partwise", new XAttribute("version", "4.0"), new XElement("work", new XElement("work-title", title)),
            new XElement("identification", new XElement("encoding", new XElement("software", "Aurora Audio Studio"))),
            new XElement("defaults", new XElement("scaling", new XElement("millimeters", 7), new XElement("tenths", 40))));
        var list = new XElement("part-list"); root.Add(list);
        var bar = Division * 4 * score.Options.Beats / score.Options.BeatType;
        var quantum = Division * 4 / score.Options.Grid;
        var count = (score.Parts.SelectMany(part => part.Notes).Max(note => note.End) + bar - 1) / bar;
        if (count > 4000) throw new InvalidDataException("scoreTooLarge");
        var slices = 0;
        for (var index = 0; index < score.Parts.Count; index++)
        {
            var part = score.Parts[index]; var id = "P" + (index + 1);
            var declaration = new XElement("score-part", new XAttribute("id", id), new XElement("part-name", part.Name));
            var pitches = part.Instrument.Percussion ? part.Notes.Select(note => note.Pitch).Distinct().Order().ToArray() : [part.Instrument.Program];
            foreach (var pitch in pitches) declaration.Add(new XElement("score-instrument", new XAttribute("id", id + "I" + pitch), new XElement("instrument-name", part.Instrument.Percussion ? "GM Percussion " + pitch : part.Name)));
            declaration.Add(new XElement("midi-device", new XAttribute("port", index / 15 + 1)));
            foreach (var pitch in pitches) declaration.Add(new XElement("midi-instrument", new XAttribute("id", id + "I" + pitch),
                new XElement("midi-channel", part.Instrument.Percussion ? 10 : MidiChannel(index) + 1),
                new XElement("midi-program", part.Instrument.Program + 1), part.Instrument.Percussion ? new XElement("midi-unpitched", pitch + 1) : null));
            list.Add(declaration);
            var body = new XElement("part", new XAttribute("id", id)); root.Add(body);
            var byMeasure = new Dictionary<int, List<ArrangedNote>>();
            foreach (var note in part.Notes)
                for (var measure = note.Start / bar; measure <= (note.End - 1) / bar; measure++)
                {
                    if (++slices > 300000) throw new InvalidDataException("scoreTooLarge");
                    if (!byMeasure.TryGetValue(measure, out var listOfNotes)) byMeasure[measure] = listOfNotes = [];
                    listOfNotes.Add(note);
                }
            for (var measure = 0; measure < count; measure++)
            {
                token.ThrowIfCancellationRequested();
                var start = measure * bar; var end = start + bar;
                var measureNotes = byMeasure.GetValueOrDefault(measure) ?? [];
                var voices = measureNotes.Select(note => (note.Staff, note.Voice)).Distinct().OrderBy(value => value.Staff).ThenBy(value => value.Voice).ToList();
                foreach (var staff in part.Instrument.GrandStaff ? new[] { 1, 2 } : new[] { 1 })
                    if (!voices.Any(value => value.Staff == staff)) voices.Add((staff, (staff - 1) * 100 + 1));
                var accidentals = Accidentals(measureNotes, part.Instrument.Percussion, start, end, score.Options.Fifths);
                var element = new XElement("measure", new XAttribute("number", measure + 1)); body.Add(element);
                if (measure == 0)
                {
                    var attributes = new XElement("attributes", new XElement("divisions", Division),
                        part.Instrument.Percussion ? null : new XElement("key", new XElement("fifths", score.Options.Fifths)),
                        new XElement("time", new XElement("beats", score.Options.Beats), new XElement("beat-type", score.Options.BeatType)),
                        part.Instrument.GrandStaff ? new XElement("staves", 2) : null);
                    attributes.Add(Clef(part.Instrument, 1)); if (part.Instrument.GrandStaff) attributes.Add(Clef(part.Instrument, 2));
                    element.Add(attributes);
                }
                if (index == 0)
                    foreach (var tempo in score.Tempos.Where(tempo => tempo.Beat * Division >= start && tempo.Beat * Division < end))
                        element.Add(new XElement("direction", new XElement("direction-type", new XElement("metronome", new XElement("beat-unit", "quarter"),
                            new XElement("per-minute", (60000000d / tempo.Microseconds).ToString("0.###", CultureInfo.InvariantCulture)))),
                            new XElement("offset", (int)Math.Round(tempo.Beat * Division) - start),
                            new XElement("sound", new XAttribute("tempo", (60000000d / tempo.Microseconds).ToString("0.###", CultureInfo.InvariantCulture)))));
                var firstVoice = true;
                foreach (var voice in voices)
                {
                    if (!firstVoice) element.Add(new XElement("backup", new XElement("duration", bar)));
                    firstVoice = false; var cursor = start;
                    foreach (var group in measureNotes.Where(note => note.Voice == voice.Voice)
                        .GroupBy(note => (note.Start, note.End)).OrderBy(group => group.Key.Start))
                    {
                        var onset = Math.Max(start, group.Key.Start); var stop = Math.Min(end, group.Key.End);
                        AddRest(element, onset - cursor, voice, part.Instrument.GrandStaff, cursor - start, quantum);
                        var position = onset;
                        foreach (var duration in Durations(stop - onset, onset - start, quantum))
                        {
                            var chord = false;
                            foreach (var note in group.OrderBy(note => note.Pitch))
                            {
                                element.Add(Note(note, duration, position > note.Start, position + duration.Ticks < note.End, chord, part.Instrument, id, score.Options.Fifths,
                                    accidentals.Contains((note.Start, note.Pitch, note.Staff))));
                                chord = true;
                            }
                            position += duration.Ticks;
                        }
                        cursor = stop;
                    }
                    AddRest(element, end - cursor, voice, part.Instrument.GrandStaff, cursor - start, quantum);
                }
                AddBeamsAndTuplets(element, score.Options);
                if (measure == count - 1) element.Add(new XElement("barline", new XAttribute("location", "right"), new XElement("bar-style", "light-heavy")));
            }
        }
        return new XDocument(new XDeclaration("1.0", "utf-8", null), root);
    }

    private static XElement Clef(ScoreInstrument instrument, int staff)
    {
        var sign = instrument.Percussion ? "percussion" : instrument.GrandStaff && staff == 2 || instrument.Key is "scoreBass" or "scoreCello" ? "F" : instrument.Key == "scoreViola" ? "C" : "G";
        return new XElement("clef", instrument.GrandStaff ? new XAttribute("number", staff) : null,
            new XElement("sign", sign), sign == "percussion" ? null : new XElement("line", sign == "F" ? 4 : sign == "C" ? 3 : 2));
    }
    private sealed record Duration(int Ticks, string Type, bool Dot = false, bool Triplet = false);
    private static IEnumerable<Duration> Durations(int remaining, int position, int quantum)
    {
        Duration[] choices = [new(1920,"whole"), new(1440,"half",true), new(960,"half"), new(720,"quarter",true), new(480,"quarter"),
            new(360,"eighth",true), new(320,"quarter",Triplet:true), new(240,"eighth"), new(180,"16th",true), new(160,"eighth",Triplet:true),
            new(120,"16th"), new(80,"16th",Triplet:true), new(60,"32nd"), new(40,"32nd",Triplet:true), new(30,"64th"), new(20,"64th",Triplet:true)];
        while (remaining > 0)
        {
            if (quantum is 80 or 160)
            {
                var span = quantum * 3;
                // Whole triplet groups can use an ordinary note value. Partial
                // groups stay inside their bracket, including rests and ties.
                var full = position % span == 0 ? choices.FirstOrDefault(value => !value.Triplet && value.Ticks <= remaining && value.Ticks % span == 0) : null;
                var limitTriplet = Math.Min(remaining, span - position % span);
                var triplet = full ?? choices.FirstOrDefault(value => value.Triplet && value.Ticks <= limitTriplet && value.Ticks % quantum == 0)
                    ?? throw new InvalidDataException("scoreInvalidRhythm");
                yield return triplet; remaining -= triplet.Ticks; position += triplet.Ticks;
                continue;
            }
            var limit = position % Division == 0 ? remaining : Math.Min(remaining, Division - position % Division);
            var value = choices.FirstOrDefault(duration => duration.Ticks <= limit && duration.Ticks % quantum == 0)
                ?? choices.FirstOrDefault(duration => duration.Ticks <= limit && (remaining - duration.Ticks) % 10 == 0)
                ?? throw new InvalidDataException("scoreInvalidRhythm");
            yield return value; remaining -= value.Ticks; position += value.Ticks;
        }
    }
    private static void AddRest(XElement measure, int ticks, (int Staff, int Voice) voice, bool grandStaff, int position, int quantum)
    {
        foreach (var duration in Durations(ticks, position, quantum))
        {
            var rest = new XElement("note", new XElement("rest"), new XElement("duration", duration.Ticks), new XElement("voice", voice.Voice), new XElement("type", duration.Type));
            AddDuration(rest, duration); if (grandStaff) rest.Add(new XElement("staff", voice.Staff)); measure.Add(rest);
        }
    }
    private static Dictionary<string, int> KeyAlterations(int fifths)
    {
        var result = new Dictionary<string, int> { ["C"] = 0, ["D"] = 0, ["E"] = 0, ["F"] = 0, ["G"] = 0, ["A"] = 0, ["B"] = 0 };
        var order = fifths < 0 ? "BEADGCF" : "FCGDAEB";
        foreach (var step in order.Take(Math.Abs(fifths))) result[step.ToString()] = Math.Sign(fifths);
        return result;
    }
    private static (string Step, int Alter, int Octave) Spelling(int pitch, int fifths)
    {
        var key = KeyAlterations(fifths);
        var bases = new Dictionary<string, int> { ["C"] = 0, ["D"] = 2, ["E"] = 4, ["F"] = 5, ["G"] = 7, ["A"] = 9, ["B"] = 11 };
        foreach (var step in bases.Keys)
            if ((bases[step] + key[step] + 12) % 12 == pitch % 12) return (step, key[step], (pitch - bases[step] - key[step]) / 12 - 1);
        string[] steps = fifths < 0 ? ["C","D","D","E","E","F","G","G","A","A","B","B"] : ["C","C","D","D","E","F","F","G","G","A","A","B"];
        int[] alterations = fifths < 0 ? [0,-1,0,-1,0,0,-1,0,-1,0,-1,0] : [0,1,0,1,0,0,1,0,1,0,1,0];
        var selected = steps[pitch % 12]; var alter = alterations[pitch % 12];
        return (selected, alter, (pitch - bases[selected] - alter) / 12 - 1);
    }
    private static HashSet<(int, int, int)> Accidentals(IReadOnlyList<ArrangedNote> notes, bool percussion, int start, int end, int fifths)
    {
        var marks = new HashSet<(int, int, int)>();
        if (percussion) return marks;
        var key = KeyAlterations(fifths);
        var state = new Dictionary<(int Staff, string Step, int Octave), int>();
        // Compute in score time across voices, not in XML's voice-by-voice order.
        foreach (var note in notes.Where(note => note.Start >= start && note.Start < end).OrderBy(note => note.Start).ThenBy(note => note.Pitch))
        {
            var pitch = Spelling(note.Pitch, fifths); var identity = (note.Staff, pitch.Step, pitch.Octave);
            if (state.GetValueOrDefault(identity, key[pitch.Step]) != pitch.Alter) marks.Add((note.Start, note.Pitch, note.Staff));
            state[identity] = pitch.Alter;
        }
        return marks;
    }
    private static XElement Note(ArrangedNote value, Duration duration, bool stop, bool start, bool chord, ScoreInstrument instrument, string part, int fifths, bool showAccidental)
    {
        var spelling = Spelling(value.Pitch, fifths);
        var pitch = instrument.Percussion ? new XElement("unpitched", new XElement("display-step", value.Pitch is 35 or 36 ? "F" : value.Pitch is 38 or 40 ? "C" : "G"), new XElement("display-octave", value.Pitch is 35 or 36 ? 4 : 5))
            : new XElement("pitch", new XElement("step", spelling.Step), spelling.Alter == 0 ? null : new XElement("alter", spelling.Alter), new XElement("octave", spelling.Octave));
        var note = new XElement("note", chord ? new XElement("chord") : null, pitch, new XElement("duration", duration.Ticks));
        if (stop) note.Add(new XElement("tie", new XAttribute("type", "stop")));
        if (start) note.Add(new XElement("tie", new XAttribute("type", "start")));
        if (instrument.Percussion) note.Add(new XElement("instrument", new XAttribute("id", part + "I" + value.Pitch)));
        note.Add(new XElement("voice", value.Voice), new XElement("type", duration.Type));
        AddDuration(note, duration, showAccidental && !stop ? spelling.Alter switch { -1 => "flat", 1 => "sharp", _ => "natural" } : null);
        if (instrument.GrandStaff) note.Add(new XElement("staff", value.Staff));
        if (stop || start) note.Add(new XElement("notations", stop ? new XElement("tied", new XAttribute("type", "stop")) : null, start ? new XElement("tied", new XAttribute("type", "start")) : null));
        return note;
    }
    private static void AddDuration(XElement note, Duration duration, string? accidental = null)
    {
        if (duration.Dot) note.Add(new XElement("dot"));
        if (accidental is not null) note.Add(new XElement("accidental", accidental));
        if (duration.Triplet) note.Add(new XElement("time-modification", new XElement("actual-notes", 3), new XElement("normal-notes", 2), new XElement("normal-type", duration.Type)));
    }

    private static void AddBeamsAndTuplets(XElement measure, ScoreOptions options)
    {
        var events = new List<(XElement Node, int At, int Ticks, int Voice, int Levels)>(); var cursor = 0;
        foreach (var element in measure.Elements())
        {
            if (element.Name == "backup") cursor -= (int)element.Element("duration")!;
            if (element.Name != "note" || element.Element("chord") is not null) continue;
            var ticks = (int)element.Element("duration")!;
            var levels = element.Element("rest") is not null ? 0 : element.Element("type")?.Value switch { "eighth" => 1, "16th" => 2, "32nd" => 3, "64th" => 4, _ => 0 };
            events.Add((element, cursor, ticks, (int)element.Element("voice")!, levels)); cursor += ticks;
        }
        var beat = options.BeatType == 8 && options.Beats % 3 == 0 ? Division * 3 / 2 : Division;
        foreach (var voice in events.GroupBy(item => item.Voice))
        {
            var ordered = voice.OrderBy(item => item.At).ToArray();
            for (var level = 1; level <= 4; level++)
            {
                for (var i = 0; i < ordered.Length; i++)
                {
                    if (ordered[i].Levels < level) continue;
                    var end = i;
                    while (end + 1 < ordered.Length && ordered[end + 1].Levels >= level
                        && ordered[end].At + ordered[end].Ticks == ordered[end + 1].At && ordered[i].At / beat == ordered[end + 1].At / beat) end++;
                    if (end > i || level > 1 && ordered[i].Node.Elements("beam").Any())
                        for (var n = i; n <= end; n++)
                        {
                            var beam = new XElement("beam", new XAttribute("number", level), end == i ? (i + 1 < ordered.Length ? "forward hook" : "backward hook") : n == i ? "begin" : n == end ? "end" : "continue");
                            if (ordered[n].Node.Element("notations") is { } notation) notation.AddBeforeSelf(beam); else ordered[n].Node.Add(beam);
                        }
                    i = end;
                }
            }
            var tripletSpan = Division * 4 / options.Grid * 3;
            foreach (var group in ordered.Where(item => item.Node.Element("time-modification") is not null).GroupBy(item => item.At / tripletSpan))
            {
                foreach (var boundary in new[] { (group.First().Node, "start"), (group.Last().Node, "stop") })
                {
                    var notation = boundary.Node.Element("notations");
                    if (notation is null) { notation = new XElement("notations"); boundary.Node.Add(notation); }
                    notation.Add(new XElement("tuplet", new XAttribute("type", boundary.Item2), new XAttribute("number", 1)));
                }
            }
        }
    }

    private static int MidiChannel(int part) { var channel = part % 15; return channel >= 9 ? channel + 1 : channel; }
    public static void WriteMidi(ScoreArrangement score, string path)
    {
        using var writer = new BinaryWriter(File.Create(path));
        void Big16(int value) { Span<byte> data = stackalloc byte[2]; BinaryPrimitives.WriteUInt16BigEndian(data, checked((ushort)value)); writer.Write(data); }
        void Big32(int value) { Span<byte> data = stackalloc byte[4]; BinaryPrimitives.WriteUInt32BigEndian(data, checked((uint)value)); writer.Write(data); }
        void Track(List<(int At, int Order, byte[] Bytes)> events)
        {
            using var memory = new MemoryStream(); var previous = 0;
            Span<byte> bytes = stackalloc byte[4];
            foreach (var item in events.OrderBy(item => item.At).ThenBy(item => item.Order))
            {
                var delta = item.At - previous; var i = 3; bytes[i] = (byte)(delta & 127);
                while ((delta >>= 7) > 0) bytes[--i] = (byte)((delta & 127) | 128);
                memory.Write(bytes[i..]); memory.Write(item.Bytes); previous = item.At;
            }
            memory.Write([0,255,47,0]); writer.Write(Encoding.ASCII.GetBytes("MTrk")); Big32(checked((int)memory.Length)); writer.Write(memory.ToArray());
        }
        writer.Write(Encoding.ASCII.GetBytes("MThd")); Big32(6); Big16(1); Big16(score.Parts.Count + 1); Big16(Division);
        var meta = score.Tempos.Select(tempo => ((int)Math.Round(tempo.Beat * Division), 0, new byte[] { 255,81,3,(byte)(tempo.Microseconds >> 16),(byte)(tempo.Microseconds >> 8),(byte)tempo.Microseconds })).ToList();
        meta.Add((0, 1, [255,88,4,(byte)score.Options.Beats,(byte)Math.Log2(score.Options.BeatType),24,8]));
        meta.Add((0, 2, [255,89,2,unchecked((byte)score.Options.Fifths),0])); Track(meta);
        for (var i = 0; i < score.Parts.Count; i++)
        {
            var part = score.Parts[i]; var channel = part.Instrument.Percussion ? 9 : MidiChannel(i);
            var events = new List<(int, int, byte[])> { (0, -3, [255,33,1,(byte)(i / 15)]), (0, -2, [(byte)(0xC0 | channel),(byte)part.Instrument.Program]) };
            var name = Encoding.UTF8.GetBytes(part.Name); if (name.Length > 127) name = Encoding.UTF8.GetBytes("Part " + (i + 1));
            events.Add((0, -1, new byte[] { 255,3,(byte)name.Length }.Concat(name).ToArray()));
            foreach (var note in part.Notes) { events.Add((note.Start, 1, [(byte)(0x90 | channel),(byte)note.Pitch,(byte)note.Velocity])); events.Add((note.End, 0, [(byte)(0x80 | channel),(byte)note.Pitch,0])); }
            Track(events);
        }
    }

    public static void RenderAudio(ScoreArrangement score, string path, CancellationToken token = default)
    {
        const int rate = 22050;
        var last = score.Parts.SelectMany(part => part.Notes).Max(note => note.End);
        var seconds = ToSeconds((double)last / Division, score.Tempos) + .2;
        if (seconds > 1800) throw new InvalidDataException("scorePreviewTooLong");
        var samples = new float[checked((int)Math.Ceiling(seconds * rate))];
        foreach (var part in score.Parts)
            foreach (var note in part.Notes)
            {
                token.ThrowIfCancellationRequested();
                var start = (int)Math.Round(ToSeconds((double)note.Start / Division, score.Tempos) * rate);
                var end = Math.Min(samples.Length, (int)Math.Round(ToSeconds((double)note.End / Division, score.Tempos) * rate));
                var frequency = Math.Min(rate * .45, 440 * Math.Pow(2, (note.Pitch - 69) / 12d));
                for (var i = start; i < end; i++)
                {
                    if ((i & 16383) == 0) token.ThrowIfCancellationRequested();
                    var t = (double)(i - start) / rate;
                    var envelope = Math.Min(1, t / .008) * Math.Min(1, (double)(end - i) / (rate * .025));
                    if (part.Instrument.Percussion) envelope *= Math.Exp(-t * 18);
                    samples[i] += (float)(Math.Sin(2 * Math.PI * frequency * t) * envelope * note.Velocity / 127);
                }
            }
        var peak = Math.Max(1, samples.Max(value => Math.Abs(value)));
        using var writer = new BinaryWriter(File.Create(path));
        writer.Write(Encoding.ASCII.GetBytes("RIFF")); writer.Write(samples.Length * 2 + 36); writer.Write(Encoding.ASCII.GetBytes("WAVEfmt "));
        writer.Write(16); writer.Write((short)1); writer.Write((short)1); writer.Write(rate); writer.Write(rate * 2); writer.Write((short)2); writer.Write((short)16);
        writer.Write(Encoding.ASCII.GetBytes("data")); writer.Write(samples.Length * 2);
        foreach (var value in samples) writer.Write((short)Math.Round(value / peak * 28000));
    }
}
