using PooSweep.Core.Audio;
using PooSweep.Core.Stats;
using PooSweep.Graphics;
using PooSweep.Services;

namespace PooSweep.Pages;

public partial class SettingsPage : ContentPage
{
    private readonly Settings _settings;
    private readonly AudioService _audio;
    private readonly StatsBook _stats;
    private bool _loading;

    public SettingsPage(Settings settings, AudioService audio, StatsBook stats)
    {
        InitializeComponent();
        Shell.SetBackButtonBehavior(this, new BackButtonBehavior { IsVisible = false });
        _settings = settings;
        _audio = audio;
        _stats = stats;
        HapticsRow.IsVisible = DeviceInfo.Idiom != DeviceIdiom.Desktop;
        VersionLabel.Text = $"PooSweep {AppInfo.Current.Version.Major}.{AppInfo.Current.Version.Minor}\nEvery note of music and every squelch is synthesized live on your device.";
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _loading = true;
        MusicSlider.Value = _settings.MusicVolume;
        SfxSlider.Value = _settings.SfxVolume;
        MuteSwitch.IsToggled = _settings.Muted;
        ChordSwitch.IsToggled = _settings.ChordOnTap;
        HapticsSwitch.IsToggled = _settings.Haptics;
        ColorblindSwitch.IsToggled = _settings.Colorblind;
        MotionSwitch.IsToggled = _settings.ReduceMotion;
        _loading = false;
        UpdateLabels();
        UpdateTheme();
    }

    private void UpdateLabels()
    {
        MusicValue.Text = $"{MusicSlider.Value:P0}";
        SfxValue.Text = $"{SfxSlider.Value:P0}";
    }

    private void UpdateTheme()
    {
        Scene.Palette = _settings.Palette;
        var day = _settings.Theme == BoardTheme.Day;
        DayButton.Opacity = day ? 1 : 0.55;
        DuskButton.Opacity = day ? 0.55 : 1;
        DayButton.Text = day ? "✓ SUNNY DAY" : "SUNNY DAY";
        DuskButton.Text = day ? "DUSK" : "✓ DUSK";
    }

    private void OnMusicChanged(object? sender, ValueChangedEventArgs e)
    {
        if (_loading) return;
        _settings.MusicVolume = e.NewValue;
        UpdateLabels();
    }

    private void OnSfxChanged(object? sender, ValueChangedEventArgs e)
    {
        if (_loading) return;
        _settings.SfxVolume = e.NewValue;
        UpdateLabels();
    }

    private void OnSfxDragCompleted(object? sender, EventArgs e) => _audio.PlayDig();

    private void OnMuteToggled(object? sender, ToggledEventArgs e)
    {
        if (_loading) return;
        _settings.Muted = e.Value;
        if (!e.Value)
        {
            _audio.PlayMusic(MusicTrack.Title);
            _audio.Play(Sfx.UiToggle);
        }
    }

    private void OnChordToggled(object? sender, ToggledEventArgs e)
    {
        if (_loading) return;
        _settings.ChordOnTap = e.Value;
        _audio.Play(Sfx.UiToggle);
    }

    private void OnHapticsToggled(object? sender, ToggledEventArgs e)
    {
        if (_loading) return;
        _settings.Haptics = e.Value;
        _audio.Play(Sfx.UiToggle);
        if (e.Value) ServiceHelper.Get<Haptics>()?.Press();
    }

    private void OnColorblindToggled(object? sender, ToggledEventArgs e)
    {
        if (_loading) return;
        _settings.Colorblind = e.Value;
        _audio.Play(Sfx.UiToggle);
    }

    private void OnMotionToggled(object? sender, ToggledEventArgs e)
    {
        if (_loading) return;
        _settings.ReduceMotion = e.Value;
        _audio.Play(Sfx.UiToggle);
    }

    private void OnDay(object? sender, EventArgs e)
    {
        _settings.Theme = BoardTheme.Day;
        UpdateTheme();
    }

    private void OnDusk(object? sender, EventArgs e)
    {
        _settings.Theme = BoardTheme.Dusk;
        UpdateTheme();
    }

    private async void OnResetStats(object? sender, EventArgs e)
    {
        if (!await DisplayAlertAsync("Reset all stats?", "This wipes every best time, streak and win count. There's no undo.", "Reset", "Cancel"))
            return;
        _stats.ResetAll();
        _audio.Play(Sfx.Splat, 0.6);
        await DisplayAlertAsync("Done", "Fresh lawn, fresh start.", "OK");
    }

    private async void OnBack(object? sender, EventArgs e) => await Shell.Current.GoToAsync("..");
}
