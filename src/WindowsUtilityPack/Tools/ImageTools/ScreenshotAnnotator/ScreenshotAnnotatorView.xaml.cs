using System.Windows.Controls;
using System.Windows.Input;
using System.Windows;
using System.Windows.Media;

namespace WindowsUtilityPack.Tools.ImageTools.ScreenshotAnnotator;

public partial class ScreenshotAnnotatorView : UserControl
{
    private enum DragKind
    {
        None,
        Create,
        Move,
        Resize,
    }

    private DragKind _dragKind;

    public ScreenshotAnnotatorView()
    {
        InitializeComponent();
    }

    private void OnPreviewSurfaceMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is not ScreenshotAnnotatorViewModel vm)
        {
            return;
        }

        var position = e.GetPosition(PreviewSurface);
        if (!vm.BeginInteractiveAnnotation(position.X, position.Y))
        {
            return;
        }

        _dragKind = DragKind.Create;
        PreviewSurface.CaptureMouse();
        e.Handled = true;
    }

    private void OnPreviewSurfaceMouseMove(object sender, MouseEventArgs e)
    {
        if (_dragKind == DragKind.None || DataContext is not ScreenshotAnnotatorViewModel vm)
        {
            return;
        }

        var position = e.GetPosition(PreviewSurface);
        if (_dragKind == DragKind.Create)
        {
            vm.UpdateInteractiveAnnotation(position.X, position.Y);
        }
        else
        {
            vm.UpdateMoveOrResize(position.X, position.Y);
        }
    }

    private void OnPreviewSurfaceMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_dragKind == DragKind.None || DataContext is not ScreenshotAnnotatorViewModel vm)
        {
            return;
        }

        var position = e.GetPosition(PreviewSurface);
        if (_dragKind == DragKind.Create)
        {
            vm.UpdateInteractiveAnnotation(position.X, position.Y);
            vm.CommitInteractiveAnnotation();
        }
        else
        {
            vm.UpdateMoveOrResize(position.X, position.Y);
            vm.CommitMoveOrResize();
        }
        ReleasePreviewDrag();
        e.Handled = true;
    }

    private void OnPreviewSurfaceMouseLeave(object sender, MouseEventArgs e)
    {
        if (_dragKind == DragKind.None || e.LeftButton == MouseButtonState.Pressed || DataContext is not ScreenshotAnnotatorViewModel vm)
        {
            return;
        }

        if (_dragKind == DragKind.Create)
        {
            vm.CancelInteractiveAnnotation();
        }
        else
        {
            vm.CancelMoveOrResize();
        }
        ReleasePreviewDrag();
    }

    private void ReleasePreviewDrag()
    {
        _dragKind = DragKind.None;
        if (PreviewSurface.IsMouseCaptured)
        {
            PreviewSurface.ReleaseMouseCapture();
        }
    }

    private void OnAnnotationMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is not ScreenshotAnnotatorViewModel vm)
        {
            return;
        }

        if (sender is not ContentPresenter presenter || presenter.Content is not AnnotationRow annotation)
        {
            return;
        }

        vm.SelectedAnnotation = annotation;

        if (e.ClickCount >= 2 && vm.BeginEditSelectedText())
        {
            _dragKind = DragKind.None;
            e.Handled = true;
            Dispatcher.BeginInvoke(() =>
            {
                var textBox = FindVisualChild<TextBox>(presenter, "EditTextBox");
                if (textBox is null)
                {
                    return;
                }

                textBox.Focus();
                textBox.SelectAll();
            });
            return;
        }

        var position = e.GetPosition(PreviewSurface);
        if (vm.BeginMoveSelected(position.X, position.Y))
        {
            _dragKind = DragKind.Move;
            PreviewSurface.CaptureMouse();
            e.Handled = true;
        }
    }

    private void OnResizeHandleMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is not ScreenshotAnnotatorViewModel vm || vm.SelectedAnnotation is null)
        {
            return;
        }

        if (sender is not FrameworkElement element || element.Tag is not string tag)
        {
            return;
        }

        var handle = tag switch
        {
            "TopLeft" => ScreenshotAnnotatorViewModel.ResizeHandle.TopLeft,
            "TopRight" => ScreenshotAnnotatorViewModel.ResizeHandle.TopRight,
            "BottomLeft" => ScreenshotAnnotatorViewModel.ResizeHandle.BottomLeft,
            "BottomRight" => ScreenshotAnnotatorViewModel.ResizeHandle.BottomRight,
            "ArrowStart" => ScreenshotAnnotatorViewModel.ResizeHandle.ArrowStart,
            "ArrowEnd" => ScreenshotAnnotatorViewModel.ResizeHandle.ArrowEnd,
            _ => (ScreenshotAnnotatorViewModel.ResizeHandle?)null,
        };

        if (handle is null)
        {
            return;
        }

        var position = e.GetPosition(PreviewSurface);
        if (vm.BeginResizeSelected(handle.Value, position.X, position.Y))
        {
            _dragKind = DragKind.Resize;
            PreviewSurface.CaptureMouse();
            e.Handled = true;
        }
    }

    private void OnRootPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (DataContext is not ScreenshotAnnotatorViewModel vm)
        {
            return;
        }

        if (e.Key == Key.Escape && vm.SelectedAnnotation?.IsEditing == true)
        {
            vm.CancelEditSelectedText();
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Escape && vm.IsInteractiveAnnotationActive)
        {
            vm.CancelInteractiveAnnotation();
            vm.CancelMoveOrResize();
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Enter && vm.SelectedAnnotation?.IsEditing == true)
        {
            vm.CommitEditSelectedText();
            e.Handled = true;
            return;
        }

        var isCtrl = Keyboard.Modifiers.HasFlag(ModifierKeys.Control);
        var isShift = Keyboard.Modifiers.HasFlag(ModifierKeys.Shift);

        if (isCtrl && (e.Key == Key.Z || e.Key == Key.Y))
        {
            if (e.Key == Key.Z && isShift)
            {
                if (vm.RedoCommand.CanExecute(null))
                {
                    vm.RedoCommand.Execute(null);
                }
            }
            else if (e.Key == Key.Z)
            {
                if (vm.UndoCommand.CanExecute(null))
                {
                    vm.UndoCommand.Execute(null);
                }
            }
            else if (e.Key == Key.Y)
            {
                if (vm.RedoCommand.CanExecute(null))
                {
                    vm.RedoCommand.Execute(null);
                }
            }

            e.Handled = true;
            return;
        }

        if (isCtrl && e.Key == Key.C)
        {
            vm.CopySelectedAnnotationToClipboard();
            e.Handled = true;
            return;
        }

        if (isCtrl && e.Key == Key.V)
        {
            vm.PasteAnnotationFromClipboard();
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Delete)
        {
            vm.DeleteSelectedAnnotation();
            e.Handled = true;
            return;
        }

        var step = isShift ? 10 : 1;

        if (e.Key == Key.Left)
        {
            vm.NudgeSelectedAnnotation(-1, 0, step);
            e.Handled = true;
        }
        else if (e.Key == Key.Right)
        {
            vm.NudgeSelectedAnnotation(1, 0, step);
            e.Handled = true;
        }
        else if (e.Key == Key.Up)
        {
            vm.NudgeSelectedAnnotation(0, -1, step);
            e.Handled = true;
        }
        else if (e.Key == Key.Down)
        {
            vm.NudgeSelectedAnnotation(0, 1, step);
            e.Handled = true;
        }
    }

    private void OnTextEditorLostFocus(object sender, RoutedEventArgs e)
    {
        if (DataContext is ScreenshotAnnotatorViewModel vm)
        {
            vm.CommitEditSelectedText();
        }
    }

    private void OnTextEditorKeyDown(object sender, KeyEventArgs e)
    {
        if (DataContext is not ScreenshotAnnotatorViewModel vm)
        {
            return;
        }

        if (e.Key == Key.Escape)
        {
            vm.CancelEditSelectedText();
            e.Handled = true;
        }
        else if (e.Key == Key.Enter && !Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
        {
            vm.CommitEditSelectedText();
            e.Handled = true;
        }
    }

    private static TChild? FindVisualChild<TChild>(DependencyObject parent, string? name = null)
        where TChild : FrameworkElement
    {
        var count = VisualTreeHelper.GetChildrenCount(parent);
        for (var i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is TChild match && (name is null || string.Equals(match.Name, name, StringComparison.Ordinal)))
            {
                return match;
            }

            var nested = FindVisualChild<TChild>(child, name);
            if (nested is not null)
            {
                return nested;
            }
        }

        return null;
    }
}
