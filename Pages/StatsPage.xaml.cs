using Microsoft.Maui.Controls.Shapes;
using PooSweep.Controls;
using PooSweep.Core;
using PooSweep.Core.Audio;
using PooSweep.Core.Stats;
using PooSweep.Services;

namespace PooSweep.Pages;

public partial class StatsPage : ContentPage
{
    private static readonly DifficultyId[] Ids = [DifficultyId.Easy, DifficultyId.Medium, DifficultyId.Hard, DifficultyId.Custom];
    private static readonly Color[] Medals = [Color.FromArgb("#FFC83D"), Color.FromArgb("#C0C6CC"), Color.FromArgb("#D08A4E"), Color.FromArgb("#A88F7A"), Color.FromArgb("#A88F7A")];

    private readonly StatsBook _stats;
    private readonly Settings _settings;
    private readonly AudioService _audio;
    private DifficultyId _selected;

    public StatsPage(StatsBook stats, Settings settings, AudioService audio)
    {
        InitializeComponent();
        Shell.SetBackButtonBehavior(this, new BackButtonBehavior { IsVisible = false });
        _stats = stats;
        _settings = settings;
        _audio = audio;
        _selected = settings.LastDifficulty;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        Scene.Palette = _settings.Palette;
        Refresh();
    }

    private void Refresh()
    {
        BuildTabs();
        var s = _stats.Get(_selected);
        Played.Text = s.Played.ToString();
        Won.Text = s.Won.ToString();
        Rate.Text = s.Played == 0 ? "—" : $"{s.WinRate:P0}";
        Streak.Text = s.CurrentStreak.ToString();
        BestStreak.Text = s.BestStreak.ToString();
        BestTime.Text = s.Best is { } b ? Format.Time(b.Seconds) : "—";

        Leaderboard.Children.Clear();
        if (!StatsBook.TracksTimes(_selected))
        {
            Leaderboard.Children.Add(new Label { Text = "Custom yards come in all shapes and sizes, so times aren't ranked.", Style = (Style)Application.Current!.Resources["Caption"] });
        }
        else if (s.BestTimes.Count == 0)
        {
            Leaderboard.Children.Add(new Label { Text = "No clean sweeps yet. Get out there!", Style = (Style)Application.Current!.Resources["Caption"] });
        }
        else
        {
            for (var i = 0; i < s.BestTimes.Count; i++)
                Leaderboard.Children.Add(MakeRow(i, s.BestTimes[i]));
        }

        ResetButton.Text = $"RESET {_selected.ToString().ToUpperInvariant()}";
    }

    private View MakeRow(int index, TimeEntry entry)
    {
        var when = entry.When == DateTimeOffset.MinValue ? "from an older version" : entry.When.LocalDateTime.ToString("d MMM yyyy");
        var row = new Border
        {
            StrokeShape = new RoundRectangle { CornerRadius = 12 },
            Stroke = (Color)Application.Current!.Resources["PaperEdge"],
            StrokeThickness = 2,
            BackgroundColor = index == 0 ? Color.FromArgb("#FFF3C4") : Colors.White,
            Padding = new Thickness(10, 6),
            Content = new Grid
            {
                ColumnDefinitions = [new ColumnDefinition(36), new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto)],
                ColumnSpacing = 10,
                Children =
                {
                    new Border
                    {
                        WidthRequest = 32, HeightRequest = 32,
                        StrokeShape = new Ellipse(),
                        Stroke = (Color)Application.Current!.Resources["WoodDark"],
                        StrokeThickness = 2,
                        BackgroundColor = Medals[Math.Min(index, Medals.Length - 1)],
                        Content = new Label { Text = (index + 1).ToString(), FontFamily = "Display", FontSize = 17, HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center, TextColor = (Color)Application.Current!.Resources["Ink"] },
                    },
                    new Label { Text = when, VerticalOptions = LayoutOptions.Center, Style = (Style)Application.Current!.Resources["Caption"] }.Column(1),
                    new Label { Text = Format.Time(entry.Seconds), FontFamily = "Display", FontSize = 22, VerticalOptions = LayoutOptions.Center, TextColor = (Color)Application.Current!.Resources["Ink"] }.Column(2),
                },
            },
        };
        SemanticProperties.SetDescription(row, $"Rank {index + 1}, {Format.Time(entry.Seconds)}, {when}");
        return row;
    }

    private void BuildTabs()
    {
        Tabs.Children.Clear();
        for (var i = 0; i < Ids.Length; i++)
        {
            var id = Ids[i];
            var selected = id == _selected;
            var tab = new Border
            {
                StrokeShape = new RoundRectangle { CornerRadius = 12 },
                Stroke = (Color)Application.Current!.Resources["WoodDark"],
                StrokeThickness = 2,
                BackgroundColor = selected ? (Color)Application.Current!.Resources["Orange"] : Colors.White,
                Padding = new Thickness(4, 8),
                Content = new Label
                {
                    Text = id.ToString().ToUpperInvariant(),
                    FontFamily = "Display",
                    FontSize = 14,
                    HorizontalOptions = LayoutOptions.Center,
                    TextColor = selected ? Colors.White : (Color)Application.Current!.Resources["Ink"],
                },
            }.Column(i);
            SemanticProperties.SetDescription(tab, selected ? $"{id}, selected" : id.ToString());
            var tap = new TapGestureRecognizer();
            tap.Tapped += (_, _) =>
            {
                if (_selected == id) return;
                _selected = id;
                _audio.Play(Sfx.UiToggle);
                Refresh();
            };
            tab.GestureRecognizers.Add(tap);
            Tabs.Children.Add(tab);
        }
    }

    private async void OnReset(object? sender, EventArgs e)
    {
        if (!await DisplayAlertAsync($"Reset {_selected} stats?", "Best times, streaks and win counts for this difficulty will be wiped.", "Reset", "Cancel"))
            return;
        _stats.Reset(_selected);
        _audio.Play(Sfx.Splat, 0.6);
        Refresh();
    }

    private async void OnBack(object? sender, EventArgs e) => await Shell.Current.GoToAsync("..");
}
