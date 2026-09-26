using AuroraAudioStudio.Models;

namespace AuroraAudioStudio.Services;

public static class ArtifactPresentation
{
    public static string Duration(double? seconds) => seconds is null ? "—" : TimeSpan.FromSeconds(seconds.Value).ToString(seconds >= 3600 ? @"h\:mm\:ss" : @"m\:ss");
    public static string Summary(ArtifactInfo? info, LocalizationService text)
    {
        if (info is null) return text.Get("legacyOutputInfo");
        var summary = info.Format switch
        {
            "WAV" => text.Format("audioSummary", Duration(info.DurationSeconds), info.SampleRate, info.Channels),
            "MIDI" => text.Format("midiSummary", info.Tracks, info.Notes, Duration(info.DurationSeconds)),
            "SRT" => text.Format("subtitleSummary", info.Subtitles, Duration(info.DurationSeconds)),
            _ => info.Format
        };
        return info.Warnings?.Count > 0 ? summary + " · " + text.Get("outputNeedsReview") : summary;
    }
}
