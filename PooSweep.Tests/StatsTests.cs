using PooSweep.Core;
using PooSweep.Core.Stats;

namespace PooSweep.Tests;

public sealed class StatsTests
{
    private static readonly DateTimeOffset T0 = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void WinsAndLosses_UpdateCountsAndStreaks()
    {
        var book = new StatsBook(new InMemoryKeyValueStore());

        book.RecordWin(DifficultyId.Easy, TimeSpan.FromSeconds(30), T0);
        book.RecordWin(DifficultyId.Easy, TimeSpan.FromSeconds(40), T0);
        book.RecordLoss(DifficultyId.Easy);
        var r = book.RecordWin(DifficultyId.Easy, TimeSpan.FromSeconds(50), T0);

        var s = book.Get(DifficultyId.Easy);
        Assert.Equal(4, s.Played);
        Assert.Equal(3, s.Won);
        Assert.Equal(1, s.CurrentStreak);
        Assert.Equal(2, s.BestStreak);
        Assert.Equal(0.75, s.WinRate, 3);
        Assert.Equal(1, r.Streak);
    }

    [Fact]
    public void Leaderboard_KeepsTopFiveSorted_AndReportsRank()
    {
        var book = new StatsBook(new InMemoryKeyValueStore());
        foreach (var sec in new[] { 50, 40, 60, 70, 80, 90 })
            book.RecordWin(DifficultyId.Medium, TimeSpan.FromSeconds(sec), T0);

        var s = book.Get(DifficultyId.Medium);
        Assert.Equal([40, 50, 60, 70, 80], s.BestTimes.Select(t => t.Seconds));

        var r = book.RecordWin(DifficultyId.Medium, TimeSpan.FromSeconds(35), T0);
        Assert.True(r.IsPersonalBest);
        Assert.Equal(1, r.Rank);
        Assert.Equal(TimeSpan.FromSeconds(40), r.PreviousBest);

        var slow = book.RecordWin(DifficultyId.Medium, TimeSpan.FromSeconds(500), T0);
        Assert.Null(slow.Rank);
        Assert.False(slow.IsPersonalBest);
    }

    [Fact]
    public void Custom_DoesNotTrackTimes()
    {
        var book = new StatsBook(new InMemoryKeyValueStore());
        var r = book.RecordWin(DifficultyId.Custom, TimeSpan.FromSeconds(10), T0);
        Assert.Null(r.Rank);
        Assert.Empty(book.Get(DifficultyId.Custom).BestTimes);
        Assert.Equal(1, book.Get(DifficultyId.Custom).Won);
    }

    [Fact]
    public void LegacyBestTime_IsMigrated_AndResetClearsIt()
    {
        var store = new InMemoryKeyValueStore();
        store.Set("poosweep.best.Hard", "123");
        var book = new StatsBook(store);

        Assert.Equal(123, book.Get(DifficultyId.Hard).Best?.Seconds);

        book.Reset(DifficultyId.Hard);
        Assert.Null(book.Get(DifficultyId.Hard).Best);
    }

    [Fact]
    public void CorruptData_StartsFresh()
    {
        var store = new InMemoryKeyValueStore();
        store.Set("poosweep.stats.v2.Easy", "{not json");
        Assert.Equal(0, new StatsBook(store).Get(DifficultyId.Easy).Played);
    }

    [Fact]
    public void Engine_RecordsExplodedCell()
    {
        var engine = new GameEngine(new Difficulty(DifficultyId.Custom, "T", 6, 6, 12), new GameRules { FirstTapSafe = true }, new Random(4));
        engine.Reveal(0, 0);
        Assert.Null(engine.ExplodedCell);
        var poo = engine.Board.First(c => c.IsPoo);
        engine.Reveal(poo.Row, poo.Column);
        Assert.Equal(GameState.Lost, engine.State);
        Assert.Same(poo, engine.ExplodedCell);
    }

    [Fact]
    public void Losing_KeepsCorrectFlagsFlagged()
    {
        var engine = new GameEngine(new Difficulty(DifficultyId.Custom, "T", 6, 6, 8), new GameRules { FirstTapSafe = true }, new Random(2));
        engine.Reveal(0, 0);
        var poos = engine.Board.Where(c => c.IsPoo).ToList();
        engine.ToggleFlag(poos[0].Row, poos[0].Column);
        engine.Reveal(poos[1].Row, poos[1].Column);

        Assert.Equal(GameState.Lost, engine.State);
        Assert.True(poos[0].IsFlagged);
        Assert.False(poos[0].IsRevealed);
        Assert.All(poos.Skip(1), p => Assert.True(p.IsRevealed));
    }
}
