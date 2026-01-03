using Microsoft.Maui.Storage;
using PooSweep.Core;

namespace PooSweep.Services;

public static class GamePreferences
{
    private const string DifficultyIdKey = "poosweep.difficulty.id";
    private const string CustomRowsKey = "poosweep.difficulty.custom.rows";
    private const string CustomColsKey = "poosweep.difficulty.custom.cols";
    private const string CustomPooKey = "poosweep.difficulty.custom.poo";

    private const string BestTimePrefix = "poosweep.best.";

    public static void SaveSelectedDifficulty(DifficultyId id)
        => Preferences.Default.Set(DifficultyIdKey, id.ToString());

    public static DifficultyId LoadSelectedDifficulty()
    {
        var raw = Preferences.Default.Get(DifficultyIdKey, DifficultyId.Easy.ToString());
        return Enum.TryParse<DifficultyId>(raw, out var id) ? id : DifficultyId.Easy;
    }

    public static void SaveCustom(int rows, int cols, int poo)
    {
        Preferences.Default.Set(CustomRowsKey, rows);
        Preferences.Default.Set(CustomColsKey, cols);
        Preferences.Default.Set(CustomPooKey, poo);
    }

    public static (int rows, int cols, int poo) LoadCustom((int rows, int cols, int poo) fallback)
    {
        var rows = Preferences.Default.Get(CustomRowsKey, fallback.rows);
        var cols = Preferences.Default.Get(CustomColsKey, fallback.cols);
        var poo = Preferences.Default.Get(CustomPooKey, fallback.poo);
        return (rows, cols, poo);
    }

    public static TimeSpan? LoadBestTime(DifficultyId id)
    {
        var seconds = Preferences.Default.Get(BestTimePrefix + id, -1);
        return seconds >= 0 ? TimeSpan.FromSeconds(seconds) : null;
    }

    public static bool TrySaveBestTime(DifficultyId id, TimeSpan elapsed)
    {
        if (elapsed <= TimeSpan.Zero) return false;

        var key = BestTimePrefix + id;
        var current = Preferences.Default.Get(key, -1);
        var next = (int)Math.Round(elapsed.TotalSeconds);

        if (current < 0 || next < current)
        {
            Preferences.Default.Set(key, next);
            return true;
        }

        return false;
    }
}
