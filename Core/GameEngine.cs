namespace PooSweep.Core;

public sealed class GameEngine
{
    private readonly Random _rng;

    public GameEngine(Difficulty difficulty, GameRules? rules = null, Random? rng = null)
    {
        Difficulty = difficulty ?? throw new ArgumentNullException(nameof(difficulty));
        Difficulty.Validate();

        Rules = rules ?? new GameRules();
        Rules.Validate();

        _rng = rng ?? Random.Shared;

        Board = BoardGenerator.CreateEmpty(Difficulty);
        State = GameState.NotStarted;
        RemainingFlags = Difficulty.PooCount;
    }

    public Difficulty Difficulty { get; }
    public GameRules Rules { get; }

    public Board Board { get; private set; }

    public GameState State { get; private set; }

    public int RemainingFlags { get; private set; }

    public int RevealedSafeCells { get; private set; }

    public bool CanInteract => State is GameState.NotStarted or GameState.InProgress;

    public void Restart(Difficulty? difficulty = null)
    {
        var nextDifficulty = difficulty ?? Difficulty;
        nextDifficulty.Validate();

        Board = BoardGenerator.CreateEmpty(nextDifficulty);
        RemainingFlags = nextDifficulty.PooCount;
        RevealedSafeCells = 0;
        State = GameState.NotStarted;
    }

    public Cell GetCell(int row, int column) => Board[row, column];

    public void ToggleFlag(int row, int column)
    {
        if (!CanInteract) return;
        if (!Board.InBounds(row, column)) return;

        var cell = Board[row, column];
        if (cell.IsRevealed) return;

        if (cell.IsFlagged)
        {
            cell.IsFlagged = false;
            RemainingFlags++;
            return;
        }

        if (RemainingFlags <= 0) return;

        cell.IsFlagged = true;
        RemainingFlags--;
    }

    public void Reveal(int row, int column)
    {
        if (!CanInteract) return;
        if (!Board.InBounds(row, column)) return;

        var cell = Board[row, column];
        if (cell.IsRevealed || cell.IsFlagged) return;

        if (State == GameState.NotStarted)
        {
            (int row, int col)? safeCenter = Rules.FirstTapSafe ? (row, column) : null;
            BoardGenerator.PlacePooAndCounts(Board, Difficulty.PooCount, _rng, safeCenter, Rules.FirstTapSafeRadius);
            State = GameState.InProgress;
        }

        if (cell.IsPoo)
        {
            cell.IsRevealed = true;
            State = GameState.Lost;
            RevealAllPoo();
            return;
        }

        RevealFloodFill(row, column);

        if (CheckWin())
        {
            State = GameState.Won;
            AutoFlagAllPoo();
        }
    }

    public void Chord(int row, int column)
    {
        if (!CanInteract) return;
        if (!Board.InBounds(row, column)) return;

        var cell = Board[row, column];
        if (!cell.IsRevealed) return;
        if (cell.AdjacentPooCount <= 0) return;

        var flagged = 0;
        foreach (var n in Board.Neighbors(row, column))
            if (n.IsFlagged) flagged++;

        if (flagged != cell.AdjacentPooCount) return;

        foreach (var n in Board.Neighbors(row, column))
        {
            if (n.IsRevealed || n.IsFlagged) continue;
            Reveal(n.Row, n.Column);
            if (State is GameState.Lost) return;
        }
    }

    private void RevealFloodFill(int startRow, int startColumn)
    {
        var queue = new Queue<Cell>();
        var start = Board[startRow, startColumn];
        queue.Enqueue(start);

        while (queue.Count > 0)
        {
            var cell = queue.Dequeue();

            if (cell.IsRevealed || cell.IsFlagged) continue;
            if (cell.IsPoo) continue;

            cell.IsRevealed = true;
            RevealedSafeCells++;

            if (cell.AdjacentPooCount != 0) continue;

            foreach (var n in Board.Neighbors(cell.Row, cell.Column))
            {
                if (!n.IsRevealed && !n.IsFlagged && !n.IsPoo)
                    queue.Enqueue(n);
            }
        }
    }

    private bool CheckWin()
    {
        var needed = Difficulty.CellCount - Difficulty.PooCount;
        return RevealedSafeCells >= needed;
    }

    private void RevealAllPoo()
    {
        foreach (var c in Board)
            if (c.IsPoo)
                c.IsRevealed = true;
    }

    private void AutoFlagAllPoo()
    {
        foreach (var c in Board)
        {
            if (!c.IsPoo) continue;
            if (c.IsFlagged) continue;
            if (c.IsRevealed) continue;

            c.IsFlagged = true;
            if (RemainingFlags > 0) RemainingFlags--;
        }
    }
}
