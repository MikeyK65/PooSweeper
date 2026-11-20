namespace PooSweep.Core.Audio;

/// <summary>Music tracks.</summary>
public enum MusicTrack
{
    Title,
    Game,
}

/// <summary>Original chiptune compositions, synthesized at runtime.</summary>
public static class Music
{
    public const int SampleRate = 32000;

    // ---------------- instruments ----------------

    private static readonly Tone Lead = new()
    {
        Wave = Wave.Pulse25, Volume = 0.30,
        Attack = 0.004, Decay = 0.09, Sustain = 0.55, Release = 0.06,
        VibratoRate = 5.5, VibratoDepth = 0.18, VibratoDelay = 0.14,
    };

    private static readonly Tone Pluck = new()
    {
        Wave = Wave.Square, Volume = 0.22, LowPass = 3200,
        Attack = 0.002, Decay = 0.07, Sustain = 0.25, Release = 0.03,
    };

    private static readonly Tone Bass = new()
    {
        Wave = Wave.Triangle, Volume = 0.62,
        Attack = 0.003, Decay = 0.05, Sustain = 0.85, Release = 0.02,
    };

    private static readonly Tone Arp = new()
    {
        Wave = Wave.Pulse12, Volume = 0.10,
        Attack = 0.002, Decay = 0.05, Sustain = 0.2, Release = 0.02,
    };

    private static readonly Tone Double = new()
    {
        Wave = Wave.Square, Volume = 0.11, LowPass = 1800,
        Attack = 0.01, Decay = 0.1, Sustain = 0.6, Release = 0.05,
    };

    private static readonly Tone Kick = new()
    {
        Wave = Wave.Sine, Freq = 160, FreqEnd = 42, SweepCurve = 0.4,
        Duration = 0.13, Volume = 0.85, Attack = 0.001, Decay = 0.1, Sustain = 0.3, Release = 0.03,
    };

    private static readonly Tone Snare = new()
    {
        Wave = Wave.Noise, Freq = 9000, Duration = 0.09, Volume = 0.33, LowPass = 7000, LowPassEnd = 2500,
        Attack = 0.001, Decay = 0.08, Sustain = 0.15, Release = 0.05,
    };

    private static readonly Tone SnareBody = new()
    {
        Wave = Wave.Triangle, Freq = 240, FreqEnd = 170, Duration = 0.06, Volume = 0.25,
        Attack = 0.001, Decay = 0.05, Sustain = 0.1, Release = 0.02,
    };

    private static readonly Tone Hat = new()
    {
        Wave = Wave.Noise, Freq = 22000, Duration = 0.018, Volume = 0.10,
        Attack = 0.0005, Decay = 0.015, Sustain = 0.1, Release = 0.02,
    };

    private static readonly Tone Crash = new()
    {
        Wave = Wave.Noise, Freq = 18000, Duration = 0.9, Volume = 0.16, LowPass = 9000, LowPassEnd = 2500,
        Attack = 0.001, Decay = 0.6, Sustain = 0.05, Release = 0.3,
    };

    // ---------------- public API ----------------

    public static SampleBuffer Render(MusicTrack track) => track switch
    {
        MusicTrack.Title => TitleTheme(),
        MusicTrack.Game => GameTheme(),
        _ => throw new ArgumentOutOfRangeException(nameof(track)),
    };

    /// <summary>"Walkies!" – bouncy C major title theme (16 bar loop).</summary>
    public static SampleBuffer TitleTheme()
    {
        var s = new Sequencer(SampleRate, 148, 16, loop: true);

        string[] a = ["C", "Am", "F", "G", "C", "Am", "F G", "C"];
        string[] b = ["F", "C", "Dm", "G", "F", "C", "Dm G", "C G7"];

        const string melA =
            "E5:2 G5:2 C6:2 G5:2 A5:2 G5:2 E5:4 | " +
            "C5:2 E5:2 A5:3 G5:1 E5:4 .:4 | " +
            "F5:2 A5:2 C6:2 A5:2 G5:2 F5:2 E5:2 F5:2 | " +
            "G5:6 D5:2 B4:4 .:4 | " +
            "E5:2 G5:2 C6:2 E6:2 D6:2 C6:2 G5:4 | " +
            "A5:2 C6:2 E6:2 C6:2 B5:2 A5:2 E5:4 | " +
            "F5:2 A5:2 C6:4 B5:2 D6:2 G5:4 | " +
            "C6:4 G5:2 E5:2 C5:4 .:4";

        const string melB =
            "A5:4 G5:2 F5:2 A5:4 C6:4 | " +
            "G5:6 E5:2 C5:4 E5:4 | " +
            "F5:2 E5:2 D5:2 F5:2 A5:4 D6:4 | " +
            "B5:4 A5:2 G5:2 D5:4 G5:4 | " +
            "A5:2 C6:2 F6:4 E6:2 D6:2 C6:4 | " +
            "E6:2 D6:2 C6:2 G5:2 E5:4 G5:4 | " +
            "F5:2 A5:2 D6:4 D6:2 B5:2 G5:4 | " +
            "C6:2 G5:2 E5:2 G5:2 B5:4 D6:4";

        s.Pattern(Lead, 0, melA);
        s.Pattern(Lead, 8 * 16, melB);
        s.Pattern(Double, 8 * 16, melB, transpose: -12);

        Accompany(s, 0, a, BassStyle.Bounce, arp: true);
        Accompany(s, 8 * 16, b, BassStyle.Bounce, arp: true);
        Drums(s, 0, 16, DrumStyle.Bouncy);

        return s.Mixdown(echoBeats: 0.75, echoFeedback: 0.28, echoMix: 0.3);
    }

    /// <summary>"Sniffing Around" – sneaky-but-cheerful A minor gameplay loop (32 bars).</summary>
    public static SampleBuffer GameTheme()
    {
        var s = new Sequencer(SampleRate, 116, 32, loop: true);

        string[] a = ["Am", "Am", "Dm", "E", "Am", "F", "Dm E", "Am"];
        string[] b = ["C", "G", "Am", "E", "F", "C", "Dm", "E"];

        const string melA =
            "A4:1 .:1 C5:1 .:1 E5:2 .:2 D#5:1 E5:1 .:2 C5:2 .:2 | " +
            "A4:1 .:1 C5:1 .:1 E5:2 .:2 A5:4 .:4 | " +
            "D5:1 .:1 F5:1 .:1 A5:2 .:2 G#5:1 A5:1 .:2 F5:2 .:2 | " +
            "E5:2 G#5:2 B5:2 D6:2 C6:2 B5:2 G#5:4 | " +
            "C6:2 B5:1 A5:1 E5:4 C6:2 B5:1 A5:1 E5:4 | " +
            "F5:2 E5:1 F5:1 A5:4 C6:4 .:4 | " +
            "D6:2 C6:2 A5:4 B5:2 G#5:2 E5:4 | " +
            "A5:4 E5:2 C5:2 A4:4 .:4";

        const string melB =
            "G5:2 E5:2 C5:2 E5:2 G5:4 C6:4 | " +
            "B5:2 G5:2 D5:2 G5:2 B5:4 D6:4 | " +
            "C6:2 B5:2 A5:2 E5:2 A5:4 C6:4 | " +
            "B5:8 G#5:4 E5:4 | " +
            "A5:2 C6:2 F6:2 C6:2 A5:2 F5:2 A5:4 | " +
            "G5:2 C6:2 E6:2 C6:2 G5:2 E5:2 G5:4 | " +
            "F5:2 A5:2 D6:4 C6:2 A5:2 F5:4 | " +
            "E5:2 G#5:2 B5:2 E6:2 D6:4 B5:4";

        // A  A'  B  A
        s.Pattern(Pluck, 0, melA, gate: 0.7);
        s.Pattern(Lead, 8 * 16, melA);
        s.Pattern(Double, 8 * 16, melA, transpose: -12);
        s.Pattern(Lead, 16 * 16, melB);
        s.Pattern(Pluck, 24 * 16, melA, gate: 0.7);

        Accompany(s, 0, a, BassStyle.Sneak, arp: false);
        Accompany(s, 8 * 16, a, BassStyle.Sneak, arp: true);
        Accompany(s, 16 * 16, b, BassStyle.Bounce, arp: true);
        Accompany(s, 24 * 16, a, BassStyle.Sneak, arp: false);
        Drums(s, 0, 32, DrumStyle.Sneaky);

        return s.Mixdown(echoBeats: 0.5, echoFeedback: 0.25, echoMix: 0.28);
    }

    /// <summary>Victory fanfare (one-shot).</summary>
    public static SampleBuffer WinJingle()
    {
        var s = new Sequencer(SampleRate, 160, 2, loop: false, tailSeconds: 1.2);

        s.Pattern(Lead, 0,
            "G4:1 C5:1 E5:1 G5:1 C5:1 E5:1 G5:1 C6:1 E5:1 G5:1 C6:1 E6:1 G6:3 E6:1 | " +
            "C6:2 D6:2 E6:2 G6:2 C7:8", velocity: 0.9);
        s.Pattern(Double, 16, "E5:2 F5:2 G5:2 B5:2 E6:8");
        s.Pattern(Bass, 0, "C3:4 E3:4 G3:4 C4:4 | F3:2 G3:2 A3:2 B3:2 C4:8", lead: false);

        var chord = Arp with { Volume = 0.16, Arpeggio = [0, 4, 7, 12], ArpeggioRate = 24, Sustain = 0.8, Release = 0.4 };
        s.Note(chord, 24, 8, Notes.Midi("C5"));

        s.Hit(Kick, 0); s.Hit(Kick, 8); s.Hit(Snare, 12); s.Hit(Snare, 14); s.Hit(Snare, 15, 0.7);
        s.Hit(Kick, 16); s.Hit(Snare, 20); s.Hit(Snare, 22); s.Hit(Kick, 24); s.Hit(Crash, 24);

        return s.Mixdown(echoBeats: 0.5, echoFeedback: 0.3, echoMix: 0.3, peak: 0.9);
    }

    /// <summary>"Wah wah wah waaah" – sad trombone (one-shot).</summary>
    public static SampleBuffer LoseJingle()
    {
        var s = new Sequencer(SampleRate, 100, 2, loop: false, tailSeconds: 0.6);

        var bone = new Tone
        {
            Wave = Wave.Saw, Freq = 1, FreqEnd = 0.965, SweepCurve = 1.6, Volume = 0.45,
            LowPass = 450, LowPassEnd = 1700,
            Attack = 0.03, Decay = 0.15, Sustain = 0.8, Release = 0.12,
        };
        var boneLast = bone with
        {
            FreqEnd = 0.94, SweepCurve = 2.5, VibratoRate = 6.5, VibratoDepth = 0.55, VibratoDelay = 0.25,
            LowPassEnd = 1100,
        };
        var mute = bone with { Wave = Wave.Square, Volume = 0.15, LowPass = 600, LowPassEnd = 1200 };

        s.Note(bone, 0, 4, Notes.Midi("D4"), gate: 0.85, lead: true);
        s.Note(bone, 4, 4, Notes.Midi("C#4"), gate: 0.85, lead: true);
        s.Note(bone, 8, 4, Notes.Midi("C4"), gate: 0.85, lead: true);
        s.Note(boneLast, 12, 16, Notes.Midi("B3"), gate: 0.95, lead: true);

        s.Note(mute, 0, 4, Notes.Midi("D3"), gate: 0.85);
        s.Note(mute, 4, 4, Notes.Midi("C#3"), gate: 0.85);
        s.Note(mute, 8, 4, Notes.Midi("C3"), gate: 0.85);
        s.Note(mute with { VibratoRate = 6.5, VibratoDepth = 0.5, VibratoDelay = 0.25 }, 12, 16, Notes.Midi("B2"), gate: 0.95);

        return s.Mixdown(echoMix: 0, peak: 0.85);
    }

    // ---------------- arrangement helpers ----------------

    private enum BassStyle { Bounce, Sneak }

    private enum DrumStyle { Bouncy, Sneaky }

    private static void Accompany(Sequencer s, int startStep, string[] bars, BassStyle style, bool arp)
    {
        for (var bar = 0; bar < bars.Length; bar++)
        {
            var symbols = bars[bar].Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var span = 16 / symbols.Length;
            for (var c = 0; c < symbols.Length; c++)
            {
                var (root, iv) = Chords.Parse(symbols[c]);
                var at = startStep + bar * 16 + c * span;
                var bassRoot = 36 + root; // C2..B2
                var fifth = bassRoot + 7;

                for (var k = 0; k < span; k += 2)
                {
                    var step = at + k;
                    switch (style)
                    {
                        case BassStyle.Bounce:
                            {
                                var n = (k / 2) % 4 switch
                                {
                                    0 => bassRoot,
                                    1 => bassRoot + 12,
                                    2 => fifth,
                                    _ => bassRoot + 12,
                                };
                                s.Note(Bass, step, 2, n, velocity: k % 4 == 0 ? 1 : 0.8, gate: 0.55);
                                break;
                            }
                        case BassStyle.Sneak:
                            {
                                if (k % 4 != 0) break; // quarter notes, tip-toe staccato
                                var n = (k / 4) % 4 switch
                                {
                                    0 => bassRoot,
                                    1 => fifth,
                                    2 => bassRoot + 12,
                                    _ => fifth,
                                };
                                s.Note(Bass, step, 4, n, gate: 0.3);
                                break;
                            }
                    }
                }

                if (arp)
                {
                    var tones = iv.Select(x => 60 + root + x).Append(72 + root).ToArray();
                    for (var k = 0; k < span; k++)
                        s.Note(Arp, at + k, 1, tones[k % tones.Length], velocity: k % 4 == 0 ? 1 : 0.7, gate: 0.8);
                }
            }
        }
    }

    private static void Drums(Sequencer s, int startStep, int bars, DrumStyle style)
    {
        for (var bar = 0; bar < bars; bar++)
        {
            var at = startStep + bar * 16;
            var fill = bar % 8 == 7;

            if (style == DrumStyle.Bouncy)
            {
                s.Hit(Kick, at + 0);
                s.Hit(Kick, at + 8);
                if (bar % 2 == 1) s.Hit(Kick, at + 10, 0.7);
                s.Hit(Snare, at + 4); s.Hit(SnareBody, at + 4);
                if (!fill) { s.Hit(Snare, at + 12); s.Hit(SnareBody, at + 12); }
                for (var k = 0; k < 16; k += 2) s.Hit(Hat, at + k, k % 4 == 2 ? 1 : 0.6);
            }
            else
            {
                s.Hit(Kick, at + 0);
                s.Hit(Kick, at + 7, 0.6);
                s.Hit(Kick, at + 8);
                s.Hit(Snare, at + 4, 0.6); s.Hit(SnareBody, at + 4, 0.6);
                if (!fill) { s.Hit(Snare, at + 12, 0.6); s.Hit(SnareBody, at + 12, 0.6); }
                for (var k = 2; k < 16; k += 4) s.Hit(Hat, at + k);
            }

            if (fill)
            {
                s.Hit(Snare, at + 12, 0.7); s.Hit(Snare, at + 13, 0.8);
                s.Hit(Snare, at + 14, 0.9); s.Hit(Snare, at + 15, 1.0);
                s.Hit(SnareBody, at + 14); s.Hit(SnareBody, at + 15);
            }
        }
    }
}
