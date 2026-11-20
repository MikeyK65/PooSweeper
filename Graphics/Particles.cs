using SkiaSharp;

namespace PooSweep.Graphics;

public enum ParticleKind
{
    Dirt,
    Grass,
    Splat,
    Confetti,
    Sparkle,
}

public struct Particle
{
    public ParticleKind Kind;
    public float X, Y, Vx, Vy, Rot, Vr, Size;
    public float Start, Life;
    public SKColor Color;
}

/// <summary>A small, allocation-free particle system.</summary>
public sealed class ParticleSystem
{
    private const int Max = 900;
    private readonly Particle[] _items = new Particle[Max];
    private int _count;
    private readonly Random _rng = new();
    private readonly SKPaint _paint = new() { IsAntialias = true };

    private static readonly SKColor[] ConfettiColors =
    [
        new(0xFF, 0x5A, 0x4E), new(0xFF, 0xC8, 0x2E), new(0x4C, 0xC9, 0x6A),
        new(0x2E, 0x9C, 0xFF), new(0xB0, 0x6C, 0xFF), new(0xFF, 0x8A, 0xC8),
    ];

    public bool IsActive => _count > 0;

    public void Clear() => _count = 0;

    private float R(float a, float b) => a + (float)_rng.NextDouble() * (b - a);

    private void Add(in Particle p)
    {
        if (_count >= Max) return;
        _items[_count++] = p;
    }

    /// <summary>Clods of dirt and grass flying out of a freshly dug tile.</summary>
    public void Dig(float x, float y, float tile, float start, Palette palette, int amount = 4)
    {
        for (var i = 0; i < amount; i++)
        {
            var grass = i % 2 == 0;
            Add(new Particle
            {
                Kind = grass ? ParticleKind.Grass : ParticleKind.Dirt,
                X = x + R(-0.3f, 0.3f) * tile, Y = y + R(-0.3f, 0.2f) * tile,
                Vx = R(-1.6f, 1.6f) * tile, Vy = R(-4.2f, -2.2f) * tile,
                Rot = R(0, 360), Vr = R(-600, 600),
                Size = tile * R(0.06f, 0.12f),
                Start = start, Life = R(0.45f, 0.7f),
                Color = grass ? (i % 4 == 0 ? palette.GrassA : palette.GrassBlade) : palette.DirtSpeck,
            });
        }
    }

    public void Splat(float x, float y, float tile, float start)
    {
        for (var i = 0; i < 36; i++)
        {
            var ang = R(0, MathF.Tau);
            var speed = R(2, 9) * tile;
            Add(new Particle
            {
                Kind = ParticleKind.Splat,
                X = x, Y = y,
                Vx = MathF.Cos(ang) * speed, Vy = MathF.Sin(ang) * speed - 3 * tile,
                Size = tile * R(0.06f, 0.2f),
                Start = start, Life = R(0.6f, 1.1f),
                Color = new SKColor(0x6A, 0x3E, 0x1C),
            });
        }
    }

    public void Confetti(float width, float height, float start, int amount = 160)
    {
        var unit = Math.Min(width, height);
        for (var i = 0; i < amount; i++)
        {
            Add(new Particle
            {
                Kind = ParticleKind.Confetti,
                X = R(0, width), Y = R(-height * 0.6f, -10),
                Vx = R(-0.15f, 0.15f) * unit, Vy = R(0.25f, 0.5f) * unit,
                Rot = R(0, 360), Vr = R(-400, 400),
                Size = unit * R(0.012f, 0.022f),
                Start = start + R(0, 0.8f), Life = R(2.5f, 4f),
                Color = ConfettiColors[_rng.Next(ConfettiColors.Length)],
            });
        }
    }

    public void Sparkle(float x, float y, float tile, float start)
    {
        for (var i = 0; i < 6; i++)
        {
            var ang = i / 6f * MathF.Tau;
            Add(new Particle
            {
                Kind = ParticleKind.Sparkle,
                X = x, Y = y,
                Vx = MathF.Cos(ang) * tile * 1.6f, Vy = MathF.Sin(ang) * tile * 1.6f,
                Size = tile * 0.12f, Start = start, Life = 0.5f,
                Color = new SKColor(0xFF, 0xE0, 0x6A),
            });
        }
    }

    /// <summary>Advances and draws; returns true while particles remain.</summary>
    public bool UpdateAndDraw(SKCanvas c, float now, float dt, float gravity)
    {
        var i = 0;
        while (i < _count)
        {
            ref var p = ref _items[i];
            if (now < p.Start)
            {
                i++;
                continue;
            }

            var age = now - p.Start;
            if (age > p.Life)
            {
                _items[i] = _items[--_count];
                continue;
            }

            switch (p.Kind)
            {
                case ParticleKind.Confetti:
                    p.Vx += MathF.Sin(now * 3 + i) * gravity * 0.02f * dt;
                    p.X += p.Vx * dt;
                    p.Y += p.Vy * dt;
                    break;
                case ParticleKind.Sparkle:
                    p.X += p.Vx * dt;
                    p.Y += p.Vy * dt;
                    p.Vx *= 0.9f;
                    p.Vy *= 0.9f;
                    break;
                default:
                    p.Vy += gravity * dt;
                    p.X += p.Vx * dt;
                    p.Y += p.Vy * dt;
                    break;
            }
            p.Rot += p.Vr * dt;

            var fade = 1 - Math.Clamp((age - p.Life * 0.6f) / (p.Life * 0.4f), 0, 1);
            _paint.Color = p.Color.WithAlpha((byte)(p.Color.Alpha * fade));
            _paint.Style = SKPaintStyle.Fill;

            switch (p.Kind)
            {
                case ParticleKind.Grass:
                    c.Save();
                    c.Translate(p.X, p.Y);
                    c.RotateDegrees(p.Rot);
                    c.DrawRect(-p.Size * 0.25f, -p.Size, p.Size * 0.5f, p.Size * 2, _paint);
                    c.Restore();
                    break;
                case ParticleKind.Confetti:
                    c.Save();
                    c.Translate(p.X, p.Y);
                    c.RotateDegrees(p.Rot);
                    c.Scale(MathF.Cos(now * 6 + i), 1);
                    c.DrawRect(-p.Size, -p.Size * 0.55f, p.Size * 2, p.Size * 1.1f, _paint);
                    c.Restore();
                    break;
                case ParticleKind.Sparkle:
                    {
                        var s = p.Size * (1 - age / p.Life);
                        c.Save();
                        c.Translate(p.X, p.Y);
                        c.RotateDegrees(45 + age * 300);
                        c.DrawRect(-s * 0.2f, -s, s * 0.4f, s * 2, _paint);
                        c.DrawRect(-s, -s * 0.2f, s * 2, s * 0.4f, _paint);
                        c.Restore();
                        break;
                    }
                default:
                    c.DrawCircle(p.X, p.Y, p.Size, _paint);
                    break;
            }

            i++;
        }

        return _count > 0;
    }
}
