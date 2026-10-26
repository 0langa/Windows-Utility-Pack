using System.Net;
using System.Text;
using WindowsUtilityPack.Models;

namespace WindowsUtilityPack.Services;

/// <summary>
/// Hosts a local API mock server with configurable endpoints.
/// </summary>
public interface IApiMockServerService
{
    bool IsRunning { get; }

    string BaseUrl { get; }

    IReadOnlyList<ApiMockEndpoint> GetEndpoints();

    void SetEndpoints(IReadOnlyList<ApiMockEndpoint> endpoints);

    IReadOnlyList<ApiMockRequestLogEntry> GetRequestLog(int limit = 200);

    void ClearRequestLog();

    Task StartAsync(int port, CancellationToken cancellationToken = default);

    Task StopAsync();
}

/// <summary>
/// Default local API mock server implementation backed by HttpListener.
/// </summary>
public sealed class ApiMockServerService : IApiMockServerService
{
    private readonly object _sync = new();
    private readonly SemaphoreSlim _lifecycleGate = new(1, 1);
    private readonly List<ApiMockEndpoint> _endpoints =
    [
        new ApiMockEndpoint
        {
            Name = "Health",
            Method = "GET",
            Path = "/health",
            StatusCode = 200,
            ContentType = "application/json",
            ResponseBody = "{\"status\":\"ok\"}",
            Enabled = true,
        },
    ];

    private readonly List<ApiMockRequestLogEntry> _requestLog = [];

    private HttpListener? _listener;
    private CancellationTokenSource? _cts;
    private Task? _loopTask;

    public bool IsRunning
    {
        get
        {
            lock (_sync)
            {
                return _listener?.IsListening == true;
            }
        }
    }

    public string BaseUrl { get; private set; } = string.Empty;

    public IReadOnlyList<ApiMockEndpoint> GetEndpoints()
    {
        lock (_sync)
        {
            return _endpoints.Select(CloneEndpoint).ToList();
        }
    }

    public void SetEndpoints(IReadOnlyList<ApiMockEndpoint> endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        if (endpoints.Count > 256) throw new ArgumentException("At most 256 endpoints are supported.", nameof(endpoints));
        var validated = endpoints.Select(endpoint =>
        {
            if (endpoint is null || endpoint.StatusCode is < 200 or > 599 ||
                string.IsNullOrWhiteSpace(endpoint.Method) || endpoint.Method.Any(c => !char.IsAsciiLetter(c)) ||
                !System.Net.Http.Headers.MediaTypeHeaderValue.TryParse(endpoint.ContentType, out _) ||
                Encoding.UTF8.GetByteCount(endpoint.ResponseBody ?? string.Empty) > BoundedHttpContent.DefaultMaximumBytes)
                throw new ArgumentException("Each endpoint needs a valid method, final status (200–599), content type and body of at most 4 MiB.", nameof(endpoints));
            return CloneEndpoint(endpoint);
        }).ToArray();

        lock (_sync)
        {
            _endpoints.Clear();
            _endpoints.AddRange(validated);
        }
    }

    public IReadOnlyList<ApiMockRequestLogEntry> GetRequestLog(int limit = 200)
    {
        lock (_sync)
        {
            return _requestLog
                .TakeLast(Math.Max(1, limit))
                .Reverse()
                .ToList();
        }
    }

    public void ClearRequestLog()
    {
        lock (_sync)
        {
            _requestLog.Clear();
        }
    }

    public async Task StartAsync(int port, CancellationToken cancellationToken = default)
    {
        if (port is < 1025 or > 65535) throw new InvalidOperationException("Port must be between 1025 and 65535.");
        await _lifecycleGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (IsRunning) return;
            if (_loopTask is not null) await _loopTask.ConfigureAwait(false);
            _cts?.Dispose();
            var listener = new HttpListener();
            var prefix = $"http://localhost:{port}/";
            listener.Prefixes.Add(prefix);
            try { listener.Start(); }
            catch (Exception ex) { listener.Close(); throw new InvalidOperationException($"Unable to start listener on {prefix}.", ex); }
            var lifetime = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            lock (_sync)
            {
                _listener = listener;
                _cts = lifetime;
                BaseUrl = prefix;
                _loopTask = Task.Run(() => AcceptLoopAsync(listener, lifetime.Token));
            }
        }
        finally { _lifecycleGate.Release(); }
    }

    public async Task StopAsync()
    {
        await _lifecycleGate.WaitAsync().ConfigureAwait(false);
        try
        {
            _cts?.Cancel();
            if (_loopTask is not null) await _loopTask.ConfigureAwait(false);
            lock (_sync)
            {
                _listener?.Close();
                _listener = null;
                _loopTask = null;
                _cts?.Dispose();
                _cts = null;
                BaseUrl = string.Empty;
            }
        }
        finally { _lifecycleGate.Release(); }
    }
    internal (int StatusCode, string ContentType, string Body, string EndpointName) ResolveResponse(string method, string path)
    {
        var normalizedMethod = (method ?? string.Empty).Trim().ToUpperInvariant();
        var normalizedPath = NormalizePath(path);

        ApiMockEndpoint? endpoint;
        lock (_sync)
        {
            endpoint = _endpoints.FirstOrDefault(e =>
                e.Enabled &&
                e.Method.Equals(normalizedMethod, StringComparison.OrdinalIgnoreCase) &&
                NormalizePath(e.Path).Equals(normalizedPath, StringComparison.OrdinalIgnoreCase));
        }

        if (endpoint is null)
        {
            return (404, "application/json", "{\"error\":\"Not Found\"}", "(No match)");
        }

        return (endpoint.StatusCode, endpoint.ContentType, endpoint.ResponseBody, endpoint.Name);
    }

    private async Task AcceptLoopAsync(HttpListener listener, CancellationToken cancellationToken)
    {
        var requests = new HashSet<Task>();
        using var registration = cancellationToken.Register(() =>
        {
            try { listener.Stop(); } catch (ObjectDisposedException) { }
        });
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                if (requests.Count >= 16) await Task.WhenAny(requests).ConfigureAwait(false);
                requests.RemoveWhere(task => task.IsCompleted);
                var context = await listener.GetContextAsync().ConfigureAwait(false);
                requests.Add(HandleRequestAsync(context, cancellationToken));
            }
        }
        catch (Exception ex) when (ex is HttpListenerException or ObjectDisposedException or OperationCanceledException) { }
        finally
        {
            listener.Close();
            await Task.WhenAll(requests).ConfigureAwait(false);
        }
    }

    private async Task HandleRequestAsync(HttpListenerContext context, CancellationToken cancellationToken)
    {
        var method = context.Request.HttpMethod;
        var path = context.Request.Url?.AbsolutePath ?? "/";
        var resolved = ResolveResponse(method, path);
        try
        {
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            deadline.CancelAfter(TimeSpan.FromSeconds(10));
            context.Response.StatusCode = resolved.StatusCode;
            context.Response.ContentType = resolved.ContentType;
            var payload = Encoding.UTF8.GetBytes(resolved.Body ?? string.Empty);
            context.Response.ContentLength64 = payload.Length;
            if (!method.Equals("HEAD", StringComparison.OrdinalIgnoreCase))
                await context.Response.OutputStream.WriteAsync(payload, deadline.Token).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is HttpListenerException or System.IO.IOException or ObjectDisposedException or OperationCanceledException) { }
        finally
        {
            try { context.Response.Close(); } catch (ObjectDisposedException) { }
        }
        lock (_sync)
        {
            _requestLog.Add(new ApiMockRequestLogEntry
            {
                TimestampUtc = DateTime.UtcNow, Method = method, Path = path,
                StatusCode = resolved.StatusCode, EndpointName = resolved.EndpointName,
            });
            if (_requestLog.Count > 2_000) _requestLog.RemoveRange(0, _requestLog.Count - 2_000);
        }
    }
    private static string NormalizePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return "/";
        }

        var trimmed = path.Trim();
        if (!trimmed.StartsWith('/'))
        {
            trimmed = "/" + trimmed;
        }

        return trimmed.Replace("//", "/", StringComparison.Ordinal);
    }

    private static ApiMockEndpoint CloneEndpoint(ApiMockEndpoint endpoint)
    {
        return new ApiMockEndpoint
        {
            Name = endpoint.Name,
            Method = endpoint.Method,
            Path = endpoint.Path,
            StatusCode = endpoint.StatusCode,
            ContentType = endpoint.ContentType,
            ResponseBody = endpoint.ResponseBody,
            Enabled = endpoint.Enabled,
        };
    }
}
