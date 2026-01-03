namespace PooSweep.Core;

public enum DifficultyId
{
    Easy,
    Medium,
    Hard,
    Custom
}

public sealed record Difficulty(
    DifficultyId Id,
    string Name,
    int Rows,
    int Columns,
    int PooCount)
{
    public static readonly Difficulty Easy = new(DifficultyId.Easy, "Easy", 8, 8, 10);
    public static readonly Difficulty Medium = new(DifficultyId.Medium, "Medium", 12, 12, 25);
    public static readonly Difficulty Hard = new(DifficultyId.Hard, "Hard", 16, 16, 50);

    public int CellCount => checked(Rows * Columns);

    public void Validate()
    {
        if (Rows <= 0) throw new ArgumentOutOfRangeException(nameof(Rows));
        if (Columns <= 0) throw new ArgumentOutOfRangeException(nameof(Columns));
        if (PooCount < 0) throw new ArgumentOutOfRangeException(nameof(PooCount));
        if (PooCount >= CellCount) throw new ArgumentOutOfRangeException(nameof(PooCount), "PooCount must be less than total cell count.");
    }
}
