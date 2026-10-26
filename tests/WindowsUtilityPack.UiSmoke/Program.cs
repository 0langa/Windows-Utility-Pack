using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using WindowsUtilityPack;
using WindowsUtilityPack.Services;
using WindowsUtilityPack.Tools;
using WindowsUtilityPack.Tools.ImageTools.ScreenshotAnnotator;

// A real WPF composition/layout probe. This does not replace interaction tests.
internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        var reportPath = Path.GetFullPath(args.FirstOrDefault() ?? "output/revival/ui-smoke.json");
        Directory.CreateDirectory(Path.GetDirectoryName(reportPath)!);
        if (args.Contains("--external-downloads", StringComparer.Ordinal))
            return ExternalDownloadAcceptance.RunAsync(reportPath, args[2], args.Contains("--gallery-only", StringComparer.Ordinal)).GetAwaiter().GetResult();
        if (args.Contains("--sandbox-services", StringComparer.Ordinal))
            return SandboxAcceptance.Run(reportPath);
        var profile = Path.Combine(Path.GetDirectoryName(reportPath)!, "profile-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(profile);
        AppContext.SetData("WindowsUtilityPack.DataDirectory", profile);
        // Disable machine integrations for the isolated test profile.
        File.WriteAllText(Path.Combine(profile, "settings.json"), """
            { "RememberWindowPosition": false,
              "HotkeysEnabled": false, "TrayModeEnabled": false, "CloseToTray": false,
              "ClipboardMonitoringEnabled": false, "ClipboardMonitoringConsentAccepted": false }
            """);

        var app = new App();
        app.InitializeComponent();
        app.StartupUri = new Uri("/WindowsUtilityPack;component/MainWindow.xaml", UriKind.Relative);
        // Manual UI checks use the same isolated profile without running the automatic sweep.
        if (args.Contains("--interactive", StringComparer.Ordinal))
            return app.Run();
        var observations = new List<Observation>();
        Observation? current = null;
        using var watchdog = new System.Threading.Timer(_ =>
        {
            File.WriteAllText(reportPath + ".timeout.txt", $"WPF smoke timed out near {current?.Key ?? "startup"}.");
            Environment.Exit(2);
        }, null, TimeSpan.FromMinutes(3), Timeout.InfiniteTimeSpan);
        var bindingTrace = new BindingTrace(message => current?.BindingErrors.Add(message));
        PresentationTraceSources.DataBindingSource.Listeners.Add(bindingTrace);
        PresentationTraceSources.DataBindingSource.Switch.Level = SourceLevels.Error;
        app.DispatcherUnhandledException += (_, e) =>
        {
            (current ??= new Observation("startup", "Startup")).Exceptions.Add(e.Exception.ToString());
            if (!observations.Contains(current)) observations.Add(current);
            e.Handled = true;
        };

        app.Startup += (_, _) => app.Dispatcher.BeginInvoke(async () =>
        {
            try
            {
                foreach (var theme in args.Contains("--annotation-layout-only", StringComparer.Ordinal)
                    ? Array.Empty<AppTheme>() : new[] { AppTheme.Dark, AppTheme.Light, AppTheme.Aurora })
                {
                    App.ThemeService.SetTheme(theme);
                    foreach (var tool in ToolRegistry.All.ToArray())
                    {
                        current = new Observation(tool.Key, tool.Name) { Theme = theme.ToString() };
                        observations.Add(current);
                        try
                        {
                            App.NavigationService.NavigateTo(tool.Key);
                            await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
                            app.MainWindow.UpdateLayout();
                            await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
                            current.ViewModel = App.NavigationService.CurrentViewModel.GetType().FullName;
                            current.HasTemplate = app.TryFindResource(new DataTemplateKey(App.NavigationService.CurrentViewModel.GetType())) is DataTemplate;
                            var toolView = Descendants(app.MainWindow).OfType<UserControl>()
                                .FirstOrDefault(view => ReferenceEquals(view.DataContext, App.NavigationService.CurrentViewModel));
                            if (toolView is null || toolView.Content is null)
                                current.Exceptions.Add("The registered tool template produced an empty view (check InitializeComponent).");
                            // Keep timer/subscription lifetime bounded between views.
                            App.NavigationService.ClearHistory();
                        }
                        catch (Exception ex)
                        {
                            current.Exceptions.Add(ex.ToString());
                        }
                    }
                }
                current = new Observation("screenshot-pixel-layout", "Screenshot pixel alignment") { Theme = App.ThemeService.CurrentTheme.ToString() };
                observations.Add(current);
                try
                {
                    var fixture = Path.Combine(profile, "preview-300dpi.png");
                    var bitmap = BitmapSource.Create(400, 240, 300, 300, PixelFormats.Bgra32, null, new byte[400 * 240 * 4], 400 * 4);
                    var encoder = new PngBitmapEncoder();
                    encoder.Frames.Add(BitmapFrame.Create(bitmap));
                    using (var stream = File.Create(fixture)) encoder.Save(stream);
                    App.NavigationService.NavigateTo("home");
                    App.QuickCaptureStateService.LastCapturePath = fixture;
                    App.NavigationService.NavigateTo("screenshot-annotator");
                    await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
                    app.MainWindow.UpdateLayout();
                    var viewModel = (ScreenshotAnnotatorViewModel)App.NavigationService.CurrentViewModel;
                    current.HasTemplate = app.TryFindResource(new DataTemplateKey(viewModel.GetType())) is DataTemplate;
                    var preview = Descendants(app.MainWindow).OfType<Image>().Single(item => ReferenceEquals(item.Source, viewModel.PreviewImage));
                    if (Math.Abs(preview.ActualWidth - 400) > 0.1 || Math.Abs(preview.ActualHeight - 240) > 0.1)
                        throw new InvalidDataException($"300-DPI image occupies {preview.ActualWidth}x{preview.ActualHeight} instead of the 400x240 annotation coordinate space.");
                }
                catch (Exception ex) { current.Exceptions.Add(ex.ToString()); }
            }
            catch (Exception ex)
            {
                var failure = new Observation("harness", "Harness");
                failure.Exceptions.Add(ex.ToString());
                observations.Add(failure);
            }
            finally
            {
                File.WriteAllText(reportPath, JsonSerializer.Serialize(observations, new JsonSerializerOptions { WriteIndented = true }));
                app.Shutdown(observations.Any(o => !o.HasTemplate || o.Exceptions.Count > 0 || o.BindingErrors.Count > 0) ? 1 : 0);
            }
        }, DispatcherPriority.ApplicationIdle);

        return app.Run();
    }

    private sealed record Observation(string Key, string Name)
    {
        public string? ViewModel { get; set; }
        public string Theme { get; set; } = string.Empty;
        public bool HasTemplate { get; set; }
        public List<string> Exceptions { get; } = [];
        public List<string> BindingErrors { get; } = [];
    }

    private static IEnumerable<DependencyObject> Descendants(DependencyObject parent)
    {
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(parent); index++)
        {
            var child = VisualTreeHelper.GetChild(parent, index);
            yield return child;
            foreach (var descendant in Descendants(child)) yield return descendant;
        }
    }

    private sealed class BindingTrace(Action<string> record) : TraceListener
    {
        public override void Write(string? message) { if (message is not null) record(message); }
        public override void WriteLine(string? message) { if (message is not null) record(message); }
    }
}
