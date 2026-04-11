using System.Net;
using System.Net.Http;
using System.Text;
using WindowsUtilityPack.Services;
using WindowsUtilityPack.Tools.NetworkInternet.HttpRequestTester;
using Xunit;

namespace WindowsUtilityPack.Tests.ViewModels;

public class HttpRequestTesterViewModelTests
{
    [Fact]
    public void Method_Setter_TogglesRequestBodyVisibility()
    {
        var vm = new HttpRequestTesterViewModel(new TestClipboardService(), new HttpClient(new StubHttpMessageHandler()));

        vm.Method = "GET";
        Assert.False(vm.ShowRequestBody);

        vm.Method = "POST";
        Assert.True(vm.ShowRequestBody);
    }

    [Fact]
    public async Task SendCommand_ParsesHeadersAndPrettyPrintsJson()
    {
        var handler = new StubHttpMessageHandler
        {
            Responder = _ => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"name\":\"juliu\"}", Encoding.UTF8, "application/json")
            }
        };
        var vm = new HttpRequestTesterViewModel(new TestClipboardService(), new HttpClient(handler));
        vm.Url = "https://example.test/api";
        vm.Method = "POST";
        vm.RequestHeaders = "X-Trace: abc123";
        vm.RequestBody = "{\"name\":\"juliu\"}";

        vm.SendCommand.Execute(null);
        await WaitUntilAsync(() => !vm.IsSending && vm.ResponseStatusCode != 0);

        Assert.Equal(HttpMethod.Post.Method, handler.LastMethod);
        Assert.Equal("https://example.test/api", handler.LastUri);
        Assert.Equal("abc123", handler.LastHeaders["X-Trace"]);
        Assert.Equal("application/json", handler.LastContentType);
        Assert.Contains("\"name\": \"juliu\"", vm.ResponseBody);
        Assert.Contains("application/json", vm.ResponseHeaders, StringComparison.OrdinalIgnoreCase);
        Assert.StartsWith("200", vm.ResponseStatus);
    }

    [Fact]
    public void CopyResponseCommand_CopiesBodyToClipboard()
    {
        var clipboard = new TestClipboardService();
        var vm = new HttpRequestTesterViewModel(clipboard, new HttpClient(new StubHttpMessageHandler()))
        {
            ResponseBody = "copied-response"
        };

        vm.CopyResponseCommand.Execute(null);

        Assert.Equal("copied-response", clipboard.LastText);
    }

    [Fact]
    public void ClearCommand_ResetsResponseState()
    {
        var vm = new HttpRequestTesterViewModel(new TestClipboardService(), new HttpClient(new StubHttpMessageHandler()))
        {
            ResponseBody = "body",
            ResponseHeaders = "headers",
            ResponseStatus = "200 OK",
            ResponseStatusCode = 200,
            ResponseTimeMs = 123
        };

        vm.ClearCommand.Execute(null);

        Assert.Equal(string.Empty, vm.ResponseBody);
        Assert.Equal(string.Empty, vm.ResponseHeaders);
        Assert.Equal(string.Empty, vm.ResponseStatus);
        Assert.Equal(0, vm.ResponseStatusCode);
        Assert.Equal(0, vm.ResponseTimeMs);
    }

    private static async Task WaitUntilAsync(Func<bool> predicate, int timeoutMs = 4000)
    {
        var start = DateTime.UtcNow;
        while (!predicate())
        {
            if ((DateTime.UtcNow - start).TotalMilliseconds > timeoutMs)
            {
                throw new TimeoutException("Condition was not met before timeout.");
            }

            await Task.Delay(15);
        }
    }

    private sealed class StubHttpMessageHandler : HttpMessageHandler
    {
        public Func<HttpRequestMessage, HttpResponseMessage>? Responder { get; init; }

        public string LastMethod { get; private set; } = string.Empty;
        public string LastUri { get; private set; } = string.Empty;
        public Dictionary<string, string> LastHeaders { get; } = new(StringComparer.OrdinalIgnoreCase);
        public string? LastContentType { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastMethod = request.Method.Method;
            LastUri = request.RequestUri?.ToString() ?? string.Empty;
            LastHeaders.Clear();
            foreach (var header in request.Headers)
            {
                LastHeaders[header.Key] = string.Join(",", header.Value);
            }

            LastContentType = request.Content?.Headers.ContentType?.MediaType;
            if (request.Content is not null)
            {
                _ = await request.Content.ReadAsStringAsync(cancellationToken);
            }

            return Responder?.Invoke(request)
                   ?? new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("ok") };
        }
    }

    private sealed class TestClipboardService : IClipboardService
    {
        public string LastText { get; private set; } = string.Empty;

        public bool TryGetText(out string text)
        {
            text = LastText;
            return !string.IsNullOrEmpty(text);
        }

        public void SetText(string text) => LastText = text;

        public bool TrySetImage(System.Windows.Media.Imaging.BitmapSource image) => false;
    }
}
