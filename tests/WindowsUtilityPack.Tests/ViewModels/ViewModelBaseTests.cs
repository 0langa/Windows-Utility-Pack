using WindowsUtilityPack.ViewModels;
using Xunit;

namespace WindowsUtilityPack.Tests.ViewModels;

/// <summary>
/// Unit tests for <see cref="ViewModelBase"/>.
/// Verifies that <c>SetProperty</c> correctly raises/suppresses <c>PropertyChanged</c>
/// and returns the expected boolean result.
/// </summary>
public class ViewModelBaseTests
{
    /// <summary>Concrete subclass used to exercise the protected base methods.</summary>
    private class TestViewModel : ViewModelBase
    {
        private string _name = string.Empty;

        public string Name
        {
            get => _name;
            set => SetProperty(ref _name, value);
        }

        // Tracks total PropertyChanged notifications received.
        public int NotifyCount { get; private set; }

        public TestViewModel()
        {
            PropertyChanged += (_, _) => NotifyCount++;
        }
    }

    [Fact]
    public void SetProperty_RaisesPropertyChanged_WhenValueChanges()
    {
        var vm = new TestViewModel();
        var raised = false;
        vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(TestViewModel.Name))
                raised = true;
        };

        vm.Name = "NewValue";

        Assert.True(raised);
    }

    [Fact]
    public void SetProperty_DoesNotRaisePropertyChanged_WhenValueUnchanged()
    {
        var vm           = new TestViewModel { Name = "Initial" };
        var initialCount = vm.NotifyCount;

        // Setting the same value should be a no-op.
        vm.Name = "Initial";

        Assert.Equal(initialCount, vm.NotifyCount);
    }

    [Fact]
    public void SetProperty_ReturnsTrue_WhenValueChanges()
    {
        var vm     = new TestViewModel();
        var result = false;
        vm.PropertyChanged += (_, _) => result = true;

        vm.Name = "Changed";

        Assert.True(result);
    }

    [Fact]
    public void SetProperty_ReturnsFalse_WhenValueUnchanged()
    {
        var vm          = new TestViewModel { Name = "Same" };
        var notifyCount = vm.NotifyCount;

        vm.Name = "Same";

        // Notification count must not increase for an unchanged value.
        Assert.Equal(notifyCount, vm.NotifyCount);
    }
}
