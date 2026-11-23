using SkiaSharp;

namespace PooSweep.Graphics;

public enum DogMood
{
    Happy,
    Worried,
    Celebrate,
    Sick,
}

/// <summary>All of the game's procedural cartoon art.</summary>
public static class Art
{
    private static readonly SKColor Ink = new(0x3B, 0x24, 0x14);
    private static readonly SKColor PooLight = new(0xA8, 0x72, 0x40);
    private static readonly SKColor PooMid = new(0x84, 0x52, 0x2A);
    private static readonly SKColor PooDark = new(0x5A, 0x34, 0x18);
    private static readonly SKColor PooLine = new(0x3E, 0x23, 0x0F);

    private static readonly SKPaint Fill = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private static readonly SKPaint Stroke = new() { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeCap = SKStrokeCap.Round, StrokeJoin = SKStrokeJoin.Round };

    private static SKPaint F(SKColor c)
    {
        Fill.Shader = null;
        Fill.Color = c;
        Fill.MaskFilter = null;
        return Fill;
    }

    private static SKPaint S(SKColor c, float width)
    {
        Stroke.Shader = null;
        Stroke.Color = c;
        Stroke.StrokeWidth = width;
        Stroke.MaskFilter = null;
        return Stroke;
    }

    /// <summary>Deterministic per-cell hash for decoration placement.</summary>
    public static uint Hash(int a, int b, int salt = 0)
    {
        unchecked
        {
            var h = (uint)(a * 73856093) ^ (uint)(b * 19349663) ^ (uint)(salt * 83492791);
            h ^= h >> 13;
            h *= 0x5bd1e995;
            h ^= h >> 15;
            return h;
        }
    }

    private static float H01(int a, int b, int salt) => (Hash(a, b, salt) & 0xFFFF) / 65535f;

    // ======================================================================
    //  Tiles
    // ======================================================================

    /// <summary>An un-dug grass tile with a soft bevel, blades and the odd daisy.</summary>
    public static void GrassTile(SKCanvas c, SKRect r, Palette p, int row, int col, bool hover, bool pressed, float alpha = 1, bool flowers = true)
    {
        var s = r.Width;
        var radius = s * 0.16f;
        var gap = Math.Max(1f, s * 0.035f);
        var rr = new SKRect(r.Left + gap, r.Top + gap, r.Right - gap, r.Bottom - gap);
        var lift = pressed ? 0 : s * 0.07f;
        var a = (byte)(255 * alpha);

        // shadow / side
        c.DrawRoundRect(rr, radius, radius, F(p.GrassShadow.WithAlpha(a)));

        var baseColor = (row + col) % 2 == 0 ? p.GrassA : p.GrassB;
        if (hover && !pressed) baseColor = p.GrassHover;
        if (pressed) baseColor = Darken(baseColor, 0.08f);

        var top = new SKRect(rr.Left, rr.Top, rr.Right, rr.Bottom - lift);
        Fill.Color = SKColors.White;
        using (var shader = SKShader.CreateLinearGradient(
            new SKPoint(0, top.Top), new SKPoint(0, top.Bottom),
            [Lighten(baseColor, 0.10f).WithAlpha(a), baseColor.WithAlpha(a)], SKShaderTileMode.Clamp))
        {
            Fill.Shader = shader;
            c.DrawRoundRect(top, radius, radius, Fill);
            Fill.Shader = null;
        }

        // grass blades
        var blade = p.GrassBlade.WithAlpha((byte)(200 * alpha));
        var bw = Math.Max(1f, s * 0.045f);
        var n = 2 + (int)(Hash(row, col, 1) % 2);
        for (var i = 0; i < n; i++)
        {
            var x = top.Left + top.Width * (0.18f + 0.64f * H01(row, col, 10 + i));
            var y = top.Top + top.Height * (0.30f + 0.55f * H01(row, col, 20 + i));
            var h = s * 0.12f;
            c.DrawLine(x, y, x - h * 0.45f, y - h, S(blade, bw));
            c.DrawLine(x, y, x + h * 0.1f, y - h * 1.15f, S(blade, bw));
            c.DrawLine(x, y, x + h * 0.55f, y - h * 0.8f, S(blade, bw));
        }

        // occasional daisy
        if (flowers && Hash(row, col, 3) % 11 == 0 && s > 18)
        {
            var fx = top.Left + top.Width * (0.25f + 0.5f * H01(row, col, 4));
            var fy = top.Top + top.Height * (0.25f + 0.4f * H01(row, col, 5));
            var pr = s * 0.05f;
            for (var k = 0; k < 5; k++)
            {
                var ang = k * MathF.Tau / 5;
                c.DrawCircle(fx + MathF.Cos(ang) * pr * 1.3f, fy + MathF.Sin(ang) * pr * 1.3f, pr, F(SKColors.White.WithAlpha(a)));
            }
            c.DrawCircle(fx, fy, pr * 0.9f, F(new SKColor(0xFF, 0xC8, 0x2E, a)));
        }

        // top shine
        if (!pressed)
        {
            var shine = new SKRect(top.Left + radius * 0.6f, top.Top + s * 0.06f, top.Right - radius * 0.6f, top.Top + s * 0.06f + Math.Max(1.2f, s * 0.04f));
            c.DrawRoundRect(shine, shine.Height, shine.Height, F(SKColors.White.WithAlpha((byte)(70 * alpha))));
        }
    }

    /// <summary>A dug-up dirt tile with a few pebbles.</summary>
    public static void DirtTile(SKCanvas c, SKRect r, Palette p, int row, int col, bool exploded)
    {
        var s = r.Width;
        var col0 = exploded ? p.Exploded : (row + col) % 2 == 0 ? p.DirtA : p.DirtB;
        c.DrawRect(r, F(col0));

        if (exploded) return;

        var speck = p.DirtSpeck;
        for (var i = 0; i < 3; i++)
        {
            if (Hash(row, col, 40 + i) % 3 == 0) continue;
            var x = r.Left + r.Width * (0.15f + 0.7f * H01(row, col, 50 + i));
            var y = r.Top + r.Height * (0.15f + 0.7f * H01(row, col, 60 + i));
            var rad = s * (0.025f + 0.025f * H01(row, col, 70 + i));
            c.DrawOval(x, y, rad * 1.4f, rad, F(speck));
        }
    }

    /// <summary>Soft shadow cast onto dirt by the raised grass next to it.</summary>
    public static void LipShadow(SKCanvas c, SKRect r, bool top, bool left)
    {
        var s = r.Width;
        if (top)
        {
            Fill.Shader = SKShader.CreateLinearGradient(new SKPoint(0, r.Top), new SKPoint(0, r.Top + s * 0.2f),
                [new SKColor(0, 0, 0, 60), SKColors.Transparent], SKShaderTileMode.Clamp);
            c.DrawRect(new SKRect(r.Left, r.Top, r.Right, r.Top + s * 0.2f), Fill);
            Fill.Shader = null;
        }
        if (left)
        {
            Fill.Shader = SKShader.CreateLinearGradient(new SKPoint(r.Left, 0), new SKPoint(r.Left + s * 0.14f, 0),
                [new SKColor(0, 0, 0, 40), SKColors.Transparent], SKShaderTileMode.Clamp);
            c.DrawRect(new SKRect(r.Left, r.Top, r.Left + s * 0.14f, r.Bottom), Fill);
            Fill.Shader = null;
        }
    }

    public static void Number(SKCanvas c, SKRect r, int n, bool colorblind, float scale = 1)
    {
        if (n <= 0) return;
        var colors = colorblind ? Palette.NumbersColorblind : Palette.Numbers;
        var s = r.Width;
        using var font = new SKFont(GameFonts.Display, s * 0.66f * scale);
        var text = n.ToString();
        var metrics = font.Metrics;
        var baseline = r.MidY - (metrics.Ascent + metrics.Descent) / 2f;

        c.DrawText(text, r.MidX + s * 0.03f, baseline + s * 0.04f, SKTextAlign.Center, font, F(new SKColor(0, 0, 0, 45)));
        c.DrawText(text, r.MidX, baseline, SKTextAlign.Center, font, F(colors[Math.Clamp(n, 1, 8)]));

    }

    // ======================================================================
    //  Poo
    // ======================================================================

    /// <summary>Draws a cartoon poo centred in a box of size <paramref name="s"/>.</summary>
    public static void Poo(SKCanvas c, float cx, float cy, float s, float time = 0, bool face = true, bool stink = false, float squash = 0)
    {
        c.Save();
        c.Translate(cx, cy + s * 0.04f);
        if (squash != 0) c.Scale(1 + squash, 1 - squash, 0, s * 0.35f);

        var line = Math.Max(1f, s * 0.035f);

        // ground shadow
        c.DrawOval(0, s * 0.36f, s * 0.36f, s * 0.07f, F(new SKColor(0, 0, 0, 50)));

        void Blob(float y, float w, float h)
        {
            var rect = SKRect.Create(-w / 2, y - h / 2, w, h);
            Fill.Shader = SKShader.CreateRadialGradient(new SKPoint(-w * 0.18f, y - h * 0.3f), w * 0.75f,
                [PooLight, PooMid, PooDark], [0f, 0.55f, 1f], SKShaderTileMode.Clamp);
            c.DrawRoundRect(rect, h / 2, h / 2, Fill);
            Fill.Shader = null;
            c.DrawRoundRect(rect, h / 2, h / 2, S(PooLine, line));
            // shine
            c.DrawOval(-w * 0.2f, y - h * 0.18f, w * 0.12f, h * 0.12f, F(new SKColor(255, 255, 255, 90)));
        }

        // swirl tip
        using (var tip = new SKPathBuilder())
        {
            tip.MoveTo(-s * 0.1f, -s * 0.18f);
            tip.CubicTo(-s * 0.08f, -s * 0.34f, s * 0.08f, -s * 0.36f, s * 0.12f, -s * 0.42f);
            tip.CubicTo(s * 0.1f, -s * 0.3f, s * 0.12f, -s * 0.24f, s * 0.1f, -s * 0.16f);
            tip.Close();
            c.DrawPath(tip, F(PooMid));
            c.DrawPath(tip, S(PooLine, line));
        }

        Blob(s * 0.25f, s * 0.80f, s * 0.26f);
        Blob(s * 0.04f, s * 0.62f, s * 0.24f);
        Blob(-s * 0.15f, s * 0.40f, s * 0.20f);

        if (face && s >= 14)
        {
            // eyes
            var ey = s * 0.03f;
            var ex = s * 0.11f;
            var er = s * 0.065f;
            var blink = ((int)(time * 10) % 37) == 0;
            foreach (var sx in new[] { -1f, 1f })
            {
                if (blink)
                {
                    c.DrawLine(sx * ex - er, ey, sx * ex + er, ey, S(PooLine, line));
                }
                else
                {
                    c.DrawCircle(sx * ex, ey, er, F(SKColors.White));
                    c.DrawCircle(sx * ex, ey, er, S(PooLine, line * 0.7f));
                    c.DrawCircle(sx * ex + er * 0.25f, ey + er * 0.15f, er * 0.5f, F(Ink));
                    c.DrawCircle(sx * ex + er * 0.05f, ey - er * 0.15f, er * 0.18f, F(SKColors.White));
                }
            }
            // grin
            using var mouth = new SKPathBuilder();
            mouth.MoveTo(-s * 0.1f, s * 0.21f);
            mouth.QuadTo(0, s * 0.3f, s * 0.1f, s * 0.21f);
            c.DrawPath(mouth, S(PooLine, line));
        }

        c.Restore();

        if (stink) StinkLines(c, cx, cy - s * 0.25f, s, time);
    }

    public static void StinkLines(SKCanvas c, float cx, float cy, float s, float time)
    {
        var w = Math.Max(1f, s * 0.04f);
        for (var i = 0; i < 3; i++)
        {
            var phase = (time * 0.6f + i / 3f) % 1f;
            var x = cx + (i - 1) * s * 0.18f;
            var y = cy - phase * s * 0.35f;
            var alpha = (byte)(200 * MathF.Sin(phase * MathF.PI));
            using var p = new SKPathBuilder();
            p.MoveTo(x, y);
            for (var k = 1; k <= 6; k++)
            {
                var yy = y - k * s * 0.035f;
                var xx = x + MathF.Sin(k * 1.4f + time * 6 + i) * s * 0.035f;
                p.LineTo(xx, yy);
            }
            c.DrawPath(p, S(new SKColor(0x7C, 0xB3, 0x42, alpha), w));
        }
    }

    public static void Splat(SKCanvas c, float cx, float cy, float s, int seed)
    {
        var col = PooDark.WithAlpha(150);
        c.DrawOval(cx, cy + s * 0.18f, s * 0.42f, s * 0.2f, F(col));
        for (var i = 0; i < 8; i++)
        {
            var ang = i / 8f * MathF.Tau + H01(seed, i, 1) * 0.5f;
            var d = s * (0.28f + 0.2f * H01(seed, i, 2));
            var rad = s * (0.04f + 0.05f * H01(seed, i, 3));
            c.DrawCircle(cx + MathF.Cos(ang) * d, cy + s * 0.12f + MathF.Sin(ang) * d * 0.6f, rad, F(col));
        }
    }

    public static void Fly(SKCanvas c, float x, float y, float s, float time)
    {
        var wing = (int)(time * 40) % 2 == 0 ? 0.6f : 1f;
        c.DrawOval(x - s * 0.03f, y - s * 0.03f, s * 0.035f, s * 0.02f * wing, F(new SKColor(255, 255, 255, 190)));
        c.DrawOval(x + s * 0.03f, y - s * 0.03f, s * 0.035f, s * 0.02f * wing, F(new SKColor(255, 255, 255, 190)));
        c.DrawCircle(x, y, s * 0.03f, F(new SKColor(0x22, 0x22, 0x22)));
    }

    // ======================================================================
    //  Flag
    // ======================================================================

    public static void Flag(SKCanvas c, float cx, float cy, float s, float time = 0, bool wrong = false, float alpha = 1)
    {
        var a = (byte)(255 * alpha);
        var x = cx - s * 0.12f;
        var top = cy - s * 0.32f;
        var bottom = cy + s * 0.28f;

        // mound
        c.DrawOval(x + s * 0.02f, bottom, s * 0.2f, s * 0.07f, F(new SKColor(0x6B, 0x45, 0x22, (byte)(170 * alpha))));
        // pole
        c.DrawLine(x, bottom, x, top, S(new SKColor(0x6E, 0x40, 0x1E, a), Math.Max(1.5f, s * 0.06f)));
        c.DrawCircle(x, top, s * 0.045f, F(new SKColor(0xFF, 0xD5, 0x4F, a)));

        // waving pennant
        var wave = MathF.Sin(time * 5) * s * 0.025f;
        using var p = new SKPathBuilder();
        p.MoveTo(x, top + s * 0.02f);
        p.QuadTo(x + s * 0.22f, top + s * 0.02f + wave, x + s * 0.46f, top + s * 0.15f + wave * 0.5f);
        p.QuadTo(x + s * 0.22f, top + s * 0.26f - wave, x, top + s * 0.3f);
        p.Close();
        Fill.Color = SKColors.White;
        Fill.Shader = SKShader.CreateLinearGradient(new SKPoint(x, top), new SKPoint(x + s * 0.46f, top + s * 0.3f),
            [new SKColor(0xFF, 0x5A, 0x4E, a), new SKColor(0xD3, 0x2F, 0x2F, a)], SKShaderTileMode.Clamp);
        c.DrawPath(p, Fill);
        Fill.Shader = null;
        c.DrawPath(p, S(new SKColor(0x8E, 0x1B, 0x1B, a), Math.Max(1f, s * 0.025f)));

        // paw print on the pennant
        var px = x + s * 0.17f;
        var py = top + s * 0.165f + wave * 0.6f;
        var pr = s * 0.045f;
        var white = new SKColor(255, 255, 255, (byte)(230 * alpha));
        c.DrawOval(px, py + pr * 0.4f, pr * 1.0f, pr * 0.8f, F(white));
        c.DrawCircle(px - pr * 1.1f, py - pr * 0.6f, pr * 0.42f, F(white));
        c.DrawCircle(px - pr * 0.35f, py - pr * 1.2f, pr * 0.42f, F(white));
        c.DrawCircle(px + pr * 0.45f, py - pr * 1.15f, pr * 0.42f, F(white));
        c.DrawCircle(px + pr * 1.15f, py - pr * 0.5f, pr * 0.42f, F(white));

        if (wrong)
        {
            var w = Math.Max(2f, s * 0.09f);
            var d = s * 0.3f;
            c.DrawLine(cx - d, cy - d, cx + d, cy + d, S(new SKColor(0xFF, 0xFF, 0xFF, a), w * 1.6f));
            c.DrawLine(cx + d, cy - d, cx - d, cy + d, S(new SKColor(0xFF, 0xFF, 0xFF, a), w * 1.6f));
            c.DrawLine(cx - d, cy - d, cx + d, cy + d, S(new SKColor(0xD3, 0x2F, 0x2F, a), w));
            c.DrawLine(cx + d, cy - d, cx - d, cy + d, S(new SKColor(0xD3, 0x2F, 0x2F, a), w));
        }
    }

    // ======================================================================
    //  Icons
    // ======================================================================

    public static void Shovel(SKCanvas c, float cx, float cy, float s, SKColor tint)
    {
        c.Save();
        c.Translate(cx, cy);
        c.RotateDegrees(35);
        var w = s * 0.08f;
        c.DrawLine(0, -s * 0.42f, 0, s * 0.05f, S(new SKColor(0x8D, 0x5A, 0x2B), w));
        c.DrawLine(-s * 0.1f, -s * 0.42f, s * 0.1f, -s * 0.42f, S(new SKColor(0x8D, 0x5A, 0x2B), w));
        using var blade = new SKPathBuilder();
        blade.MoveTo(-s * 0.15f, s * 0.04f);
        blade.LineTo(s * 0.15f, s * 0.04f);
        blade.LineTo(s * 0.13f, s * 0.3f);
        blade.QuadTo(0, s * 0.46f, -s * 0.13f, s * 0.3f);
        blade.Close();
        c.DrawPath(blade, F(tint));
        c.DrawPath(blade, S(Ink, s * 0.035f));
        c.Restore();
    }

    public static void Clock(SKCanvas c, float cx, float cy, float s, float time)
    {
        var r = s * 0.38f;
        c.DrawCircle(cx, cy, r, F(SKColors.White));
        c.DrawCircle(cx, cy, r, S(Ink, s * 0.07f));
        c.DrawLine(cx, cy - r - s * 0.06f, cx, cy - r - s * 0.12f, S(Ink, s * 0.08f));
        var ang = time * MathF.Tau / 4f - MathF.PI / 2;
        c.DrawLine(cx, cy, cx + MathF.Cos(ang) * r * 0.7f, cy + MathF.Sin(ang) * r * 0.7f, S(new SKColor(0xD3, 0x2F, 0x2F), s * 0.06f));
        c.DrawLine(cx, cy, cx, cy - r * 0.5f, S(Ink, s * 0.06f));
        c.DrawCircle(cx, cy, s * 0.04f, F(Ink));
    }

    public static void Speaker(SKCanvas c, float cx, float cy, float s, bool on, SKColor color)
    {
        using var p = new SKPathBuilder();
        p.MoveTo(cx - s * 0.32f, cy - s * 0.1f);
        p.LineTo(cx - s * 0.16f, cy - s * 0.1f);
        p.LineTo(cx + s * 0.04f, cy - s * 0.28f);
        p.LineTo(cx + s * 0.04f, cy + s * 0.28f);
        p.LineTo(cx - s * 0.16f, cy + s * 0.1f);
        p.LineTo(cx - s * 0.32f, cy + s * 0.1f);
        p.Close();
        c.DrawPath(p, F(color));
        var w = s * 0.07f;
        if (on)
        {
            c.DrawArc(SKRect.Create(cx - s * 0.08f, cy - s * 0.16f, s * 0.3f, s * 0.32f), -50, 100, false, S(color, w));
            c.DrawArc(SKRect.Create(cx - s * 0.12f, cy - s * 0.3f, s * 0.5f, s * 0.6f), -50, 100, false, S(color, w));
        }
        else
        {
            c.DrawLine(cx + s * 0.12f, cy - s * 0.12f, cx + s * 0.34f, cy + s * 0.12f, S(color, w));
            c.DrawLine(cx + s * 0.34f, cy - s * 0.12f, cx + s * 0.12f, cy + s * 0.12f, S(color, w));
        }
    }

    public static void Gear(SKCanvas c, float cx, float cy, float s, SKColor color, float rot = 0)
    {
        c.Save();
        c.Translate(cx, cy);
        c.RotateDegrees(rot);
        for (var i = 0; i < 8; i++)
        {
            c.Save();
            c.RotateDegrees(i * 45);
            c.DrawRoundRect(SKRect.Create(-s * 0.07f, -s * 0.42f, s * 0.14f, s * 0.2f), s * 0.03f, s * 0.03f, F(color));
            c.Restore();
        }
        c.DrawCircle(0, 0, s * 0.28f, F(color));
        c.DrawCircle(0, 0, s * 0.11f, F(SKColors.White));
        c.Restore();
    }

    public static void Trophy(SKCanvas c, float cx, float cy, float s, SKColor color)
    {
        using var cup = new SKPathBuilder();
        cup.MoveTo(cx - s * 0.26f, cy - s * 0.32f);
        cup.LineTo(cx + s * 0.26f, cy - s * 0.32f);
        cup.QuadTo(cx + s * 0.26f, cy + s * 0.08f, cx, cy + s * 0.1f);
        cup.QuadTo(cx - s * 0.26f, cy + s * 0.08f, cx - s * 0.26f, cy - s * 0.32f);
        cup.Close();
        c.DrawArc(SKRect.Create(cx - s * 0.4f, cy - s * 0.3f, s * 0.24f, s * 0.24f), 90, 180, false, S(color, s * 0.06f));
        c.DrawArc(SKRect.Create(cx + s * 0.16f, cy - s * 0.3f, s * 0.24f, s * 0.24f), -90, 180, false, S(color, s * 0.06f));
        c.DrawPath(cup, F(color));
        c.DrawRect(SKRect.Create(cx - s * 0.05f, cy + s * 0.08f, s * 0.1f, s * 0.14f), F(color));
        c.DrawRoundRect(SKRect.Create(cx - s * 0.2f, cy + s * 0.22f, s * 0.4f, s * 0.1f), s * 0.03f, s * 0.03f, F(color));
        c.DrawOval(cx - s * 0.1f, cy - s * 0.18f, s * 0.04f, s * 0.08f, F(new SKColor(255, 255, 255, 120)));
    }

    public static void Paw(SKCanvas c, float cx, float cy, float s, SKColor color)
    {
        c.DrawOval(cx, cy + s * 0.12f, s * 0.22f, s * 0.18f, F(color));
        c.DrawOval(cx - s * 0.26f, cy - s * 0.08f, s * 0.09f, s * 0.11f, F(color));
        c.DrawOval(cx - s * 0.09f, cy - s * 0.26f, s * 0.09f, s * 0.11f, F(color));
        c.DrawOval(cx + s * 0.09f, cy - s * 0.26f, s * 0.09f, s * 0.11f, F(color));
        c.DrawOval(cx + s * 0.26f, cy - s * 0.08f, s * 0.09f, s * 0.11f, F(color));
    }

    public static void Back(SKCanvas c, float cx, float cy, float s, SKColor color)
    {
        using var p = new SKPathBuilder();
        p.MoveTo(cx + s * 0.12f, cy - s * 0.26f);
        p.LineTo(cx - s * 0.14f, cy);
        p.LineTo(cx + s * 0.12f, cy + s * 0.26f);
        c.DrawPath(p, S(color, s * 0.11f));
    }

    public static void Question(SKCanvas c, float cx, float cy, float s, SKColor color)
    {
        using var font = new SKFont(GameFonts.Display, s * 0.8f);
        var m = font.Metrics;
        c.DrawText("?", cx, cy - (m.Ascent + m.Descent) / 2, SKTextAlign.Center, font, F(color));
    }

    // ======================================================================
    //  Dog mascot
    // ======================================================================

    private static readonly SKColor DogFur = new(0xE8, 0xB0, 0x6E);
    private static readonly SKColor DogFurDark = new(0xB9, 0x7A, 0x3E);
    private static readonly SKColor DogEar = new(0x8A, 0x55, 0x2A);
    private static readonly SKColor DogMuzzle = new(0xFF, 0xF3, 0xE0);
    private static readonly SKColor DogLine = new(0x5A, 0x34, 0x18);
    private static readonly SKColor Tongue = new(0xF0, 0x6E, 0x8A);

    /// <summary>The PooSweep mascot – a scruffy terrier whose face reacts to the game.</summary>
    public static void Dog(SKCanvas c, float cx, float cy, float d, DogMood mood, float time, float press = 0)
    {
        var line = Math.Max(1.2f, d * 0.028f);
        var bob = mood == DogMood.Celebrate ? MathF.Abs(MathF.Sin(time * 9)) * -d * 0.05f : MathF.Sin(time * 2.2f) * d * 0.012f;
        c.Save();
        c.Translate(cx, cy + bob + press * d * 0.03f);
        if (mood == DogMood.Sick) c.RotateDegrees(MathF.Sin(time * 3) * 4);
        if (press > 0) c.Scale(1 + press * 0.04f, 1 - press * 0.06f);

        // ears (behind)
        var earSwing = mood switch
        {
            DogMood.Worried => 32f,
            DogMood.Celebrate => MathF.Sin(time * 18) * 16 + 14,
            DogMood.Sick => -14f,
            _ => MathF.Sin(time * 2.2f) * 5,
        };
        foreach (var side in new[] { -1f, 1f })
        {
            c.Save();
            c.Translate(side * d * 0.3f, -d * 0.24f);
            // positive swing lifts the ears outward; the ears hang down the side of the head
            c.RotateDegrees(-side * (18 + earSwing));
            using var ear = new SKPathBuilder();
            ear.MoveTo(0, 0);
            ear.CubicTo(side * d * 0.22f, -d * 0.02f, side * d * 0.2f, d * 0.32f, side * d * 0.08f, d * 0.36f);
            ear.CubicTo(-side * d * 0.02f, d * 0.38f, -side * d * 0.06f, d * 0.12f, 0, 0);
            ear.Close();
            c.DrawPath(ear, F(DogEar));
            c.DrawPath(ear, S(DogLine, line));
            c.Restore();
        }

        // head
        var head = SKRect.Create(-d * 0.38f, -d * 0.36f, d * 0.76f, d * 0.7f);
        Fill.Shader = SKShader.CreateRadialGradient(new SKPoint(-d * 0.1f, -d * 0.18f), d * 0.55f,
            [Lighten(DogFur, 0.12f), DogFur, DogFurDark], [0, 0.6f, 1], SKShaderTileMode.Clamp);
        c.DrawOval(head, Fill);
        Fill.Shader = null;

        // eye patch
        c.DrawOval(d * 0.16f, -d * 0.07f, d * 0.13f, d * 0.12f, F(DogEar.WithAlpha(200)));

        // tuft
        using (var tuft = new SKPathBuilder())
        {
            tuft.MoveTo(-d * 0.1f, -d * 0.33f);
            tuft.QuadTo(-d * 0.06f, -d * 0.46f, d * 0.0f, -d * 0.35f);
            tuft.QuadTo(d * 0.05f, -d * 0.47f, d * 0.1f, -d * 0.33f);
            c.DrawPath(tuft, F(DogFur));
            c.DrawPath(tuft, S(DogLine, line));
        }
        c.DrawOval(head, S(DogLine, line));

        // muzzle
        var muzzleColor = mood == DogMood.Sick ? new SKColor(0xD8, 0xF0, 0xB8) : DogMuzzle;
        c.DrawOval(0, d * 0.15f, d * 0.24f, d * 0.16f, F(muzzleColor));
        c.DrawOval(0, d * 0.15f, d * 0.24f, d * 0.16f, S(DogLine.WithAlpha(120), line * 0.7f));

        // cheeks
        if (mood != DogMood.Sick)
        {
            c.DrawOval(-d * 0.27f, d * 0.08f, d * 0.06f, d * 0.04f, F(new SKColor(0xFF, 0x80, 0x90, 90)));
            c.DrawOval(d * 0.27f, d * 0.08f, d * 0.06f, d * 0.04f, F(new SKColor(0xFF, 0x80, 0x90, 90)));
        }

        // eyes
        var ex = d * 0.16f;
        var ey = -d * 0.06f;
        var blink = mood == DogMood.Happy && (time % 3.7f) < 0.13f;
        switch (mood)
        {
            case DogMood.Celebrate:
                {
                    // shades
                    var shade = new SKColor(0x1E, 0x1E, 0x2A);
                    c.DrawLine(-ex, ey, ex, ey, S(shade, line * 1.4f));
                    foreach (var sx in new[] { -1f, 1f })
                    {
                        var rr = SKRect.Create(sx * ex - d * 0.11f, ey - d * 0.07f, d * 0.22f, d * 0.14f);
                        c.DrawRoundRect(rr, d * 0.05f, d * 0.05f, F(shade));
                        c.DrawLine(rr.Left + d * 0.04f, rr.Top + d * 0.03f, rr.Left + d * 0.09f, rr.Top + d * 0.03f, S(new SKColor(255, 255, 255, 160), line));
                    }
                    break;
                }
            case DogMood.Sick:
                foreach (var sx in new[] { -1f, 1f })
                {
                    var r = d * 0.05f;
                    c.DrawLine(sx * ex - r, ey - r, sx * ex + r, ey + r, S(DogLine, line * 1.3f));
                    c.DrawLine(sx * ex + r, ey - r, sx * ex - r, ey + r, S(DogLine, line * 1.3f));
                }
                break;
            case DogMood.Worried:
                foreach (var sx in new[] { -1f, 1f })
                {
                    c.DrawOval(sx * ex, ey, d * 0.075f, d * 0.09f, F(SKColors.White));
                    c.DrawOval(sx * ex, ey, d * 0.075f, d * 0.09f, S(DogLine, line * 0.8f));
                    c.DrawCircle(sx * ex, ey + d * 0.015f, d * 0.03f, F(Ink));
                    // worried brows
                    c.DrawLine(sx * ex - d * 0.06f * sx, ey - d * 0.15f, sx * ex + d * 0.06f * sx, ey - d * 0.12f, S(DogLine, line));
                }
                break;
            default:
                foreach (var sx in new[] { -1f, 1f })
                {
                    if (blink)
                    {
                        c.DrawArc(SKRect.Create(sx * ex - d * 0.05f, ey - d * 0.03f, d * 0.1f, d * 0.06f), 0, 180, false, S(Ink, line));
                    }
                    else
                    {
                        c.DrawOval(sx * ex, ey, d * 0.045f, d * 0.06f, F(Ink));
                        c.DrawCircle(sx * ex - d * 0.012f, ey - d * 0.022f, d * 0.017f, F(SKColors.White));
                    }
                }
                break;
        }

        // nose
        var noseY = d * 0.07f;
        using (var nose = new SKPathBuilder())
        {
            nose.MoveTo(-d * 0.075f, noseY - d * 0.035f);
            nose.QuadTo(0, noseY - d * 0.06f, d * 0.075f, noseY - d * 0.035f);
            nose.QuadTo(d * 0.06f, noseY + d * 0.04f, 0, noseY + d * 0.05f);
            nose.QuadTo(-d * 0.06f, noseY + d * 0.04f, -d * 0.075f, noseY - d * 0.035f);
            nose.Close();
            c.DrawPath(nose, F(new SKColor(0x26, 0x18, 0x10)));
            c.DrawOval(-d * 0.02f, noseY - d * 0.025f, d * 0.022f, d * 0.012f, F(new SKColor(255, 255, 255, 140)));
        }

        // mouth
        var my = noseY + d * 0.06f;
        switch (mood)
        {
            case DogMood.Worried:
                c.DrawOval(0, my + d * 0.06f, d * 0.035f, d * 0.045f, F(DogLine));
                break;
            case DogMood.Sick:
                {
                    using var m = new SKPathBuilder();
                    m.MoveTo(-d * 0.1f, my + d * 0.08f);
                    for (var i = 1; i <= 4; i++)
                        m.LineTo(-d * 0.1f + i * d * 0.05f, my + d * 0.08f + (i % 2 == 0 ? 0 : -d * 0.025f));
                    c.DrawPath(m, S(DogLine, line));
                    // sweat drop
                    var sy = -d * 0.2f + (time * 0.4f % 1) * d * 0.15f;
                    c.DrawOval(d * 0.34f, sy, d * 0.03f, d * 0.045f, F(new SKColor(0x7F, 0xD3, 0xFF)));
                    break;
                }
            default:
                {
                    var open = mood == DogMood.Celebrate ? 1f : 0.6f;
                    using var m = new SKPathBuilder();
                    m.MoveTo(-d * 0.12f, my);
                    m.QuadTo(-d * 0.06f, my + d * 0.05f, 0, my);
                    m.QuadTo(d * 0.06f, my + d * 0.05f, d * 0.12f, my);
                    // open mouth + tongue
                    using var mouth = new SKPathBuilder();
                    mouth.MoveTo(-d * 0.08f, my + d * 0.02f);
                    mouth.QuadTo(0, my + d * 0.16f * open, d * 0.08f, my + d * 0.02f);
                    mouth.Close();
                    c.DrawPath(mouth, F(new SKColor(0x7A, 0x22, 0x2E)));
                    var wag = MathF.Sin(time * (mood == DogMood.Celebrate ? 14 : 4)) * d * 0.01f;
                    c.DrawOval(wag, my + d * 0.1f * open + d * 0.02f, d * 0.05f, d * 0.06f * open + d * 0.01f, F(Tongue));
                    c.DrawLine(wag, my + d * 0.07f * open, wag, my + d * 0.13f * open, S(Darken(Tongue, 0.2f), line * 0.6f));
                    c.DrawPath(m, S(DogLine, line));
                    break;
                }
        }

        c.Restore();
    }

    // ======================================================================
    //  Scenery
    // ======================================================================

    public static void Cloud(SKCanvas c, float x, float y, float s, SKColor color)
    {
        var p = F(color);
        c.DrawCircle(x, y, s * 0.28f, p);
        c.DrawCircle(x + s * 0.28f, y + s * 0.06f, s * 0.22f, p);
        c.DrawCircle(x - s * 0.28f, y + s * 0.08f, s * 0.2f, p);
        c.DrawRoundRect(SKRect.Create(x - s * 0.48f, y + s * 0.02f, s * 0.96f, s * 0.24f), s * 0.12f, s * 0.12f, p);
    }

    /// <summary>Paints the animated backyard backdrop used behind every screen.</summary>
    public static void Scenery(SKCanvas c, SKRect r, Palette p, float time, float horizon = 0.62f)
    {
        var w = r.Width;
        var h = r.Height;
        var unit = Math.Min(w, h);

        Fill.Color = SKColors.White;
        Fill.Shader = SKShader.CreateLinearGradient(new SKPoint(0, 0), new SKPoint(0, h * horizon),
            [p.SkyTop, p.SkyBottom], SKShaderTileMode.Clamp);
        c.DrawRect(r, Fill);
        Fill.Shader = null;

        if (p.Stars)
        {
            for (var i = 0; i < 40; i++)
            {
                var sx = w * H01(i, 1, 99);
                var sy = h * horizon * 0.7f * H01(i, 2, 99);
                var tw = 0.5f + 0.5f * MathF.Sin(time * 2 + i);
                c.DrawCircle(sx, sy, Math.Max(1, unit * 0.003f), F(new SKColor(255, 255, 230, (byte)(80 + 140 * tw))));
            }
        }

        // sun / moon with rays
        var sunX = w * 0.82f;
        var sunY = h * 0.13f;
        var sunR = unit * 0.075f;
        if (!p.Stars)
        {
            c.Save();
            c.Translate(sunX, sunY);
            c.RotateDegrees(time * 8);
            for (var i = 0; i < 12; i++)
            {
                c.RotateDegrees(30);
                c.DrawRoundRect(SKRect.Create(-sunR * 0.12f, -sunR * 1.75f, sunR * 0.24f, sunR * 0.5f), sunR * 0.1f, sunR * 0.1f, F(p.Sun.WithAlpha(150)));
            }
            c.Restore();
        }
        c.DrawCircle(sunX, sunY, sunR * (p.Stars ? 1.35f : 1.15f), F(p.Sun.WithAlpha(p.Stars ? (byte)50 : (byte)90)));
        c.DrawCircle(sunX, sunY, sunR, F(p.Sun));
        if (p.Stars)
        {
            // moon craters
            c.DrawCircle(sunX - sunR * 0.3f, sunY - sunR * 0.2f, sunR * 0.18f, F(new SKColor(0, 0, 0, 22)));
            c.DrawCircle(sunX + sunR * 0.25f, sunY + sunR * 0.3f, sunR * 0.12f, F(new SKColor(0, 0, 0, 22)));
            c.DrawCircle(sunX + sunR * 0.35f, sunY - sunR * 0.3f, sunR * 0.08f, F(new SKColor(0, 0, 0, 22)));
        }

        // drifting clouds
        for (var i = 0; i < 4; i++)
        {
            var speed = 8 + i * 5;
            var cs = unit * (0.18f + 0.06f * (i % 2));
            var span = w + cs * 2;
            var x = (H01(i, 5, 7) * span + time * speed) % span - cs;
            var y = h * (0.08f + 0.09f * i) * horizon * 1.6f;
            Cloud(c, x, y, cs, p.Cloud.WithAlpha((byte)(210 - i * 25)));
        }

        // hills
        using (var far = new SKPathBuilder())
        {
            far.MoveTo(0, h * horizon);
            for (var x = 0f; x <= w; x += w / 24f)
                far.LineTo(x, h * horizon - unit * 0.07f - MathF.Sin(x / w * 5.5f + 1) * unit * 0.045f);
            far.LineTo(w, h);
            far.LineTo(0, h);
            far.Close();
            c.DrawPath(far, F(p.HillFar));
        }

        // picket fence
        var fenceY = h * horizon;
        var post = unit * 0.035f;
        var fenceH = unit * 0.09f;
        var fenceColor = p.Stars ? new SKColor(0xD8, 0xCF, 0xE0) : SKColors.White;
        c.DrawRect(SKRect.Create(0, fenceY - fenceH * 0.75f, w, post * 0.45f), F(fenceColor.WithAlpha(230)));
        c.DrawRect(SKRect.Create(0, fenceY - fenceH * 0.35f, w, post * 0.45f), F(fenceColor.WithAlpha(230)));
        for (var x = post * 0.5f; x < w; x += post * 1.9f)
        {
            using var pk = new SKPathBuilder();
            pk.MoveTo(x, fenceY);
            pk.LineTo(x, fenceY - fenceH);
            pk.LineTo(x + post / 2, fenceY - fenceH - post * 0.5f);
            pk.LineTo(x + post, fenceY - fenceH);
            pk.LineTo(x + post, fenceY);
            pk.Close();
            c.DrawPath(pk, F(fenceColor));
            c.DrawPath(pk, S(new SKColor(0, 0, 0, 30), Math.Max(1, post * 0.08f)));
        }

        using (var near = new SKPathBuilder())
        {
            near.MoveTo(0, h * horizon + unit * 0.01f);
            for (var x = 0f; x <= w; x += w / 24f)
                near.LineTo(x, h * horizon + unit * 0.015f - MathF.Sin(x / w * 3.2f + 2.4f) * unit * 0.02f);
            near.LineTo(w, h);
            near.LineTo(0, h);
            near.Close();
            Fill.Color = SKColors.White;
            Fill.Shader = SKShader.CreateLinearGradient(new SKPoint(0, h * horizon), new SKPoint(0, h),
                [p.HillNear, p.GrassShadow], SKShaderTileMode.Clamp);
            c.DrawPath(near, Fill);
            Fill.Shader = null;
        }

        // mowing stripes
        for (var i = 0; i < 8; i++)
        {
            if (i % 2 == 0) continue;
            using var stripe = new SKPathBuilder();
            var x0 = w * (i / 8f);
            var x1 = w * ((i + 1) / 8f);
            stripe.MoveTo(x0 + (x0 - w / 2) * 0.1f, h * horizon + unit * 0.04f);
            stripe.LineTo(x1 + (x1 - w / 2) * 0.1f, h * horizon + unit * 0.04f);
            stripe.LineTo(x1 + (x1 - w / 2) * 0.9f, h);
            stripe.LineTo(x0 + (x0 - w / 2) * 0.9f, h);
            stripe.Close();
            c.DrawPath(stripe, F(new SKColor(255, 255, 255, 18)));
        }

        // swaying tufts along the bottom
        for (var i = 0; i < 18; i++)
        {
            var x = w * H01(i, 3, 17);
            var y = h * (horizon + 0.06f) + (h * (1 - horizon) - unit * 0.06f) * H01(i, 4, 17);
            var sway = MathF.Sin(time * 1.6f + i) * unit * 0.006f;
            var bh = unit * 0.03f;
            var bw = Math.Max(1.2f, unit * 0.005f);
            c.DrawLine(x, y, x - bh * 0.4f + sway, y - bh, S(p.GrassBlade, bw));
            c.DrawLine(x, y, x + sway, y - bh * 1.2f, S(p.GrassBlade, bw));
            c.DrawLine(x, y, x + bh * 0.4f + sway, y - bh * 0.9f, S(p.GrassBlade, bw));
        }
    }

    /// <summary>Big chunky outlined title text with a per-letter bounce.</summary>
    public static void TitleText(SKCanvas c, string text, float cx, float baseline, float size, float time, SKColor top, SKColor bottom)
    {
        using var font = new SKFont(GameFonts.Display, size);
        var widths = text.Select(ch => font.MeasureText(ch.ToString())).ToArray();
        var total = widths.Sum();
        var x = cx - total / 2;

        for (var i = 0; i < text.Length; i++)
        {
            var ch = text[i].ToString();
            var dy = MathF.Sin(time * 3 + i * 0.55f) * size * 0.05f;
            var rot = MathF.Sin(time * 2 + i) * 3;
            var lx = x + widths[i] / 2;
            c.Save();
            c.Translate(lx, baseline + dy);
            c.RotateDegrees(rot);

            c.DrawText(ch, size * 0.04f, size * 0.07f, SKTextAlign.Center, font, F(new SKColor(0x3B, 0x24, 0x14, 120)));
            c.DrawText(ch, 0, 0, SKTextAlign.Center, font, S(Ink, size * 0.14f));
            Fill.Color = SKColors.White;
            Fill.Shader = SKShader.CreateLinearGradient(new SKPoint(0, -size * 0.75f), new SKPoint(0, 0), [top, bottom], SKShaderTileMode.Clamp);
            c.DrawText(ch, 0, 0, SKTextAlign.Center, font, Fill);
            Fill.Shader = null;
            c.Restore();
            x += widths[i];
        }
    }

    // ======================================================================
    //  Colour helpers
    // ======================================================================

    public static SKColor Lighten(SKColor c, float amount)
        => new((byte)(c.Red + (255 - c.Red) * amount), (byte)(c.Green + (255 - c.Green) * amount), (byte)(c.Blue + (255 - c.Blue) * amount), c.Alpha);

    public static SKColor Darken(SKColor c, float amount)
        => new((byte)(c.Red * (1 - amount)), (byte)(c.Green * (1 - amount)), (byte)(c.Blue * (1 - amount)), c.Alpha);
}

internal static class SkiaExtensions
{
    /// <summary>Draws the builder's current path, disposing the snapshot afterwards.</summary>
    public static void DrawPath(this SKCanvas canvas, SKPathBuilder builder, SKPaint paint)
    {
        using var path = builder.Snapshot();
        canvas.DrawPath(path, paint);
    }
}
