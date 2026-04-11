using WindowsUtilityPack.Models;
using WindowsUtilityPack.Services;
using WindowsUtilityPack.Tools.NetworkInternet.SshRemoteTool;
using Xunit;

namespace WindowsUtilityPack.Tests.ViewModels;

public class SshRemoteToolViewModelTests
{
    [Fact]
    public async Task RefreshAsync_LoadsProfiles()
    {
        var vm = new SshRemoteToolViewModel(new StubService(), new StubDialogs(), new StubClipboard());

        await vm.RefreshAsync();

        Assert.Single(vm.Profiles);
    }

    private sealed class StubService : ISshRemoteToolService
    {
        public string BuildSshCommand(SshConnectionProfile profile) => "ssh command";

        public Task<bool> DeleteProfileAsync(string name, CancellationToken cancellationToken = default)
            => Task.FromResult(true);

        public Task<IReadOnlyList<SshConnectionProfile>> GetProfilesAsync(CancellationToken cancellationToken = default)
        {
            IReadOnlyList<SshConnectionProfile> profiles =
            [
                new SshConnectionProfile
                {
                    Name = "Demo",
                    Host = "localhost",
                    Port = 22,
                    Username = "user",
                    PrivateKeyPath = string.Empty,
                    CreatedUtc = DateTime.UtcNow,
                    UpdatedUtc = DateTime.UtcNow,
                },
            ];
            return Task.FromResult(profiles);
        }

        public Task SaveProfileAsync(SshConnectionProfile profile, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<(bool IsReachable, string Message)> TestConnectionAsync(string host, int port, TimeSpan timeout, CancellationToken cancellationToken = default)
            => Task.FromResult((true, "ok"));
    }

    private sealed class StubDialogs : IUserDialogService
    {
        public bool Confirm(string title, string message) => true;

        public void ShowError(string title, string message) { }

        public void ShowInfo(string title, string message) { }
    }

    private sealed class StubClipboard : IClipboardService
    {
        public bool TryGetText(out string text)
        {
            text = string.Empty;
            return false;
        }

        public void SetText(string text) { }

        public bool TrySetImage(System.Windows.Media.Imaging.BitmapSource image) => false;
    }
}