using System.Diagnostics;
using PooSweep.Graphics;
using SkiaSharp;
using SkiaSharp.Views.Maui;
using SkiaSharp.Views.Maui.Controls;

namespace PooSweep.Controls;

/// <summary>Base for small Skia views that animate while they're on screen.</summary>
public abstract class AnimatedSkiaView : SKCanvasView
{
    private static readonly Stopwatch Clock = Stopwatch.StartNew();
    private IDispatcherTimer? _timer;

    protected AnimatedSkiaView()
    {
        Loaded += (_, _) => StartAnimating();
        Unloaded += (_, _) => _timer?.Stop();
    }

    protected static float Time => (float)Clock.Elapsed.TotalSeconds;

    /// <summary>Frame interval; override to throttle.</summary>
    protected virtual int FrameMs => 33;

    protected virtual bool Animated => true;

    protected void StartAnimating()
    {
        if (!Animated || Dispatcher is null) { InvalidateSurface(); return; }
        _timer ??= CreateTimer();
        _timer.Interval = TimeSpan.FromMilliseconds(FrameMs);
        if (!_timer.IsRunning) _timer.Start();
    }

    private IDispatcherTimer CreateTimer()
    {
        var t = Dispatcher.CreateTimer();
        t.Tick += (_, _) => InvalidateSurface();
        return t;
    }

    protected float Density => Width > 0 ? CanvasSize.Width / (float)Width : 1;
}

/// <summary>The dog mascot – doubles as the "new game" button, like the classic smiley.</summary>
public sealed class DogView : AnimatedSkiaView
{
    public static readonly BindableProperty MoodProperty =
        BindableProperty.Create(nameof(Mood), typeof(DogMood), typeof(DogView), DogMood.Happy,
            propertyChanged: (b, _, _) => ((DogView)b).InvalidateSurface());

    private float _pressAt = -10;

    public DogView()
    {
        var tap = new TapGestureRecognizer();
        tap.Tapped += (_, _) =>
        {
            _pressAt = Time;
            Tapped?.Invoke(this, EventArgs.Empty);
        };
        GestureRecognizers.Add(tap);
    }

    public event EventHandler? Tapped;

    public DogMood Mood
    {
        get => (DogMood)GetValue(MoodProperty);
        set => SetValue(MoodProperty, value);
    }

    protected override void OnPaintSurface(SKPaintSurfaceEventArgs e)
    {
        var c = e.Surface.Canvas;
        c.Clear(SKColors.Transparent);
        var size = Math.Min(e.Info.Width, e.Info.Height);
        var since = Time - _pressAt;
        var press = since < 0.25f ? MathF.Sin(since / 0.25f * MathF.PI) : 0;
        Art.Dog(c, e.Info.Width / 2f, e.Info.Height / 2f + size * 0.04f, size * 0.95f, Mood, Time, press);
    }
}

public enum SceneMode
{
    Backdrop,
    Title,
}

/// <summary>Animated backyard scenery used behind every page.</summary>
public sealed class SceneView : AnimatedSkiaView
{
    public static readonly BindableProperty ModeProperty =
        BindableProperty.Create(nameof(Mode), typeof(SceneMode), typeof(SceneView), SceneMode.Backdrop);

    public SceneMode Mode
    {
        get => (SceneMode)GetValue(ModeProperty);
        set => SetValue(ModeProperty, value);
    }

    /// <summary>Where the horizon sits (0..1 of the height).</summary>
    public float Horizon { get; set; } = 0.55f;

    public Palette Palette { get; set { field = value; InvalidateSurface(); } } = Palette.Day;

    /// <summary>Vertical fraction where the title is drawn (Title mode).</summary>
    public float TitleY { get; set; } = 0.2f;

    protected override int FrameMs => Mode == SceneMode.Title ? 16 : 50;

    protected override void OnPaintSurface(SKPaintSurfaceEventArgs e)
    {
        var c = e.Surface.Canvas;
        var w = e.Info.Width;
        var h = e.Info.Height;
        var t = Time;
        Art.Scenery(c, SKRect.Create(0, 0, w, h), Palette, t, Horizon);

        if (Mode != SceneMode.Title) return;

        var unit = Math.Min(w, h * 0.8f);

        // title
        var size = Math.Min(unit * 0.2f, w / 6.2f);
        Art.TitleText(c, "PooSweep!", w / 2f, h * TitleY, size, t, new SKColor(0xFF, 0xE0, 0x5C), new SKColor(0xFF, 0x8C, 0x1A));

        using (var font = new SKFont(GameFonts.Display, size * 0.32f))
        using (var stroke = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = size * 0.07f, Color = new SKColor(0x3B, 0x24, 0x14), StrokeJoin = SKStrokeJoin.Round })
        using (var fill = new SKPaint { IsAntialias = true, Color = SKColors.White })
        {
            var y = h * TitleY + size * 0.55f;
            c.DrawText("MIND YOUR STEP", w / 2f, y, SKTextAlign.Center, font, stroke);
            c.DrawText("MIND YOUR STEP", w / 2f, y, SKTextAlign.Center, font, fill);
        }

        // the star of the show, bouncing on the lawn, and an ominous poo nearby
        var groundY = h * (Horizon + 0.12f);
        var dogSize = unit * 0.34f;
        var hop = MathF.Abs(MathF.Sin(t * 2.6f)) * dogSize * 0.12f;
        var dogX = w * 0.5f + MathF.Sin(t * 0.5f) * w * 0.06f;
        using (var shadow = new SKPaint { IsAntialias = true, Color = new SKColor(0, 0, 0, 50) })
            c.DrawOval(dogX, groundY + dogSize * 0.42f, dogSize * 0.32f * (1 - hop / dogSize), dogSize * 0.06f, shadow);
        Art.Dog(c, dogX, groundY - hop, dogSize, DogMood.Happy, t);

        var pooX = w * 0.5f + dogSize * 0.95f;
        Art.Poo(c, pooX, groundY + dogSize * 0.28f, dogSize * 0.36f, t, face: true, stink: true);
        Art.Flag(c, w * 0.5f - dogSize * 0.95f, groundY + dogSize * 0.22f, dogSize * 0.45f, t);
    }
}

public enum IconKind
{
    None,
    Poo,
    Flag,
    Shovel,
    Clock,
    SpeakerOn,
    SpeakerOff,
    Gear,
    Trophy,
    Paw,
    Back,
    Question,
}

/// <summary>A small vector icon drawn with the game's art.</summary>
public sealed class IconView : AnimatedSkiaView
{
    public static readonly BindableProperty IconProperty =
        BindableProperty.Create(nameof(Icon), typeof(IconKind), typeof(IconView), IconKind.Poo,
            propertyChanged: (b, _, _) => ((IconView)b).InvalidateSurface());

    public static readonly BindableProperty TintProperty =
        BindableProperty.Create(nameof(Tint), typeof(Color), typeof(IconView), Colors.White,
            propertyChanged: (b, _, _) => ((IconView)b).InvalidateSurface());

    public IconView()
    {
        InputTransparent = true;
    }

    public IconKind Icon
    {
        get => (IconKind)GetValue(IconProperty);
        set => SetValue(IconProperty, value);
    }

    public Color Tint
    {
        get => (Color)GetValue(TintProperty);
        set => SetValue(TintProperty, value);
    }

    protected override bool Animated => Icon is IconKind.Flag or IconKind.Clock or IconKind.Poo;

    protected override int FrameMs => 66;

    protected override void OnPaintSurface(SKPaintSurfaceEventArgs e)
    {
        var c = e.Surface.Canvas;
        c.Clear(SKColors.Transparent);
        var s = Math.Min(e.Info.Width, e.Info.Height);
        var cx = e.Info.Width / 2f;
        var cy = e.Info.Height / 2f;
        var tint = Tint.ToSKColor();
        var t = Time;

        switch (Icon)
        {
            case IconKind.Poo: Art.Poo(c, cx, cy, s * 0.95f, t, face: s >= 28); break;
            case IconKind.Flag: Art.Flag(c, cx + s * 0.06f, cy, s, t); break;
            case IconKind.Shovel: Art.Shovel(c, cx, cy, s, new SKColor(0xB8, 0xC4, 0xCC)); break;
            case IconKind.Clock: Art.Clock(c, cx, cy, s, t); break;
            case IconKind.SpeakerOn: Art.Speaker(c, cx, cy, s, true, tint); break;
            case IconKind.SpeakerOff: Art.Speaker(c, cx, cy, s, false, tint); break;
            case IconKind.Gear: Art.Gear(c, cx, cy, s, tint); break;
            case IconKind.Trophy: Art.Trophy(c, cx, cy, s, tint); break;
            case IconKind.Paw: Art.Paw(c, cx, cy, s, tint); break;
            case IconKind.Back: Art.Back(c, cx, cy, s, tint); break;
            case IconKind.Question: Art.Question(c, cx, cy, s, tint); break;
        }
    }
}
