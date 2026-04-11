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

        lock (_sync)
        {
            _endpoints.Clear();
            _endpoints.AddRange(endpoints.Select(CloneEndpoint));
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

    public Task StartAsync(int port, CancellationToken cancellationToken = default)
    {
        if (port is < 1025 or > 65535)
        {
            throw new InvalidOperationException("Port must be between 1025 and 65535.");
        }

        lock (_sync)
        {
            if (_listener?.IsListening == true)
            {
                return Task.CompletedTask;
            }

            var prefix = $"http://localhost:{port}/";
            _listener = new HttpListener();
            _listener.Prefixes.Add(prefix);

            try
            {
                _listener.Start();
            }
            catch (HttpListenerException ex)
            {
                _listener = null;
                throw new InvalidOperationException($"Unable to start listener on {prefix}.", ex);
            }

            _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            BaseUrl = prefix;
            _loopTask = Task.Run(() => AcceptLoopAsync(_listener, _cts.Token), _cts.Token);
        }

        return Task.CompletedTask;
    }

    public async Task StopAsync()
    {
        Task? loopTask;
        HttpListener? listener;
        CancellationTokenSource? cts;

        lock (_sync)
        {
            loopTask = _loopTask;
            listener = _listener;
            cts = _cts;
            _loopTask = null;
            _listener = null;
            _cts = null;
            BaseUrl = string.Empty;
        }

        cts?.Cancel();
        try
        {
            listener?.Stop();
        }
        catch
        {
            // Ignore listener shutdown errors.
        }

        if (loopTask is not null)
        {
            try
            {
                await loopTask.ConfigureAwait(false);
            }
            catch
            {
                // Ignore loop cancellation errors.
            }
        }

        listener?.Close();
        cts?.Dispose();
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
        while (!cancellationToken.IsCancellationRequested)
        {
            HttpListenerContext context;
            try
            {
                context = await listener.GetContextAsync().ConfigureAwait(false);
            }
            catch (Exception)
            {
                if (!listener.IsListening || cancellationToken.IsCancellationRequested)
                {
                    break;
                }

                continue;
            }

            _ = Task.Run(() => HandleRequestAsync(context, cancellationToken), cancellationToken);
        }
    }

    private async Task HandleRequestAsync(HttpListenerContext context, CancellationToken cancellationToken)
    {
        var method = context.Request.HttpMethod;
        var path = context.Request.Url?.AbsolutePath ?? "/";
        var resolved = ResolveResponse(method, path);

        context.Response.StatusCode = resolved.StatusCode;
        context.Response.ContentType = string.IsNullOrWhiteSpace(resolved.ContentType)
            ? "application/json"
            : resolved.ContentType;

        var payload = Encoding.UTF8.GetBytes(resolved.Body ?? string.Empty);
        context.Response.ContentLength64 = payload.Length;

        try
        {
            await context.Response.OutputStream.WriteAsync(payload.AsMemory(0, payload.Length), cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            // Ignore write failures from disconnected clients.
        }
        finally
        {
            context.Response.OutputStream.Close();
            context.Response.Close();
        }

        lock (_sync)
        {
            _requestLog.Add(new ApiMockRequestLogEntry
            {
                TimestampUtc = DateTime.UtcNow,
                Method = method,
                Path = path,
                StatusCode = resolved.StatusCode,
                EndpointName = resolved.EndpointName,
            });

            if (_requestLog.Count > 2_000)
            {
                _requestLog.RemoveRange(0, _requestLog.Count - 2_000);
            }
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