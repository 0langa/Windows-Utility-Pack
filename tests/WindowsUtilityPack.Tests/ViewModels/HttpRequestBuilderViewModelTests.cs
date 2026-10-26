using System.Net.Http;
using System.Text.Json;
using WindowsUtilityPack.Services;
using WindowsUtilityPack.Services.Pentesting;
using WindowsUtilityPack.Tools.Pentesting.HttpRequestBuilder;
using WindowsUtilityPack.ViewModels;
using Xunit;

namespace WindowsUtilityPack.Tests.ViewModels;

public sealed class HttpRequestBuilderViewModelTests
{
    [Fact]
    public void RemovingSavedAuthorizationAndEditingQuery_PersistsWithoutSending()
    {
        var settings = new TestSettings();
        using var client = new HttpClient();
        var transport = new PentestHttpTransportService(client);
        var vm = new HttpRequestBuilderViewModel(transport, new PentestSharedStateService(), new Clipboard(), settings);
        Assert.Equal(0, settings.Saves);
        vm.RemoveHeaderCommand.Execute(vm.Headers[0]);
        vm.QueryParameters[0].Value = "new value";
        var reopened = new HttpRequestBuilderViewModel(transport, new PentestSharedStateService(), new Clipboard(), settings);
        Assert.DoesNotContain(reopened.Headers, row => row.Name == "Authorization");
        Assert.Equal("new value", Assert.Single(reopened.QueryParameters).Value);
    }

    [Fact]
    public void LoadingNonDefaultRequest_DoesNotWritePartiallyLoadedState()
    {
        var settings = new TestSettings(); settings.Current.PentestLastRequestUrl = "https://example.com/fixture";
        settings.Current.PentestLastRequestMethod = "POST"; settings.Current.PentestLastRequestBody = "fixture body";
        using var client = new HttpClient();
        var vm = new HttpRequestBuilderViewModel(new PentestHttpTransportService(client), new PentestSharedStateService(), new Clipboard(), settings);
        Assert.Equal(0, settings.Saves);
        Assert.Equal("Bearer fixture", Assert.Single(vm.Headers).Value);
    }

    [Fact]
    public void FailedRequestStateWrite_ReportsErrorAndGuardsDraft()
    {
        var settings = new TestSettings { Refuse = true };
        using var client = new HttpClient();
        var vm = new HttpRequestBuilderViewModel(new PentestHttpTransportService(client), new PentestSharedStateService(), new Clipboard(), settings);
        vm.Headers[0].Value = "edited fixture";
        Assert.Contains("not saved", vm.StatusText, StringComparison.OrdinalIgnoreCase);
        Assert.False(Assert.IsAssignableFrom<INavigationGuard>(vm).CanNavigateAway());
        Assert.Equal("edited fixture", vm.Headers[0].Value);
    }

    private sealed class TestSettings : ISettingsService
    {
        public AppSettings Current { get; set; } = new()
        {
            PentestLastRequestUrl = "https://example.com", PentestLastRequestMethod = "GET",
            PentestLastRequestContentType = "application/json", PentestLastRequestHeaders = "Authorization\tBearer fixture",
            PentestLastQueryParameters = "q\told value"
        };
        public int Saves { get; private set; }
        public bool Refuse { get; set; }
        public AppSettings Load() => JsonSerializer.Deserialize<AppSettings>(JsonSerializer.Serialize(Current, Options), Options)!;
        private static readonly JsonSerializerOptions Options = new() { NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.AllowNamedFloatingPointLiterals };
        public void Save(AppSettings value) { Saves++; if (Refuse) throw new System.IO.IOException("fixture write failure"); Current = value; }
    }
    private sealed class Clipboard : IClipboardService
    {
        public bool TryGetText(out string text) { text = ""; return false; }
        public void SetText(string text) { }
        public bool TrySetImage(System.Windows.Media.Imaging.BitmapSource image) => false;
    }
}
