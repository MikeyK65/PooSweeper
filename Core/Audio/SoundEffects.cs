namespace PooSweep.Core.Audio;

/// <summary>Every sound effect in the game.</summary>
public enum Sfx
{
    UiClick,
    UiBack,
    UiToggle,
    Dig1,
    Dig2,
    Dig3,
    Cascade,
    Flag,
    Unflag,
    Chord,
    Denied,
    Splat,
    Whimper,
    Bark,
    NewGame,
    Win,
    Lose,
}

/// <summary>Procedurally synthesized sound effects.</summary>
public static class SoundEffects
{
    public const int SampleRate = 44100;

    public static SampleBuffer Render(Sfx sfx) => sfx switch
    {
        Sfx.UiClick => UiClick(),
        Sfx.UiBack => UiBack(),
        Sfx.UiToggle => UiToggle(),
        Sfx.Dig1 => Dig(0),
        Sfx.Dig2 => Dig(1),
        Sfx.Dig3 => Dig(2),
        Sfx.Cascade => Cascade(),
        Sfx.Flag => Flag(),
        Sfx.Unflag => Unflag(),
        Sfx.Chord => Chord(),
        Sfx.Denied => Denied(),
        Sfx.Splat => Splat(),
        Sfx.Whimper => Whimper(),
        Sfx.Bark => Bark(),
        Sfx.NewGame => NewGame(),
        Sfx.Win => Music.WinJingle(),
        Sfx.Lose => Music.LoseJingle(),
        _ => throw new ArgumentOutOfRangeException(nameof(sfx)),
    };

    private static SampleBuffer Finish(SampleBuffer b, double peak = 0.8)
    {
        b.Master(peak);
        b.Declick(0.002);
        return b;
    }

    private static SampleBuffer UiClick()
    {
        var b = new SampleBuffer(SampleRate, 0.08);
        b.Add(new Tone { Wave = Wave.Square, Freq = 1500, FreqEnd = 1100, Duration = 0.02, Volume = 0.35, LowPass = 5000, Decay = 0.02, Sustain = 0.2, Release = 0.02 }, 0);
        b.Add(new Tone { Wave = Wave.Sine, Freq = 700, FreqEnd = 1000, Duration = 0.035, Volume = 0.5, Decay = 0.03, Sustain = 0.3, Release = 0.02 }, 0);
        return Finish(b, 0.55);
    }

    private static SampleBuffer UiBack()
    {
        var b = new SampleBuffer(SampleRate, 0.12);
        b.Add(new Tone { Wave = Wave.Sine, Freq = 950, FreqEnd = 420, Duration = 0.07, Volume = 0.6, Decay = 0.06, Sustain = 0.4, Release = 0.03 }, 0);
        b.Add(new Tone { Wave = Wave.Square, Freq = 950, FreqEnd = 420, Duration = 0.05, Volume = 0.12, LowPass = 3000, Release = 0.02 }, 0);
        return Finish(b, 0.55);
    }

    private static SampleBuffer UiToggle()
    {
        var b = new SampleBuffer(SampleRate, 0.14);
        b.Add(new Tone { Wave = Wave.Pulse25, Freq = Notes.Freq("E6"), Duration = 0.035, Volume = 0.3, LowPass = 6000, Release = 0.02 }, 0);
        b.Add(new Tone { Wave = Wave.Pulse25, Freq = Notes.Freq("B6"), Duration = 0.05, Volume = 0.3, LowPass = 6000, Release = 0.03 }, 0.045);
        return Finish(b, 0.5);
    }

    /// <summary>A scrunchy spade-in-turf sound.</summary>
    private static SampleBuffer Dig(int variant)
    {
        var b = new SampleBuffer(SampleRate, 0.14);
        b.Add(new Tone
        {
            Wave = Wave.Noise, Freq = 5000 + variant * 1200, Duration = 0.06, Volume = 0.55,
            LowPass = 3200 + variant * 400, LowPassEnd = 700, Attack = 0.001, Decay = 0.05, Sustain = 0.2, Release = 0.04,
        }, 0, seed: 11 + variant);
        b.Add(new Tone
        {
            Wave = Wave.Sine, Freq = 190 + variant * 15, FreqEnd = 70, Duration = 0.06, Volume = 0.7,
            Attack = 0.001, Decay = 0.05, Sustain = 0.2, Release = 0.03,
        }, 0);
        b.Add(new Tone
        {
            Wave = Wave.Triangle, Freq = 660 + variant * 90, Duration = 0.025, Volume = 0.18, Release = 0.02,
        }, 0.01);
        return Finish(b, 0.7);
    }

    /// <summary>Big flood-fill: a whoosh with a sparkly arpeggio.</summary>
    private static SampleBuffer Cascade()
    {
        var b = new SampleBuffer(SampleRate, 0.6);
        b.Add(new Tone
        {
            Wave = Wave.Noise, Freq = 12000, Duration = 0.35, Volume = 0.3,
            LowPass = 500, LowPassEnd = 6000, Attack = 0.08, Decay = 0.2, Sustain = 0.5, Release = 0.15,
        }, 0, seed: 7);
        string[] notes = ["C6", "E6", "G6", "C7", "E7"];
        for (var i = 0; i < notes.Length; i++)
        {
            b.Add(new Tone
            {
                Wave = Wave.Pulse12, Freq = Notes.Freq(notes[i]), Duration = 0.05, Volume = 0.16,
                Decay = 0.04, Sustain = 0.4, Release = 0.12,
            }, 0.04 + i * 0.045);
        }
        return Finish(b, 0.6);
    }

    /// <summary>Flag plant: a springy "boing" with a tap.</summary>
    private static SampleBuffer Flag()
    {
        var b = new SampleBuffer(SampleRate, 0.3);
        b.Add(new Tone
        {
            Wave = Wave.Noise, Freq = 8000, Duration = 0.015, Volume = 0.3, LowPass = 4000, Release = 0.01,
        }, 0);
        b.Add(new Tone
        {
            Wave = Wave.Triangle, Freq = 280, FreqEnd = 820, SweepCurve = 0.5, Duration = 0.11, Volume = 0.55,
            Attack = 0.002, Decay = 0.08, Sustain = 0.6, Release = 0.05,
        }, 0.005);
        b.Add(new Tone
        {
            Wave = Wave.Sine, Freq = 820, Duration = 0.12, Volume = 0.25,
            VibratoRate = 22, VibratoDepth = 1.2, Attack = 0.01, Decay = 0.1, Sustain = 0.3, Release = 0.06,
        }, 0.1);
        return Finish(b, 0.6);
    }

    private static SampleBuffer Unflag()
    {
        var b = new SampleBuffer(SampleRate, 0.2);
        b.Add(new Tone
        {
            Wave = Wave.Triangle, Freq = 760, FreqEnd = 230, SweepCurve = 0.6, Duration = 0.11, Volume = 0.55,
            Attack = 0.002, Decay = 0.08, Sustain = 0.5, Release = 0.04,
        }, 0);
        b.Add(new Tone { Wave = Wave.Noise, Freq = 6000, Duration = 0.02, Volume = 0.15, LowPass = 3000, Release = 0.01 }, 0.0);
        return Finish(b, 0.55);
    }

    private static SampleBuffer Chord()
    {
        var b = new SampleBuffer(SampleRate, 0.2);
        b.Add(new Tone { Wave = Wave.Pulse25, Freq = Notes.Freq("G5"), Duration = 0.03, Volume = 0.25, LowPass = 5000, Release = 0.02 }, 0);
        b.Add(new Tone { Wave = Wave.Pulse25, Freq = Notes.Freq("D6"), Duration = 0.05, Volume = 0.25, LowPass = 5000, Release = 0.04 }, 0.04);
        b.Add(new Tone { Wave = Wave.Noise, Freq = 6000, Duration = 0.05, Volume = 0.25, LowPass = 3000, LowPassEnd = 800, Release = 0.03 }, 0.0, seed: 3);
        return Finish(b, 0.55);
    }

    private static SampleBuffer Denied()
    {
        var b = new SampleBuffer(SampleRate, 0.25);
        var t = new Tone { Wave = Wave.Square, Freq = 165, Duration = 0.06, Volume = 0.4, LowPass = 1400, Release = 0.02 };
        b.Add(t, 0);
        b.Add(t with { Freq = 140 }, 0.09);
        return Finish(b, 0.5);
    }

    /// <summary>The dreaded squelch.</summary>
    private static SampleBuffer Splat()
    {
        var b = new SampleBuffer(SampleRate, 0.75);
        // wet impact
        b.Add(new Tone
        {
            Wave = Wave.Noise, Freq = 4000, Duration = 0.4, Volume = 0.9,
            LowPass = 3500, LowPassEnd = 180, Attack = 0.001, Decay = 0.15, Sustain = 0.35, Release = 0.2,
        }, 0, seed: 5);
        // body drop
        b.Add(new Tone
        {
            Wave = Wave.Sine, Freq = 240, FreqEnd = 38, SweepCurve = 0.5, Duration = 0.35, Volume = 1.0,
            Attack = 0.001, Decay = 0.25, Sustain = 0.4, Release = 0.1,
        }, 0);
        // squelchy wobble
        b.Add(new Tone
        {
            Wave = Wave.Saw, Freq = 130, FreqEnd = 55, Duration = 0.3, Volume = 0.45,
            LowPass = 700, LowPassEnd = 250, VibratoRate = 19, VibratoDepth = 3.5,
            Attack = 0.02, Decay = 0.2, Sustain = 0.5, Release = 0.12,
        }, 0.05);
        // little drips
        b.Add(new Tone { Wave = Wave.Sine, Freq = 900, FreqEnd = 1500, Duration = 0.04, Volume = 0.25, Release = 0.03 }, 0.42);
        b.Add(new Tone { Wave = Wave.Sine, Freq = 750, FreqEnd = 1300, Duration = 0.04, Volume = 0.18, Release = 0.03 }, 0.55);
        return Finish(b, 0.9);
    }

    /// <summary>Sad doggy whine.</summary>
    private static SampleBuffer Whimper()
    {
        var b = new SampleBuffer(SampleRate, 0.9);
        b.Add(new Tone
        {
            Wave = Wave.Sine, Freq = 820, FreqEnd = 1180, SweepCurve = 0.7, Duration = 0.22, Volume = 0.45,
            VibratoRate = 9, VibratoDepth = 0.35, Attack = 0.04, Decay = 0.1, Sustain = 0.8, Release = 0.05,
        }, 0);
        b.Add(new Tone
        {
            Wave = Wave.Sine, Freq = 1180, FreqEnd = 640, SweepCurve = 1.4, Duration = 0.45, Volume = 0.45,
            VibratoRate = 8, VibratoDepth = 0.45, Attack = 0.01, Decay = 0.2, Sustain = 0.7, Release = 0.12,
        }, 0.24);
        b.Add(new Tone
        {
            Wave = Wave.Triangle, Freq = 410, FreqEnd = 320, Duration = 0.65, Volume = 0.12,
            VibratoRate = 8, VibratoDepth = 0.4, Attack = 0.05, Release = 0.1, LowPass = 1200,
        }, 0.02);
        return Finish(b, 0.6);
    }

    /// <summary>"Woof woof!"</summary>
    private static SampleBuffer Bark()
    {
        var b = new SampleBuffer(SampleRate, 0.55);
        void Woof(double at, double pitch)
        {
            b.Add(new Tone
            {
                Wave = Wave.Saw, Freq = 560 * pitch, FreqEnd = 250 * pitch, SweepCurve = 0.45, Duration = 0.11, Volume = 0.7,
                LowPass = 1900, LowPassEnd = 700, VibratoRate = 35, VibratoDepth = 0.6,
                Attack = 0.004, Decay = 0.07, Sustain = 0.5, Release = 0.05,
            }, at);
            b.Add(new Tone
            {
                Wave = Wave.Square, Freq = 300 * pitch, FreqEnd = 170 * pitch, Duration = 0.1, Volume = 0.3,
                LowPass = 900, Attack = 0.004, Decay = 0.06, Sustain = 0.4, Release = 0.04,
            }, at);
            b.Add(new Tone
            {
                Wave = Wave.Noise, Freq = 9000, Duration = 0.05, Volume = 0.25,
                LowPass = 2500, LowPassEnd = 900, Attack = 0.002, Release = 0.04,
            }, at, seed: (int)(pitch * 100));
        }
        Woof(0, 1.0);
        Woof(0.2, 0.92);
        return Finish(b, 0.75);
    }

    private static SampleBuffer NewGame()
    {
        var b = new SampleBuffer(SampleRate, 0.45);
        b.Add(new Tone
        {
            Wave = Wave.Noise, Freq = 10000, Duration = 0.22, Volume = 0.25,
            LowPass = 300, LowPassEnd = 4500, Attack = 0.06, Decay = 0.1, Sustain = 0.4, Release = 0.1,
        }, 0, seed: 9);
        string[] notes = ["C5", "E5", "G5", "C6"];
        for (var i = 0; i < notes.Length; i++)
        {
            b.Add(new Tone
            {
                Wave = Wave.Pulse25, Freq = Notes.Freq(notes[i]), Duration = 0.05, Volume = 0.2, LowPass = 5000,
                Decay = 0.04, Sustain = 0.5, Release = 0.06,
            }, 0.05 + i * 0.05);
        }
        return Finish(b, 0.6);
    }
}
