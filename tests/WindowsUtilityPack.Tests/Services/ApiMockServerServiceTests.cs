using WindowsUtilityPack.Models;
using WindowsUtilityPack.Services;
using Xunit;

namespace WindowsUtilityPack.Tests.Services;

public class ApiMockServerServiceTests
{
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