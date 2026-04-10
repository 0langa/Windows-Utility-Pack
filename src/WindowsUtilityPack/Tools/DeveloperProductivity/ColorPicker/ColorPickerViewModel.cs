using System.Collections.ObjectModel;
using System.Runtime.InteropServices;
using System.Windows.Media;
using WindowsUtilityPack.Commands;
using WindowsUtilityPack.Services;
using WindowsUtilityPack.ViewModels;

namespace WindowsUtilityPack.Tools.DeveloperProductivity.ColorPicker;

public class SavedColor
{
    public string Name { get; set; } = string.Empty;
    public string Hex { get; set; } = string.Empty;
    public System.Windows.Media.Color Color { get; set; }
}

public sealed class ColorPickerViewModel : ViewModelBase
{
    [StructLayout(LayoutKind.Sequential)]
    private struct Point
    {
        public int X;
        public int Y;
    }

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out Point lpPoint);

    [DllImport("user32.dll")]
    private static extern nint GetDC(nint hwnd);

    [DllImport("user32.dll")]
    private static extern int ReleaseDC(nint hwnd, nint hdc);

    [DllImport("gdi32.dll")]
    private static extern uint GetPixel(nint hdc, int nXPos, int nYPos);

    private readonly IClipboardService _clipboardService;

    private bool _updating;

    private byte _r = 255;
    private byte _g = 0;
    private byte _b = 0;
    private double _h;
    private double _s;
    private double _l;
    private string _hexColor = "#FF0000";
    private SolidColorBrush _previewBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 0, 0));
    private string _cssRgb = "rgb(255, 0, 0)";
    private string _cssHsl = "hsl(0, 100%, 50%)";
    private string _paletteName = "My Color";

    public byte R
    {
        get => _r;
        set
        {
            if (SetProperty(ref _r, value) && !_updating)
                UpdateFromRgb();
        }
    }

    public byte G
    {
        get => _g;
        set
        {
            if (SetProperty(ref _g, value) && !_updating)
                UpdateFromRgb();
        }
    }

    public byte B
    {
        get => _b;
        set
        {
            if (SetProperty(ref _b, value) && !_updating)
                UpdateFromRgb();
        }
    }

    public double H
    {
        get => _h;
        private set => SetProperty(ref _h, value);
    }

    public double S
    {
        get => _s;
        private set => SetProperty(ref _s, value);
    }

    public double L
    {
        get => _l;
        private set => SetProperty(ref _l, value);
    }

    public string HexColor
    {
        get => _hexColor;
        set
        {
            if (SetProperty(ref _hexColor, value) && !_updating)
                TryParseHex(value);
        }
    }

    public SolidColorBrush PreviewBrush
    {
        get => _previewBrush;
        private set => SetProperty(ref _previewBrush, value);
    }

    public string CssRgb
    {
        get => _cssRgb;
        private set => SetProperty(ref _cssRgb, value);
    }

    public string CssHsl
    {
        get => _cssHsl;
        private set => SetProperty(ref _cssHsl, value);
    }

    public string PaletteName
    {
        get => _paletteName;
        set => SetProperty(ref _paletteName, value);
    }

    public ObservableCollection<SavedColor> Palette { get; } = [];

    public RelayCommand CopyHexCommand { get; }
    public RelayCommand CopyRgbCommand { get; }
    public RelayCommand CopyHslCommand { get; }
    public RelayCommand PickFromScreenCommand { get; }
    public RelayCommand AddToPaletteCommand { get; }
    public RelayCommand RemoveFromPaletteCommand { get; }
    public RelayCommand SelectPaletteColorCommand { get; }
    public RelayCommand ClearPaletteCommand { get; }

    public ColorPickerViewModel(IClipboardService clipboardService)
    {
        _clipboardService = clipboardService;

        CopyHexCommand = new RelayCommand(_ => _clipboardService.SetText(HexColor));
        CopyRgbCommand = new RelayCommand(_ => _clipboardService.SetText(CssRgb));
        CopyHslCommand = new RelayCommand(_ => _clipboardService.SetText(CssHsl));
        PickFromScreenCommand = new RelayCommand(_ => PickFromScreen());
        AddToPaletteCommand = new RelayCommand(_ => AddToPalette());
        RemoveFromPaletteCommand = new RelayCommand(param =>
        {
            if (param is SavedColor sc)
                Palette.Remove(sc);
        });
        SelectPaletteColorCommand = new RelayCommand(param =>
        {
            if (param is SavedColor sc)
                SelectPaletteColor(sc);
        });
        ClearPaletteCommand = new RelayCommand(_ => Palette.Clear());

        UpdateFromRgb();
    }

    private void PickFromScreen()
    {
        if (!GetCursorPos(out var point))
            return;

        var hdc = GetDC(nint.Zero);
        if (hdc == nint.Zero)
            return;

        try
        {
            var pixel = GetPixel(hdc, point.X, point.Y);
            var r = (byte)(pixel & 0x000000FF);
            var g = (byte)((pixel & 0x0000FF00) >> 8);
            var b = (byte)((pixel & 0x00FF0000) >> 16);

            _updating = true;
            try
            {
                _r = r;
                _g = g;
                _b = b;
                OnPropertyChanged(nameof(R));
                OnPropertyChanged(nameof(G));
                OnPropertyChanged(nameof(B));
            }
            finally
            {
                _updating = false;
            }

            UpdateFromRgb();
        }
        finally
        {
            ReleaseDC(nint.Zero, hdc);
        }
    }

    private void UpdateFromRgb()
    {
        _updating = true;
        try
        {
            var color = System.Windows.Media.Color.FromRgb(_r, _g, _b);
            _hexColor = $"#{_r:X2}{_g:X2}{_b:X2}";
            OnPropertyChanged(nameof(HexColor));

            PreviewBrush = new SolidColorBrush(color);

            RgbToHsl(_r, _g, _b, out var h, out var s, out var l);
            H = Math.Round(h, 1);
            S = Math.Round(s * 100, 1);
            L = Math.Round(l * 100, 1);

            CssRgb = $"rgb({_r}, {_g}, {_b})";
            CssHsl = $"hsl({H:F0}, {S:F0}%, {L:F0}%)";
        }
        finally
        {
            _updating = false;
        }
    }

    private void TryParseHex(string hex)
    {
        if (string.IsNullOrWhiteSpace(hex)) return;

        var trimmed = hex.TrimStart('#');
        if (trimmed.Length == 6 && uint.TryParse(trimmed, System.Globalization.NumberStyles.HexNumber, null, out var value))
        {
            _updating = true;
            try
            {
                _r = (byte)((value >> 16) & 0xFF);
                _g = (byte)((value >> 8) & 0xFF);
                _b = (byte)(value & 0xFF);
                OnPropertyChanged(nameof(R));
                OnPropertyChanged(nameof(G));
                OnPropertyChanged(nameof(B));

                var color = System.Windows.Media.Color.FromRgb(_r, _g, _b);
                PreviewBrush = new SolidColorBrush(color);

                RgbToHsl(_r, _g, _b, out var h, out var s, out var l);
                H = Math.Round(h, 1);
                S = Math.Round(s * 100, 1);
                L = Math.Round(l * 100, 1);

                CssRgb = $"rgb({_r}, {_g}, {_b})";
                CssHsl = $"hsl({H:F0}, {S:F0}%, {L:F0}%)";
            }
            finally
            {
                _updating = false;
            }
        }
    }

    private void AddToPalette()
    {
        var name = string.IsNullOrWhiteSpace(PaletteName) ? HexColor : PaletteName;
        Palette.Add(new SavedColor
        {
            Name = name,
            Hex = HexColor,
            Color = System.Windows.Media.Color.FromRgb(_r, _g, _b)
        });
    }

    private void SelectPaletteColor(SavedColor sc)
    {
        TryParseHex(sc.Hex);
        _updating = true;
        try
        {
            _hexColor = sc.Hex;
            OnPropertyChanged(nameof(HexColor));
        }
        finally
        {
            _updating = false;
        }
    }

    private static void RgbToHsl(byte r, byte g, byte b, out double h, out double s, out double l)
    {
        double rd = r / 255.0, gd = g / 255.0, bd = b / 255.0;
        double max = Math.Max(rd, Math.Max(gd, bd));
        double min = Math.Min(rd, Math.Min(gd, bd));
        double delta = max - min;

        l = (max + min) / 2.0;

        if (delta == 0)
        {
            h = 0;
            s = 0;
            return;
        }

        s = delta / (1.0 - Math.Abs(2.0 * l - 1.0));

        if (max == rd)
            h = 60.0 * (((gd - bd) / delta) % 6);
        else if (max == gd)
            h = 60.0 * ((bd - rd) / delta + 2);
        else
            h = 60.0 * ((rd - gd) / delta + 4);

        if (h < 0) h += 360;
    }
}
