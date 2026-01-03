using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using PooSweep.Core;
using PooSweep.Services;
using CoreCell = PooSweep.Core.Cell;

namespace PooSweep.ViewModels;

public sealed class MainViewModel : INotifyPropertyChanged
{
    private GameEngine _engine;
    private Difficulty _selectedDifficulty;

    private readonly IDispatcherTimer _timer;
    private DateTimeOffset? _startedAt;
    private TimeSpan _elapsed;

    private int _gamesPlayed;
    private int _gamesWon;

    private int _customRows = 10;
    private int _customColumns = 10;
    private int _customPooCount = 15;

    private TimeSpan? _bestTime;

    public MainViewModel()
    {
        var loadedId = GamePreferences.LoadSelectedDifficulty();

        Difficulties = new ObservableCollection<Difficulty>
        {
            Difficulty.Easy,
            Difficulty.Medium,
            Difficulty.Hard,
            new Difficulty(DifficultyId.Custom, "Custom", 10, 10, 15)
        };

        var loadedCustom = GamePreferences.LoadCustom((10, 10, 15));
        _customRows = loadedCustom.rows;
        _customColumns = loadedCustom.cols;
        _customPooCount = loadedCustom.poo;

        _selectedDifficulty = Difficulties.First(d => d.Id == loadedId);
        if (_selectedDifficulty.Id == DifficultyId.Custom)
            _selectedDifficulty = BuildCustomDifficulty();

        _engine = CreateEngine(_selectedDifficulty);

        System.Diagnostics.Debug.WriteLine($"[MainViewModel] Initialized with Difficulty: {_selectedDifficulty.Name}, Rows: {_engine.Board.Rows}, Columns: {_engine.Board.Columns}");

        _timer = Application.Current?.Dispatcher.CreateTimer() ?? throw new InvalidOperationException("No dispatcher available.");
        _timer.Interval = TimeSpan.FromSeconds(1);
        _timer.Tick += (_, _) =>
        {
            if (_startedAt is null) return;
            Elapsed = DateTimeOffset.UtcNow - _startedAt.Value;
        };

        NewGameCommand = new Command(NewGame);
        RevealCommand = new Command<CellViewModel>(cell => { if (cell is not null) Reveal(cell); });
        ToggleFlagCommand = new Command<CellViewModel>(cell => { if (cell is not null) ToggleFlag(cell); });
        ChordCommand = new Command<CellViewModel>(cell => { if (cell is not null) Chord(cell); });
        ApplyCustomDifficultyCommand = new Command(ApplyCustomDifficulty);

        BestTime = GamePreferences.LoadBestTime(_selectedDifficulty.Id);

        RebuildCells();
        UpdateDerived();
        
        System.Diagnostics.Debug.WriteLine($"[MainViewModel] After RebuildCells: Cells.Count = {Cells.Count}, Rows = {Rows}, Columns = {Columns}");
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<Difficulty> Difficulties { get; }

    public Difficulty SelectedDifficulty
    {
        get => _selectedDifficulty;
        set
        {
            if (Equals(_selectedDifficulty, value)) return;
            _selectedDifficulty = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsCustomSelected));

            GamePreferences.SaveSelectedDifficulty(_selectedDifficulty.Id);

            BestTime = GamePreferences.LoadBestTime(_selectedDifficulty.Id);
            OnPropertyChanged(nameof(BestTimeText));

            if (_selectedDifficulty.Id != DifficultyId.Custom)
                NewGame();
        }
    }

    public ObservableCollection<CellViewModel> Cells { get; } = new();

    public int Rows => _engine.Board.Rows;
    public int Columns => _engine.Board.Columns;

    public int GamesPlayed
    {
        get => _gamesPlayed;
        private set { if (_gamesPlayed == value) return; _gamesPlayed = value; OnPropertyChanged(); }
    }

    public int GamesWon
    {
        get => _gamesWon;
        private set { if (_gamesWon == value) return; _gamesWon = value; OnPropertyChanged(); OnPropertyChanged(nameof(WinRateText)); }
    }

    public string WinRateText
        => GamesPlayed == 0 ? "—" : $"{(int)Math.Round((double)GamesWon * 100 / GamesPlayed)}%";

    public TimeSpan Elapsed
    {
        get => _elapsed;
        private set
        {
            if (_elapsed == value) return;
            _elapsed = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(ElapsedText));
        }
    }

    public string ElapsedText
        => $"{(int)Elapsed.TotalMinutes:00}:{Elapsed.Seconds:00}";

    public string StatusText
        => _engine.State switch
        {
            GameState.NotStarted => "Tap a tile to start",
            GameState.InProgress => "Sweeping…",
            GameState.Won => "Yard is clean!",
            GameState.Lost => "Stepped in it!",
            _ => string.Empty
        };

    public int RemainingFlags => _engine.RemainingFlags;

    private string? _lastAnnouncement;

    public string? LastAnnouncement
    {
        get => _lastAnnouncement;
        private set
        {
            if (_lastAnnouncement == value) return;
            _lastAnnouncement = value;
            OnPropertyChanged();
        }
    }

    public bool IsGameOver => _engine.State is GameState.Won or GameState.Lost;

    public bool IsCustomSelected => SelectedDifficulty.Id == DifficultyId.Custom;

    public int CustomRows
    {
        get => _customRows;
        set
        {
            if (_customRows == value) return;
            _customRows = value;
            GamePreferences.SaveCustom(_customRows, _customColumns, _customPooCount);
            OnPropertyChanged();
            OnPropertyChanged(nameof(CustomSummary));
        }
    }

    public int CustomColumns
    {
        get => _customColumns;
        set
        {
            if (_customColumns == value) return;
            _customColumns = value;
            GamePreferences.SaveCustom(_customRows, _customColumns, _customPooCount);
            OnPropertyChanged();
            OnPropertyChanged(nameof(CustomSummary));
        }
    }

    public int CustomPooCount
    {
        get => _customPooCount;
        set
        {
            if (_customPooCount == value) return;
            _customPooCount = value;
            GamePreferences.SaveCustom(_customRows, _customColumns, _customPooCount);
            OnPropertyChanged();
            OnPropertyChanged(nameof(CustomSummary));
        }
    }

    public string CustomSummary => $"{CustomRows}×{CustomColumns}, {CustomPooCount} ??";

    public ICommand NewGameCommand { get; }
    public ICommand RevealCommand { get; }
    public ICommand ToggleFlagCommand { get; }
    public ICommand ChordCommand { get; }
    public ICommand ApplyCustomDifficultyCommand { get; }

    public TimeSpan? BestTime
    {
        get => _bestTime;
        private set
        {
            if (_bestTime == value) return;
            _bestTime = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(BestTimeText));
        }
    }

    public string BestTimeText
    {
        get
        {
            if (SelectedDifficulty.Id == DifficultyId.Custom)
                return "—";

            return BestTime is { } bt
                ? $"{(int)bt.TotalMinutes:00}:{bt.Seconds:00}"
                : "—";
        }
    }

    private static GameEngine CreateEngine(Difficulty difficulty)
        => new(difficulty, new GameRules { FirstTapSafe = true, FirstTapSafeRadius = 0 });

    private void NewGame()
    {
        StopTimer();
        Elapsed = TimeSpan.Zero;

        _engine = CreateEngine(_selectedDifficulty);
        RebuildCells();
        UpdateDerived();

        BestTime = GamePreferences.LoadBestTime(_selectedDifficulty.Id);
        OnPropertyChanged(nameof(BestTimeText));

        GamesPlayed++;
    }

    private void EnsureTimerStarted()
    {
        if (_startedAt is not null) return;
        _startedAt = DateTimeOffset.UtcNow;
        if (!_timer.IsRunning) _timer.Start();
    }

    private void StopTimer()
    {
        _timer.Stop();
        _startedAt = null;
    }

    private void Reveal(CellViewModel cell)
    {
        var before = _engine.State;

        if (before == GameState.NotStarted)
            EnsureTimerStarted();

        _engine.Reveal(cell.Row, cell.Column);

        SyncToViewModels();
        UpdateDerived();

        if (_engine.State is GameState.Won or GameState.Lost)
        {
            StopTimer();
            if (_engine.State is GameState.Won)
            {
                GamesWon++;
                if (_selectedDifficulty.Id != DifficultyId.Custom)
                {
                    if (GamePreferences.TrySaveBestTime(_selectedDifficulty.Id, Elapsed))
                        BestTime = Elapsed;
                }
            }
        }
    }

    private void ToggleFlag(CellViewModel cell)
    {
        try
        {
            System.Diagnostics.Debug.WriteLine($"[ToggleFlag] Invoked for cell: Row={cell?.Row}, Col={cell?.Column}, CellViewModel type: {cell?.GetType().FullName}");
            _engine.ToggleFlag(cell.Row, cell.Column);
            SyncToViewModels();
            UpdateDerived();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ToggleFlag] Exception: {ex}\nStackTrace: {ex.StackTrace}");
            LastAnnouncement = $"An error occurred while flagging the cell: {ex.Message}";
        }
    }

    private void Chord(CellViewModel cell)
    {
        _engine.Chord(cell.Row, cell.Column);
        SyncToViewModels();
        UpdateDerived();

        if (_engine.State is GameState.Won or GameState.Lost)
        {
            StopTimer();
            if (_engine.State is GameState.Won)
            {
                GamesWon++;
                if (_selectedDifficulty.Id != DifficultyId.Custom)
                {
                    if (GamePreferences.TrySaveBestTime(_selectedDifficulty.Id, Elapsed))
                        BestTime = Elapsed;
                }
            }
        }
    }

    private void RebuildCells()
    {
        Cells.Clear();
        
        System.Diagnostics.Debug.WriteLine($"[RebuildCells] Starting rebuild. Board: {_engine.Board.Rows}x{_engine.Board.Columns}");
        
        // Notify that grid dimensions might change
        OnPropertyChanged(nameof(Rows));
        OnPropertyChanged(nameof(Columns));
        
        foreach (var c in _engine.Board)
            Cells.Add(new CellViewModel(c.Row, c.Column, ToggleFlagCommand, ChordCommand, RevealCommand));
        SyncToViewModels();
        
        System.Diagnostics.Debug.WriteLine($"[RebuildCells] Completed. Added {Cells.Count} cells");
    }

    private void SyncToViewModels()
    {
        foreach (var vm in Cells)
        {
            var cell = _engine.GetCell(vm.Row, vm.Column);
            vm.Apply(cell);
        }
    }

    private void UpdateDerived()
    {
        OnPropertyChanged(nameof(StatusText));
        OnPropertyChanged(nameof(RemainingFlags));
        OnPropertyChanged(nameof(IsGameOver));

        if (_engine.State is GameState.Won)
            LastAnnouncement = "Yard is clean!";
        else if (_engine.State is GameState.Lost)
            LastAnnouncement = "Stepped in it!";
        else
            LastAnnouncement = null;
    }

    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    private Difficulty BuildCustomDifficulty()
        => new(DifficultyId.Custom, "Custom", CustomRows, CustomColumns, CustomPooCount);

    private void ApplyCustomDifficulty()
    {
        if (!IsCustomSelected) return;

        var d = BuildCustomDifficulty();

        try
        {
            d.Validate();
        }
        catch
        {
            LastAnnouncement = "Invalid custom difficulty";
            return;
        }

        _selectedDifficulty = d;
        OnPropertyChanged(nameof(SelectedDifficulty));
        OnPropertyChanged(nameof(CustomSummary));

        GamePreferences.SaveSelectedDifficulty(DifficultyId.Custom);
        GamePreferences.SaveCustom(CustomRows, CustomColumns, CustomPooCount);

        NewGame();
    }
}

public sealed class CellViewModel : INotifyPropertyChanged
{
    private bool _isRevealed;
    private bool _isFlagged;
    private bool _isPoo;
    private int _adjacent;

    public CellViewModel(int row, int column, ICommand toggleFlagCommand, ICommand chordCommand, ICommand revealCommand)
    {
        Row = row;
        Column = column;
        ToggleFlagCommand = toggleFlagCommand;
        ChordCommand = chordCommand;
        RevealCommand = revealCommand;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public int Row { get; }
    public int Column { get; }
    
    public ICommand ToggleFlagCommand { get; }
    public ICommand ChordCommand { get; }
    public ICommand RevealCommand { get; }

    public bool IsRevealed
    {
        get => _isRevealed;
        private set { if (_isRevealed == value) return; _isRevealed = value; OnPropertyChanged(); OnPropertyChanged(nameof(DisplayText)); }
    }

    public bool IsFlagged
    {
        get => _isFlagged;
        private set { if (_isFlagged == value) return; _isFlagged = value; OnPropertyChanged(); OnPropertyChanged(nameof(DisplayText)); }
    }

    public bool IsPoo
    {
        get => _isPoo;
        private set { if (_isPoo == value) return; _isPoo = value; OnPropertyChanged(); OnPropertyChanged(nameof(DisplayText)); }
    }

    public int Adjacent
    {
        get => _adjacent;
        private set { if (_adjacent == value) return; _adjacent = value; OnPropertyChanged(); OnPropertyChanged(nameof(DisplayText)); }
    }

    public Color BackgroundColor
    {
        get
        {
            if (!IsRevealed)
                return IsFlagged ? Colors.Goldenrod : Colors.DarkSeaGreen;

            if (IsPoo)
                return Colors.IndianRed;

            return Colors.Beige;
        }
    }

    public Color TextColor
    {
        get
        {
            if (!IsRevealed)
                return Colors.Black;

            return Adjacent switch
            {
                1 => Colors.Blue,
                2 => Colors.Green,
                3 => Colors.Red,
                4 => Colors.DarkBlue,
                5 => Colors.DarkRed,
                6 => Colors.Teal,
                7 => Colors.Black,
                8 => Colors.Gray,
                _ => Colors.Black
            };
        }
    }

    public string DisplayText
    {
        get
        {
            if (!IsRevealed)
                return IsFlagged ? "??" : string.Empty;

            if (IsPoo)
                return "??";

            return Adjacent == 0 ? string.Empty : Adjacent.ToString();
        }
    }

    public void Apply(CoreCell cell)
    {
        IsRevealed = cell.IsRevealed;
        IsFlagged = cell.IsFlagged;
        IsPoo = cell.IsPoo;
        Adjacent = cell.AdjacentPooCount;

        OnPropertyChanged(nameof(BackgroundColor));
        OnPropertyChanged(nameof(TextColor));
    }

    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
