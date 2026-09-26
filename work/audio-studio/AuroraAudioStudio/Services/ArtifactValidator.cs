using System.Buffers.Binary;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using AuroraAudioStudio.Models;

namespace AuroraAudioStudio.Services;

public static class ArtifactValidator
{
    public static string CreateRunDirectory(string outputRoot, string group, string source)
    {
        var name = Path.GetFileNameWithoutExtension(source);
        name = string.Concat(name.Where(c => !Path.GetInvalidFileNameChars().Contains(c)));
        if (name.Length > 60) name = name[..60];
        var path = Path.Combine(outputRoot, group, $"{DateTime.Now:yyyyMMdd-HHmmss}-{name}-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }

    public static IReadOnlyList<string> Collect(string feature, string directory)
    {
        if (!Directory.Exists(directory)) throw new InvalidDataException("输出目录不存在，无法确认处理结果。请检查保存位置和任务日志。");
        var extension = feature == "transcription" ? ".mid" : feature == "subtitles" ? ".srt" : ".wav";
        var files = Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories)
            .Where(path => Path.GetExtension(path).Equals(extension, StringComparison.OrdinalIgnoreCase)).ToArray();
        if (files.Length == 0) throw new InvalidDataException("引擎没有生成可用的成品文件。请查看任务日志。");
        foreach (var path in files) Validate(path);
        return files;
    }

    public static void Validate(string path) => Inspect(path);

    public static ArtifactInfo Inspect(string path)
    {
        if (!File.Exists(path)) throw new FileNotFoundException("成品文件不存在。", path);
        try
        {
            var result = Path.GetExtension(path).ToLowerInvariant() switch
            {
                ".wav" => ReadWave(path),
                ".mid" or ".midi" => ReadMidi(path),
                ".srt" => ReadSubtitles(path),
                _ => throw new InvalidDataException("支持导入 WAV、MIDI 和 SRT 成品。")
            };
            if (result.Format == "MIDI" && result.Notes == 0)
                throw new InvalidDataException("未识别出 MIDI 音符，请使用钢琴演奏或选择适合素材的扒谱模型。");
            return result with { Bytes = new FileInfo(path).Length };
        }
        catch (EndOfStreamException ex) { throw new InvalidDataException("成品文件的数据不完整。", ex); }
    }

    private static ArtifactInfo ReadWave(string path)
    {
        using var file = File.OpenRead(path);
        using var reader = new BinaryReader(file);
        if (file.Length < 44 || Encoding.ASCII.GetString(reader.ReadBytes(4)) != "RIFF") throw new InvalidDataException("WAV 文件头无效。");
        var riffEnd = 8L + reader.ReadUInt32();
        if (riffEnd < 44 || riffEnd > file.Length) throw new InvalidDataException("WAV 声明的数据长度无效。");
        if (Encoding.ASCII.GetString(reader.ReadBytes(4)) != "WAVE") throw new InvalidDataException("WAV 格式无效。");
        ushort encoding = 0, channels = 0, bits = 0, alignment = 0;
        uint rate = 0, byteRate = 0;
        var chunks = new List<uint>();
        while (file.Position + 8 <= riffEnd)
        {
            var id = Encoding.ASCII.GetString(reader.ReadBytes(4));
            var size = reader.ReadUInt32(); var end = file.Position + size;
            if (end > riffEnd) throw new InvalidDataException("WAV 音频数据不完整。");
            if (id == "fmt " && size >= 16)
            {
                encoding = reader.ReadUInt16(); channels = reader.ReadUInt16(); rate = reader.ReadUInt32();
                byteRate = reader.ReadUInt32(); alignment = reader.ReadUInt16(); bits = reader.ReadUInt16();
                // WAVEFORMATEXTENSIBLE stores valid bits separately from its byte-aligned container.
                // https://learn.microsoft.com/windows/win32/api/mmreg/ns-mmreg-waveformatextensible
                if (encoding == 65534)
                {
                    if (size < 40 || reader.ReadUInt16() < 22) throw new InvalidDataException("WAV 扩展格式不完整。");
                    var validBits = reader.ReadUInt16(); reader.ReadUInt32();
                    var subtype = new Guid(reader.ReadBytes(16));
                    encoding = subtype == new Guid("00000001-0000-0010-8000-00aa00389b71") ? (ushort)1
                        : subtype == new Guid("00000003-0000-0010-8000-00aa00389b71") ? (ushort)3 : (ushort)0;
                    if (validBits == 0 || validBits > bits) throw new InvalidDataException("WAV 有效位深无效。");
                }
            }
            if (id == "data") chunks.Add(size);
            file.Position = Math.Min(riffEnd, end + (size & 1));
        }
        var supportedBits = encoding == 1 ? bits is 8 or 16 or 24 or 32 : encoding == 3 && bits is 32 or 64;
        if (!supportedBits || channels == 0 || rate == 0 || rate > int.MaxValue || alignment != channels * (bits / 8)
            || byteRate != (long)rate * alignment || chunks.Count == 0 || chunks.Sum(x => (long)x) == 0
            || chunks.Any(size => size % alignment != 0))
            throw new InvalidDataException("WAV 格式、采样帧或音频长度无效。");
        return new("WAV", DurationSeconds: chunks.Sum(x => (long)x) / (double)byteRate, SampleRate: checked((int)rate), Channels: channels);
    }

    public static int MidiNoteCount(string path) => Inspect(path).Notes;

    private static ArtifactInfo ReadMidi(string path)
    {
        using var file = File.OpenRead(path); using var reader = new BinaryReader(file);
        if (file.Length < 22 || Encoding.ASCII.GetString(reader.ReadBytes(4)) != "MThd") throw new InvalidDataException("MIDI 文件头无效。");
        var headerLength = BinaryPrimitives.ReadUInt32BigEndian(reader.ReadBytes(4));
        if (headerLength < 6 || headerLength > 1024 || headerLength + 8 > file.Length) throw new InvalidDataException("MIDI 文件头不完整。");
        var header = reader.ReadBytes((int)headerLength);
        var format = BinaryPrimitives.ReadUInt16BigEndian(header);
        var tracks = BinaryPrimitives.ReadUInt16BigEndian(header.AsSpan(2));
        var division = BinaryPrimitives.ReadUInt16BigEndian(header.AsSpan(4));
        var fps = -(sbyte)(division >> 8);
        if (format > 2 || tracks == 0 || format == 0 && tracks != 1 || division == 0
            || (division & 0x8000) != 0 && (fps is not (24 or 25 or 29 or 30) || (division & 255) == 0))
            throw new InvalidDataException("MIDI 格式、音轨数量或时间基准无效。");
        var notes = 0; var unclosed = 0; long lastTick = 0;
        var tempos = new List<(long Tick, int Tempo)>();
        var independentDuration = 0d;
        for (var i = 0; i < tracks; i++)
        {
            if (file.Position + 8 > file.Length || Encoding.ASCII.GetString(reader.ReadBytes(4)) != "MTrk") throw new InvalidDataException("MIDI 音轨无效。");
            var length = BinaryPrimitives.ReadUInt32BigEndian(reader.ReadBytes(4));
            if (length < 4 || file.Position + length > file.Length) throw new InvalidDataException("MIDI 音轨数据不完整。");
            var end = file.Position + length;
            byte runningStatus = 0;
            var ended = false; long tick = 0;
            var active = new int[16 * 128];
            var trackTempos = new List<(long Tick, int Tempo)>();
            while (file.Position < end)
            {
                tick += ReadVariableLength(reader, end);
                RequireBytes(reader, end, 1);
                var status = reader.ReadByte();
                if (status < 0x80) { file.Position--; status = runningStatus; }
                if (status == 0xff)
                {
                    RequireBytes(reader, end, 1);
                    var type = reader.ReadByte(); var size = ReadVariableLength(reader, end);
                    RequireBytes(reader, end, size);
                    if (type == 0x2f)
                    {
                        if (size != 0 || file.Position != end) throw new InvalidDataException("MIDI 结束事件无效。");
                        ended = true;
                    }
                    if (type == 0x51)
                    {
                        if (size != 3) throw new InvalidDataException("MIDI 速度事件无效。");
                        var tempo = (reader.ReadByte() << 16) | (reader.ReadByte() << 8) | reader.ReadByte();
                        if (tempo == 0) throw new InvalidDataException("MIDI 速度不能为零。");
                        trackTempos.Add((tick, tempo));
                    }
                    else file.Position += size;
                }
                else if (status is 0xf0 or 0xf7)
                {
                    var size = ReadVariableLength(reader, end); RequireBytes(reader, end, size);
                    file.Position += size; runningStatus = 0;
                }
                else if (status is >= 0x80 and <= 0xef)
                {
                    runningStatus = status;
                    RequireBytes(reader, end, (status & 0xf0) is 0xc0 or 0xd0 ? 1 : 2);
                    var first = reader.ReadByte();
                    var second = (status & 0xf0) is 0xc0 or 0xd0 ? 0 : reader.ReadByte();
                    if (first > 127 || second > 127) throw new InvalidDataException("MIDI 事件数据无效。");
                    var key = (status & 15) * 128 + first;
                    if ((status & 0xf0) == 0x90 && second > 0) { notes++; active[key]++; }
                    else if ((status & 0xf0) == 0x80 || (status & 0xf0) == 0x90 && second == 0)
                        active[key] = Math.Max(0, active[key] - 1);
                }
                else throw new InvalidDataException("MIDI 事件状态无效。");
                if (file.Position > end) throw new InvalidDataException("MIDI 事件越过音轨边界。");
            }
            if (!ended) throw new InvalidDataException("MIDI 音轨缺少结束事件。");
            unclosed += active.Sum(); lastTick = Math.Max(lastTick, tick);
            tempos.AddRange(trackTempos);
            independentDuration = Math.Max(independentDuration, MidiSeconds(tick, division, trackTempos));
        }
        return new("MIDI", DurationSeconds: format == 2 ? independentDuration : MidiSeconds(lastTick, division, tempos), Tracks: tracks, Notes: notes,
            Warnings: unclosed > 0 ? ["MIDI 存在未关闭的音符，请在音乐软件中检查延音与音符时长。"] : []);
    }

    private static double MidiSeconds(long tick, ushort division, List<(long Tick, int Tempo)> tempos)
    {
        if ((division & 0x8000) != 0)
        {
            var fps = -(sbyte)(division >> 8);
            return tick / ((fps == 29 ? 30000d / 1001 : fps) * (division & 255));
        }
        long previous = 0; var tempo = 500000; var seconds = 0d;
        foreach (var change in tempos.OrderBy(x => x.Tick))
        {
            seconds += (change.Tick - previous) * (double)tempo / division / 1000000;
            previous = change.Tick; tempo = change.Tempo;
        }
        return seconds + (tick - previous) * (double)tempo / division / 1000000;
    }

    private static void RequireBytes(BinaryReader reader, long end, long count)
    {
        if (count < 0 || reader.BaseStream.Position + count > end) throw new InvalidDataException("MIDI 事件越过音轨边界。");
    }

    private static int ReadVariableLength(BinaryReader reader, long end)
    {
        var value = 0;
        for (var i = 0; i < 4; i++) { RequireBytes(reader, end, 1); var next = reader.ReadByte(); value = (value << 7) | (next & 127); if ((next & 128) == 0) return value; }
        throw new InvalidDataException("MIDI 事件长度无效。");
    }

    private static ArtifactInfo ReadSubtitles(string path)
    {
        var text = File.ReadAllText(path);
        if (string.IsNullOrWhiteSpace(text))
        {
            var json = Path.ChangeExtension(path, ".json");
            if (!File.Exists(json)) throw new InvalidDataException("空字幕缺少对应的静音识别记录。");
            using var document = JsonDocument.Parse(File.ReadAllText(json));
            if (!document.RootElement.TryGetProperty("segments", out var segments) || segments.ValueKind != JsonValueKind.Array || segments.GetArrayLength() != 0)
                throw new InvalidDataException("空字幕缺少对应的静音识别记录。");
            return new("SRT", Subtitles: 0, Warnings: ["未检测到语音，已保存空字幕和识别记录。"]);
        }
        return ReadSubtitleText(text);
    }

    public static void ValidateSubtitleText(string text) => ReadSubtitleText(text);

    private static ArtifactInfo ReadSubtitleText(string text)
    {
        var blocks = Regex.Split(text.Trim().TrimStart('\uFEFF').Replace("\r\n", "\n"), @"\n[\t ]*\n");
        var ids = new HashSet<int>(); var previousStart = -1d; var duration = 0d;
        foreach (var block in blocks)
        {
            var lines = block.Split('\n');
            if (lines.Length < 3 || !int.TryParse(lines[0].Trim(), out var id) || id <= 0 || !ids.Add(id)
                || string.IsNullOrWhiteSpace(string.Join("\n", lines.Skip(2)))) throw new InvalidDataException("SRT 字幕块缺少有效编号、时间轴或正文。");
            var times = lines[1].Split("-->", StringSplitOptions.TrimEntries);
            if (times.Length != 2 || !TrySubtitleTime(times[0], out var start) || !TrySubtitleTime(times[1], out var end)
                || end <= start || start < previousStart) throw new InvalidDataException("SRT 时间范围或片段顺序无效。");
            previousStart = start; duration = Math.Max(duration, end);
        }
        return new("SRT", DurationSeconds: duration, Subtitles: ids.Count);
    }

    private static bool TrySubtitleTime(string text, out double seconds)
    {
        seconds = 0;
        var match = Regex.Match(text, @"^(\d{2,}):([0-5]\d):([0-5]\d)[,.](\d{3})$", RegexOptions.CultureInvariant);
        if (!match.Success || !int.TryParse(match.Groups[1].Value, out var hours)) return false;
        seconds = hours * 3600d + int.Parse(match.Groups[2].Value) * 60 + int.Parse(match.Groups[3].Value) + int.Parse(match.Groups[4].Value) / 1000d;
        return true;
    }
}
