using System.Collections;

namespace PooSweep.Core;

public sealed class Board : IEnumerable<Cell>
{
    private readonly Cell[,] _cells;

    public Board(int rows, int columns)
    {
        if (rows <= 0) throw new ArgumentOutOfRangeException(nameof(rows));
        if (columns <= 0) throw new ArgumentOutOfRangeException(nameof(columns));

        Rows = rows;
        Columns = columns;

        _cells = new Cell[rows, columns];
        for (var r = 0; r < rows; r++)
        for (var c = 0; c < columns; c++)
            _cells[r, c] = new Cell(r, c);
    }

    public int Rows { get; }
    public int Columns { get; }

    public Cell this[int row, int column] => _cells[row, column];

    public bool InBounds(int row, int column)
        => row >= 0 && row < Rows && column >= 0 && column < Columns;

    public IEnumerable<Cell> Neighbors(int row, int column)
    {
        for (var dr = -1; dr <= 1; dr++)
        for (var dc = -1; dc <= 1; dc++)
        {
            if (dr == 0 && dc == 0) continue;
            var nr = row + dr;
            var nc = column + dc;
            if (InBounds(nr, nc))
                yield return _cells[nr, nc];
        }
    }

    public IEnumerator<Cell> GetEnumerator()
    {
        for (var r = 0; r < Rows; r++)
        for (var c = 0; c < Columns; c++)
            yield return _cells[r, c];
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
