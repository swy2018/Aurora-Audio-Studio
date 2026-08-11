namespace AuroraAudioStudio.Services;

public sealed class SingleInstanceGuard : IDisposable
{
    public const string ApplicationKey = @"Local\AuroraAudioStudio.SingleInstance";

    private readonly Mutex mutex;
    private readonly bool ownsMutex;

    public bool IsPrimary => ownsMutex;

    public SingleInstanceGuard(string name = ApplicationKey)
    {
        mutex = new Mutex(true, name, out ownsMutex);
    }

    public void Dispose()
    {
        if (ownsMutex)
        {
            try { mutex.ReleaseMutex(); } catch (ApplicationException) { }
        }
        mutex.Dispose();
    }
}
