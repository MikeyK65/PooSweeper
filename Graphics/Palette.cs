using SkiaSharp;

namespace PooSweep.Graphics;

public enum BoardTheme
{
    Day,
    Dusk,
}

/// <summary>Colours for one visual theme of the yard.</summary>
public sealed class Palette
{
    public required string Name { get; init; }

    public required SKColor SkyTop { get; init; }
    public required SKColor SkyBottom { get; init; }
    public required SKColor HillFar { get; init; }
    public required SKColor HillNear { get; init; }
    public required SKColor Sun { get; init; }
    public required SKColor Cloud { get; init; }
    public bool Stars { get; init; }

    public required SKColor GrassA { get; init; }
    public required SKColor GrassB { get; init; }
    public required SKColor GrassHover { get; init; }
    public required SKColor GrassShadow { get; init; }
    public required SKColor GrassBlade { get; init; }

    public required SKColor DirtA { get; init; }
    public required SKColor DirtB { get; init; }
    public required SKColor DirtSpeck { get; init; }
    public required SKColor Exploded { get; init; }

    public required SKColor FrameWood { get; init; }
    public required SKColor FrameWoodDark { get; init; }
    public required SKColor FrameWoodLight { get; init; }

    public static readonly Palette Day = new()
    {
        Name = "Sunny Day",
        SkyTop = new SKColor(0x6F, 0xC3, 0xF5),
        SkyBottom = new SKColor(0xD4, 0xF1, 0xFF),
        HillFar = new SKColor(0x9C, 0xD8, 0x6E),
        HillNear = new SKColor(0x74, 0xC1, 0x4F),
        Sun = new SKColor(0xFF, 0xD9, 0x4A),
        Cloud = SKColors.White,
        GrassA = new SKColor(0x8E, 0xD0, 0x5C),
        GrassB = new SKColor(0x7F, 0xC4, 0x4E),
        GrassHover = new SKColor(0xA9, 0xE0, 0x76),
        GrassShadow = new SKColor(0x4F, 0x8F, 0x2C),
        GrassBlade = new SKColor(0x5E, 0xA3, 0x37),
        DirtA = new SKColor(0xEC, 0xCF, 0xA2),
        DirtB = new SKColor(0xE2, 0xC2, 0x93),
        DirtSpeck = new SKColor(0xC7, 0xA2, 0x70),
        Exploded = new SKColor(0xF2, 0x7A, 0x6B),
        FrameWood = new SKColor(0xA8, 0x6B, 0x3A),
        FrameWoodDark = new SKColor(0x6E, 0x40, 0x1E),
        FrameWoodLight = new SKColor(0xC9, 0x8B, 0x55),
    };

    public static readonly Palette Dusk = new()
    {
        Name = "Dusk",
        SkyTop = new SKColor(0x2B, 0x23, 0x5E),
        SkyBottom = new SKColor(0xF0, 0x8A, 0x5D),
        HillFar = new SKColor(0x4E, 0x6B, 0x55),
        HillNear = new SKColor(0x37, 0x57, 0x3E),
        Sun = new SKColor(0xFF, 0xF3, 0xC4),
        Cloud = new SKColor(0xF7, 0xC6, 0xB8),
        Stars = true,
        GrassA = new SKColor(0x5C, 0x9A, 0x4B),
        GrassB = new SKColor(0x51, 0x8D, 0x42),
        GrassHover = new SKColor(0x74, 0xB0, 0x5F),
        GrassShadow = new SKColor(0x2C, 0x55, 0x26),
        GrassBlade = new SKColor(0x3A, 0x6E, 0x31),
        DirtA = new SKColor(0xC9, 0xA7, 0x86),
        DirtB = new SKColor(0xBD, 0x9A, 0x79),
        DirtSpeck = new SKColor(0x9C, 0x7A, 0x5A),
        Exploded = new SKColor(0xD8, 0x5F, 0x55),
        FrameWood = new SKColor(0x7E, 0x50, 0x2E),
        FrameWoodDark = new SKColor(0x4A, 0x2B, 0x16),
        FrameWoodLight = new SKColor(0x9A, 0x69, 0x41),
    };

    public static Palette For(BoardTheme theme) => theme == BoardTheme.Dusk ? Dusk : Day;

    /// <summary>Classic minesweeper-ish number colours, tuned for the dirt background.</summary>
    public static readonly SKColor[] Numbers =
    [
        SKColors.Transparent,
        new(0x1F, 0x5F, 0xD1), // 1 blue
        new(0x2E, 0x8B, 0x3A), // 2 green
        new(0xD6, 0x2B, 0x3A), // 3 red
        new(0x5B, 0x2C, 0x9E), // 4 purple
        new(0x9A, 0x2A, 0x14), // 5 maroon
        new(0x0E, 0x8C, 0x8C), // 6 teal
        new(0x2A, 0x2A, 0x2A), // 7 black
        new(0x6E, 0x6E, 0x6E), // 8 grey
    ];

    /// <summary>Okabe–Ito based palette that stays distinguishable for colour-blind players.</summary>
    public static readonly SKColor[] NumbersColorblind =
    [
        SKColors.Transparent,
        new(0x00, 0x5A, 0xB5), // 1 blue
        new(0xC2, 0x5E, 0x00), // 2 orange
        new(0x7A, 0x1F, 0x86), // 3 purple
        new(0x00, 0x7A, 0x5C), // 4 bluish green
        new(0xB0, 0x1E, 0x55), // 5 reddish purple
        new(0x1B, 0x7E, 0xB8), // 6 sky
        new(0x00, 0x00, 0x00), // 7 black
        new(0x55, 0x55, 0x55), // 8 grey
    ];
}
