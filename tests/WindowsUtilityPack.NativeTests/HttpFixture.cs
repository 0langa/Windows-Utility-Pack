using System.Collections.Concurrent;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

namespace WindowsUtilityPack.NativeTests;

internal sealed class HttpFixture : IDisposable
{
    private readonly HttpListener listener = new();
    private readonly CancellationTokenSource stop = new();
    private readonly Task loop;
    internal int Port { get; }
    internal string Url => $"http://127.0.0.1:{Port}";
    internal ConcurrentQueue<string> Requests { get; } = new();
    internal HttpFixture()
    {
        var reservation = new TcpListener(IPAddress.Loopback, 0); reservation.Start();
        Port = ((IPEndPoint)reservation.LocalEndpoint).Port; reservation.Stop();
        listener.Prefixes.Add(Url + "/"); listener.Start();
        loop = Task.Run(async () => {
            while (!stop.IsCancellationRequested)
            {
                HttpListenerContext context;
                try { context = await listener.GetContextAsync(); }
                catch (Exception) when (stop.IsCancellationRequested) { break; }
                _ = Serve(context);
            }
        });
    }
    private async Task Serve(HttpListenerContext context)
    {
        try
        {
            var request = context.Request; var response = context.Response;
            var path = request.Url!.AbsolutePath;
            Requests.Enqueue(request.HttpMethod + " " + path);
            using var body = new MemoryStream(); await request.InputStream.CopyToAsync(body, stop.Token);
            if (path == "/delay") await Task.Delay(5000, stop.Token);
            response.Headers.Add("X-Native-Fixture", "observed");
            response.Headers.Add("Set-Cookie", "session=fixture; HttpOnly; SameSite=Lax");
            response.ContentType = "application/json; charset=utf-8";
            byte[] bytes;
            switch (path)
            {
                case "/bytes": bytes = new byte[2_000_000]; response.ContentType = "application/octet-stream"; break;
                case "/download.bin": bytes = "WUP native download fixture\n"u8.ToArray(); response.ContentType = "application/octet-stream"; break;
                case "/": case "/child":
                    bytes = Encoding.UTF8.GetBytes("<html><title>Native fixture</title><a href='/child'>child</a><a href='/Case'>case</a><a href='/case'>other case</a></html>"); response.ContentType = "text/html"; break;
                case "/sitemap.xml": bytes = Encoding.UTF8.GetBytes($"<urlset xmlns='http://www.sitemaps.org/schemas/sitemap/0.9'><url><loc>{Url}/child</loc></url></urlset>"); response.ContentType = "application/xml"; break;
                default:
                    bytes = JsonSerializer.SerializeToUtf8Bytes(new { Method = request.HttpMethod, Path = path, Body = body.Length < 65536 ? Encoding.UTF8.GetString(body.ToArray()) : "large-upload", Bytes = body.Length, Header = request.Headers["X-Test"] }); break;
            }
            response.ContentLength64 = bytes.Length;
            if (request.HttpMethod != "HEAD") await response.OutputStream.WriteAsync(bytes, stop.Token);
            response.Close();
        }
        catch (Exception) when (stop.IsCancellationRequested) { }
        catch (HttpListenerException) { }
        catch (IOException) { }
    }
    public void Dispose() { stop.Cancel(); listener.Close(); loop.Wait(TimeSpan.FromSeconds(2)); stop.Dispose(); }
}
