namespace AuroraAudioStudio.Services;

// Discard callbacks queued before completion when they eventually reach the UI.
public sealed class OperationProgress<T>(Action<T> handler, Action<Action> dispatch,
    CancellationToken cancellationToken = default) : IProgress<T>, IDisposable
{
    private int active = 1;
    public void Report(T value)
    {
        if (Volatile.Read(ref active) == 0 || cancellationToken.IsCancellationRequested) return;
        dispatch(() =>
        {
            if (Volatile.Read(ref active) != 0 && !cancellationToken.IsCancellationRequested) handler(value);
        });
    }
    public void Dispose() => Interlocked.Exchange(ref active, 0);
}
