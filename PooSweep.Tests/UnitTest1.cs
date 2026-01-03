using PooSweep.Core;

namespace PooSweep.Tests;

public sealed class GameEngineTests
{
    [Fact]
    public void FirstTapSafe_DoesNotPlacePooOnFirstCell()
    {
        var difficulty = new Difficulty(DifficultyId.Custom, "Test", 8, 8, 10);
        var rules = new GameRules { FirstTapSafe = true, FirstTapSafeRadius = 0 };
        var rng = new Random(123);
        var engine = new GameEngine(difficulty, rules, rng);

        engine.Reveal(0, 0);

        var cell = engine.Board[0, 0];
        Assert.False(cell.IsPoo);
        Assert.True(cell.IsRevealed);
    }

    [Fact]
    public void FloodFill_RevealsMultipleCells_WhenZeroAdjacent()
    {
        var difficulty = new Difficulty(DifficultyId.Custom, "Test", 5, 5, 0);
        var engine = new GameEngine(difficulty, new GameRules { FirstTapSafe = false }, new Random(1));

        engine.Reveal(2, 2);

        var revealed = engine.Board.Count(c => c.IsRevealed);
        Assert.Equal(25, revealed);
        Assert.Equal(GameState.Won, engine.State);
    }

    [Fact]
    public void ToggleFlag_DoesNotAllowMoreFlagsThanPooCount()
    {
        var difficulty = new Difficulty(DifficultyId.Custom, "Test", 3, 3, 1);
        var engine = new GameEngine(difficulty, new GameRules { FirstTapSafe = false }, new Random(1));

        engine.ToggleFlag(0, 0);
        engine.ToggleFlag(0, 1);

        Assert.True(engine.Board[0, 0].IsFlagged);
        Assert.False(engine.Board[0, 1].IsFlagged);
        Assert.Equal(0, engine.RemainingFlags);
    }

    [Fact]
    public void RevealPoo_LosesGame_AndRevealsAllPoo()
    {
        var difficulty = new Difficulty(DifficultyId.Custom, "Test", 2, 2, 1);
        var engine = new GameEngine(difficulty, new GameRules { FirstTapSafe = false }, new Random(0));

        engine.Reveal(0, 0); // triggers placement

        var pooCell = engine.Board.First(c => c.IsPoo);
        engine.Reveal(pooCell.Row, pooCell.Column);

        Assert.Equal(GameState.Lost, engine.State);
        Assert.All(engine.Board.Where(c => c.IsPoo), c => Assert.True(c.IsRevealed));
    }
}
