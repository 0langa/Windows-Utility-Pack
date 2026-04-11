using WindowsUtilityPack.Models;
using WindowsUtilityPack.Services;
using WindowsUtilityPack.Tools.SystemUtilities.RegistryEditor;
using Xunit;

namespace WindowsUtilityPack.Tests.ViewModels;

public class RegistryEditorViewModelTests
{
    [Fact]
    public async Task LoadKeyAsync_PopulatesCollections()
    {
        var vm = new RegistryEditorViewModel(new StubService(), new StubDialogs());

        await vm.LoadKeyAsync();

        Assert.Single(vm.SubKeys);
        Assert.Single(vm.Values);
    }

    private sealed class StubService : IRegistryEditorService
    {
        public Task BackupAsync(string keyPath, string outputFilePath, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task DeleteValueAsync(string keyPath, string valueName, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<IReadOnlyList<string>> GetSubKeyNamesAsync(string keyPath, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<string>>(["SubA"]);

        public Task<IReadOnlyList<RegistryValueRow>> GetValuesAsync(string keyPath, CancellationToken cancellationToken = default)
        {
            IReadOnlyList<RegistryValueRow> values =
            [
                new RegistryValueRow { Name = "ValueA", Kind = "String", DisplayData = "X" },
            ];

            return Task.FromResult(values);
        }

        public Task RestoreAsync(string inputFilePath, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task SetValueAsync(string keyPath, string valueName, string valueData, string valueKind, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }

    private sealed class StubDialogs : IUserDialogService
    {
        public bool Confirm(string title, string message) => true;

        public void ShowError(string title, string message) { }

        public void ShowInfo(string title, string message) { }
    }
}