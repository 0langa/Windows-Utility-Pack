using System.Windows.Controls;

namespace WindowsUtilityPack.Tools.SystemUtilities.StorageMaster;

/// <summary>
/// Code-behind for the Storage Master view.
/// All logic is in StorageMasterViewModel; this file contains only
/// minimal WPF plumbing.
/// </summary>
public partial class StorageMasterView : UserControl
{
    public StorageMasterView()
    {
        InitializeComponent();
    }

    private void CleanupColumnHeader_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        if (e.OriginalSource is GridViewColumnHeader header
            && header.Role != GridViewColumnHeaderRole.Padding
            && header.Column?.Header is string columnName
            && !string.IsNullOrEmpty(columnName)
            && DataContext is StorageMasterViewModel vm)
        {
            vm.SortCleanupByColumn(columnName);
        }
    }
}
