using PooSweep.Services;

namespace PooSweep.Pages;

public partial class HowToPlayPage : ContentPage
{
    private readonly Settings _settings;

    public HowToPlayPage(Settings settings)
    {
        InitializeComponent();
        Shell.SetBackButtonBehavior(this, new BackButtonBehavior { IsVisible = false });
        _settings = settings;

        var desktop = DeviceInfo.Idiom == DeviceIdiom.Desktop;
        DigText.Text = desktop
            ? "Left-click a patch of grass to dig it up."
            : "Tap a patch of grass to dig it up.";
        FlagText.Text = desktop
            ? "Right-click where you think a poo is to plant a warning flag. Right-click again to pull it up. Or flip the switch at the bottom to Flag mode."
            : "Press and hold where you think a poo is to plant a warning flag. Or flip the switch at the bottom to Flag mode and just tap.";
        ChordText.Text = desktop
            ? "Once a number has the right number of flags around it, click it (or middle-click) to dig everything else around it in one go."
            : "Once a number has the right number of flags around it, tap it to dig everything else around it in one go.";
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        Scene.Palette = _settings.Palette;
    }

    private async void OnBack(object? sender, EventArgs e) => await Shell.Current.GoToAsync("..");
}
