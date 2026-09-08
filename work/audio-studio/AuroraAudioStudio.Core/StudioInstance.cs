using System.IO.Pipes;
using System.Security.Cryptography;
using System.Text;

namespace AuroraAudioStudio.Core;

public sealed class StudioInstance : IDisposable
{
    private readonly FileStream? file;
    private readonly string pipeName;
    private readonly CancellationTokenSource lifetime = new();
    public bool IsPrimary => file is not null;
    public event Action? ActivationRequested;
    public Task Completion { get; }

    public StudioInstance(string root)
    {
        Directory.CreateDirectory(root);
        pipeName = "aurora-" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Path.GetFullPath(root))))[..24];
        try { file = new FileStream(Path.Combine(root, "mac-instance.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None); }
        catch (IOException) { }
        Completion = IsPrimary ? ListenAsync() : Task.CompletedTask;
    }

    public async Task NotifyPrimaryAsync()
    {
        using var pipe = new NamedPipeClientStream(".", pipeName, PipeDirection.Out, PipeOptions.Asynchronous);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        try { await pipe.ConnectAsync(timeout.Token); await pipe.WriteAsync(new byte[] { 1 }, timeout.Token); }
        catch (Exception ex) when (ex is IOException or OperationCanceledException or TimeoutException) { }
    }

    private async Task ListenAsync()
    {
        while (!lifetime.IsCancellationRequested)
        {
            try
            {
                using var pipe = new NamedPipeServerStream(pipeName, PipeDirection.In, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
                await pipe.WaitForConnectionAsync(lifetime.Token);
                var marker = new byte[1];
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);
                timeout.CancelAfter(TimeSpan.FromSeconds(2));
                if (await pipe.ReadAsync(marker, timeout.Token) == 1 && marker[0] == 1) ActivationRequested?.Invoke();
            }
            catch (Exception ex) when (ex is IOException or OperationCanceledException) { }
        }
    }

    public void Dispose() { lifetime.Cancel(); file?.Dispose(); }
}
