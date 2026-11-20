using System.Diagnostics;
using Plugin.Maui.Audio;
using PooSweep.Core.Audio;

namespace PooSweep.Services;

/// <summary>
/// Synthesizes all music and sound effects at start-up (in the background) and plays them.
/// Music crossfades between tracks and ducks under the win/lose jingles.
/// </summary>
public sealed class AudioService
{
    private readonly IAudioManager _manager;
    private readonly Settings _settings;
    private readonly Dictionary<Sfx, IAudioPlayer[]> _sfx = new();
    private readonly Dictionary<Sfx, int> _nextVoice = new();
    private readonly Dictionary<MusicTrack, IAudioPlayer> _music = new();
    private readonly Dictionary<MusicTrack, byte[]> _musicData = new();
    private readonly Random _rng = new();

    private MusicTrack? _wantedTrack;
    private MusicTrack? _currentTrack;
    private double _musicGain = 1;   // fade multiplier 0..1
    private double _duck = 1;        // jingle ducking multiplier
    private IDispatcherTimer? _fadeTimer;
    private bool _suspended;
    private Task? _ready;

    public AudioService(IAudioManager manager, Settings settings)
    {
        _manager = manager;
        _settings = settings;
        _settings.Changed += (_, _) => ApplyMusicVolume();
    }

    /// <summary>Kicks off synthesis. Safe to call more than once.</summary>
    public Task InitializeAsync() => _ready ??= InitializeCoreAsync();

    private async Task InitializeCoreAsync()
    {
        try
        {
            // title music first so the menu has a tune ASAP
            var title = await Task.Run(() => Music.Render(MusicTrack.Title).ToWav());
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                _musicData[MusicTrack.Title] = title;
                if (_wantedTrack == MusicTrack.Title) StartTrack(MusicTrack.Title);
            });

            var sfx = await Task.Run(() => Enum.GetValues<Sfx>().ToDictionary(s => s, s => SoundEffects.Render(s).ToWav()));
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                foreach (var (id, wav) in sfx)
                {
                    var voices = id is Sfx.Dig1 or Sfx.Dig2 or Sfx.Dig3 or Sfx.Flag or Sfx.UiClick ? 2 : 1;
                    _sfx[id] = Enumerable.Range(0, voices).Select(_ => _manager.CreatePlayer(new MemoryStream(wav))).ToArray();
                }
            });

            var game = await Task.Run(() => Music.Render(MusicTrack.Game).ToWav());
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                _musicData[MusicTrack.Game] = game;
                if (_wantedTrack == MusicTrack.Game) StartTrack(MusicTrack.Game);
            });
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[Audio] init failed: {ex}");
        }
    }

    private double SfxVolume => _settings.Muted ? 0 : _settings.SfxVolume;
    private double MusicVolume => _settings.Muted ? 0 : _settings.MusicVolume;

    // ------------------------------------------------------------------
    //  Sound effects
    // ------------------------------------------------------------------

    public void Play(Sfx sfx, double gain = 1)
    {
        var vol = SfxVolume * gain;
        if (vol <= 0.001 || _suspended) return;
        if (!_sfx.TryGetValue(sfx, out var voices)) return;

        try
        {
            var idx = _nextVoice.GetValueOrDefault(sfx);
            _nextVoice[sfx] = (idx + 1) % voices.Length;
            var p = voices[idx];
            if (p.IsPlaying) p.Stop();
            p.Volume = Math.Clamp(vol, 0, 1);
            p.Play();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[Audio] sfx {sfx} failed: {ex.Message}");
        }
    }

    public void PlayDig() => Play(_rng.Next(3) switch { 0 => Sfx.Dig1, 1 => Sfx.Dig2, _ => Sfx.Dig3 });

    /// <summary>Plays a jingle while ducking the music, then brings the music back.</summary>
    public void PlayJingle(Sfx jingle, TimeSpan holdMusic)
    {
        Play(jingle);
        _duck = 0.0;
        ApplyMusicVolume();
        var token = ++_duckToken;
        Application.Current?.Dispatcher.DispatchDelayed(holdMusic, () =>
        {
            if (token != _duckToken) return;
            FadeDuck(1);
        });
    }

    private int _duckToken;

    // ------------------------------------------------------------------
    //  Music
    // ------------------------------------------------------------------

    public void PlayMusic(MusicTrack track)
    {
        _wantedTrack = track;
        _duckToken++;
        _duck = 1;
        if (_currentTrack == track && _music.TryGetValue(track, out var existing) && existing.IsPlaying)
        {
            ApplyMusicVolume();
            return;
        }
        if (_musicData.ContainsKey(track)) StartTrack(track);
    }

    public void StopMusic()
    {
        _wantedTrack = null;
        FadeTo(0, () =>
        {
            foreach (var p in _music.Values) if (p.IsPlaying) p.Pause();
            _currentTrack = null;
        });
    }

    private void StartTrack(MusicTrack track)
    {
        if (_suspended) { _currentTrack = null; return; }

        void Begin()
        {
            try
            {
                foreach (var (id, other) in _music)
                    if (id != track && other.IsPlaying) other.Pause();

                if (!_music.TryGetValue(track, out var player))
                {
                    player = _manager.CreatePlayer(new MemoryStream(_musicData[track]));
                    player.Loop = true;
                    _music[track] = player;
                }

                _currentTrack = track;
                _musicGain = 0;
                ApplyMusicVolume();
                player.Seek(0);
                player.Play();
                FadeTo(1, null);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Audio] music {track} failed: {ex.Message}");
            }
        }

        if (_currentTrack is not null && _currentTrack != track && _musicGain > 0.05)
            FadeTo(0, Begin, 0.35);
        else
            Begin();
    }

    private void ApplyMusicVolume()
    {
        if (_currentTrack is { } t && _music.TryGetValue(t, out var p))
        {
            try { p.Volume = Math.Clamp(MusicVolume * _musicGain * _duck * 0.7, 0, 1); }
            catch { /* player may be disposed during shutdown */ }
        }
    }

    private void FadeTo(double target, Action? done, double seconds = 0.6)
    {
        _fadeTimer?.Stop();
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is null) { _musicGain = target; ApplyMusicVolume(); done?.Invoke(); return; }

        var start = _musicGain;
        var sw = Stopwatch.StartNew();
        _fadeTimer = dispatcher.CreateTimer();
        _fadeTimer.Interval = TimeSpan.FromMilliseconds(30);
        _fadeTimer.Tick += (s, _) =>
        {
            var k = Math.Min(1, sw.Elapsed.TotalSeconds / seconds);
            _musicGain = start + (target - start) * k;
            ApplyMusicVolume();
            if (k >= 1)
            {
                ((IDispatcherTimer)s!).Stop();
                done?.Invoke();
            }
        };
        _fadeTimer.Start();
    }

    private void FadeDuck(double target)
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is null) { _duck = target; ApplyMusicVolume(); return; }
        var start = _duck;
        var sw = Stopwatch.StartNew();
        var timer = dispatcher.CreateTimer();
        timer.Interval = TimeSpan.FromMilliseconds(40);
        timer.Tick += (_, _) =>
        {
            var k = Math.Min(1, sw.Elapsed.TotalSeconds / 1.5);
            _duck = start + (target - start) * k;
            ApplyMusicVolume();
            if (k >= 1) timer.Stop();
        };
        timer.Start();
    }

    // ------------------------------------------------------------------
    //  App lifecycle
    // ------------------------------------------------------------------

    public void Suspend()
    {
        _suspended = true;
        foreach (var p in _music.Values)
        {
            try { if (p.IsPlaying) p.Pause(); } catch { }
        }
    }

    public void Resume()
    {
        if (!_suspended) return; // Activated fires on every focus change on desktop
        _suspended = false;
        if (_wantedTrack is { } t && _music.TryGetValue(t, out var p))
        {
            try
            {
                _currentTrack = t;
                ApplyMusicVolume();
                p.Play();
            }
            catch { }
        }
        else if (_wantedTrack is { } w && _musicData.ContainsKey(w))
        {
            StartTrack(w);
        }
    }
}
