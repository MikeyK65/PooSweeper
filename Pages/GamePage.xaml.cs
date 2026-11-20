using PooSweep.Controls;
using PooSweep.Core;
using PooSweep.Core.Audio;
using PooSweep.Graphics;
using PooSweep.Services;

namespace PooSweep.Pages;

public partial class GamePage : ContentPage, IQueryAttributable
{
    private static readonly string[] WinTitles = ["SPOTLESS!", "GOOD DOG!", "YARD CLEAN!", "PAWSOME!"];
    private static readonly string[] WinLines =
    [
        "Not a single squish. Your shoes thank you.",
        "Every last poo found. Treats all round!",
        "The lawn is safe for barefoot cartwheels.",
        "Pristine. The neighbours are jealous.",
    ];
    private static readonly string[] LoseTitles = ["SQUELCH!", "EWWW!", "STEPPED IN IT!", "OH NO!"];
    private static readonly string[] LoseLines =
    [
        "That's going straight into the carpet.",
        "Somebody's getting a bath tonight.",
        "Check your shoes... actually, don't.",
        "The dog looks very pleased with itself.",
    ];

    private readonly AudioService _audio;
    private readonly Settings _settings;
    private readonly Haptics _haptics;
    private readonly GameSession _session;
    private readonly Random _rng = new();
    private IDispatcherTimer? _hudTimer;
    private Difficulty _difficulty = Difficulty.Easy;
    private bool _flagMode;
    private int _gameToken;
    private bool _confirming;

    public GamePage(AudioService audio, Settings settings, Haptics haptics, GameSession session)
    {
        InitializeComponent();
        Shell.SetBackButtonBehavior(this, new BackButtonBehavior { IsVisible = false });
        _audio = audio;
        _settings = settings;
        _haptics = haptics;
        _session = session;

        Board.CellAction += OnCellAction;
        Board.PressingChanged += (_, pressing) =>
        {
            if (_session.Engine.CanInteract) Dog.Mood = pressing ? DogMood.Worried : DogMood.Happy;
        };

        ApplySettings();
        var desktop = DeviceInfo.Idiom == DeviceIdiom.Desktop;
        HintLabel.Text = desktop ? "Right-click: flag\nClick a number: clear" : "Hold: flag\nTap a number: clear";
        SetMode(false, playSound: false);
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("difficulty", out var d) && d is Difficulty difficulty)
        {
            _difficulty = difficulty;
            StartNewGame(playSound: false);
        }
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        ApplySettings();
        _audio.PlayMusic(MusicTrack.Game);
        _hudTimer ??= CreateHudTimer();
        _hudTimer.Start();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _hudTimer?.Stop();
    }

    private IDispatcherTimer CreateHudTimer()
    {
        var t = Dispatcher.CreateTimer();
        t.Interval = TimeSpan.FromMilliseconds(250);
        t.Tick += (_, _) => TimerLabel.Text = Format.Clock(_session.Elapsed);
        return t;
    }

    private void ApplySettings()
    {
        Scene.Palette = _settings.Palette;
        Board.Palette = _settings.Palette;
        Board.Colorblind = _settings.Colorblind;
        Board.ChordOnTap = _settings.ChordOnTap;
        Board.ReduceMotion = _settings.ReduceMotion;
        SoundButton.Icon = _settings.Muted ? IconKind.SpeakerOff : IconKind.SpeakerOn;
    }

    // ------------------------------------------------------------------
    //  Game flow
    // ------------------------------------------------------------------

    private void StartNewGame(bool playSound = true)
    {
        _gameToken++;
        _session.Start(_difficulty);
        Board.NewBoard(_session.Engine);
        Dog.Mood = DogMood.Happy;
        DifficultyLabel.Text = $"{_difficulty.Name.ToUpperInvariant()}\n{_difficulty.Rows}×{_difficulty.Columns}";
        TimerLabel.Text = Format.Clock(TimeSpan.Zero);
        UpdateHud();
        HideEndCard();
        if (playSound) _audio.Play(Sfx.NewGame);
        _audio.PlayMusic(MusicTrack.Game);
    }

    private void OnCellAction(object? sender, BoardActionEventArgs e)
    {
        var outcome = e.Kind switch
        {
            BoardActionKind.Reveal => _session.Reveal(e.Row, e.Column),
            BoardActionKind.Flag => _session.ToggleFlag(e.Row, e.Column),
            _ => _session.Chord(e.Row, e.Column),
        };

        Board.Sync(e.Row, e.Column);

        switch (outcome.Result)
        {
            case ActionResult.Dug:
                _audio.PlayDig();
                _haptics.Tap();
                break;
            case ActionResult.Cascade:
                _audio.Play(Sfx.Cascade);
                _audio.PlayDig();
                _haptics.Tap();
                break;
            case ActionResult.Chorded:
                _audio.Play(Sfx.Chord);
                _audio.PlayDig();
                _haptics.Tap();
                break;
            case ActionResult.Flagged:
                _audio.Play(Sfx.Flag);
                _haptics.Press();
                break;
            case ActionResult.Unflagged:
                _audio.Play(Sfx.Unflag);
                _haptics.Tap();
                break;
            case ActionResult.Denied:
                _audio.Play(Sfx.Denied, 0.7);
                Board.Nudge(e.Row, e.Column);
                break;
            case ActionResult.Won:
                OnWon();
                break;
            case ActionResult.Lost:
                OnLost();
                break;
        }

        UpdateHud();
    }

    private void OnWon()
    {
        var token = _gameToken;
        Dog.Mood = DogMood.Celebrate;
        _haptics.Tap();
        _audio.PlayJingle(Sfx.Win, TimeSpan.FromSeconds(4.5));
        TimerLabel.Text = Format.Clock(_session.Elapsed);
        SemanticScreenReader.Announce("You win! The yard is clean.");
        After(1.1, token, () => _audio.Play(Sfx.Bark));
        After(_settings.ReduceMotion ? 0.6 : 1.8, token, () => ShowEndCard(won: true));
    }

    private void OnLost()
    {
        var token = _gameToken;
        Dog.Mood = DogMood.Sick;
        _audio.Play(Sfx.Splat);
        _haptics.Thud();
        SemanticScreenReader.Announce("Oh no, you stepped in it. Game over.");
        After(0.55, token, () => _audio.PlayJingle(Sfx.Lose, TimeSpan.FromSeconds(5)));
        After(2.3, token, () => _audio.Play(Sfx.Whimper, 0.8));
        After(_settings.ReduceMotion ? 0.8 : 2.2, token, () => ShowEndCard(won: false));
    }

    private void After(double seconds, int token, Action action)
        => Dispatcher.DispatchDelayed(TimeSpan.FromSeconds(seconds), () =>
        {
            if (token == _gameToken) action();
        });

    private void UpdateHud()
    {
        var engine = _session.Engine;
        PooCounter.Text = engine.RemainingFlags.ToString();
        var hidden = engine.Difficulty.CellCount - engine.Difficulty.PooCount - engine.RevealedSafeCells;
        SemanticProperties.SetDescription(Board,
            $"Yard {engine.Board.Rows} by {engine.Board.Columns}. {engine.RemainingFlags} flags left. {hidden} safe tiles still to dig.");
    }

    // ------------------------------------------------------------------
    //  End card
    // ------------------------------------------------------------------

    private async void ShowEndCard(bool won)
    {
        var stats = ServiceHelper.Get<Core.Stats.StatsBook>()!.Get(_difficulty.Id);

        EndDog.Mood = won ? DogMood.Celebrate : DogMood.Sick;
        EndTitle.Text = won ? WinTitles[_rng.Next(WinTitles.Length)] : LoseTitles[_rng.Next(LoseTitles.Length)];
        EndTitle.TextColor = won ? Color.FromArgb("#2E8B3A") : Color.FromArgb("#C0392B");
        EndMessage.Text = won ? WinLines[_rng.Next(WinLines.Length)] : LoseLines[_rng.Next(LoseLines.Length)];

        EndTime.Text = won ? Format.Time(_session.Elapsed.TotalSeconds) : "—";
        EndBest.Text = stats.Best is { } b ? Format.Time(b.Seconds) : "—";
        EndStreak.Text = stats.CurrentStreak.ToString();

        RecordBadge.IsVisible = false;
        if (won && _session.LastWin is { Rank: { } rank } win)
        {
            RecordBadge.IsVisible = true;
            RecordLabel.Text = win.IsPersonalBest
                ? (win.PreviousBest is null ? "FIRST BEST TIME!" : "NEW BEST TIME!")
                : $"#{rank} ON YOUR LEADERBOARD";
        }
        else if (won && _difficulty.Id == DifficultyId.Custom)
        {
            RecordBadge.IsVisible = true;
            RecordLabel.Text = "CUSTOM YARD CLEARED";
        }

        EndOverlay.IsVisible = true;
        EndCard.Scale = 0.7;
        EndCard.TranslationY = 40;
        await Task.WhenAll(
            EndOverlay.FadeToAsync(1, 200),
            EndCard.ScaleToAsync(1, 380, Easing.SpringOut),
            EndCard.TranslateToAsync(0, 0, 380, Easing.SpringOut));

        if (RecordBadge.IsVisible)
        {
            await RecordBadge.ScaleToAsync(1.15, 140, Easing.CubicOut);
            await RecordBadge.ScaleToAsync(1, 220, Easing.SpringOut);
        }
    }

    private void HideEndCard()
    {
        EndOverlay.IsVisible = false;
        EndOverlay.Opacity = 0;
    }

    private void OnPlayAgain(object? sender, EventArgs e) => StartNewGame();

    private async void OnViewBoard(object? sender, EventArgs e)
    {
        await EndOverlay.FadeToAsync(0, 150);
        EndOverlay.IsVisible = false;
    }

    // ------------------------------------------------------------------
    //  Controls
    // ------------------------------------------------------------------

    private async void OnDogTapped(object? sender, EventArgs e)
    {
        if (_session.InProgress && !await ConfirmAbandon("Start a new yard?", "New yard")) return;
        StartNewGame();
    }

    private async void OnBack(object? sender, EventArgs e)
    {
        if (_session.InProgress && !await ConfirmAbandon("Leave this yard?", "Leave")) return;
        _session.Abandon();
        _gameToken++;
        await Shell.Current.GoToAsync("..");
    }

    protected override bool OnBackButtonPressed()
    {
        OnBack(this, EventArgs.Empty);
        return true;
    }

    private async Task<bool> ConfirmAbandon(string title, string accept)
    {
        if (_confirming) return false;
        _confirming = true;
        try
        {
            return await DisplayAlertAsync(title, "This game is still going - giving up counts as a loss.", accept, "Keep sweeping");
        }
        finally
        {
            _confirming = false;
        }
    }

    private void OnToggleSound(object? sender, EventArgs e)
    {
        _settings.Muted = !_settings.Muted;
        SoundButton.Icon = _settings.Muted ? IconKind.SpeakerOff : IconKind.SpeakerOn;
        if (!_settings.Muted) _audio.PlayMusic(MusicTrack.Game);
    }

    private void OnDigMode(object? sender, TappedEventArgs e) => SetMode(false);

    private void OnFlagMode(object? sender, TappedEventArgs e) => SetMode(true);

    private void SetMode(bool flag, bool playSound = true)
    {
        if (playSound && flag != _flagMode)
        {
            _audio.Play(Sfx.UiToggle);
            _haptics.Tap();
        }
        _flagMode = flag;
        Board.FlagMode = flag;

        var active = (Color)Application.Current!.Resources["Orange"];
        DigChip.BackgroundColor = flag ? Colors.Transparent : active;
        FlagChip.BackgroundColor = flag ? active : Colors.Transparent;
        DigModeLabel.TextColor = Colors.White;
        FlagModeLabel.TextColor = Colors.White;
        DigChip.Opacity = flag ? 0.75 : 1;
        FlagChip.Opacity = flag ? 1 : 0.75;
        SemanticProperties.SetDescription(DigChip, flag ? "Dig mode" : "Dig mode, selected");
        SemanticProperties.SetDescription(FlagChip, flag ? "Flag mode, selected" : "Flag mode");
    }
}
