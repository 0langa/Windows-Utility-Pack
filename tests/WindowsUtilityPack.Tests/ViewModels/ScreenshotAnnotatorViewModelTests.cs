using WindowsUtilityPack.Services;
using WindowsUtilityPack.Services.ImageTools;
using WindowsUtilityPack.Tools.ImageTools.ScreenshotAnnotator;
using Xunit;

namespace WindowsUtilityPack.Tests.ViewModels;

public sealed class ScreenshotAnnotatorViewModelTests
{
    [Fact]
    public void SelectedAnnotation_PopulatesEditorFields()
    {
        var vm = CreateViewModel();
        vm.Annotations.Add(new AnnotationRow
        {
            Type = "Text",
            X = 42,
            Y = 24,
            Width = 220,
            Height = 60,
            Text = "Hello",
            Color = "#00FF00",
            Thickness = 4,
        });

        vm.SelectedAnnotation = vm.Annotations[0];

        Assert.Equal("Text", vm.AnnotationType);
        Assert.Equal(42, vm.X);
        Assert.Equal(24, vm.Y);
        Assert.Equal("Hello", vm.AnnotationText);
        Assert.Equal("#00FF00", vm.AnnotationColor);
    }

    [Fact]
    public void ApplyEditorToSelected_UpdatesSelectedAnnotation()
    {
        var vm = CreateViewModel();
        vm.AddAnnotationCommand.Execute(null);
        vm.SelectedAnnotation = vm.Annotations[0];
        vm.AnnotationType = "Arrow";
        vm.X = 10;
        vm.Y = 20;
        vm.Width = 300;
        vm.Height = 150;
        vm.AnnotationText = "Updated";
        vm.AnnotationColor = "#112233";
        vm.AnnotationThickness = 8;

        vm.ApplyEditorToSelectedCommand.Execute(null);

        Assert.Equal("Arrow", vm.Annotations[0].Type);
        Assert.Equal(10, vm.Annotations[0].X);
        Assert.Equal(20, vm.Annotations[0].Y);
        Assert.Equal("Updated", vm.Annotations[0].Text);
        Assert.Equal("#112233", vm.Annotations[0].Color);
        Assert.Equal(8, vm.Annotations[0].Thickness);
    }

    [Fact]
    public void DuplicateSelected_CreatesOffsetCopy()
    {
        var vm = CreateViewModel();
        vm.AddAnnotationCommand.Execute(null);
        vm.SelectedAnnotation = vm.Annotations[0];

        vm.DuplicateSelectedAnnotationCommand.Execute(null);

        Assert.Equal(2, vm.Annotations.Count);
        Assert.Equal(vm.Annotations[0].X + 12, vm.Annotations[1].X);
        Assert.Equal(vm.Annotations[0].Y + 12, vm.Annotations[1].Y);
    }

    private static ScreenshotAnnotatorViewModel CreateViewModel()
        => new(new NoopImageProcessingService(), new TestClipboardService());

    private sealed class TestClipboardService : IClipboardService
    {
        public bool TryGetText(out string text)
        {
            text = string.Empty;
            return false;
        }

        public void SetText(string text)
        {
        }

        public bool TrySetImage(System.Windows.Media.Imaging.BitmapSource image) => true;
    }

    private sealed class NoopImageProcessingService : IImageProcessingService
    {
        public Task<ScreenshotCaptureResult> CaptureScreenshotAsync(string outputPath, CancellationToken cancellationToken)
            => Task.FromResult(new ScreenshotCaptureResult { Success = false, ErrorMessage = "not used" });

        public Task<IReadOnlyList<ImageProcessResult>> ConvertAsync(ImageConvertRequest request, CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<ImageProcessResult>>([]);

        public Task<IReadOnlyList<ImageProcessResult>> ResizeAsync(ImageResizeRequest request, CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<ImageProcessResult>>([]);

        public Task<ImageProcessResult> AnnotateAsync(string inputPath, string outputPath, IReadOnlyList<ImageAnnotation> annotations, ImageOutputFormat outputFormat, int quality, CancellationToken cancellationToken)
            => Task.FromResult(new ImageProcessResult { Success = false, ErrorMessage = "not used" });
    }
}
