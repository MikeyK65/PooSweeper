namespace PooSweep.Core;

public sealed class GameRules
{
    public bool FirstTapSafe { get; init; } = true;

    public int FirstTapSafeRadius { get; init; } = 0;

    public void Validate()
    {
        if (FirstTapSafeRadius < 0) throw new ArgumentOutOfRangeException(nameof(FirstTapSafeRadius));
    }
}
