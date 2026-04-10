using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using System.IO;
using WindowsUtilityPack.Services.ImageTools;
using Xunit;

namespace WindowsUtilityPack.Tests.Services;

public sealed class ImageProcessingServiceTests
{
    [Fact]
    public async Task ConvertAsync_ConvertsPngToJpeg()
    {
        var service = new ImageProcessingService();
        var root = Path.Combine(Path.GetTempPath(), "wup-image-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);

        try
        {
            var input = Path.Combine(root, "input.png");
            using (var image = new Image<Rgba32>(64, 64, new Rgba32(255, 0, 0)))
            {
                await image.SaveAsPngAsync(input);
            }

            var outputDir = Path.Combine(root, "out");
            var results = await service.ConvertAsync(new ImageConvertRequest
            {
                InputPaths = [input],
                OutputDirectory = outputDir,
                OutputFormat = ImageOutputFormat.Jpeg,
                Quality = 80,
            }, CancellationToken.None);

            Assert.Single(results);
            Assert.True(results[0].Success, results[0].ErrorMessage);
            Assert.True(File.Exists(results[0].OutputPath));
        }
        finally
        {
            TryDelete(root);
        }
    }

    [Fact]
    public async Task AnnotateAsync_AddsOverlayAndWritesOutput()
    {
        var service = new ImageProcessingService();
        var root = Path.Combine(Path.GetTempPath(), "wup-annotate-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);

        try
        {
            var input = Path.Combine(root, "input.png");
            var output = Path.Combine(root, "annotated.png");

            using (var image = new Image<Rgba32>(120, 80, new Rgba32(255, 255, 255)))
            {
                await image.SaveAsPngAsync(input);
            }

            var result = await service.AnnotateAsync(
                input,
                output,
                [new ImageAnnotation { Type = AnnotationType.Rectangle, X = 10, Y = 10, Width = 60, Height = 30, ColorHex = "#FF0000", StrokeThickness = 2 }],
                ImageOutputFormat.Png,
                quality: 90,
                CancellationToken.None);

            Assert.True(result.Success, result.ErrorMessage);
            Assert.True(File.Exists(output));
            Assert.True(result.OutputBytes > 0);
        }
        finally
        {
            TryDelete(root);
        }
    }

    private static void TryDelete(string directory)
    {
        try
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, recursive: true);
        }
        catch
        {
            // Best effort cleanup.
        }
    }
}
