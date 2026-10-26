using WindowsUtilityPack.Models;
using WindowsUtilityPack.Services;
using Xunit;

namespace WindowsUtilityPack.Tests.Services;

public class ApiMockServerServiceTests
{
    [Fact]
    public async Task Listener_StopDrainsRequestsAndAllowsSamePortRestart()
    {
        using var reservation = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, 0);
        reservation.Start();
        var port = ((System.Net.IPEndPoint)reservation.LocalEndpoint).Port;
        reservation.Stop();
        var service = new ApiMockServerService();
        using var client = new System.Net.Http.HttpClient { Timeout = TimeSpan.FromSeconds(5) };
        try
        {
            for (var cycle = 0; cycle < 2; cycle++)
            {
                await service.StartAsync(port);
                var responses = await Task.WhenAll(Enumerable.Range(0, 20).Select(_ => client.GetStringAsync(service.BaseUrl + "health")));
                Assert.All(responses, response => Assert.Contains("ok", response));
                await service.StopAsync().WaitAsync(TimeSpan.FromSeconds(5));
                Assert.False(service.IsRunning);
                Assert.Empty(service.BaseUrl);
            }
        }
        finally { await service.StopAsync(); }
    }
    [Fact]
    public void SetEndpoints_RejectsInvalidStatusWithoutReplacingExistingEndpoints()
    {
        var service = new ApiMockServerService();
        Assert.Throws<ArgumentException>(() => service.SetEndpoints([new() { StatusCode = 999, Path = "/bad", Method = "GET" }]));
        Assert.Equal("Health", Assert.Single(service.GetEndpoints()).Name);
    }
    [Fact]
    public void ResolveResponse_MatchesConfiguredEndpoint()
    {
        var service = new ApiMockServerService();
        service.SetEndpoints(
        [
            new ApiMockEndpoint
            {
                Name = "Ping",
                Method = "GET",
                Path = "/ping",
                StatusCode = 200,
                ContentType = "application/json",
                ResponseBody = "{\"pong\":true}",
                Enabled = true,
            },
        ]);

        var response = service.ResolveResponse("GET", "/ping");

        Assert.Equal(200, response.StatusCode);
        Assert.Equal("Ping", response.EndpointName);
    }

    [Fact]
    public void ResolveResponse_ReturnsNotFound_WhenNoMatch()
    {
        var service = new ApiMockServerService();

        var response = service.ResolveResponse("POST", "/missing");

        Assert.Equal(404, response.StatusCode);
        Assert.Equal("(No match)", response.EndpointName);
    }
}
