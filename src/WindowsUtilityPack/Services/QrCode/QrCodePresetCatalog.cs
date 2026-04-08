using System.Collections.ObjectModel;
using System.Windows.Media;

namespace WindowsUtilityPack.Services.QrCode;

/// <summary>
/// Built-in style presets and labels for the QR tool.
/// </summary>
public static class QrCodePresetCatalog
{
    /// <summary>UI-friendly preset labels in stable order.</summary>
    public static IReadOnlyDictionary<QrCodeStylePreset, string> PresetLabels { get; } =
        new ReadOnlyDictionary<QrCodeStylePreset, string>(
            new Dictionary<QrCodeStylePreset, string>
            {
                [QrCodeStylePreset.Classic] = "Classic",
                [QrCodeStylePreset.DarkMinimal] = "Dark Minimal",
                [QrCodeStylePreset.BrandAccent] = "Brand Accent",
                [QrCodeStylePreset.Transparent] = "Transparent",
                [QrCodeStylePreset.PrintFriendly] = "Print Friendly",
            });

    /// <summary>Creates options configured for the specified preset.</summary>
    public static QrCodeStyleOptions CreatePreset(QrCodeStylePreset preset)
    {
        return preset switch
        {
            QrCodeStylePreset.DarkMinimal => new QrCodeStyleOptions
            {
                ForegroundColor = Color.FromRgb(3, 7, 18),
                BackgroundColor = Color.FromRgb(243, 244, 246),
                ErrorCorrectionLevel = QrCodeErrorCorrectionLevel.Quartile,
                ModuleShape = QrCodeModuleShape.Square,
                IncludeFrame = false,
                IncludeCaption = false,
                QuietZoneModules = 4,
            },
            QrCodeStylePreset.BrandAccent => new QrCodeStyleOptions
            {
                ForegroundColor = Color.FromRgb(30, 64, 175),
                BackgroundColor = Color.FromRgb(239, 246, 255),
                ErrorCorrectionLevel = QrCodeErrorCorrectionLevel.High,
                ModuleShape = QrCodeModuleShape.Rounded,
                IncludeFrame = true,
                FrameColor = Color.FromRgb(29, 78, 216),
                IncludeCaption = true,
                QuietZoneModules = 4,
            },
            QrCodeStylePreset.Transparent => new QrCodeStyleOptions
            {
                ForegroundColor = Color.FromRgb(15, 23, 42),
                BackgroundColor = Colors.White,
                TransparentBackground = true,
                ErrorCorrectionLevel = QrCodeErrorCorrectionLevel.Quartile,
                ModuleShape = QrCodeModuleShape.Rounded,
                IncludeFrame = false,
                IncludeCaption = false,
                QuietZoneModules = 4,
            },
            QrCodeStylePreset.PrintFriendly => new QrCodeStyleOptions
            {
                ForegroundColor = Colors.Black,
                BackgroundColor = Colors.White,
                ErrorCorrectionLevel = QrCodeErrorCorrectionLevel.High,
                ModuleShape = QrCodeModuleShape.Square,
                IncludeFrame = true,
                FrameColor = Colors.Black,
                IncludeCaption = true,
                QuietZoneModules = 6,
            },
            _ => new QrCodeStyleOptions
            {
                ForegroundColor = Color.FromRgb(17, 24, 39),
                BackgroundColor = Colors.White,
                ErrorCorrectionLevel = QrCodeErrorCorrectionLevel.Medium,
                ModuleShape = QrCodeModuleShape.Square,
                IncludeFrame = false,
                IncludeCaption = false,
                QuietZoneModules = 4,
            },
        };
    }
}
