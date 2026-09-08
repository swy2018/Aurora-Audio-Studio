namespace AuroraAudioStudio.Core;

/// <summary>Mac model adapters supply their own local UI and own its process lifetime.</summary>
public interface IModelWorkbench
{
    string ModelId { get; }
    bool IsReady => true;
    Task<ModelWorkbenchConnection> StartAsync(string language, CancellationToken cancellationToken);
}

public sealed class ModelWorkbenchConnection : IDisposable
{
    public Uri Uri { get; }
    private Action? release;

    public ModelWorkbenchConnection(Uri uri, Action release)
    {
        if (!IsLocal(uri)) throw new ArgumentException("A model workbench must use a local HTTP endpoint.", nameof(uri));
        Uri = uri;
        this.release = release;
    }

    public static bool IsLocal(Uri uri) => uri.IsAbsoluteUri && uri.Scheme == "http" && uri.UserInfo.Length == 0
        && (uri.Host == "127.0.0.1" || uri.Host == "[::1]" || uri.Host == "::1");
    public bool Owns(Uri? uri) => uri is not null && IsLocal(uri) && uri.Scheme == Uri.Scheme && uri.Host == Uri.Host && uri.Port == Uri.Port;
    public void Dispose() => Interlocked.Exchange(ref release, null)?.Invoke();
}

public sealed class ModelWorkbenchRegistry
{
    private readonly Dictionary<string, IModelWorkbench> adapters = new(StringComparer.Ordinal);
    public void Register(IModelWorkbench adapter) => adapters.Add(adapter.ModelId, adapter);
    public bool IsAvailable(string modelId) => adapters.TryGetValue(modelId, out var adapter) && adapter.IsReady;
    public Task<ModelWorkbenchConnection> StartAsync(string modelId, string language, CancellationToken token)
        => adapters.TryGetValue(modelId, out var adapter) ? adapter.StartAsync(language, token)
            : throw new InvalidOperationException("The macOS workbench for this model has not been installed and connected.");
}

public static class UtilityPresets
{
    // These mappings match MainPage.ApplyUtilityPreset in the Windows application.
    public static string Model(string feature, string trackMode, string preset) => (feature, trackMode, preset) switch
    {
        ("separation", "two-stem", _) => "roformer-vocals",
        ("separation", "multi-stem", "fast") => "demucs",
        ("separation", "multi-stem", _) => "roformer",
        ("transcription", _, "fast") => "basic-pitch",
        ("transcription", _, _) => "transkun",
        ("subtitles", _, "fast") => "whisper-small",
        ("subtitles", _, "quality") => "whisper-large-v3",
        ("subtitles", _, _) => "whisper-large-v3-turbo",
        _ => ""
    };
}
