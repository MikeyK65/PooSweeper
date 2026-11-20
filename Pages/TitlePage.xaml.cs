using Microsoft.Maui.Controls.Shapes;
using PooSweep.Controls;
using PooSweep.Core;
using PooSweep.Core.Audio;
using PooSweep.Core.Stats;
using PooSweep.Services;

namespace PooSweep.Pages;

public partial class TitlePage : ContentPage
{
    private readonly AudioService _audio;
    private readonly Settings _settings;
    private readonly StatsBook _stats;
    private bool _loadingCustom;
    private bool _openPickerOnReturn;

    public TitlePage(AudioService audio, Settings settings, StatsBook stats)
    {
        InitializeComponent();
        Shell.SetBackButtonBehavior(this, new BackButtonBehavior { IsVisible = false });
        _audio = audio;
        _settings = settings;
        _stats = stats;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        Scene.Palette = _settings.Palette;
        _audio.PlayMusic(MusicTrack.Title);
        _ = _audio.InitializeAsync();
        Picker.IsVisible = false;
        Picker.Opacity = 0;

        if (_openPickerOnReturn)
        {
            _openPickerOnReturn = false;
            Dispatcher.DispatchDelayed(TimeSpan.FromMilliseconds(250), () => _ = ShowPicker());
        }
    }

    // ------------------------------------------------------------------

    private async void OnPlay(object? sender, EventArgs e)
    {
        if (!_settings.SeenHowTo)
        {
            _settings.SeenHowTo = true;
            _openPickerOnReturn = true;
            await Shell.Current.GoToAsync(nameof(HowToPlayPage));
            return;
        }
        await ShowPicker();
    }

    private async void OnHowTo(object? sender, EventArgs e) => await Shell.Current.GoToAsync(nameof(HowToPlayPage));

    private async void OnStats(object? sender, EventArgs e) => await Shell.Current.GoToAsync(nameof(StatsPage));

    private async void OnSettings(object? sender, EventArgs e) => await Shell.Current.GoToAsync(nameof(SettingsPage));

    private async Task ShowPicker()
    {
        BuildDifficultyList();
        CustomEditor.IsVisible = false;
        Picker.IsVisible = true;
        PickerCard.Scale = 0.85;
        await Task.WhenAll(Picker.FadeToAsync(1, 150), PickerCard.ScaleToAsync(1, 260, Easing.SpringOut));
    }

    private async Task HidePicker()
    {
        await Picker.FadeToAsync(0, 120);
        Picker.IsVisible = false;
    }

    private async void OnClosePicker(object? sender, EventArgs e) => await HidePicker();

    private async void OnPickerBackgroundTapped(object? sender, TappedEventArgs e)
    {
        _audio.Play(Sfx.UiBack);
        await HidePicker();
    }

    private void BuildDifficultyList()
    {
        DifficultyList.Children.Clear();
        var colors = new[] { "#3FAE49", "#FF9F1C", "#E4572E", "#8E5BD8" };
        var ids = new[] { DifficultyId.Easy, DifficultyId.Medium, DifficultyId.Hard, DifficultyId.Custom };
        var blurbs = new[] { "A quick stroll round the garden", "The neighbour's dog visited", "Doggy day-care backyard", "Design your own disaster" };

        for (var i = 0; i < ids.Length; i++)
        {
            var id = ids[i];
            var d = _settings.Resolve(id);
            var stats = _stats.Get(id);
            var best = stats.Best is { } b ? $"Best {Format.Time(b.Seconds)}" : "No best time yet";
            var subtitle = id == DifficultyId.Custom
                ? $"{d.Rows}×{d.Columns} · {d.PooCount} poos"
                : $"{d.Rows}×{d.Columns} · {d.PooCount} poos · {best}";
            DifficultyList.Children.Add(MakeCard(d.Name.ToUpperInvariant(), blurbs[i], subtitle, Color.FromArgb(colors[i]), id, id == _settings.LastDifficulty));
        }
    }

    private View MakeCard(string title, string blurb, string subtitle, Color color, DifficultyId id, bool last)
    {
        var dark = Color.FromRgba(color.Red * 0.6, color.Green * 0.6, color.Blue * 0.6, 1);
        var card = new Border
        {
            StrokeShape = new RoundRectangle { CornerRadius = 16 },
            Stroke = dark,
            StrokeThickness = last ? 4 : 2.5,
            BackgroundColor = Colors.White,
            Padding = new Thickness(0),
            Content = new Grid
            {
                ColumnDefinitions = [new ColumnDefinition(new GridLength(10)), new ColumnDefinition(GridLength.Star)],
                Children =
                {
                    new BoxView { Color = color },
                    new VerticalStackLayout
                    {
                        Padding = new Thickness(12, 8),
                        Spacing = 0,
                        Children =
                        {
                            new Label { Text = title, FontFamily = "Display", FontSize = 22, TextColor = dark },
                            new Label { Text = blurb, FontSize = 13, TextColor = (Color)Application.Current!.Resources["Ink"] },
                            new Label { Text = subtitle, Style = (Style)Application.Current!.Resources["Caption"] },
                        },
                    }.Column(1),
                },
            },
        };
        SemanticProperties.SetDescription(card, $"{title}. {blurb}. {subtitle}");

        var tap = new TapGestureRecognizer();
        tap.Tapped += async (_, _) =>
        {
            _audio.Play(Sfx.UiClick);
            await card.ScaleToAsync(0.96, 60);
            await card.ScaleToAsync(1, 100, Easing.SpringOut);
            if (id == DifficultyId.Custom)
            {
                ShowCustomEditor();
                return;
            }
            await StartGame(_settings.Resolve(id));
        };
        card.GestureRecognizers.Add(tap);
        return card;
    }

    private void ShowCustomEditor()
    {
        _loadingCustom = true;
        var d = _settings.Custom;
        RowsStepper.Maximum = Settings.MaxRows;
        ColsStepper.Maximum = Settings.MaxColumns;
        RowsStepper.Value = d.Rows;
        ColsStepper.Value = d.Columns;
        PooStepper.Maximum = Settings.MaxPoo(d.Rows, d.Columns);
        PooStepper.Value = d.PooCount;
        _loadingCustom = false;
        UpdateCustomLabels();
        CustomEditor.IsVisible = true;
    }

    private void OnCustomChanged(object? sender, ValueChangedEventArgs e)
    {
        if (_loadingCustom) return;
        _audio.Play(Sfx.UiToggle, 0.6);
        PooStepper.Maximum = Settings.MaxPoo((int)RowsStepper.Value, (int)ColsStepper.Value);
        if (PooStepper.Value > PooStepper.Maximum) PooStepper.Value = PooStepper.Maximum;
        UpdateCustomLabels();
    }

    private void UpdateCustomLabels()
    {
        RowsLabel.Text = ((int)RowsStepper.Value).ToString();
        ColsLabel.Text = ((int)ColsStepper.Value).ToString();
        PooLabel.Text = ((int)PooStepper.Value).ToString();
        var density = PooStepper.Value / (RowsStepper.Value * ColsStepper.Value);
        var mood = density switch
        {
            < 0.1 => "A gentle stroll",
            < 0.16 => "Watch your step",
            < 0.22 => "Proper minefield",
            < 0.3 => "Who let the dogs out?!",
            _ => "Absolutely disgusting",
        };
        DensityLabel.Text = $"{density:P0} poo density · {mood}";
    }

    private async void OnStartCustom(object? sender, EventArgs e)
    {
        var d = new Difficulty(DifficultyId.Custom, "Custom", (int)RowsStepper.Value, (int)ColsStepper.Value, (int)PooStepper.Value);
        _settings.Custom = d;
        await StartGame(d);
    }

    private async Task StartGame(Difficulty d)
    {
        _settings.LastDifficulty = d.Id;
        await HidePicker();
        await Shell.Current.GoToAsync(nameof(GamePage), new ShellNavigationQueryParameters { ["difficulty"] = d });
    }
}

internal static class ViewExtensions
{
    public static T Column<T>(this T view, int column) where T : BindableObject
    {
        Grid.SetColumn(view, column);
        return view;
    }

    public static T Row<T>(this T view, int row) where T : BindableObject
    {
        Grid.SetRow(view, row);
        return view;
    }
}

public static class Format
{
    public static string Time(double seconds)
    {
        if (seconds < 60) return $"{seconds:0.0}s";
        var ts = TimeSpan.FromSeconds(seconds);
        return $"{(int)ts.TotalMinutes}:{ts.Seconds:00}";
    }

    public static string Clock(TimeSpan t)
    {
        var total = Math.Min(t.TotalSeconds, 5999);
        return $"{(int)(total / 60):00}:{(int)(total % 60):00}";
    }
}
