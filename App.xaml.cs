using PooSweep.Services;

namespace PooSweep;

public partial class App : Application
{
    private readonly AudioService _audio;

    public App(AudioService audio)
    {
        InitializeComponent();
        _audio = audio;

        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            System.Diagnostics.Debug.WriteLine($"[UnhandledException] IsTerminating={e.IsTerminating} Exception={e.ExceptionObject}");

        TaskScheduler.UnobservedTaskException += (_, e) =>
            System.Diagnostics.Debug.WriteLine($"[UnobservedTaskException] Exception={e.Exception}");

        // Start synthesizing music & sound effects straight away, off the UI thread.
        _ = _audio.InitializeAsync();
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        var window = new Window(new AppShell())
        {
            Title = "PooSweep!",
        };

        if (DeviceInfo.Idiom == DeviceIdiom.Desktop)
        {
            window.Width = 820;
            window.Height = 900;
            window.MinimumWidth = 420;
            window.MinimumHeight = 560;
        }

        // Pause the music when the app goes to the background.
        window.Stopped += (_, _) => _audio.Suspend();
        window.Deactivated += (_, _) =>
        {
            if (DeviceInfo.Platform == DevicePlatform.Android || DeviceInfo.Platform == DevicePlatform.iOS) _audio.Suspend();
        };
        window.Resumed += (_, _) => _audio.Resume();
        window.Activated += (_, _) => _audio.Resume();

        return window;
    }
}
