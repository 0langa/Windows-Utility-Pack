using System.Windows;
using System.Windows.Controls;

namespace WindowsUtilityPack.Tools.FileDataTools.FileHashCalculator;

public partial class FileHashCalculatorView : UserControl
{
    public FileHashCalculatorView() { InitializeComponent(); }

    private void DropZone_DragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop)
            ? DragDropEffects.Copy
            : DragDropEffects.None;
        e.Handled = true;
    }

    private void DropZone_Drop(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(DataFormats.FileDrop)) return;

        var files = e.Data.GetData(DataFormats.FileDrop) as string[];
        var path  = files?.FirstOrDefault();
        if (string.IsNullOrEmpty(path)) return;

        if (DataContext is FileHashCalculatorViewModel vm)
        {
            vm.FilePath = path;
            vm.ComputeCommand.Execute(null);
        }
    }
}
