using WindowsUtilityPack.Models;
using WindowsUtilityPack.Services;
using WindowsUtilityPack.Tools.DeveloperProductivity.ApiMockServer;
using Xunit;

namespace WindowsUtilityPack.Tests.ViewModels;

public class ApiMockServerViewModelTests
{
    [Theory]
    [InlineData(999, "application/json")]
    [InlineData(200, "invalid content type")]
    public async Task SaveEndpointsCommand_InvalidEditIsReportedWithoutLosingDraftOrActiveConfiguration(int status, string type)
    {
        var service = new ApiMockServerService();
        var original = new ApiMockEndpoint { Name = "fixture", Method = "GET", Path = "/fixture", StatusCode = 200, ContentType = "application/json", ResponseBody = "{}" };
        service.SetEndpoints([original]);
        var vm = new ApiMockServerViewModel(service, new StubDialogs());
        vm.Endpoints[0].StatusCode = status; vm.Endpoints[0].ContentType = type;
        var error = Record.Exception(() => vm.SaveEndpointsCommand.Execute(null));
        Assert.Null(error);
        Assert.Contains("not saved", vm.StatusMessage, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(status, vm.Endpoints[0].StatusCode);
        Assert.Equal(200, Assert.Single(service.GetEndpoints()).StatusCode);
        await vm.StartAsync();
        Assert.False(service.IsRunning);
    }

    [Fact]
    public async Task StartAsync_UpdatesRunningStateMessage()
    {
        var service = new StubService();
        var vm = new ApiMockServerViewModel(service, new StubDialogs());

        await vm.StartAsync();

        Assert.True(service.IsRunning);
        Assert.Contains("started", vm.StatusMessage, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class StubService : IApiMockServerService
    {
        private readonly List<ApiMockEndpoint> _endpoints =
        [
            new ApiMockEndpoint { Name = "A", Method = "GET", Path = "/a", StatusCode = 200, ContentType = "application/json", ResponseBody = "{}", Enabled = true },
        ];

        public bool IsRunning { get; private set; }

        public string BaseUrl { get; private set; } = string.Empty;

        public void ClearRequestLog() { }

        public IReadOnlyList<ApiMockEndpoint> GetEndpoints() => _endpoints.ToList();

        public IReadOnlyList<ApiMockRequestLogEntry> GetRequestLog(int limit = 200) => [];

        public void SetEndpoints(IReadOnlyList<ApiMockEndpoint> endpoints)
        {
            _endpoints.Clear();
            _endpoints.AddRange(endpoints);
        }

        public Task StartAsync(int port, CancellationToken cancellationToken = default)
        {
            IsRunning = true;
            BaseUrl = $"http://localhost:{port}/";
            return Task.CompletedTask;
        }

        public Task StopAsync()
        {
            IsRunning = false;
            BaseUrl = string.Empty;
            return Task.CompletedTask;
        }
    }

    private sealed class StubDialogs : IUserDialogService
    {
        public bool Confirm(string title, string message) => true;

        public void ShowError(string title, string message) { }

        public void ShowInfo(string title, string message) { }
    }
}