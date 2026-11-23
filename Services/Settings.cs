using PooSweep.Core;
using PooSweep.Core.Stats;
using PooSweep.Graphics;

namespace PooSweep.Services;

/// <summary>Adapts MAUI Preferences to the core key/value store.</summary>
public sealed class PreferencesStore : IKeyValueStore
{
    public string? Get(string key)
    {
        if (!Preferences.Default.ContainsKey(key)) return null;
        try
        {
            return Preferences.Default.Get<string?>(key, null);
        }
        catch
        {
            // value stored with a different type by an older version (e.g. int best times)
            try { return Preferences.Default.Get(key, -1).ToString(); }
            catch { return null; }
        }
    }

    public void Set(string key, string value) => Preferences.Default.Set(key, value);

    public void Remove(string key) => Preferences.Default.Remove(key);
}

/// <summary>User options, persisted with Preferences and observable for live updates.</summary>
public sealed class Settings
{
    private const string P = "poosweep.settings.";

    public event EventHandler? Changed;

    public double MusicVolume
    {
        get => Preferences.Default.Get(P + "music", 0.55);
        set { Preferences.Default.Set(P + "music", Math.Clamp(value, 0, 1)); Changed?.Invoke(this, EventArgs.Empty); }
    }

    public double SfxVolume
    {
        get => Preferences.Default.Get(P + "sfx", 0.8);
        set { Preferences.Default.Set(P + "sfx", Math.Clamp(value, 0, 1)); Changed?.Invoke(this, EventArgs.Empty); }
    }

    public bool Muted
    {
        get => Preferences.Default.Get(P + "muted", false);
        set { Preferences.Default.Set(P + "muted", value); Changed?.Invoke(this, EventArgs.Empty); }
    }

    public bool Haptics
    {
        get => Preferences.Default.Get(P + "haptics", true);
        set { Preferences.Default.Set(P + "haptics", value); Changed?.Invoke(this, EventArgs.Empty); }
    }

    public bool Colorblind
    {
        get => Preferences.Default.Get(P + "colorblind", false);
        set { Preferences.Default.Set(P + "colorblind", value); Changed?.Invoke(this, EventArgs.Empty); }
    }

    public bool ReduceMotion
    {
        get => Preferences.Default.Get(P + "reducemotion", false);
        set { Preferences.Default.Set(P + "reducemotion", value); Changed?.Invoke(this, EventArgs.Empty); }
    }

    public bool ChordOnTap
    {
        get => Preferences.Default.Get(P + "chordtap", true);
        set { Preferences.Default.Set(P + "chordtap", value); Changed?.Invoke(this, EventArgs.Empty); }
    }

    public BoardTheme Theme
    {
        get => Enum.TryParse<BoardTheme>(Preferences.Default.Get(P + "theme", nameof(BoardTheme.Day)), out var t) ? t : BoardTheme.Day;
        set { Preferences.Default.Set(P + "theme", value.ToString()); Changed?.Invoke(this, EventArgs.Empty); }
    }

    public Palette Palette => Palette.For(Theme);

    public bool SeenHowTo
    {
        get => Preferences.Default.Get(P + "seenhowto", false);
        set => Preferences.Default.Set(P + "seenhowto", value);
    }

    // ---- difficulty ----

    public const int MinSize = 5;
    public const int MaxRows = 24;
    public const int MaxColumns = 30;

    public DifficultyId LastDifficulty
    {
        get => Enum.TryParse<DifficultyId>(Preferences.Default.Get("poosweep.difficulty.id", nameof(DifficultyId.Easy)), out var id) ? id : DifficultyId.Easy;
        set => Preferences.Default.Set("poosweep.difficulty.id", value.ToString());
    }

    public Difficulty Custom
    {
        get
        {
            var rows = Math.Clamp(Preferences.Default.Get("poosweep.difficulty.custom.rows", 10), MinSize, MaxRows);
            var cols = Math.Clamp(Preferences.Default.Get("poosweep.difficulty.custom.cols", 10), MinSize, MaxColumns);
            var poo = Math.Clamp(Preferences.Default.Get("poosweep.difficulty.custom.poo", 15), 1, MaxPoo(rows, cols));
            return new Difficulty(DifficultyId.Custom, "Custom", rows, cols, poo);
        }
        set
        {
            Preferences.Default.Set("poosweep.difficulty.custom.rows", value.Rows);
            Preferences.Default.Set("poosweep.difficulty.custom.cols", value.Columns);
            Preferences.Default.Set("poosweep.difficulty.custom.poo", value.PooCount);
        }
    }

    /// <summary>Leave room for the 3x3 safe opening.</summary>
    public static int MaxPoo(int rows, int cols) => Math.Max(1, rows * cols - 9);

    public Difficulty Resolve(DifficultyId id) => id switch
    {
        DifficultyId.Easy => Difficulty.Easy,
        DifficultyId.Medium => Difficulty.Medium,
        DifficultyId.Hard => Difficulty.Hard,
        _ => Custom,
    };
}
