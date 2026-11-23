using System.Text.Json;

namespace PooSweep.Core.Stats;

/// <summary>Minimal string key/value persistence (Preferences in the app, a dictionary in tests).</summary>
public interface IKeyValueStore
{
    string? Get(string key);
    void Set(string key, string value);
    void Remove(string key);
}

public sealed class InMemoryKeyValueStore : IKeyValueStore
{
    private readonly Dictionary<string, string> _values = new();
    public string? Get(string key) => _values.TryGetValue(key, out var v) ? v : null;
    public void Set(string key, string value) => _values[key] = value;
    public void Remove(string key) => _values.Remove(key);
}

public sealed record TimeEntry(double Seconds, DateTimeOffset When);

public sealed class DifficultyStats
{
    public int Played { get; set; }
    public int Won { get; set; }
    public int CurrentStreak { get; set; }
    public int BestStreak { get; set; }
    public List<TimeEntry> BestTimes { get; set; } = [];

    public int Lost => Played - Won;
    public double WinRate => Played == 0 ? 0 : (double)Won / Played;
    public TimeEntry? Best => BestTimes.Count > 0 ? BestTimes[0] : null;
}

/// <summary>Outcome of recording a win.</summary>
/// <param name="Rank">1-based leaderboard position, or null if it didn't place.</param>
public sealed record WinRecord(int? Rank, bool IsPersonalBest, TimeSpan? PreviousBest, int Streak);

/// <summary>Per-difficulty statistics and top-5 best times.</summary>
public sealed class StatsBook
{
    public const int LeaderboardSize = 5;
    private const string Prefix = "poosweep.stats.v2.";
    private const string LegacyBestPrefix = "poosweep.best.";

    private readonly IKeyValueStore _store;

    public StatsBook(IKeyValueStore store) => _store = store ?? throw new ArgumentNullException(nameof(store));

    public static bool TracksTimes(DifficultyId id) => id != DifficultyId.Custom;

    public DifficultyStats Get(DifficultyId id)
    {
        var raw = _store.Get(Prefix + id);
        if (!string.IsNullOrEmpty(raw))
        {
            try
            {
                var s = JsonSerializer.Deserialize<DifficultyStats>(raw);
                if (s is not null)
                {
                    s.BestTimes ??= [];
                    return s;
                }
            }
            catch (JsonException)
            {
                // corrupted – start over
            }
        }

        var fresh = new DifficultyStats();

        // migrate the single best time stored by the first version of the game
        if (int.TryParse(_store.Get(LegacyBestPrefix + id), out var legacy) && legacy > 0 && TracksTimes(id))
            fresh.BestTimes.Add(new TimeEntry(legacy, DateTimeOffset.MinValue));

        return fresh;
    }

    public WinRecord RecordWin(DifficultyId id, TimeSpan time, DateTimeOffset when)
    {
        var s = Get(id);
        s.Played++;
        s.Won++;
        s.CurrentStreak++;
        s.BestStreak = Math.Max(s.BestStreak, s.CurrentStreak);

        int? rank = null;
        var pb = false;
        TimeSpan? previous = s.Best is { } b ? TimeSpan.FromSeconds(b.Seconds) : null;

        if (TracksTimes(id) && time > TimeSpan.Zero)
        {
            var entry = new TimeEntry(Math.Round(time.TotalSeconds, 1), when);
            s.BestTimes.Add(entry);
            s.BestTimes = s.BestTimes.OrderBy(t => t.Seconds).ThenBy(t => t.When).Take(LeaderboardSize).ToList();
            var idx = s.BestTimes.IndexOf(entry);
            if (idx >= 0)
            {
                rank = idx + 1;
                pb = idx == 0;
            }
        }

        Save(id, s);
        return new WinRecord(rank, pb, previous, s.CurrentStreak);
    }

    public void RecordLoss(DifficultyId id)
    {
        var s = Get(id);
        s.Played++;
        s.CurrentStreak = 0;
        Save(id, s);
    }

    public void Reset(DifficultyId id)
    {
        _store.Remove(Prefix + id);
        _store.Remove(LegacyBestPrefix + id);
    }

    public void ResetAll()
    {
        foreach (var id in Enum.GetValues<DifficultyId>()) Reset(id);
    }

    private void Save(DifficultyId id, DifficultyStats s)
        => _store.Set(Prefix + id, JsonSerializer.Serialize(s));
}
