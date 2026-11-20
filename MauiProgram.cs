using Microsoft.Extensions.Logging;
using Plugin.Maui.Audio;
using PooSweep.Core.Stats;
using PooSweep.Pages;
using PooSweep.Services;
using SkiaSharp.Views.Maui.Controls.Hosting;

namespace PooSweep;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .UseSkiaSharp()
            .AddAudio()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                fonts.AddFont("LuckiestGuy-Regular.ttf", "Display");
            });

        builder.Services.AddSingleton<Settings>();
        builder.Services.AddSingleton<IKeyValueStore, PreferencesStore>();
        builder.Services.AddSingleton<StatsBook>();
        builder.Services.AddSingleton<AudioService>();
        builder.Services.AddSingleton<Haptics>();
        builder.Services.AddTransient<GameSession>();

        builder.Services.AddTransient<TitlePage>();
        builder.Services.AddTransient<GamePage>();
        builder.Services.AddTransient<SettingsPage>();
        builder.Services.AddTransient<StatsPage>();
        builder.Services.AddTransient<HowToPlayPage>();

#if DEBUG
        builder.Logging.AddDebug();
#endif

        var app = builder.Build();
        ServiceHelper.Services = app.Services;
        return app;
    }
}
