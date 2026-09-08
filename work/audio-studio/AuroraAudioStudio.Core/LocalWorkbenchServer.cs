using System.Net;
using System.Net.Sockets;
using System.Text;

namespace AuroraAudioStudio.Core;

public sealed class LocalWorkbenchServer : IDisposable
{
    private readonly HttpListener listener = new();
    private readonly CancellationTokenSource lifetime = new();
    private sealed record Resource(byte[]? Bytes, string? Path, string Mime);
    private readonly Dictionary<string, Resource> resources = [];
    private readonly string token = Guid.NewGuid().ToString("N");
    public Uri BaseUri { get; }
    public Task Completion { get; }

    public LocalWorkbenchServer()
    {
        for (var attempt = 0; ; attempt++)
        {
            var probe = new TcpListener(IPAddress.Loopback, 0);
            probe.Start();
            var port = ((IPEndPoint)probe.LocalEndpoint).Port;
            probe.Stop();
            var prefix = $"http://127.0.0.1:{port}/";
            listener.Prefixes.Clear();
            listener.Prefixes.Add(prefix);
            try { listener.Start(); BaseUri = new Uri(prefix + token + "/"); break; }
            catch (HttpListenerException) when (attempt < 5) { }
        }
        Completion = ServeAsync();
    }

    public Uri AddPage(string html) => AddResource(Encoding.UTF8.GetBytes(html), "text/html; charset=utf-8");
    public Uri AddResource(byte[] bytes, string mime)
    {
        var key = Guid.NewGuid().ToString("N");
        lock (resources) resources[key] = new(bytes, null, mime);
        return new Uri(BaseUri, key);
    }

    public Uri AddFile(string path, string mime)
    {
        path = Path.GetFullPath(path);
        if (!File.Exists(path)) throw new FileNotFoundException("Audio file is unavailable.", path);
        var key = Guid.NewGuid().ToString("N");
        lock (resources) resources[key] = new(null, path, mime);
        return new Uri(BaseUri, key);
    }

    public void Remove(Uri? uri)
    {
        if (!Owns(uri)) return;
        lock (resources) resources.Remove(uri!.AbsolutePath[BaseUri.AbsolutePath.Length..]);
    }

    public bool Owns(Uri? uri) => uri is not null && uri.Scheme == BaseUri.Scheme && uri.Host == BaseUri.Host && uri.Port == BaseUri.Port
        && uri.AbsolutePath.StartsWith(BaseUri.AbsolutePath, StringComparison.Ordinal);

    private async Task ServeAsync()
    {
        while (!lifetime.IsCancellationRequested)
        {
            HttpListenerContext context;
            try { context = await listener.GetContextAsync().WaitAsync(lifetime.Token); }
            catch (Exception ex) when (ex is OperationCanceledException or HttpListenerException or ObjectDisposedException) { break; }
            try
            {
                var request = context.Request;
                var response = context.Response;
                response.Headers["Cache-Control"] = "no-store";
                response.Headers["X-Content-Type-Options"] = "nosniff";
                response.Headers["Content-Security-Policy"] = "default-src 'none'; script-src 'unsafe-inline'; style-src 'unsafe-inline'; font-src 'self'; media-src 'self' blob:; img-src 'self' data:; connect-src 'none'; frame-src 'none'; form-action 'none'; base-uri 'none'";
                if (request.Url is null || !Owns(request.Url) || request.HttpMethod is not ("GET" or "HEAD")) { response.StatusCode = 404; continue; }
                var key = request.Url.AbsolutePath[BaseUri.AbsolutePath.Length..];
                Resource? resource;
                lock (resources)
                {
                    if (!resources.TryGetValue(key, out resource)) { response.StatusCode = 404; continue; }
                }
                response.ContentType = resource.Mime;
                if (resource.Mime == "font/ttf") response.Headers["Access-Control-Allow-Origin"] = "*";
                await using Stream source = resource.Bytes is { } bytes ? new MemoryStream(bytes, false)
                    : new FileStream(resource.Path!, FileMode.Open, FileAccess.Read, FileShare.Read, 65536, FileOptions.Asynchronous);
                var length = source.Length;
                long start = 0, end = length - 1;
                response.Headers["Accept-Ranges"] = "bytes";
                if (request.Headers["Range"] is { } range)
                {
                    if (!TryRange(range, length, out start, out end))
                    { response.StatusCode = 416; response.Headers["Content-Range"] = $"bytes */{length}"; continue; }
                    response.StatusCode = 206;
                    response.Headers["Content-Range"] = $"bytes {start}-{end}/{length}";
                }
                response.ContentLength64 = Math.Max(0, end - start + 1);
                if (request.HttpMethod == "GET")
                {
                    source.Position = start;
                    var remaining = response.ContentLength64;
                    var buffer = new byte[65536];
                    while (remaining > 0)
                    {
                        var read = await source.ReadAsync(buffer.AsMemory(0, (int)Math.Min(buffer.Length, remaining)), lifetime.Token);
                        if (read == 0) break;
                        await response.OutputStream.WriteAsync(buffer.AsMemory(0, read), lifetime.Token);
                        remaining -= read;
                    }
                }
            }
            catch (Exception ex) when (ex is IOException or HttpListenerException or OperationCanceledException) { }
            finally { context.Response.Close(); }
        }
    }

    private static bool TryRange(string range, long length, out long start, out long end)
    {
        start = 0; end = length - 1;
        if (length == 0 || !range.StartsWith("bytes=", StringComparison.Ordinal)) return false;
        var parts = range[6..].Split('-');
        if (parts.Length != 2) return false;
        if (parts[0].Length == 0)
        {
            if (!long.TryParse(parts[1], out var suffix) || suffix <= 0) return false;
            start = Math.Max(0, length - suffix);
        }
        else
        {
            if (!long.TryParse(parts[0], out start) || start < 0 || start >= length) return false;
            if (parts[1].Length > 0 && (!long.TryParse(parts[1], out end) || end < start)) return false;
            end = Math.Min(end, length - 1);
        }
        return true;
    }

    public void Dispose()
    {
        lifetime.Cancel();
        listener.Close();
    }
}
