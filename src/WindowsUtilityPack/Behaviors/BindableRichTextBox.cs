using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;

namespace WindowsUtilityPack.Behaviors;

/// <summary>
/// Adds a bindable <see cref="FlowDocument"/> property to <see cref="RichTextBox"/>.
/// </summary>
public static class BindableRichTextBox
{
    public static readonly DependencyProperty DocumentProperty =
        DependencyProperty.RegisterAttached(
            "Document",
            typeof(FlowDocument),
            typeof(BindableRichTextBox),
            new PropertyMetadata(null, OnDocumentChanged));

    public static FlowDocument? GetDocument(DependencyObject dependencyObject)
    {
        return (FlowDocument?)dependencyObject.GetValue(DocumentProperty);
    }

    public static void SetDocument(DependencyObject dependencyObject, FlowDocument? value)
    {
        dependencyObject.SetValue(DocumentProperty, value);
    }

    private static void OnDocumentChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs e)
    {
        if (dependencyObject is not RichTextBox richTextBox)
        {
            return;
        }

        richTextBox.Document = e.NewValue as FlowDocument ?? new FlowDocument();
    }
}
