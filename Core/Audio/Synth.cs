namespace PooSweep.Core.Audio;

/// <summary>Oscillator shapes available to the chiptune synth.</summary>
public enum Wave
{
    /// <summary>Thin, nasal 12.5% pulse.</summary>
    Pulse12,
    /// <summary>Classic 25% pulse lead.</summary>
    Pulse25,
    /// <summary>Hollow square wave.</summary>
    Square,
    /// <summary>Soft triangle, good for bass.</summary>
    Triangle,
    /// <summary>Bright sawtooth.</summary>
    Saw,
    /// <summary>Pure sine.</summary>
    Sine,
    /// <summary>LFSR white-ish noise (frequency controls the grain).</summary>
    Noise,
}

/// <summary>Describes one synthesized sound event.</summary>
public sealed record Tone
{
    public Wave Wave { get; init; } = Wave.Square;

    /// <summary>Start frequency in Hz.</summary>
    public double Freq { get; init; } = 440;

    /// <summary>End frequency in Hz for pitch sweeps (null = no sweep).</summary>
    public double? FreqEnd { get; init; }

    /// <summary>Sweep curve exponent: 1 = linear, &gt;1 = slow start, &lt;1 = fast start.</summary>
    public double SweepCurve { get; init; } = 1;

    public double Duration { get; init; } = 0.2;
    public double Volume { get; init; } = 0.5;

    public double Attack { get; init; } = 0.005;
    public double Decay { get; init; } = 0.05;
    public double Sustain { get; init; } = 0.7;
    public double Release { get; init; } = 0.04;

    public double VibratoRate { get; init; }
    public double VibratoDepth { get; init; } // in semitones
    public double VibratoDelay { get; init; }

    /// <summary>Optional one-pole low-pass cutoff in Hz.</summary>
    public double? LowPass { get; init; }

    /// <summary>Optional end cutoff for a filter sweep.</summary>
    public double? LowPassEnd { get; init; }

    /// <summary>Semitone offsets cycled rapidly to fake chords (classic chip arpeggio).</summary>
    public int[]? Arpeggio { get; init; }

    public double ArpeggioRate { get; init; } = 30;
}

/// <summary>A mono floating-point sample buffer that tones are mixed into.</summary>
public sealed class SampleBuffer
{
    private float[] _data;

    public SampleBuffer(int sampleRate, double seconds)
    {
        if (sampleRate <= 0) throw new ArgumentOutOfRangeException(nameof(sampleRate));
        SampleRate = sampleRate;
        _data = new float[Math.Max(1, (int)Math.Ceiling(seconds * sampleRate))];
    }

    public int SampleRate { get; }
    public float[] Data => _data;
    public int Length => _data.Length;
    public double Seconds => (double)_data.Length / SampleRate;

    /// <summary>
    /// Renders a tone at <paramref name="start"/> seconds. When <paramref name="wrap"/> is true,
    /// anything that runs past the end is folded back to the start so music loops seamlessly.
    /// </summary>
    public void Add(Tone tone, double start, bool wrap = false, int seed = 1)
    {
        var sr = SampleRate;
        var total = tone.Duration + tone.Release;
        var n = (int)(total * sr);
        var offset = (int)Math.Round(start * sr);

        double phase = 0;
        uint lfsr = (uint)(0xACE1u ^ (uint)seed * 2654435761u) | 1u;
        float noiseValue = 1;
        double noisePhase = 0;
        double lp = 0;

        for (var i = 0; i < n; i++)
        {
            var t = (double)i / sr;

            // ---- frequency ----
            var f = tone.Freq;
            if (tone.FreqEnd is { } fe && tone.Duration > 0)
            {
                var k = Math.Clamp(t / tone.Duration, 0, 1);
                k = Math.Pow(k, tone.SweepCurve);
                // exponential (musical) interpolation
                f = tone.Freq * Math.Pow(fe / tone.Freq, k);
            }

            if (tone.Arpeggio is { Length: > 0 } arp)
            {
                var idx = (int)(t * tone.ArpeggioRate) % arp.Length;
                f *= Math.Pow(2, arp[idx] / 12.0);
            }

            if (tone.VibratoDepth > 0 && t > tone.VibratoDelay)
            {
                var ramp = Math.Min(1, (t - tone.VibratoDelay) / 0.15);
                f *= Math.Pow(2, tone.VibratoDepth * ramp * Math.Sin(2 * Math.PI * tone.VibratoRate * t) / 12.0);
            }

            var dt = f / sr;

            // ---- oscillator ----
            double s;
            switch (tone.Wave)
            {
                case Wave.Pulse12: s = Pulse(phase, dt, 0.125); break;
                case Wave.Pulse25: s = Pulse(phase, dt, 0.25); break;
                case Wave.Square: s = Pulse(phase, dt, 0.5); break;
                case Wave.Triangle:
                    // slightly stepped triangle for that chip flavour
                    var tri = 4 * Math.Abs(phase - 0.5) - 1;
                    s = Math.Round(tri * 8) / 8;
                    break;
                case Wave.Saw: s = 2 * phase - 1 - PolyBlep(phase, dt); break;
                case Wave.Sine: s = Math.Sin(2 * Math.PI * phase); break;
                case Wave.Noise:
                    noisePhase += dt;
                    while (noisePhase >= 1)
                    {
                        noisePhase -= 1;
                        var bit = ((lfsr >> 0) ^ (lfsr >> 1)) & 1;
                        lfsr = (lfsr >> 1) | (bit << 14);
                        noiseValue = (lfsr & 1) == 1 ? 1f : -1f;
                    }
                    s = noiseValue;
                    break;
                default: s = 0; break;
            }

            phase += dt;
            if (phase >= 1) phase -= Math.Floor(phase);

            // ---- filter ----
            if (tone.LowPass is { } cutoff)
            {
                var c = cutoff;
                if (tone.LowPassEnd is { } ce && tone.Duration > 0)
                    c = cutoff * Math.Pow(ce / cutoff, Math.Clamp(t / tone.Duration, 0, 1));
                var a = 1 - Math.Exp(-2 * Math.PI * c / sr);
                lp += a * (s - lp);
                s = lp;
            }

            // ---- envelope ----
            var env = Envelope(tone, t);
            var v = (float)(s * env * tone.Volume);

            var idxOut = offset + i;
            if (idxOut >= _data.Length)
            {
                if (!wrap) break;
                idxOut %= _data.Length;
            }
            if (idxOut < 0) continue;
            _data[idxOut] += v;
        }
    }

    private static double Envelope(Tone tone, double t)
    {
        double level;
        if (t < tone.Attack)
            level = tone.Attack <= 0 ? 1 : t / tone.Attack;
        else if (t < tone.Attack + tone.Decay)
            level = 1 - (1 - tone.Sustain) * ((t - tone.Attack) / tone.Decay);
        else
            level = tone.Sustain;

        if (t >= tone.Duration)
        {
            // value at note-off, then fade
            var atOff = tone.Duration < tone.Attack
                ? (tone.Attack <= 0 ? 1 : tone.Duration / tone.Attack)
                : tone.Duration < tone.Attack + tone.Decay
                    ? 1 - (1 - tone.Sustain) * ((tone.Duration - tone.Attack) / tone.Decay)
                    : tone.Sustain;
            var r = tone.Release <= 0 ? 1 : (t - tone.Duration) / tone.Release;
            level = atOff * Math.Max(0, 1 - r);
        }

        return level;
    }

    private static double Pulse(double phase, double dt, double duty)
    {
        var v = phase < duty ? 1.0 : -1.0;
        v += PolyBlep(phase, dt);
        var p2 = phase - duty;
        if (p2 < 0) p2 += 1;
        v -= PolyBlep(p2, dt);
        return v;
    }

    /// <summary>Band-limits discontinuities so the waveforms sound clean instead of fizzy.</summary>
    private static double PolyBlep(double t, double dt)
    {
        if (dt <= 0) return 0;
        if (t < dt)
        {
            t /= dt;
            return t + t - t * t - 1;
        }
        if (t > 1 - dt)
        {
            t = (t - 1) / dt;
            return t * t + t + t + 1;
        }
        return 0;
    }

    /// <summary>Adds a feedback echo (used to give leads some space).</summary>
    public void Echo(double delaySeconds, double feedback, double mix, bool wrap = false)
    {
        var n = _data.Length;
        var d = (int)(delaySeconds * SampleRate);
        if (d <= 0 || d >= n) return;

        // wet[i] = dry[i-d] + feedback * wet[i-d]; for loops we run twice so the
        // tail of the loop feeds the echoes at its start.
        var wet = new float[n];
        var passes = wrap ? 2 : 1;
        for (var pass = 0; pass < passes; pass++)
        for (var i = 0; i < n; i++)
        {
            var j = i - d;
            if (j < 0)
            {
                if (!wrap) continue;
                j += n;
            }
            wet[i] = (float)(_data[j] + feedback * wet[j]);
        }

        for (var i = 0; i < n; i++)
            _data[i] += (float)(wet[i] * mix);
    }

    /// <summary>Mixes another buffer (same sample rate) into this one.</summary>
    public void Mix(SampleBuffer other, double gain = 1, double start = 0)
    {
        if (other.SampleRate != SampleRate) throw new ArgumentException("Sample rates differ.", nameof(other));
        var offset = (int)(start * SampleRate);
        for (var i = 0; i < other.Length; i++)
        {
            var j = offset + i;
            if (j < 0 || j >= _data.Length) continue;
            _data[j] += (float)(other._data[i] * gain);
        }
    }

    /// <summary>Gentle saturation + normalization so nothing clips.</summary>
    public void Master(double peak = 0.89)
    {
        var max = 0f;
        foreach (var s in _data) max = Math.Max(max, Math.Abs(s));
        if (max <= 0) return;

        // soft-knee saturation only on the loudest parts
        var pre = 1.0 / max;
        for (var i = 0; i < _data.Length; i++)
        {
            var x = _data[i] * pre;
            _data[i] = (float)Math.Tanh(x * 1.2) ;
        }

        max = 0f;
        foreach (var s in _data) max = Math.Max(max, Math.Abs(s));
        if (max <= 0) return;
        var g = (float)(peak / max);
        for (var i = 0; i < _data.Length; i++) _data[i] *= g;
    }

    /// <summary>Short fades at the ends to avoid clicks on one-shot sounds.</summary>
    public void Declick(double seconds = 0.003)
    {
        var n = Math.Min(_data.Length / 2, (int)(seconds * SampleRate));
        for (var i = 0; i < n; i++)
        {
            var g = (float)i / n;
            _data[i] *= g;
            _data[_data.Length - 1 - i] *= g;
        }
    }

    /// <summary>Encodes as a 16-bit PCM mono RIFF/WAVE file.</summary>
    public byte[] ToWav()
    {
        var dataBytes = _data.Length * 2;
        using var ms = new MemoryStream(44 + dataBytes);
        using var w = new BinaryWriter(ms);
        w.Write("RIFF"u8);
        w.Write(36 + dataBytes);
        w.Write("WAVE"u8);
        w.Write("fmt "u8);
        w.Write(16);
        w.Write((short)1); // PCM
        w.Write((short)1); // mono
        w.Write(SampleRate);
        w.Write(SampleRate * 2);
        w.Write((short)2);
        w.Write((short)16);
        w.Write("data"u8);
        w.Write(dataBytes);
        foreach (var s in _data)
            w.Write((short)Math.Round(Math.Clamp(s, -1f, 1f) * 32767));
        w.Flush();
        return ms.ToArray();
    }
}

/// <summary>Note-name helpers.</summary>
public static class Notes
{
    private static readonly Dictionary<string, int> Semis = new(StringComparer.OrdinalIgnoreCase)
    {
        ["C"] = 0, ["C#"] = 1, ["Db"] = 1, ["D"] = 2, ["D#"] = 3, ["Eb"] = 3, ["E"] = 4,
        ["F"] = 5, ["F#"] = 6, ["Gb"] = 6, ["G"] = 7, ["G#"] = 8, ["Ab"] = 8, ["A"] = 9,
        ["A#"] = 10, ["Bb"] = 10, ["B"] = 11,
    };

    /// <summary>Parses a note like "C#5" into a MIDI number (C4 = 60).</summary>
    public static int Midi(string name)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Length < 2)
            throw new FormatException($"Bad note '{name}'.");
        var octStart = name.Length - 1;
        while (octStart > 0 && (char.IsDigit(name[octStart - 1]) || name[octStart - 1] == '-')) octStart--;
        var pitch = name[..octStart];
        if (!Semis.TryGetValue(pitch, out var semi) || !int.TryParse(name[octStart..], out var octave))
            throw new FormatException($"Bad note '{name}'.");
        return (octave + 1) * 12 + semi;
    }

    public static double Freq(int midi) => 440.0 * Math.Pow(2, (midi - 69) / 12.0);

    public static double Freq(string name) => Freq(Midi(name));
}
