namespace AuroraAudioStudio.Services;

public sealed class SingleInstanceGuard : IDisposable
{
    public const string ApplicationKey = @"Local\AuroraAudioStudio.SingleInstance";
    private const string ActivationKey = @"Local\AuroraAudioStudio.Activate";

    private readonly Mutex mutex;
    private readonly EventWaitHandle activationEvent;
    private readonly CancellationTokenSource listenerCancellation = new();
    private readonly bool ownsMutex;

    public bool IsPrimary => ownsMutex;

    public SingleInstanceGuard(string name = ApplicationKey)
    {
        mutex = new Mutex(true, name, out ownsMutex);
        activationEvent = new EventWaitHandle(false, EventResetMode.AutoReset, ActivationKey);
    }

    public void SignalPrimary() => activationEvent.Set();

    public void Listen(Action activation)
    {
        if (!ownsMutex) return;
        _ = Task.Run(() =>
        {
            var handles = new WaitHandle[] { activationEvent, listenerCancellation.Token.WaitHandle };
            while (WaitHandle.WaitAny(handles) == 0) activation();
        });
    }

    public void Dispose()
    {
        listenerCancellation.Cancel();
        listenerCancellation.Dispose();
        activationEvent.Dispose();
        if (ownsMutex)
        {
            try { mutex.ReleaseMutex(); } catch (ApplicationException) { }
        }
        mutex.Dispose();
    }
}
