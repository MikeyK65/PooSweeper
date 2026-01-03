namespace PooSweep.Core;

public sealed class Cell
{
    public Cell(int row, int column)
    {
        Row = row;
        Column = column;
    }

    public int Row { get; }
    public int Column { get; }

    public bool IsPoo { get; internal set; }
    public bool IsRevealed { get; internal set; }
    public bool IsFlagged { get; internal set; }

    public int AdjacentPooCount { get; internal set; }
}
