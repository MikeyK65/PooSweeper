using System.Diagnostics;
using PooSweep.Core;
using PooSweep.Graphics;
using SkiaSharp;
using SkiaSharp.Views.Maui;
using SkiaSharp.Views.Maui.Controls;

namespace PooSweep.Controls;

public enum BoardActionKind
{
    Reveal,
    Flag,
    Chord,
}

public sealed class BoardActionEventArgs(BoardActionKind kind, int row, int column, bool fromLongPress) : EventArgs
{
    public BoardActionKind Kind { get; } = kind;
    public int Row { get; } = row;
    public int Column { get; } = column;
    public bool FromLongPress { get; } = fromLongPress;
}

/// <summary>
/// The yard: draws the board with Skia, animates every change and turns mouse/touch input
/// into reveal / flag / chord actions.
/// </summary>
public sealed class BoardView : SKCanvasView
{
    private struct CellVisual
    {
        public bool Revealed;
        public float RevealAt;
        public bool Flagged;
        public float FlagAt;
        public float UnflagAt;
    }

    private const float PopDuration = 0.2f;
    private const float FlagDrop = 0.28f;
    private const int LongPressMs = 420;

    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private readonly ParticleSystem _particles = new();
    private IDispatcherTimer? _timer;
    private float _lastFrame;

    private GameEngine? _engine;
    private CellVisual[,] _vis = new CellVisual[0, 0];
    private float _introStart = -10;
    private float _lostAt = -1;
    private float _wonAt = -1;
    private float _shakeStart = -10;

    // layout (pixels)
    private float _tile;
    private SKRect _board;
    private float _density = 1;

    // input
    private (int r, int c)? _hover;
    private (int r, int c)? _press;
    private SKPoint _pressPoint;
    private bool _pressIsMouse;
    private bool _longPressFired;
    private int _longPressToken;

    public BoardView()
    {
        EnableTouchEvents = true;
        IgnorePixelScaling = false;
        Loaded += (_, _) => Animate();
        Unloaded += (_, _) => _timer?.Stop();
    }

    public event EventHandler<BoardActionEventArgs>? CellAction;

    /// <summary>Raised with true when a finger/button goes down on a hidden tile (the dog gets nervous).</summary>
    public event EventHandler<bool>? PressingChanged;

    public Palette Palette { get; set { field = value; InvalidateSurface(); } } = Palette.Day;

    public bool Colorblind { get; set { field = value; InvalidateSurface(); } }

    /// <summary>When true, a tap plants a flag and a long-press digs.</summary>
    public bool FlagMode { get; set; }

    /// <summary>Tap a satisfied number to clear around it.</summary>
    public bool ChordOnTap { get; set; } = true;

    public bool ReduceMotion { get; set; }

    private float Now => (float)_clock.Elapsed.TotalSeconds;

    // ------------------------------------------------------------------
    //  Game sync
    // ------------------------------------------------------------------

    public void NewBoard(GameEngine engine)
    {
        _engine = engine;
        _vis = new CellVisual[engine.Board.Rows, engine.Board.Columns];
        _particles.Clear();
        _introStart = ReduceMotion ? -10 : Now;
        _lostAt = -1;
        _wonAt = -1;
        _shakeStart = -10;
        _press = null;
        _hover = null;
        Animate();
    }

    /// <summary>Diffs the engine against what's on screen and schedules animations outward from the origin.</summary>
    public void Sync(int originRow, int originColumn)
    {
        if (_engine is null) return;
        var now = Now;
        var board = _engine.Board;
        var lost = _engine.State == GameState.Lost;
        var won = _engine.State == GameState.Won;
        var exploded = _engine.ExplodedCell;

        if (lost && _lostAt < 0)
        {
            _lostAt = now;
            if (!ReduceMotion) _shakeStart = now;
            if (exploded is not null && _tile > 0)
            {
                var (x, y) = Center(exploded.Row, exploded.Column);
                _particles.Splat(x, y, _tile, now);
            }
        }

        if (won && _wonAt < 0)
        {
            _wonAt = now;
            if (_tile > 0 && !ReduceMotion)
                _particles.Confetti(CanvasSize.Width, CanvasSize.Height, now + 0.35f);
        }

        var newlyRevealed = 0;
        foreach (var cell in board)
            if (cell.IsRevealed && !_vis[cell.Row, cell.Column].Revealed && !cell.IsPoo) newlyRevealed++;
        var dirtPerCell = newlyRevealed > 60 ? 1 : newlyRevealed > 20 ? 2 : 4;

        foreach (var cell in board)
        {
            ref var v = ref _vis[cell.Row, cell.Column];
            var dr = cell.Row - originRow;
            var dc = cell.Column - originColumn;
            var dist = MathF.Sqrt(dr * dr + dc * dc);

            if (cell.IsRevealed && !v.Revealed)
            {
                v.Revealed = true;
                if (cell.IsPoo)
                {
                    v.RevealAt = ReferenceEquals(cell, exploded) || ReduceMotion
                        ? now
                        : now + 0.45f + MathF.Min(dist * 0.07f, 2.2f);
                }
                else
                {
                    v.RevealAt = ReduceMotion ? now : now + MathF.Min(dist * 0.032f, 0.9f);
                    if (_tile > 0 && !ReduceMotion)
                    {
                        var (x, y) = Center(cell.Row, cell.Column);
                        _particles.Dig(x, y, _tile, v.RevealAt, Palette, dirtPerCell);
                    }
                }
            }

            if (cell.IsFlagged != v.Flagged)
            {
                v.Flagged = cell.IsFlagged;
                if (v.Flagged)
                {
                    v.FlagAt = won && !ReduceMotion ? now + 0.15f + MathF.Min(dist * 0.05f, 1.5f) : now;
                    if (won && _tile > 0 && !ReduceMotion)
                    {
                        var (x, y) = Center(cell.Row, cell.Column);
                        _particles.Sparkle(x, y, _tile, v.FlagAt + FlagDrop * 0.8f);
                    }
                }
                else
                {
                    v.UnflagAt = now;
                }
            }
        }

        Animate();
    }

    /// <summary>A little "nope" wiggle on a tile (e.g. tapping a flag in dig mode).</summary>
    public void Nudge(int row, int column)
    {
        if (_engine is null || ReduceMotion) return;
        ref var v = ref _vis[row, column];
        if (v.Flagged) v.FlagAt = Now - FlagDrop * 0.5f;
        Animate();
    }

    // ------------------------------------------------------------------
    //  Animation loop
    // ------------------------------------------------------------------

    private void Animate()
    {
        if (Dispatcher is null) return;
        if (_timer is null)
        {
            _timer = Dispatcher.CreateTimer();
            _timer.Interval = TimeSpan.FromMilliseconds(16);
            _timer.Tick += (_, _) => InvalidateSurface();
        }
        if (!_timer.IsRunning)
        {
            _lastFrame = Now;
            _timer.Start();
        }
        InvalidateSurface();
    }

    private (float x, float y) Center(int r, int c)
        => (_board.Left + (c + 0.5f) * _tile, _board.Top + (r + 0.5f) * _tile);

    private static float EaseOutBack(float t)
    {
        const float c1 = 1.70158f, c3 = c1 + 1;
        t = Math.Clamp(t, 0, 1);
        return 1 + c3 * MathF.Pow(t - 1, 3) + c1 * MathF.Pow(t - 1, 2);
    }

    private static float EaseOutBounce(float t)
    {
        t = Math.Clamp(t, 0, 1);
        const float n1 = 7.5625f, d1 = 2.75f;
        if (t < 1 / d1) return n1 * t * t;
        if (t < 2 / d1) return n1 * (t -= 1.5f / d1) * t + 0.75f;
        if (t < 2.5 / d1) return n1 * (t -= 2.25f / d1) * t + 0.9375f;
        return n1 * (t -= 2.625f / d1) * t + 0.984375f;
    }

    // ------------------------------------------------------------------
    //  Rendering
    // ------------------------------------------------------------------

    protected override void OnPaintSurface(SKPaintSurfaceEventArgs e)
    {
        var canvas = e.Surface.Canvas;
        canvas.Clear(SKColors.Transparent);
        if (_engine is null) return;

        var now = Now;
        var dt = Math.Clamp(now - _lastFrame, 0, 0.05f);
        _lastFrame = now;

        var rows = _engine.Board.Rows;
        var cols = _engine.Board.Columns;
        var W = e.Info.Width;
        var H = e.Info.Height;
        _density = Width > 0 ? (float)(W / Width) : 1;

        var pad = 6 * _density;
        var frameGuess = 14 * _density;
        var tile = MathF.Floor(MathF.Min((W - 2 * (pad + frameGuess)) / cols, (H - 2 * (pad + frameGuess)) / rows));
        tile = Math.Clamp(tile, 4, 76 * _density);
        _tile = tile;
        var bw = tile * cols;
        var bh = tile * rows;
        _board = SKRect.Create(MathF.Round((W - bw) / 2), MathF.Round((H - bh) / 2), bw, bh);

        var animating = false;

        canvas.Save();

        // screen shake
        var shakeT = now - _shakeStart;
        if (shakeT < 0.5f)
        {
            var amp = tile * 0.35f * (1 - shakeT / 0.5f);
            canvas.Translate(MathF.Sin(shakeT * 90) * amp, MathF.Cos(shakeT * 70) * amp * 0.6f);
            animating = true;
        }

        DrawFrame(canvas, _board, MathF.Max(8 * _density, tile * 0.28f));

        canvas.Save();
        var clipRadius = tile * 0.18f;
        canvas.ClipRoundRect(new SKRoundRect(_board, clipRadius), antialias: true);
        using (var bg = new SKPaint { Color = Art.Darken(Palette.GrassShadow, 0.15f) })
            canvas.DrawRect(_board, bg);

        var lost = _engine.State == GameState.Lost;
        var exploded = _engine.ExplodedCell;

        // Pass 1: dirt for every visibly-revealed tile
        for (var r = 0; r < rows; r++)
        for (var c = 0; c < cols; c++)
        {
            ref var v = ref _vis[r, c];
            if (!v.Revealed || now < v.RevealAt) continue;
            var rect = SKRect.Create(_board.Left + c * tile, _board.Top + r * tile, tile, tile);
            Art.DirtTile(canvas, rect, Palette, r, c, exploded is not null && exploded.Row == r && exploded.Column == c);
        }

        // Pass 1b: soft shadows where grass overhangs the dirt
        for (var r = 0; r < rows; r++)
        for (var c = 0; c < cols; c++)
        {
            if (!IsDirtVisible(r, c, now)) continue;
            var topGrass = r > 0 && !IsDirtVisible(r - 1, c, now);
            var leftGrass = c > 0 && !IsDirtVisible(r, c - 1, now);
            if (!topGrass && !leftGrass) continue;
            var rect = SKRect.Create(_board.Left + c * tile, _board.Top + r * tile, tile, tile);
            Art.LipShadow(canvas, rect, topGrass, leftGrass);
        }

        // Pass 2: contents of revealed tiles
        for (var r = 0; r < rows; r++)
        for (var c = 0; c < cols; c++)
        {
            ref var v = ref _vis[r, c];
            if (!v.Revealed || now < v.RevealAt) continue;
            var cell = _engine.Board[r, c];
            var rect = SKRect.Create(_board.Left + c * tile, _board.Top + r * tile, tile, tile);
            var age = now - v.RevealAt;

            if (cell.IsPoo)
            {
                var isExploded = ReferenceEquals(cell, exploded);
                if (isExploded) Art.Splat(canvas, rect.MidX, rect.MidY, tile, r * 31 + c);
                var pop = EaseOutBack(age / 0.35f);
                if (age < 0.35f) animating = true;
                var squash = isExploded && age < 0.6f ? MathF.Sin(age * 25) * 0.12f * (1 - age / 0.6f) : 0;
                Art.Poo(canvas, rect.MidX, rect.MidY, tile * 0.92f * pop, now, face: tile >= 18, stink: lost && age > 0.3f, squash: squash);
            }
            else if (cell.AdjacentPooCount > 0)
            {
                var scale = age < 0.18f ? EaseOutBack(age / 0.18f) : 1;
                if (age < 0.18f) animating = true;
                Art.Number(canvas, rect, cell.AdjacentPooCount, Colorblind, scale);
            }
        }

        // Pass 3: grass on everything still hidden (and the "pop" as tiles are dug)
        var chordPreview = PressedChordNeighbours();
        for (var r = 0; r < rows; r++)
        for (var c = 0; c < cols; c++)
        {
            ref var v = ref _vis[r, c];
            var rect = SKRect.Create(_board.Left + c * tile, _board.Top + r * tile, tile, tile);
            var intro = 1f;
            if (_introStart >= 0)
            {
                var delay = MathF.Min((r + c) * 0.018f, 0.7f);
                var t = (now - _introStart - delay) / 0.3f;
                if (t < 1) animating = true;
                if (t <= 0) continue;
                intro = EaseOutBack(t);
            }

            if (v.Revealed && now >= v.RevealAt)
            {
                var age = now - v.RevealAt;
                if (age >= PopDuration) continue;
                animating = true;
                var k = age / PopDuration;
                DrawScaled(canvas, rect, 1 - 0.5f * k, () => Art.GrassTile(canvas, rect, Palette, r, c, false, false, 1 - k));
                continue;
            }

            if (v.Revealed) animating = true; // waiting for its ripple

            var hover = _hover is { } h && h.r == r && h.c == c && _press is null;
            var pressed = (_press is { } p && p.r == r && p.c == c && !v.Flagged && !_engine.Board[r, c].IsRevealed)
                          || chordPreview.Contains((r, c));
            var flowers = !v.Flagged;
            if (intro < 1) DrawScaled(canvas, rect, intro, () => Art.GrassTile(canvas, rect, Palette, r, c, hover, pressed, 1, flowers));
            else Art.GrassTile(canvas, rect, Palette, r, c, hover, pressed, 1, flowers);
        }

        // Pass 4: flags
        var flagIdle = false;
        for (var r = 0; r < rows; r++)
        for (var c = 0; c < cols; c++)
        {
            ref var v = ref _vis[r, c];
            var rect = SKRect.Create(_board.Left + c * tile, _board.Top + r * tile, tile, tile);
            var cell = _engine.Board[r, c];

            if (v.Flagged && !(v.Revealed && now >= v.RevealAt))
            {
                var t = (now - v.FlagAt) / FlagDrop;
                if (t < 0) { animating = true; continue; }
                var drop = 1 - EaseOutBounce(t);
                if (t < 1) animating = true;
                var wrong = lost && !cell.IsPoo;
                flagIdle = true;
                var lift = _press is { } p && p.r == r && p.c == c ? 0 : tile * 0.035f;
                Art.Flag(canvas, rect.MidX, rect.MidY - drop * tile * 0.8f - lift, tile, now, wrong, Math.Clamp(t * 3, 0, 1));
            }
            else if (!v.Flagged && now - v.UnflagAt < 0.25f)
            {
                var t = (now - v.UnflagAt) / 0.25f;
                animating = true;
                Art.Flag(canvas, rect.MidX, rect.MidY - t * tile * 0.7f, tile, now, false, 1 - t);
            }
        }

        // Flies around the stepped-in poo
        if (lost && exploded is not null && !ReduceMotion)
        {
            var (fx, fy) = Center(exploded.Row, exploded.Column);
            for (var i = 0; i < 3; i++)
            {
                var a = now * (2.2f + i * 0.7f) + i * 2.1f;
                var rad = tile * (0.45f + 0.12f * i);
                Art.Fly(canvas, fx + MathF.Cos(a) * rad, fy - tile * 0.2f + MathF.Sin(a * 1.3f) * rad * 0.5f, tile, now);
            }
        }

        canvas.Restore(); // clip

        if (_particles.UpdateAndDraw(canvas, now, dt, tile * 22)) animating = true;

        canvas.Restore(); // shake

        // keep ticking while anything moves; idle at a gentle rate when flags wave or flies buzz
        if (_timer is not null)
        {
            if (animating)
            {
                _timer.Interval = TimeSpan.FromMilliseconds(16);
            }
            else if ((lost && !ReduceMotion) || (flagIdle && !ReduceMotion))
            {
                _timer.Interval = TimeSpan.FromMilliseconds(lost ? 33 : 66);
            }
            else
            {
                _timer.Stop();
                if (_introStart >= 0 && now - _introStart > 2) _introStart = -10;
            }
        }
    }

    private bool IsDirtVisible(int r, int c, float now)
    {
        ref var v = ref _vis[r, c];
        return v.Revealed && now >= v.RevealAt;
    }

    private static void DrawScaled(SKCanvas canvas, SKRect rect, float scale, Action draw)
    {
        canvas.Save();
        canvas.Scale(scale, scale, rect.MidX, rect.MidY);
        draw();
        canvas.Restore();
    }

    private void DrawFrame(SKCanvas c, SKRect board, float thickness)
    {
        var outer = SKRect.Inflate(board, thickness, thickness);
        var radius = thickness * 1.1f;
        using var paint = new SKPaint { IsAntialias = true };

        // drop shadow
        paint.Color = new SKColor(0, 0, 0, 70);
        paint.MaskFilter = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, thickness * 0.5f);
        c.DrawRoundRect(SKRect.Create(outer.Left, outer.Top + thickness * 0.35f, outer.Width, outer.Height), radius, radius, paint);
        paint.MaskFilter = null;
        paint.Color = SKColors.White; // shader alpha is modulated by the paint colour

        using (var shader = SKShader.CreateLinearGradient(new SKPoint(0, outer.Top), new SKPoint(0, outer.Bottom),
                   [Palette.FrameWoodLight, Palette.FrameWood, Palette.FrameWoodDark], [0, 0.5f, 1], SKShaderTileMode.Clamp))
        {
            paint.Shader = shader;
            c.DrawRoundRect(outer, radius, radius, paint);
            paint.Shader = null;
        }

        // wood grain
        paint.Style = SKPaintStyle.Stroke;
        paint.StrokeWidth = Math.Max(1, thickness * 0.06f);
        paint.Color = Palette.FrameWoodDark.WithAlpha(90);
        for (var i = 1; i < 3; i++)
        {
            var y = outer.Top + thickness * i / 3f;
            c.DrawLine(outer.Left + radius, y, outer.Right - radius, y, paint);
            var y2 = outer.Bottom - thickness * i / 3f;
            c.DrawLine(outer.Left + radius, y2, outer.Right - radius, y2, paint);
        }

        // inner edge
        paint.StrokeWidth = Math.Max(1.5f, thickness * 0.12f);
        paint.Color = Palette.FrameWoodDark;
        c.DrawRoundRect(SKRect.Inflate(board, paint.StrokeWidth / 2, paint.StrokeWidth / 2), _tile * 0.18f, _tile * 0.18f, paint);

        // nails
        paint.Style = SKPaintStyle.Fill;
        var n = thickness * 0.5f;
        foreach (var (x, y) in new[] { (outer.Left + n, outer.Top + n), (outer.Right - n, outer.Top + n), (outer.Left + n, outer.Bottom - n), (outer.Right - n, outer.Bottom - n) })
        {
            paint.Color = new SKColor(0xD8, 0xD8, 0xD0);
            c.DrawCircle(x, y, thickness * 0.15f, paint);
            paint.Color = new SKColor(0x88, 0x88, 0x80);
            c.DrawCircle(x + thickness * 0.03f, y + thickness * 0.03f, thickness * 0.07f, paint);
        }
    }

    private HashSet<(int, int)> PressedChordNeighbours()
    {
        var set = new HashSet<(int, int)>();
        if (_engine is null || _press is not { } p) return set;
        var cell = _engine.Board[p.r, p.c];
        if (!cell.IsRevealed || cell.AdjacentPooCount == 0) return set;
        if (!ChordOnTap && !_pressIsMouse) return set;
        foreach (var n in _engine.Board.Neighbors(p.r, p.c))
            if (!n.IsRevealed && !n.IsFlagged) set.Add((n.Row, n.Column));
        return set;
    }

    // ------------------------------------------------------------------
    //  Input
    // ------------------------------------------------------------------

    private (int r, int c)? HitTest(SKPoint pt)
    {
        if (_engine is null || _tile <= 0) return null;
        if (!_board.Contains(pt)) return null;
        var c = (int)((pt.X - _board.Left) / _tile);
        var r = (int)((pt.Y - _board.Top) / _tile);
        if (!_engine.Board.InBounds(r, c)) return null;
        return (r, c);
    }

    protected override void OnTouch(SKTouchEventArgs e)
    {
        e.Handled = true;
        if (_engine is null) return;

        var hit = HitTest(e.Location);
        var isMouse = e.DeviceType == SKTouchDeviceType.Mouse;

        if (!_engine.CanInteract)
        {
            if (_hover is not null) { _hover = null; InvalidateSurface(); }
            return;
        }

        switch (e.ActionType)
        {
            case SKTouchAction.Pressed:
                if (hit is null) return;
                if (isMouse && e.MouseButton == SKMouseButton.Right)
                {
                    var cell = _engine.Board[hit.Value.r, hit.Value.c];
                    Raise(cell.IsRevealed ? BoardActionKind.Chord : BoardActionKind.Flag, hit.Value, false);
                    return;
                }
                if (isMouse && e.MouseButton == SKMouseButton.Middle)
                {
                    Raise(BoardActionKind.Chord, hit.Value, false);
                    return;
                }

                _press = hit;
                _pressPoint = e.Location;
                _pressIsMouse = isMouse;
                _longPressFired = false;
                if (!_engine.Board[hit.Value.r, hit.Value.c].IsRevealed)
                    PressingChanged?.Invoke(this, true);
                if (!isMouse) StartLongPress(hit.Value);
                InvalidateSurface();
                break;

            case SKTouchAction.Moved:
                if (e.InContact && _press is not null)
                {
                    if (_pressIsMouse)
                    {
                        if (hit != _press)
                        {
                            _press = hit;
                            InvalidateSurface();
                        }
                    }
                    else
                    {
                        var dx = e.Location.X - _pressPoint.X;
                        var dy = e.Location.Y - _pressPoint.Y;
                        if (dx * dx + dy * dy > _tile * _tile * 0.25f)
                            CancelPress();
                    }
                }
                else if (!e.InContact && isMouse && hit != _hover)
                {
                    _hover = hit;
                    InvalidateSurface();
                }
                break;

            case SKTouchAction.Released:
                {
                    var pressed = _press;
                    var fired = _longPressFired;
                    CancelPress();
                    if (pressed is null || fired || hit != pressed) return;
                    if (isMouse && e.MouseButton != SKMouseButton.Left) return;

                    var cell = _engine.Board[pressed.Value.r, pressed.Value.c];
                    if (cell.IsRevealed)
                    {
                        if (ChordOnTap || isMouse) Raise(BoardActionKind.Chord, pressed.Value, false);
                    }
                    else
                    {
                        Raise(FlagMode ? BoardActionKind.Flag : BoardActionKind.Reveal, pressed.Value, false);
                    }
                    break;
                }

            case SKTouchAction.Cancelled:
            case SKTouchAction.Exited:
                CancelPress();
                if (_hover is not null)
                {
                    _hover = null;
                    InvalidateSurface();
                }
                break;
        }
    }

    private void StartLongPress((int r, int c) cell)
    {
        var token = ++_longPressToken;
        Dispatcher.DispatchDelayed(TimeSpan.FromMilliseconds(LongPressMs), () =>
        {
            if (token != _longPressToken || _press != cell || _engine is null) return;
            _longPressFired = true;
            var c = _engine.Board[cell.r, cell.c];
            if (c.IsRevealed) return;
            Raise(FlagMode ? BoardActionKind.Reveal : BoardActionKind.Flag, cell, true);
            _press = null;
            PressingChanged?.Invoke(this, false);
            InvalidateSurface();
        });
    }

    private void CancelPress()
    {
        _longPressToken++;
        if (_press is not null)
        {
            _press = null;
            PressingChanged?.Invoke(this, false);
            InvalidateSurface();
        }
    }

    private void Raise(BoardActionKind kind, (int r, int c) cell, bool longPress)
        => CellAction?.Invoke(this, new BoardActionEventArgs(kind, cell.r, cell.c, longPress));
}
