using SkiaSharp;

namespace PooSweep.Graphics;

/// <summary>Typefaces used for Skia-drawn text (the same font is registered for XAML as "Display").</summary>
public static class GameFonts
{
    private static SKTypeface? _display;

    public static SKTypeface Display => _display ??= Load();

    private static SKTypeface Load()
    {
        try
        {
            var asm = typeof(GameFonts).Assembly;
            using var s = asm.GetManifestResourceStream("PooSweep.LuckiestGuy.ttf");
            if (s is not null)
            {
                var tf = SKTypeface.FromStream(s);
                if (tf is not null) return tf;
            }
        }
        catch
        {
            // fall through to a system font
        }

        return SKTypeface.FromFamilyName("Arial", SKFontStyle.Bold) ?? SKTypeface.Default;
    }
}
