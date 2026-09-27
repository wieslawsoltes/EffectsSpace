using EffectsSpace.Controls;
using EffectsSpace.Documents;
using EffectsSpace.Editing;
using EffectsSpace.Workbench;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using SkiaSharp;
using Windows.Storage;

namespace EffectsSpace.App;

public partial class App : Application
{
    private Window? _window;
    private StudioWorkbench? _workbench;
    private SKTypeface? _typeface;
    public App() { InitializeComponent(); RequestedTheme = ApplicationTheme.Dark; }
    protected override async void OnLaunched(LaunchActivatedEventArgs args)
    {
        _window = new Window { Title = "EffectsSpace" };
        _window.Content = new Grid { Background = Studio.Brush("#202020"), Children = { new TextBlock { Text = "EffectsSpace\nLoading motion workspace…", FontSize = 20, Foreground = Studio.Brush("#BCA9E8"), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center } } }; _window.Activate();
        try
        {
            try
            {
                var font = await StorageFile.GetFileFromApplicationUriAsync(new Uri("ms-appx:///Assets/Fonts/Inter.ttf")); using var input = await font.OpenStreamForReadAsync(); using var bytes = new MemoryStream(); await input.CopyToAsync(bytes); using var data = SKData.CreateCopy(bytes.ToArray());
                _typeface = SKTypeface.FromData(data); if (_typeface is not null) Studio.Typeface = _typeface;
                Studio.Font = new FontFamily("ms-appx:///Assets/Fonts/Inter.ttf#Inter");
            }
            catch (Exception ex) { Console.WriteLine("Optional font unavailable: " + ex.Message); }
#if __WASM__
            IWorkspaceStorage storage = new BrowserWorkspaceStorage();
#else
            IWorkspaceStorage storage = new DesktopWorkspaceStorage();
#endif
            var project = SampleProject.Create(); string? warning = null; var restored = false;
            try { var saved = await storage.ReadRecoveryAsync(); if (!string.IsNullOrWhiteSpace(saved)) { project = ProjectJson.Load(saved); restored = true; } }
            catch (Exception ex) { warning = "Recovery copy could not be opened: " + ex.Message + ". It has not been deleted."; }
            var session = new EditorSession(project);
            if (!restored) { session.SetTime(2.4); session.Select(session.Composition.Layers.FirstOrDefault(l => l.Name == "ORBITAL")?.Id); }
            _workbench = new StudioWorkbench(session, storage); _workbench.Viewer.Renderer.Typeface = Studio.Typeface;
            _window.Content = _workbench;
            _window.Closed += (_, _) => { _workbench.Dispose(); _typeface?.Dispose(); };
            _window.Activated += (_, e) => { if (e.WindowActivationState == WindowActivationState.Deactivated) { _workbench.Viewer.SpaceDown = false; _workbench.Pause(); } };
            if (warning is not null) _workbench.ShowStatus(warning, true);
#if __WASM__
            BrowserDiagnostics.Attach(session, _workbench);
#endif
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex);
            _window.Content = new ScrollViewer { Content = new TextBlock { Text = "EffectsSpace could not start.\n\n" + ex + "\n\nNo saved project has been deleted.", TextWrapping = TextWrapping.Wrap, Foreground = Studio.Brush("#E8B0AB"), Margin = new Thickness(30), FontSize = 14 } };
        }
    }
}
