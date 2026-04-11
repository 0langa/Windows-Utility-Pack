using System.Net;
using System.Net.Http;
using WindowsUtilityPack.Services;
using WindowsUtilityPack.Tools.NetworkInternet.NetworkSpeedTest;
using Xunit;

namespace WindowsUtilityPack.Tests.ViewModels;

public class NetworkSpeedTestViewModelTests
{
    [Fact]
    public async Task RunTest_StreamsUploadAndReportsThroughput()
    {
        var handler = new StubSpeedHttpMessageHandler();
        var vm = new NetworkSpeedTestViewModel(new TestClipboardService(), new HttpClient(handler))
        {
            ServerUrl = "https://127.0.0.1/test"
        };

        vm.RunTestCommand.Execute(null);
        await WaitUntilAsync(() => !vm.IsTesting, timeoutMs: 12000);

        Assert.True(handler.PostRequests > 0);
        Assert.Equal(100, vm.UploadProgress);
        Assert.Contains("Mbps", vm.UploadSpeed, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Not measured", vm.UploadSpeed, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RunTest_WhenUploadFails_ShowsNotMeasuredStatus()
    {
        var handler = new StubSpeedHttpMessageHandler { FailUpload = true };
        var vm = new NetworkSpeedTestViewModel(new TestClipboardService(), new HttpClient(handler))
        {
            ServerUrl = "https://127.0.0.1/test"
        };

        vm.RunTestCommand.Execute(null);
        await WaitUntilAsync(() => !vm.IsTesting, timeoutMs: 12000);

        Assert.Contains("Not measured", vm.UploadSpeed, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, vm.UploadProgress);
    }

    private static async Task WaitUntilAsync(Func<bool> predicate, int timeoutMs)
    {
        var started = DateTime.UtcNow;
        while (!predicate())
        {
            if ((DateTime.UtcNow - started).TotalMilliseconds > timeoutMs)
            {
                throw new TimeoutException("Condition was not met before timeout.");
            }

            await Task.Delay(20);
        }
    }

    private sealed class StubSpeedHttpMessageHandler : HttpMessageHandler
    {
        public bool FailUpload { get; init; }
        public long UploadedBytes { get; private set; }
        public int PostRequests { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.Method == HttpMethod.Get)
            {
                var payload = new byte[512_000];
                Random.Shared.NextBytes(payload);
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new ByteArrayContent(payload)
                };
            }

            if (request.Method == HttpMethod.Post)
            {
                PostRequests++;
                if (FailUpload)
                {
                    throw new HttpRequestException("upload unavailable");
                }

                if (request.Content is not null)
                {
                    var bytes = await request.Content.ReadAsByteArrayAsync(cancellationToken);
                    UploadedBytes = bytes.LongLength;
                }

                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("ok")
                };
            }

            return new HttpResponseMessage(HttpStatusCode.MethodNotAllowed);
        }
    }

    private sealed class TestClipboardService : IClipboardService
    {
        public bool TryGetText(out string text)
        {
            text = string.Empty;
            return false;
        }

        public void SetText(string text)
        {
        }

        public bool TrySetImage(System.Windows.Media.Imaging.BitmapSource image) => false;
    }
}
