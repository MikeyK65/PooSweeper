using System.Diagnostics;
using PooSweep.Core;
using PooSweep.Core.Stats;

namespace PooSweep.Services;

public enum ActionResult
{
    None,
    Dug,
    Cascade,
    Flagged,
    Unflagged,
    Chorded,
    Denied,
    Won,
    Lost,
}

public sealed record ActionOutcome(ActionResult Result, int CellsRevealed);

/// <summary>One game in progress: wraps the engine with timing and statistics.</summary>
public sealed class GameSession(StatsBook stats)
{
    private readonly Stopwatch _clock = new();
    private bool _recorded;

    public GameEngine Engine { get; private set; } = new(Difficulty.Easy);
    public Difficulty Difficulty => Engine.Difficulty;
    public WinRecord? LastWin { get; private set; }

    public TimeSpan Elapsed => _clock.Elapsed;

    public bool InProgress => Engine.State == GameState.InProgress;

    public void Start(Difficulty difficulty)
    {
        Abandon();
        var radius = difficulty.PooCount <= difficulty.CellCount - 9 ? 1 : 0;
        Engine = new GameEngine(difficulty, new GameRules { FirstTapSafe = true, FirstTapSafeRadius = radius });
        _clock.Reset();
        _recorded = false;
        LastWin = null;
    }

    /// <summary>Leaving a started game counts as a loss (like the classic).</summary>
    public void Abandon()
    {
        if (Engine.State == GameState.InProgress && !_recorded)
        {
            stats.RecordLoss(Difficulty.Id);
            _recorded = true;
        }
        _clock.Stop();
    }

    public ActionOutcome Reveal(int row, int col)
    {
        var cell = Engine.GetCell(row, col);
        if (!Engine.CanInteract || cell.IsRevealed) return new(ActionResult.None, 0);
        if (cell.IsFlagged) return new(ActionResult.Denied, 0);

        var before = Engine.RevealedSafeCells;
        Engine.Reveal(row, col);
        if (Engine.State == GameState.InProgress && !_clock.IsRunning) _clock.Start();
        var revealed = Engine.RevealedSafeCells - before;
        return Finish(revealed > 12 ? ActionResult.Cascade : ActionResult.Dug, revealed);
    }

    public ActionOutcome ToggleFlag(int row, int col)
    {
        var cell = Engine.GetCell(row, col);
        if (!Engine.CanInteract || cell.IsRevealed) return new(ActionResult.None, 0);
        var was = cell.IsFlagged;
        Engine.ToggleFlag(row, col);
        if (cell.IsFlagged == was) return new(ActionResult.Denied, 0);
        return new(cell.IsFlagged ? ActionResult.Flagged : ActionResult.Unflagged, 0);
    }

    public ActionOutcome Chord(int row, int col)
    {
        var cell = Engine.GetCell(row, col);
        if (!Engine.CanInteract || !cell.IsRevealed || cell.AdjacentPooCount == 0) return new(ActionResult.None, 0);

        var hidden = Engine.Board.Neighbors(row, col).Count(n => !n.IsRevealed && !n.IsFlagged);
        if (hidden == 0) return new(ActionResult.None, 0);

        var before = Engine.RevealedSafeCells;
        var stateBefore = Engine.State;
        Engine.Chord(row, col);
        var revealed = Engine.RevealedSafeCells - before;
        if (revealed == 0 && Engine.State == stateBefore) return new(ActionResult.Denied, 0);
        return Finish(revealed > 12 ? ActionResult.Cascade : ActionResult.Chorded, revealed);
    }

    private ActionOutcome Finish(ActionResult normal, int revealed)
    {
        switch (Engine.State)
        {
            case GameState.Won:
                _clock.Stop();
                if (!_recorded)
                {
                    LastWin = stats.RecordWin(Difficulty.Id, _clock.Elapsed, DateTimeOffset.Now);
                    _recorded = true;
                }
                return new(ActionResult.Won, revealed);

            case GameState.Lost:
                _clock.Stop();
                if (!_recorded)
                {
                    stats.RecordLoss(Difficulty.Id);
                    _recorded = true;
                }
                return new(ActionResult.Lost, revealed);

            default:
                return new(normal, revealed);
        }
    }
}
