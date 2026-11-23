using PooSweep.Core.Audio;

namespace PooSweep.Tests;

public sealed class AudioTests
{
    [Theory]
    [InlineData("A4", 69)]
    [InlineData("C4", 60)]
    [InlineData("C#5", 73)]
    [InlineData("Bb3", 58)]
    public void Notes_ParseMidi(string name, int midi) => Assert.Equal(midi, Notes.Midi(name));

    [Fact]
    public void Notes_A4_Is440() => Assert.Equal(440.0, Notes.Freq("A4"), 6);

    [Fact]
    public void Chords_ParseMinorSeventh()
    {
        var (root, iv) = Chords.Parse("G7");
        Assert.Equal(7, root);
        Assert.Equal([0, 4, 7, 10], iv);

        var (root2, iv2) = Chords.Parse("F#m");
        Assert.Equal(6, root2);
        Assert.Equal([0, 3, 7], iv2);
    }

    [Theory]
    [MemberData(nameof(AllSfx))]
    public void EverySoundEffect_RendersAudibleUnclippedAudio(Sfx sfx)
    {
        var buffer = SoundEffects.Render(sfx);
        AssertHealthy(buffer, maxSeconds: 6);
    }

    public static TheoryData<Sfx> AllSfx()
    {
        var d = new TheoryData<Sfx>();
        foreach (var s in Enum.GetValues<Sfx>()) d.Add(s);
        return d;
    }

    [Theory]
    [InlineData(MusicTrack.Title, 16, 148)]
    [InlineData(MusicTrack.Game, 32, 116)]
    public void MusicLoops_HaveExactBarLength(MusicTrack track, int bars, double bpm)
    {
        var buffer = Music.Render(track);
        var expected = bars * 4 * 60.0 / bpm;
        Assert.InRange(buffer.Seconds, expected - 0.01, expected + 0.01);
        AssertHealthy(buffer, maxSeconds: 120);
    }

    [Fact]
    public void Wav_HasValidHeader()
    {
        var b = new SampleBuffer(8000, 0.1);
        b.Add(new Tone { Freq = 440, Duration = 0.05 }, 0);
        var wav = b.ToWav();

        Assert.Equal("RIFF", System.Text.Encoding.ASCII.GetString(wav, 0, 4));
        Assert.Equal("WAVE", System.Text.Encoding.ASCII.GetString(wav, 8, 4));
        Assert.Equal(8000, BitConverter.ToInt32(wav, 24));
        Assert.Equal(b.Length * 2, BitConverter.ToInt32(wav, 40));
        Assert.Equal(44 + b.Length * 2, wav.Length);
    }

    private static void AssertHealthy(SampleBuffer buffer, double maxSeconds)
    {
        Assert.InRange(buffer.Seconds, 0.01, maxSeconds);
        var peak = buffer.Data.Max(Math.Abs);
        Assert.InRange(peak, 0.2f, 1.0f);
        Assert.DoesNotContain(buffer.Data, s => float.IsNaN(s) || float.IsInfinity(s));
    }
}
