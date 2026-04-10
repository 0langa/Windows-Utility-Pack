using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Net.Http;
using System.Text;
using Newtonsoft.Json;
using WindowsUtilityPack.Commands;
using WindowsUtilityPack.Services;
using WindowsUtilityPack.ViewModels;

namespace WindowsUtilityPack.Tools.NetworkInternet.HttpRequestTester;

/// <summary>
/// ViewModel for the HTTP Request Tester tool.
/// Sends HTTP requests and displays response status, headers, body, and timing.
/// </summary>
public class HttpRequestTesterViewModel : ViewModelBase
{
    private static readonly HttpClient _httpClient = new();

    private readonly IClipboardService _clipboard;

    private string _url                = "https://httpbin.org/get";
    private string _method             = "GET";
    private string _requestBody        = string.Empty;
    private string _requestHeaders     = string.Empty;
    private string _responseBody       = string.Empty;
    private string _responseHeaders    = string.Empty;
    private int    _responseStatusCode;
    private string _responseStatus     = string.Empty;
    private long   _responseTimeMs;
    private bool   _isSending;
    private bool   _showRequestBody;

    public string Url
    {
        get => _url;
        set => SetProperty(ref _url, value);
    }

    public string Method
    {
        get => _method;
        set
        {
            if (SetProperty(ref _method, value))
                ShowRequestBody = value is "POST" or "PUT" or "PATCH";
        }
    }

    public ObservableCollection<string> Methods { get; } =
        ["GET", "POST", "PUT", "PATCH", "DELETE", "HEAD", "OPTIONS"];

    public string RequestBody
    {
        get => _requestBody;
        set => SetProperty(ref _requestBody, value);
    }

    public string RequestHeaders
    {
        get => _requestHeaders;
        set => SetProperty(ref _requestHeaders, value);
    }

    public string ResponseBody
    {
        get => _responseBody;
        set => SetProperty(ref _responseBody, value);
    }

    public string ResponseHeaders
    {
        get => _responseHeaders;
        set => SetProperty(ref _responseHeaders, value);
    }

    public int ResponseStatusCode
    {
        get => _responseStatusCode;
        set => SetProperty(ref _responseStatusCode, value);
    }

    public string ResponseStatus
    {
        get => _responseStatus;
        set => SetProperty(ref _responseStatus, value);
    }

    public long ResponseTimeMs
    {
        get => _responseTimeMs;
        set => SetProperty(ref _responseTimeMs, value);
    }

    public bool IsSending
    {
        get => _isSending;
        set => SetProperty(ref _isSending, value);
    }

    public bool ShowRequestBody
    {
        get => _showRequestBody;
        set => SetProperty(ref _showRequestBody, value);
    }

    public AsyncRelayCommand SendCommand         { get; }
    public RelayCommand      CopyResponseCommand { get; }
    public RelayCommand      ClearCommand        { get; }

    public HttpRequestTesterViewModel(IClipboardService clipboard)
    {
        _clipboard = clipboard;

        SendCommand         = new AsyncRelayCommand(_ => SendRequestAsync(), _ => !IsSending);
        CopyResponseCommand = new RelayCommand(_ => CopyResponse(),         _ => !string.IsNullOrEmpty(ResponseBody));
        ClearCommand        = new RelayCommand(_ => ClearAll());
    }

    private async Task SendRequestAsync()
    {
        if (string.IsNullOrWhiteSpace(Url)) return;

        IsSending     = true;
        ResponseBody    = string.Empty;
        ResponseHeaders = string.Empty;
        ResponseStatus  = string.Empty;
        ResponseStatusCode = 0;

        try
        {
            using var request = new HttpRequestMessage(new HttpMethod(Method), Url);

            // Parse and add custom request headers
            foreach (var line in RequestHeaders.Split('\n', StringSplitOptions.RemoveEmptyEntries))
            {
                var colon = line.IndexOf(':');
                if (colon > 0)
                {
                    var key = line[..colon].Trim();
                    var val = line[(colon + 1)..].Trim();
                    request.Headers.TryAddWithoutValidation(key, val);
                }
            }

            // Add body for appropriate methods
            if (ShowRequestBody && !string.IsNullOrWhiteSpace(RequestBody))
            {
                var isJson      = RequestBody.TrimStart().StartsWith('{') || RequestBody.TrimStart().StartsWith('[');
                var contentType = isJson ? "application/json" : "text/plain";
                request.Content = new StringContent(RequestBody, Encoding.UTF8, contentType);
            }

            var sw = Stopwatch.StartNew();
            using var response = await _httpClient.SendAsync(request);
            sw.Stop();

            ResponseTimeMs     = sw.ElapsedMilliseconds;
            ResponseStatusCode = (int)response.StatusCode;
            ResponseStatus     = $"{(int)response.StatusCode} {response.ReasonPhrase}";

            // Collect response headers
            var headerSb = new StringBuilder();
            foreach (var h in response.Headers)
                headerSb.AppendLine($"{h.Key}: {string.Join(", ", h.Value)}");
            foreach (var h in response.Content.Headers)
                headerSb.AppendLine($"{h.Key}: {string.Join(", ", h.Value)}");
            ResponseHeaders = headerSb.ToString().TrimEnd();

            // Read body
            var body = await response.Content.ReadAsStringAsync();

            // Pretty-print JSON
            var responseContentType = response.Content.Headers.ContentType?.MediaType ?? string.Empty;
            if (responseContentType.Contains("application/json") || responseContentType.Contains("text/json"))
            {
                try
                {
                    var obj       = JsonConvert.DeserializeObject(body);
                    body = JsonConvert.SerializeObject(obj, Formatting.Indented);
                }
                catch { /* leave as-is */ }
            }

            ResponseBody = body;
        }
        catch (Exception ex)
        {
            ResponseStatus = "Error";
            ResponseBody   = ex.Message;
        }
        finally
        {
            IsSending = false;
        }
    }

    private void CopyResponse()
    {
        _clipboard.SetText(ResponseBody);
    }

    private void ClearAll()
    {
        ResponseBody       = string.Empty;
        ResponseHeaders    = string.Empty;
        ResponseStatus     = string.Empty;
        ResponseStatusCode = 0;
        ResponseTimeMs     = 0;
    }
}
