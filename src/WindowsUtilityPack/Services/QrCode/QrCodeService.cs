using System.Globalization;
using System.IO;
using System.Security;
using System.Text;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using PdfSharp;
using PdfSharp.Drawing;
using PdfSharp.Pdf;
using QRCoder;

namespace WindowsUtilityPack.Services.QrCode;

/// <summary>
/// Production QR generation service using QRCoder data encoding and custom WPF rendering.
/// Custom rendering keeps raster/vector output consistent while enabling styling options.
/// </summary>
public sealed class QrCodeService : IQrCodeService
{
    private static readonly object PdfLock = new();

    /// <inheritdoc/>
    public bool TryNormalizeUrl(string input, out string normalizedUrl, out string errorMessage)
    {
        normalizedUrl = string.Empty;
        errorMessage = string.Empty;

        if (string.IsNullOrWhiteSpace(input))
        {
            errorMessage = "Enter a URL to generate a QR code.";
            return false;
        }

        var trimmed = input.Trim();
        if (!trimmed.Contains("://", StringComparison.Ordinal) && !trimmed.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase))
        {
            trimmed = $"https://{trimmed}";
        }

        if (!Uri.TryCreate(trimmed, UriKind.Absolute, out var uri))
        {
            errorMessage = "The entered text is not a valid URL.";
            return false;
        }

        if (uri.Scheme is not ("http" or "https" or "mailto"))
        {
            errorMessage = "Only HTTP, HTTPS, and mailto URLs are supported.";
            return false;
        }

        if (uri.Scheme is "http" or "https")
        {
            if (string.IsNullOrWhiteSpace(uri.Host) || !uri.Host.Contains('.', StringComparison.Ordinal))
            {
                errorMessage = "Enter a complete website address such as https://example.com.";
                return false;
            }
        }

        normalizedUrl = uri.AbsoluteUri;
        return true;
    }

    /// <inheritdoc/>
    public string BuildSuggestedFileName(string normalizedUrl, bool includeTimestamp)
    {
        var baseName = "qr-code";
        if (Uri.TryCreate(normalizedUrl, UriKind.Absolute, out var uri))
        {
            if (!string.IsNullOrWhiteSpace(uri.Host))
            {
                baseName = $"qr-{uri.Host.Replace(".", "-", StringComparison.Ordinal)}";
            }
        }

        if (!includeTimestamp)
        {
            return $"{baseName}.png";
        }

        return $"{baseName}-{DateTime.Now:yyyyMMdd-HHmmss}.png";
    }

    /// <inheritdoc/>
    public async Task<QrCodePreviewResult> GeneratePreviewAsync(string normalizedUrl, QrCodeStyleOptions style, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(normalizedUrl);
        ArgumentNullException.ThrowIfNull(style);

        var sanitized = Sanitize(style, normalizedUrl);

        return await Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            using var generator = new QRCodeGenerator();
            using var qrData = generator.CreateQrCode(normalizedUrl, MapErrorCorrection(sanitized.ErrorCorrectionLevel), forceUtf8: true, utf8BOM: false, eciMode: QRCodeGenerator.EciMode.Utf8);
            var raster = RenderRaster(qrData, sanitized, sanitized.SizePixels, 96);
            var svg = RenderSvg(qrData, sanitized, sanitized.SizePixels);
            var warnings = AnalyzeScannability(sanitized).Warnings;

            return new QrCodePreviewResult
            {
                Image = raster,
                SvgMarkup = svg,
                NormalizedUrl = normalizedUrl,
                Warnings = warnings,
            };
        }, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<QrCodeExportResult> ExportAsync(QrCodeExportRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var style = Sanitize(request.Style, request.NormalizedUrl);

        await Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            var directory = Path.GetDirectoryName(request.FilePath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            using var generator = new QRCodeGenerator();
            using var qrData = generator.CreateQrCode(request.NormalizedUrl, MapErrorCorrection(style.ErrorCorrectionLevel), forceUtf8: true, utf8BOM: false, eciMode: QRCodeGenerator.EciMode.Utf8);

            switch (request.Format)
            {
                case QrCodeExportFormat.Svg:
                    var svg = RenderSvg(qrData, style, request.ExportSizePixels);
                    File.WriteAllText(request.FilePath, svg, Encoding.UTF8);
                    break;

                case QrCodeExportFormat.Pdf:
                    ExportPdf(qrData, style, request.ExportSizePixels, request.FilePath);
                    break;

                default:
                    var bitmap = RenderRaster(qrData, style, request.ExportSizePixels, request.RasterDpi);
                    ExportRaster(bitmap, request.FilePath, request.Format);
                    break;
            }
        }, cancellationToken).ConfigureAwait(false);

        return new QrCodeExportResult
        {
            FilePath = request.FilePath,
            Format = request.Format,
            Warnings = AnalyzeScannability(style).Warnings,
        };
    }

    /// <inheritdoc/>
    public QrScannabilityReport AnalyzeScannability(QrCodeStyleOptions style)
    {
        var warnings = new List<string>();

        if (style.QuietZoneModules < 4)
        {
            warnings.Add("Quiet zone below 4 modules can reduce scan reliability.");
        }

        if (!style.TransparentBackground)
        {
            var contrast = CalculateContrast(style.ForegroundColor, style.BackgroundColor);
            if (contrast < 3.5)
            {
                warnings.Add("Foreground/background contrast is low. Increase contrast for better scanning.");
            }
        }

        if (style.LogoImage is not null)
        {
            if (style.LogoScalePercent > 24)
            {
                warnings.Add("Large center logos can make QR codes harder to scan.");
            }

            if (style.ErrorCorrectionLevel is QrCodeErrorCorrectionLevel.Low or QrCodeErrorCorrectionLevel.Medium)
            {
                warnings.Add("Use Quartile or High error correction when a center logo is enabled.");
            }
        }

        return new QrScannabilityReport
        {
            IsLikelyScannable = warnings.Count == 0,
            Warnings = warnings,
        };
    }

    private static QrCodeStyleOptions Sanitize(QrCodeStyleOptions style, string normalizedUrl)
    {
        var result = new QrCodeStyleOptions
        {
            SizePixels = Math.Clamp(style.SizePixels, 120, 1600),
            QuietZoneModules = Math.Clamp(style.QuietZoneModules, 0, 12),
            ForegroundColor = style.ForegroundColor,
            BackgroundColor = style.BackgroundColor,
            TransparentBackground = style.TransparentBackground,
            ErrorCorrectionLevel = style.ErrorCorrectionLevel,
            ModuleShape = style.ModuleShape,
            IncludeFrame = style.IncludeFrame,
            FrameColor = style.FrameColor,
            FrameThickness = Math.Clamp(style.FrameThickness, 0, 48),
            IncludeCaption = style.IncludeCaption,
            CaptionText = style.CaptionText?.Trim() ?? string.Empty,
            CaptionColor = style.CaptionColor,
            LogoImage = style.LogoImage,
            LogoScalePercent = Math.Clamp(style.LogoScalePercent, 8, 30),
            LogoPaddingPixels = Math.Clamp(style.LogoPaddingPixels, 0, 32),
            DomainLabel = style.DomainLabel ?? string.Empty,
        };

        if (string.IsNullOrWhiteSpace(result.CaptionText) && Uri.TryCreate(normalizedUrl, UriKind.Absolute, out var uri))
        {
            result.CaptionText = uri.Host;
        }

        return result;
    }

    private static QRCodeGenerator.ECCLevel MapErrorCorrection(QrCodeErrorCorrectionLevel level) => level switch
    {
        QrCodeErrorCorrectionLevel.Low => QRCodeGenerator.ECCLevel.L,
        QrCodeErrorCorrectionLevel.Quartile => QRCodeGenerator.ECCLevel.Q,
        QrCodeErrorCorrectionLevel.High => QRCodeGenerator.ECCLevel.H,
        _ => QRCodeGenerator.ECCLevel.M,
    };

    private static BitmapSource RenderRaster(QRCodeData qrData, QrCodeStyleOptions style, int sizePixels, int dpi)
    {
        var matrix = qrData.ModuleMatrix;
        var qrModules = matrix.Count;
        var totalModules = qrModules + (style.QuietZoneModules * 2);
        var modulePixels = Math.Max(1, sizePixels / totalModules);
        var qrBodyPixels = totalModules * modulePixels;
        var frameInset = style.IncludeFrame ? style.FrameThickness : 0;
        var contentWidth = qrBodyPixels + (frameInset * 2);
        var captionHeight = style.IncludeCaption && !string.IsNullOrWhiteSpace(style.CaptionText) ? 48 : 0;
        var canvasHeight = contentWidth + captionHeight;

        var visual = new DrawingVisual();
        using (var context = visual.RenderOpen())
        {
            var canvasRect = new Rect(0, 0, contentWidth, canvasHeight);
            if (!style.TransparentBackground)
            {
                context.DrawRectangle(new SolidColorBrush(style.BackgroundColor), null, canvasRect);
            }

            var qrOriginX = frameInset;
            var qrOriginY = frameInset;

            if (style.IncludeFrame && style.FrameThickness > 0)
            {
                var frameRect = new Rect(0, 0, contentWidth, contentWidth);
                var pen = new Pen(new SolidColorBrush(style.FrameColor), style.FrameThickness);
                context.DrawRectangle(null, pen, frameRect);
            }

            var darkBrush = new SolidColorBrush(style.ForegroundColor);
            darkBrush.Freeze();
            var radius = style.ModuleShape == QrCodeModuleShape.Rounded ? Math.Max(0.5, modulePixels * 0.28) : 0;

            for (var row = 0; row < qrModules; row++)
            {
                for (var column = 0; column < qrModules; column++)
                {
                    if (!matrix[row][column])
                    {
                        continue;
                    }

                    var drawX = qrOriginX + ((column + style.QuietZoneModules) * modulePixels);
                    var drawY = qrOriginY + ((row + style.QuietZoneModules) * modulePixels);
                    context.DrawRoundedRectangle(darkBrush, null, new Rect(drawX, drawY, modulePixels, modulePixels), radius, radius);
                }
            }

            DrawLogo(context, style, qrOriginX, qrOriginY, qrBodyPixels);
            DrawCaption(context, style, contentWidth, contentWidth + 10);
        }

        var target = new RenderTargetBitmap(contentWidth, canvasHeight, dpi, dpi, PixelFormats.Pbgra32);
        target.Render(visual);
        target.Freeze();
        return target;
    }

    private static string RenderSvg(QRCodeData qrData, QrCodeStyleOptions style, int sizePixels)
    {
        var matrix = qrData.ModuleMatrix;
        var qrModules = matrix.Count;
        var totalModules = qrModules + (style.QuietZoneModules * 2);
        var modulePixels = Math.Max(1, sizePixels / totalModules);
        var qrBodyPixels = totalModules * modulePixels;
        var frameInset = style.IncludeFrame ? style.FrameThickness : 0;
        var contentWidth = qrBodyPixels + (frameInset * 2);
        var captionHeight = style.IncludeCaption && !string.IsNullOrWhiteSpace(style.CaptionText) ? 42 : 0;
        var contentHeight = contentWidth + captionHeight;

        var foreground = ToHex(style.ForegroundColor, includeAlpha: false);
        var background = style.TransparentBackground ? "none" : ToHex(style.BackgroundColor, includeAlpha: false);
        var frame = ToHex(style.FrameColor, includeAlpha: false);
        var caption = ToHex(style.CaptionColor, includeAlpha: false);

        var sb = new StringBuilder();
        sb.AppendLine("<?xml version=\"1.0\" encoding=\"utf-8\"?>");
        sb.AppendLine($"<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"{contentWidth}\" height=\"{contentHeight}\" viewBox=\"0 0 {contentWidth} {contentHeight}\">");
        if (!style.TransparentBackground)
        {
            sb.AppendLine($"  <rect width=\"{contentWidth}\" height=\"{contentHeight}\" fill=\"{background}\" />");
        }

        if (style.IncludeFrame && style.FrameThickness > 0)
        {
            var half = Math.Max(1, style.FrameThickness / 2.0);
            var frameSize = contentWidth - (half * 2);
            sb.AppendLine($"  <rect x=\"{half.ToString(CultureInfo.InvariantCulture)}\" y=\"{half.ToString(CultureInfo.InvariantCulture)}\" width=\"{frameSize.ToString(CultureInfo.InvariantCulture)}\" height=\"{frameSize.ToString(CultureInfo.InvariantCulture)}\" fill=\"none\" stroke=\"{frame}\" stroke-width=\"{style.FrameThickness}\" />");
        }

        var radius = style.ModuleShape == QrCodeModuleShape.Rounded ? Math.Max(1, modulePixels / 3.0) : 0;
        for (var row = 0; row < qrModules; row++)
        {
            for (var column = 0; column < qrModules; column++)
            {
                if (!matrix[row][column])
                {
                    continue;
                }

                var x = frameInset + ((column + style.QuietZoneModules) * modulePixels);
                var y = frameInset + ((row + style.QuietZoneModules) * modulePixels);
                if (radius > 0)
                {
                    sb.AppendLine($"  <rect x=\"{x}\" y=\"{y}\" width=\"{modulePixels}\" height=\"{modulePixels}\" rx=\"{radius.ToString(CultureInfo.InvariantCulture)}\" ry=\"{radius.ToString(CultureInfo.InvariantCulture)}\" fill=\"{foreground}\" />");
                }
                else
                {
                    sb.AppendLine($"  <rect x=\"{x}\" y=\"{y}\" width=\"{modulePixels}\" height=\"{modulePixels}\" fill=\"{foreground}\" />");
                }
            }
        }

        if (style.LogoImage is not null)
        {
            var logoBytes = EncodePng(style.LogoImage);
            var logoBase64 = Convert.ToBase64String(logoBytes);
            var logoSize = (int)(qrBodyPixels * (style.LogoScalePercent / 100.0));
            var logoX = frameInset + ((qrBodyPixels - logoSize) / 2);
            var logoY = frameInset + ((qrBodyPixels - logoSize) / 2);
            sb.AppendLine($"  <rect x=\"{logoX - style.LogoPaddingPixels}\" y=\"{logoY - style.LogoPaddingPixels}\" width=\"{logoSize + (style.LogoPaddingPixels * 2)}\" height=\"{logoSize + (style.LogoPaddingPixels * 2)}\" fill=\"white\" rx=\"8\" ry=\"8\" />");
            sb.AppendLine($"  <image x=\"{logoX}\" y=\"{logoY}\" width=\"{logoSize}\" height=\"{logoSize}\" href=\"data:image/png;base64,{logoBase64}\" />");
        }

        if (style.IncludeCaption && !string.IsNullOrWhiteSpace(style.CaptionText))
        {
            var escaped = SecurityElement.Escape(style.CaptionText) ?? string.Empty;
            var y = contentWidth + 28;
            sb.AppendLine($"  <text x=\"{contentWidth / 2}\" y=\"{y}\" text-anchor=\"middle\" font-family=\"Segoe UI, Arial, sans-serif\" font-size=\"22\" fill=\"{caption}\">{escaped}</text>");
        }

        sb.AppendLine("</svg>");
        return sb.ToString();
    }

    private static void DrawLogo(DrawingContext context, QrCodeStyleOptions style, int originX, int originY, int qrBodyPixels)
    {
        if (style.LogoImage is null)
        {
            return;
        }

        var logoSize = (int)(qrBodyPixels * (style.LogoScalePercent / 100.0));
        var logoX = originX + ((qrBodyPixels - logoSize) / 2);
        var logoY = originY + ((qrBodyPixels - logoSize) / 2);

        if (style.LogoPaddingPixels > 0)
        {
            var paddingRect = new Rect(
                logoX - style.LogoPaddingPixels,
                logoY - style.LogoPaddingPixels,
                logoSize + (style.LogoPaddingPixels * 2),
                logoSize + (style.LogoPaddingPixels * 2));
            context.DrawRoundedRectangle(Brushes.White, null, paddingRect, 8, 8);
        }

        context.DrawImage(style.LogoImage, new Rect(logoX, logoY, logoSize, logoSize));
    }

    private static void DrawCaption(DrawingContext context, QrCodeStyleOptions style, int contentWidth, int top)
    {
        if (!style.IncludeCaption || string.IsNullOrWhiteSpace(style.CaptionText))
        {
            return;
        }

        var text = new FormattedText(
            style.CaptionText,
            CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            new Typeface("Segoe UI"),
            20,
            new SolidColorBrush(style.CaptionColor),
            1.0)
        {
            MaxTextWidth = Math.Max(120, contentWidth - 12),
            TextAlignment = TextAlignment.Center,
            Trimming = TextTrimming.CharacterEllipsis,
        };

        context.DrawText(text, new Point((contentWidth - text.Width) / 2, top));
    }

    private static void ExportRaster(BitmapSource image, string filePath, QrCodeExportFormat format)
    {
        BitmapEncoder encoder = format switch
        {
            QrCodeExportFormat.Jpeg => new JpegBitmapEncoder { QualityLevel = 92 },
            QrCodeExportFormat.Bmp => new BmpBitmapEncoder(),
            _ => new PngBitmapEncoder(),
        };

        encoder.Frames.Add(BitmapFrame.Create(image));
        using var stream = File.Create(filePath);
        encoder.Save(stream);
    }

    private static void ExportPdf(QRCodeData qrData, QrCodeStyleOptions style, int exportSizePixels, string filePath)
    {
        var pdfStyle = new QrCodeStyleOptions
        {
            SizePixels = style.SizePixels,
            QuietZoneModules = style.QuietZoneModules,
            ForegroundColor = style.ForegroundColor,
            BackgroundColor = style.BackgroundColor,
            TransparentBackground = false,
            ErrorCorrectionLevel = style.ErrorCorrectionLevel,
            ModuleShape = style.ModuleShape,
            IncludeFrame = style.IncludeFrame,
            FrameColor = style.FrameColor,
            FrameThickness = style.FrameThickness,
            IncludeCaption = style.IncludeCaption,
            CaptionText = style.CaptionText,
            CaptionColor = style.CaptionColor,
            LogoImage = style.LogoImage,
            LogoScalePercent = style.LogoScalePercent,
            LogoPaddingPixels = style.LogoPaddingPixels,
            DomainLabel = style.DomainLabel,
        };
        var raster = RenderRaster(qrData, pdfStyle, exportSizePixels, 300);
        var imageBytes = EncodePng(raster);

        lock (PdfLock)
        {
            using var document = new PdfDocument();
            var page = document.AddPage();
            page.Size = PageSize.A4;
            using var graphics = XGraphics.FromPdfPage(page);
            using var image = XImage.FromStream(new MemoryStream(imageBytes));

            const double margin = 36;
            var maxWidth = page.Width.Point - (margin * 2);
            var maxHeight = page.Height.Point - (margin * 2);
            var ratio = Math.Min(maxWidth / image.PixelWidth, maxHeight / image.PixelHeight);
            var drawWidth = image.PixelWidth * ratio;
            var drawHeight = image.PixelHeight * ratio;
            var drawX = (page.Width.Point - drawWidth) / 2;
            var drawY = (page.Height.Point - drawHeight) / 2;

            graphics.DrawImage(image, drawX, drawY, drawWidth, drawHeight);
            document.Save(filePath);
        }
    }

    private static byte[] EncodePng(BitmapSource bitmap)
    {
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = new MemoryStream();
        encoder.Save(stream);
        return stream.ToArray();
    }

    private static string ToHex(Color color, bool includeAlpha)
    {
        return includeAlpha
            ? $"#{color.A:X2}{color.R:X2}{color.G:X2}{color.B:X2}"
            : $"#{color.R:X2}{color.G:X2}{color.B:X2}";
    }

    private static double CalculateContrast(Color foreground, Color background)
    {
        var l1 = RelativeLuminance(foreground);
        var l2 = RelativeLuminance(background);
        var brighter = Math.Max(l1, l2);
        var darker = Math.Min(l1, l2);
        return (brighter + 0.05) / (darker + 0.05);
    }

    private static double RelativeLuminance(Color color)
    {
        static double ConvertChannel(byte channel)
        {
            var value = channel / 255.0;
            return value <= 0.03928 ? value / 12.92 : Math.Pow((value + 0.055) / 1.055, 2.4);
        }

        var r = ConvertChannel(color.R);
        var g = ConvertChannel(color.G);
        var b = ConvertChannel(color.B);
        return (0.2126 * r) + (0.7152 * g) + (0.0722 * b);
    }
}
