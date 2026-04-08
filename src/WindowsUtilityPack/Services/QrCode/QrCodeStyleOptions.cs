using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace WindowsUtilityPack.Services.QrCode;

/// <summary>
/// Visual and behavior options that control QR rendering.
/// </summary>
public sealed class QrCodeStyleOptions
{
    /// <summary>Preview/export target size in pixels.</summary>
    public int SizePixels { get; set; } = 360;

    /// <summary>Quiet-zone size expressed in module count.</summary>
    public int QuietZoneModules { get; set; } = 4;

    /// <summary>QR dark module color.</summary>
    public Color ForegroundColor { get; set; } = Color.FromRgb(17, 24, 39);

    /// <summary>QR light module color.</summary>
    public Color BackgroundColor { get; set; } = Colors.White;

    /// <summary>When true, the background is transparent where supported.</summary>
    public bool TransparentBackground { get; set; }

    /// <summary>Error correction strength.</summary>
    public QrCodeErrorCorrectionLevel ErrorCorrectionLevel { get; set; } = QrCodeErrorCorrectionLevel.Medium;

    /// <summary>Module shape style.</summary>
    public QrCodeModuleShape ModuleShape { get; set; } = QrCodeModuleShape.Square;

    /// <summary>Whether a frame border should be drawn around the code.</summary>
    public bool IncludeFrame { get; set; }

    /// <summary>Frame/border color.</summary>
    public Color FrameColor { get; set; } = Color.FromRgb(55, 65, 81);

    /// <summary>Border thickness around the QR area.</summary>
    public int FrameThickness { get; set; } = 8;

    /// <summary>Optional caption rendered below the QR code.</summary>
    public bool IncludeCaption { get; set; }

    /// <summary>Caption text shown below the code when enabled.</summary>
    public string CaptionText { get; set; } = string.Empty;

    /// <summary>Caption text color.</summary>
    public Color CaptionColor { get; set; } = Color.FromRgb(31, 41, 55);

    /// <summary>Optional center logo image.</summary>
    public BitmapSource? LogoImage { get; set; }

    /// <summary>Logo size in percent of QR body width.</summary>
    public int LogoScalePercent { get; set; } = 16;

    /// <summary>Logo white padding ring in pixels.</summary>
    public int LogoPaddingPixels { get; set; } = 6;

    /// <summary>Optional URL label fallback generated from domain.</summary>
    public string DomainLabel { get; set; } = string.Empty;
}
