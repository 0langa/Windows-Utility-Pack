using System.Drawing;
using System.Windows.Forms;

namespace WindowsUtilityPack.Services;

internal static class ScreenColorPicker
{
    internal static Color? Pick()
    {
        var bounds = SystemInformation.VirtualScreen;
        using var capture = new Bitmap(bounds.Width, bounds.Height);
        using (var graphics = Graphics.FromImage(capture))
            graphics.CopyFromScreen(bounds.Location, Point.Empty, bounds.Size);
        using var overlay = new Form
        {
            FormBorderStyle = FormBorderStyle.None,
            StartPosition = FormStartPosition.Manual,
            AutoScaleMode = AutoScaleMode.None,
            Bounds = bounds,
            TopMost = true,
            ShowInTaskbar = false,
            BackgroundImage = capture,
            BackgroundImageLayout = ImageLayout.None,
            Cursor = Cursors.Cross,
            KeyPreview = true,
            Text = "Pick a screen color - Escape cancels"
        };
        Color? picked = null;
        overlay.KeyDown += (_, e) => { if (e.KeyCode == Keys.Escape) overlay.Close(); };
        overlay.MouseDown += (_, e) =>
        {
            if (e.Button != MouseButtons.Left) { overlay.Close(); return; }
            var point = Cursor.Position;
            var x = point.X - bounds.X; var y = point.Y - bounds.Y;
            if (x >= 0 && x < capture.Width && y >= 0 && y < capture.Height) picked = capture.GetPixel(x, y);
            overlay.Close();
        };
        overlay.ShowDialog();
        return picked;
    }
}
