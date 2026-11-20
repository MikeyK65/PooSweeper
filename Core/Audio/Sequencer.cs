namespace PooSweep.Core.Audio;

/// <summary>
/// A tiny tracker: lays notes out on a 16th-note grid into a <see cref="SampleBuffer"/>.
/// Patterns are written as space separated tokens: <c>C5:2 E5:2 .:4 G5</c>
/// (note:length-in-16ths, <c>.</c> is a rest, length defaults to 2, <c>|</c> is ignored).
/// </summary>
public sealed class Sequencer
{
    public Sequencer(int sampleRate, double bpm, int bars, bool loop, double tailSeconds = 0)
    {
        if (bpm <= 0) throw new ArgumentOutOfRangeException(nameof(bpm));
        if (bars <= 0) throw new ArgumentOutOfRangeException(nameof(bars));
        Bpm = bpm;
        Bars = bars;
        Loop = loop;
        SampleRate = sampleRate;
        var seconds = TotalSteps * StepSeconds + (loop ? 0 : tailSeconds);
        Main = new SampleBuffer(sampleRate, seconds);
        Lead = new SampleBuffer(sampleRate, seconds);
    }

    public const int StepsPerBar = 16;

    public double Bpm { get; }
    public int Bars { get; }
    public bool Loop { get; }
    public int SampleRate { get; }
    public double StepSeconds => 60.0 / Bpm / 4;
    public int TotalSteps => Bars * StepsPerBar;

    /// <summary>Bus for rhythm section.</summary>
    public SampleBuffer Main { get; }

    /// <summary>Bus for melodies (gets echo applied on mixdown).</summary>
    public SampleBuffer Lead { get; }

    private int _seed = 1;

    public void Note(Tone instrument, int step, int lengthSteps, int midi, double velocity = 1, double gate = 0.92, bool lead = false)
    {
        // An instrument's Freq/FreqEnd act as a ratio template for per-note pitch bends.
        var f = Notes.Freq(midi);
        var tone = instrument with
        {
            Freq = f,
            FreqEnd = instrument.FreqEnd is { } fe ? f * (fe / instrument.Freq) : null,
            Duration = Math.Max(0.02, lengthSteps * StepSeconds * gate),
            Volume = instrument.Volume * velocity,
        };
        (lead ? Lead : Main).Add(tone, step * StepSeconds, Loop, _seed++);
    }

    public void Hit(Tone drum, int step, double velocity = 1)
    {
        var tone = drum with { Volume = drum.Volume * velocity };
        Main.Add(tone, step * StepSeconds, Loop, _seed++);
    }

    /// <summary>Plays a pattern string; returns the number of steps it spans.</summary>
    public int Pattern(Tone instrument, int startStep, string pattern, int transpose = 0, double velocity = 1, double gate = 0.92, bool lead = true)
    {
        var step = startStep;
        foreach (var raw in pattern.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
        {
            if (raw == "|") continue;
            var parts = raw.Split(':');
            var len = parts.Length > 1 ? int.Parse(parts[1]) : 2;
            if (parts[0] != ".")
                Note(instrument, step, len, Notes.Midi(parts[0]) + transpose, velocity, gate, lead);
            step += len;
        }
        return step - startStep;
    }

    /// <summary>Mixes buses, applies echo to the lead and masters the result.</summary>
    public SampleBuffer Mixdown(double echoBeats = 0.75, double echoFeedback = 0.3, double echoMix = 0.35, double peak = 0.85)
    {
        if (echoMix > 0)
            Lead.Echo(echoBeats * 4 * StepSeconds, echoFeedback, echoMix, Loop);
        Main.Mix(Lead);
        Main.Master(peak);
        if (!Loop) Main.Declick();
        return Main;
    }
}

/// <summary>Simple chord-symbol support ("C", "Am", "G7", "F#m", "E").</summary>
public static class Chords
{
    public static (int root, int[] intervals) Parse(string symbol)
    {
        var i = 1;
        if (symbol.Length > 1 && (symbol[1] == '#' || symbol[1] == 'b')) i = 2;
        var rootName = symbol[..i];
        var rest = symbol[i..];
        var root = Notes.Midi(rootName + "0") - 12; // pitch class 0..11
        var minor = rest.StartsWith('m');
        var seventh = rest.EndsWith('7');
        var intervals = minor ? new List<int> { 0, 3, 7 } : new List<int> { 0, 4, 7 };
        if (seventh) intervals.Add(10);
        return (root, intervals.ToArray());
    }
}
