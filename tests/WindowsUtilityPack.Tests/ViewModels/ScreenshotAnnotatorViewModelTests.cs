using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
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

    [Fact]
    public void Undo_AfterAddAnnotation_RestoresPreviousState()
    {
        var vm = CreateViewModel();

        vm.AddAnnotationCommand.Execute(null);

        Assert.True(vm.CanUndo);
        Assert.Single(vm.Annotations);

        vm.UndoCommand.Execute(null);

        Assert.Empty(vm.Annotations);
        Assert.False(vm.CanUndo);
        Assert.True(vm.CanRedo);
    }

    [Fact]
    public void Redo_AfterUndo_ReappliesAnnotation()
    {
        var vm = CreateViewModel();
        vm.AddAnnotationCommand.Execute(null);
        vm.UndoCommand.Execute(null);

        vm.RedoCommand.Execute(null);

        Assert.Single(vm.Annotations);
        Assert.True(vm.CanUndo);
        Assert.False(vm.CanRedo);
    }

    [Fact]
    public void ApplyEditorToSelected_CanBeUndone()
    {
        var vm = CreateViewModel();
        vm.AddAnnotationCommand.Execute(null);
        vm.SelectedAnnotation = vm.Annotations[0];
        vm.AnnotationText = "Changed";

        vm.ApplyEditorToSelectedCommand.Execute(null);
        vm.UndoCommand.Execute(null);

        Assert.Equal("Note", vm.Annotations[0].Text);
    }

    [Fact]
    public void LoadLastQuickCapture_PopulatesPreviewDimensions()
    {
        var imagePath = CreateTestImageFile();
        try
        {
            var vm = new ScreenshotAnnotatorViewModel(
                new NoopImageProcessingService(),
                new TestClipboardService(),
                new StubQuickCaptureStateService { LastCapturePath = imagePath });

            Assert.Equal(120, vm.PreviewPixelWidth);
            Assert.Equal(80, vm.PreviewPixelHeight);
            Assert.Equal(imagePath, vm.ImagePath);
        }
        finally
        {
            if (File.Exists(imagePath))
            {
                File.Delete(imagePath);
            }
        }
    }

    [Fact]
    public void InteractiveAnnotationDrag_CreatesAnnotationOnCommit()
    {
        var imagePath = CreateTestImageFile();
        try
        {
            var vm = new ScreenshotAnnotatorViewModel(
                new NoopImageProcessingService(),
                new TestClipboardService(),
                new StubQuickCaptureStateService { LastCapturePath = imagePath });

            Assert.True(vm.BeginInteractiveAnnotation(10, 15));

            vm.UpdateInteractiveAnnotation(90, 65);
            var committed = vm.CommitInteractiveAnnotation();

            Assert.True(committed);
            Assert.Single(vm.Annotations);
            Assert.Equal(10, vm.Annotations[0].X);
            Assert.Equal(15, vm.Annotations[0].Y);
            Assert.Equal(80, vm.Annotations[0].Width);
            Assert.Equal(50, vm.Annotations[0].Height);
            Assert.False(vm.IsInteractiveAnnotationActive);
            Assert.Null(vm.DragPreviewAnnotation);
        }
        finally
        {
            if (File.Exists(imagePath))
            {
                File.Delete(imagePath);
            }
        }
    }

    [Fact]
    public void InteractiveAnnotationCommit_RejectsTinyDrag()
    {
        var imagePath = CreateTestImageFile();
        try
        {
            var vm = new ScreenshotAnnotatorViewModel(
                new NoopImageProcessingService(),
                new TestClipboardService(),
                new StubQuickCaptureStateService { LastCapturePath = imagePath });

            vm.BeginInteractiveAnnotation(10, 10);
            vm.UpdateInteractiveAnnotation(12, 12);

            var committed = vm.CommitInteractiveAnnotation();

            Assert.False(committed);
            Assert.Empty(vm.Annotations);
            Assert.Equal("Annotation drag was too small to add.", vm.StatusMessage);
        }
        finally
        {
            if (File.Exists(imagePath))
            {
                File.Delete(imagePath);
            }
        }
    }

    [Fact]
    public void InteractiveAnnotationCancel_ClearsPreviewState()
    {
        var imagePath = CreateTestImageFile();
        try
        {
            var vm = new ScreenshotAnnotatorViewModel(
                new NoopImageProcessingService(),
                new TestClipboardService(),
                new StubQuickCaptureStateService { LastCapturePath = imagePath });

            vm.BeginInteractiveAnnotation(5, 5);
            vm.UpdateInteractiveAnnotation(25, 35);

            Assert.True(vm.IsInteractiveAnnotationActive);
            Assert.NotNull(vm.DragPreviewAnnotation);

            vm.CancelInteractiveAnnotation();

            Assert.False(vm.IsInteractiveAnnotationActive);
            Assert.Null(vm.DragPreviewAnnotation);
            Assert.Equal("Annotation drag cancelled.", vm.StatusMessage);
        }
        finally
        {
            if (File.Exists(imagePath))
            {
                File.Delete(imagePath);
            }
        }
    }

    [Fact]
    public void MoveSelected_UpdatesPosition_AndClampsToPreviewBounds()
    {
        var imagePath = CreateTestImageFile();
        try
        {
            var vm = new ScreenshotAnnotatorViewModel(
                new NoopImageProcessingService(),
                new TestClipboardService(),
                new StubQuickCaptureStateService { LastCapturePath = imagePath });

            vm.AnnotationType = "Rectangle";
            vm.X = 10;
            vm.Y = 10;
            vm.Width = 40;
            vm.Height = 20;
            vm.AddAnnotationCommand.Execute(null);
            vm.SelectedAnnotation = vm.Annotations[0];

            Assert.True(vm.BeginMoveSelected(10, 10));

            vm.UpdateMoveOrResize(500, 500);
            vm.CommitMoveOrResize();

            Assert.Equal(80, vm.PreviewPixelHeight);
            Assert.Equal(120, vm.PreviewPixelWidth);
            Assert.Equal(120 - vm.SelectedAnnotation.Width, vm.SelectedAnnotation.X);
            Assert.Equal(80 - vm.SelectedAnnotation.Height, vm.SelectedAnnotation.Y);
        }
        finally
        {
            if (File.Exists(imagePath))
            {
                File.Delete(imagePath);
            }
        }
    }

    [Fact]
    public void ResizeSelected_BottomRight_UpdatesSize()
    {
        var imagePath = CreateTestImageFile();
        try
        {
            var vm = new ScreenshotAnnotatorViewModel(
                new NoopImageProcessingService(),
                new TestClipboardService(),
                new StubQuickCaptureStateService { LastCapturePath = imagePath });

            vm.X = 10;
            vm.Y = 10;
            vm.Width = 20;
            vm.Height = 20;
            vm.AddAnnotationCommand.Execute(null);
            vm.SelectedAnnotation = vm.Annotations[0];

            Assert.True(vm.BeginResizeSelected(ScreenshotAnnotatorViewModel.ResizeHandle.BottomRight, 30, 30));

            vm.UpdateMoveOrResize(70, 55);
            vm.CommitMoveOrResize();

            Assert.Equal(10, vm.SelectedAnnotation.X);
            Assert.Equal(10, vm.SelectedAnnotation.Y);
            Assert.Equal(60, vm.SelectedAnnotation.Width);
            Assert.Equal(45, vm.SelectedAnnotation.Height);
        }
        finally
        {
            if (File.Exists(imagePath))
            {
                File.Delete(imagePath);
            }
        }
    }

    [Fact]
    public void NudgeSelectedAnnotation_MovesByStep()
    {
        var imagePath = CreateTestImageFile();
        try
        {
            var vm = new ScreenshotAnnotatorViewModel(
                new NoopImageProcessingService(),
                new TestClipboardService(),
                new StubQuickCaptureStateService { LastCapturePath = imagePath });

            vm.X = 10;
            vm.Y = 10;
            vm.Width = 10;
            vm.Height = 10;
            vm.AddAnnotationCommand.Execute(null);
            vm.SelectedAnnotation = vm.Annotations[0];

            vm.NudgeSelectedAnnotation(1, -1, 10);

            Assert.Equal(20, vm.SelectedAnnotation.X);
            Assert.Equal(0, vm.SelectedAnnotation.Y);
        }
        finally
        {
            if (File.Exists(imagePath))
            {
                File.Delete(imagePath);
            }
        }
    }

    [Fact]
    public void DeleteSelectedAnnotation_RemovesAnnotation()
    {
        var imagePath = CreateTestImageFile();
        try
        {
            var vm = new ScreenshotAnnotatorViewModel(
                new NoopImageProcessingService(),
                new TestClipboardService(),
                new StubQuickCaptureStateService { LastCapturePath = imagePath });

            vm.AddAnnotationCommand.Execute(null);
            vm.SelectedAnnotation = vm.Annotations[0];

            vm.DeleteSelectedAnnotation();

            Assert.Empty(vm.Annotations);
            Assert.Null(vm.SelectedAnnotation);
        }
        finally
        {
            if (File.Exists(imagePath))
            {
                File.Delete(imagePath);
            }
        }
    }

    private static ScreenshotAnnotatorViewModel CreateViewModel()
        => new(new NoopImageProcessingService(), new TestClipboardService());

    private static string CreateTestImageFile()
    {
        var path = Path.Combine(Path.GetTempPath(), $"annotator-test-{Guid.NewGuid():N}.png");
        var pixels = new byte[120 * 80 * 4];
        for (var index = 0; index < pixels.Length; index += 4)
        {
            pixels[index] = 0xC0;
            pixels[index + 1] = 0x7A;
            pixels[index + 2] = 0x2C;
            pixels[index + 3] = 0xFF;
        }

        var bitmap = BitmapSource.Create(120, 80, 96, 96, PixelFormats.Bgra32, null, pixels, 120 * 4);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));

        using var stream = File.Create(path);
        encoder.Save(stream);
        return path;
    }

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

    private sealed class StubQuickCaptureStateService : IQuickCaptureStateService
    {
        public string LastCapturePath { get; set; } = string.Empty;
    }
}
