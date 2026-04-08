using System.Windows.Controls;
using WindowsUtilityPack.Tools.NetworkInternet.Downloader.Models;

namespace WindowsUtilityPack.Tools.NetworkInternet.Downloader;

/// <summary>
/// Code-behind for DownloaderView.
/// </summary>
public partial class DownloaderView : UserControl
{
    public DownloaderView()
    {
        InitializeComponent();
    }

    private void QueueGrid_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (DataContext is not DownloaderViewModel vm)
        {
            return;
        }

        vm.SelectedJobs.Clear();
        foreach (var item in ((DataGrid)sender).SelectedItems)
        {
            if (item is DownloadJob job)
            {
                vm.SelectedJobs.Add(job);
            }
        }
    }
}
