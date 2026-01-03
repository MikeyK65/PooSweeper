namespace PooSweep.Core;

public static class BoardGenerator
{
    public static Board CreateEmpty(Difficulty difficulty)
    {
        if (difficulty is null) throw new ArgumentNullException(nameof(difficulty));
        difficulty.Validate();
        return new Board(difficulty.Rows, difficulty.Columns);
    }

    public static void PlacePooAndCounts(
        Board board,
        int pooCount,
        Random rng,
        (int row, int col)? safeCenter = null,
        int safeRadius = 0)
    {
        if (board is null) throw new ArgumentNullException(nameof(board));
        if (rng is null) throw new ArgumentNullException(nameof(rng));
        if (pooCount < 0) throw new ArgumentOutOfRangeException(nameof(pooCount));

        var excluded = new HashSet<(int r, int c)>();
        if (safeCenter is { } sc)
        {
            for (var r = sc.row - safeRadius; r <= sc.row + safeRadius; r++)
            for (var c = sc.col - safeRadius; c <= sc.col + safeRadius; c++)
                if (board.InBounds(r, c))
                    excluded.Add((r, c));
        }

        var available = new List<(int r, int c)>(board.Rows * board.Columns);
        for (var r = 0; r < board.Rows; r++)
        for (var c = 0; c < board.Columns; c++)
            if (!excluded.Contains((r, c)))
                available.Add((r, c));

        if (pooCount > available.Count)
            throw new ArgumentOutOfRangeException(nameof(pooCount), "PooCount exceeds available cells after applying safe zone.");

        for (var i = 0; i < pooCount; i++)
        {
            var idx = rng.Next(available.Count);
            var (r, c) = available[idx];
            available.RemoveAt(idx);
            board[r, c].IsPoo = true;
        }

        foreach (var cell in board)
        {
            var count = 0;
            foreach (var n in board.Neighbors(cell.Row, cell.Column))
                if (n.IsPoo) count++;
            cell.AdjacentPooCount = count;
        }
    }
}
