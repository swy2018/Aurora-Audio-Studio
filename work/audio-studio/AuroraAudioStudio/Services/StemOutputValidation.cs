namespace AuroraAudioStudio.Services;

public static class StemOutputValidation
{
    public static void Validate(string modelId, IReadOnlyList<string> paths, double? sourceDuration = null)
    {
        string[] expected = modelId switch
        {
            "roformer-vocals" => ["vocals", "instrumental"],
            "roformer" => ["vocals", "drums", "bass", "guitar", "piano", "other"],
            "demucs" => ["vocals", "drums", "bass", "other"],
            _ => throw new InvalidDataException("Unknown separation output contract: " + modelId)
        };
        var durations = new List<double>();
        foreach (var stem in expected)
        {
            var matches = paths.Where(path =>
            {
                var name = Path.GetFileNameWithoutExtension(path);
                return name.Equals(stem, StringComparison.OrdinalIgnoreCase) || name.EndsWith("_" + stem, StringComparison.OrdinalIgnoreCase);
            }).ToArray();
            if (matches.Length != 1) throw new InvalidDataException("分轨结果不完整或重复：" + stem);
            var info = ArtifactValidator.Inspect(matches[0]);
            if (info.Format != "WAV" || info.DurationSeconds is not > 0) throw new InvalidDataException("分轨音频无效：" + stem);
            durations.Add(info.DurationSeconds.Value);
        }
        if (durations.Max() - durations.Min() > .1)
            throw new InvalidDataException("分轨时长不一致，请查看任务日志后重试。");
        if (sourceDuration is > 0 && durations.Any(duration => Math.Abs(duration - sourceDuration.Value) > Math.Max(.25, sourceDuration.Value * .001)))
            throw new InvalidDataException("分轨时长与素材不一致，未将不完整结果标记为成功。");
    }
}
