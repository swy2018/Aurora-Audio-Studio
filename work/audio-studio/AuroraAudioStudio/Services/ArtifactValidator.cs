using System.Buffers.Binary;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

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
        if (!Directory.Exists(directory)) return [];
        var extension = feature == "transcription" ? ".mid" : feature == "subtitles" ? ".srt" : ".wav";
        var files = Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories)
            .Where(path => Path.GetExtension(path).Equals(extension, StringComparison.OrdinalIgnoreCase)).ToArray();
        if (files.Length == 0) throw new InvalidDataException("引擎没有生成可用的成品文件。请查看任务日志。");
        foreach (var path in files) Validate(path);
        return files;
    }

    public static void Validate(string path)
    {
        if (!File.Exists(path)) throw new FileNotFoundException("成品文件不存在。", path);
        switch (Path.GetExtension(path).ToLowerInvariant())
        {
            case ".wav": ValidateWave(path); break;
            case ".mid": case ".midi": ValidateMidi(path); break;
            case ".srt": ValidateSubtitles(path); break;
            default: throw new InvalidDataException("支持导入 WAV、MIDI 和 SRT 成品。");
        }
    }

    private static void ValidateWave(string path)
    {
        using var file = File.OpenRead(path);
        using var reader = new BinaryReader(file);
        if (file.Length < 44 || Encoding.ASCII.GetString(reader.ReadBytes(4)) != "RIFF") throw new InvalidDataException("WAV 文件头无效。");
        reader.ReadUInt32();
        if (Encoding.ASCII.GetString(reader.ReadBytes(4)) != "WAVE") throw new InvalidDataException("WAV 格式无效。");
        var format = false; var data = false;
        while (file.Position + 8 <= file.Length)
        {
            var id = Encoding.ASCII.GetString(reader.ReadBytes(4));
            var size = reader.ReadUInt32(); var end = file.Position + size;
            if (end > file.Length) throw new InvalidDataException("WAV 音频数据不完整。");
            if (id == "fmt " && size >= 16)
            {
                var encoding = reader.ReadUInt16(); var channels = reader.ReadUInt16(); var rate = reader.ReadUInt32();
                format = encoding is 1 or 3 or 65534 && channels > 0 && rate > 0;
            }
            if (id == "data" && size > 0) data = true;
            file.Position = Math.Min(file.Length, end + (size & 1));
        }
        if (!format || !data) throw new InvalidDataException("WAV 缺少有效格式或音频数据。");
    }

    private static void ValidateMidi(string path)
    {
        if (MidiNoteCount(path) == 0) throw new InvalidDataException("未识别出 MIDI 音符，请使用钢琴演奏或选择适合素材的扒谱模型。");
    }

    public static int MidiNoteCount(string path)
    {
        using var file = File.OpenRead(path); using var reader = new BinaryReader(file);
        if (file.Length < 22 || Encoding.ASCII.GetString(reader.ReadBytes(4)) != "MThd") throw new InvalidDataException("MIDI 文件头无效。");
        var headerLength = BinaryPrimitives.ReadUInt32BigEndian(reader.ReadBytes(4));
        if (headerLength < 6 || headerLength > 1024 || headerLength + 8 > file.Length) throw new InvalidDataException("MIDI 文件头不完整。");
        var header = reader.ReadBytes((int)headerLength);
        var tracks = BinaryPrimitives.ReadUInt16BigEndian(header.AsSpan(2));
        var notes = 0;
        if (tracks == 0) throw new InvalidDataException("MIDI 缺少音轨。");
        for (var i = 0; i < tracks; i++)
        {
            if (file.Position + 8 > file.Length || Encoding.ASCII.GetString(reader.ReadBytes(4)) != "MTrk") throw new InvalidDataException("MIDI 音轨无效。");
            var length = BinaryPrimitives.ReadUInt32BigEndian(reader.ReadBytes(4));
            if (length < 4 || file.Position + length > file.Length) throw new InvalidDataException("MIDI 音轨数据不完整。");
            var end = file.Position + length;
            byte runningStatus = 0;
            while (file.Position < end)
            {
                ReadVariableLength(reader);
                var status = reader.ReadByte();
                if (status < 0x80) { file.Position--; status = runningStatus; }
                if (status == 0xff) { reader.ReadByte(); var size = ReadVariableLength(reader); file.Position += size; }
                else if (status is 0xf0 or 0xf7) { var size = ReadVariableLength(reader); file.Position += size; runningStatus = 0; }
                else if (status is >= 0x80 and <= 0xef)
                {
                    runningStatus = status;
                    var first = reader.ReadByte();
                    var second = (status & 0xf0) is 0xc0 or 0xd0 ? 0 : reader.ReadByte();
                    if (first > 127 || second > 127) throw new InvalidDataException("MIDI 事件数据无效。");
                    if ((status & 0xf0) == 0x90 && second > 0) notes++;
                }
                else throw new InvalidDataException("MIDI 事件状态无效。");
                if (file.Position > end) throw new InvalidDataException("MIDI 事件越过音轨边界。");
            }
        }
        return notes;
    }

    private static int ReadVariableLength(BinaryReader reader)
    {
        var value = 0;
        for (var i = 0; i < 4; i++) { var next = reader.ReadByte(); value = (value << 7) | (next & 127); if ((next & 128) == 0) return value; }
        throw new InvalidDataException("MIDI 事件长度无效。");
    }

    private static void ValidateSubtitles(string path)
    {
        var text = File.ReadAllText(path);
        if (string.IsNullOrWhiteSpace(text))
        {
            var json = Path.ChangeExtension(path, ".json");
            using var document = JsonDocument.Parse(File.ReadAllText(json));
            if (!document.RootElement.TryGetProperty("segments", out var segments) || segments.GetArrayLength() != 0)
                throw new InvalidDataException("空字幕缺少对应的静音识别记录。");
            return;
        }
        ValidateSubtitleText(text);
    }

    public static void ValidateSubtitleText(string text)
    {
        var times = Regex.Matches(text, @"(?m)^(\d{2,}:\d{2}:\d{2}[,.]\d{3})\s*-->\s*(\d{2,}:\d{2}:\d{2}[,.]\d{3})");
        if (times.Count == 0) throw new InvalidDataException("SRT 缺少有效时间轴。");
        foreach (Match match in times)
        {
            if (!TimeSpan.TryParse(match.Groups[1].Value.Replace(',', '.'), out var start)
                || !TimeSpan.TryParse(match.Groups[2].Value.Replace(',', '.'), out var end) || end < start)
                throw new InvalidDataException("SRT 时间范围无效。");
        }
    }
}
