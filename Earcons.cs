using Srui;
using Srui.Audio;

namespace XGRhythm;

/// <summary>The mixing buses and the shared UI sounds. Music and SFX
/// nest under one master group, so the master slider scales everything
/// and the music slider scales only music. Each earcon is one Sound,
/// retriggered with Stop+Play; overlapping gameplay sounds use a
/// <see cref="SoundBank"/> instead.</summary>
public sealed class Earcons
{
    public SoundGroup Master { get; }
    public SoundGroup Music { get; }
    public SoundGroup Sfx { get; }

    private readonly Sound _move;
    private readonly Sound _enter;
    private readonly Sound _correct;
    private readonly Sound _wrong;

    public Earcons(SoundManager audio)
    {
        Master = audio.CreateGroup();
        Music = audio.CreateGroup(Master);
        Sfx = audio.CreateGroup(Master);
        _move = Create(audio, "menumove.ogg");
        _enter = Create(audio, "menuenter.ogg");
        _correct = Create(audio, "correct.ogg");
        _wrong = Create(audio, "wrong.ogg");
    }

    private Sound Create(SoundManager audio, string file)
    {
        var sound = audio.CreateSound(Sfx);
        sound.Load(AssetPath(file));
        return sound;
    }

    public static string AssetPath(string file) =>
        Path.Combine(AppContext.BaseDirectory, "asset", file);

    /// <summary>The navigation sound, optionally at a preview volume
    /// (0..1) — how the volume sliders demonstrate the value being
    /// chosen.</summary>
    public void Move(float volume = 1f)
    {
        _move.BaseVolume = volume;
        Retrigger(_move);
    }

    public void Enter() => Retrigger(_enter);

    /// <summary>The cue-completion ding; doubles as the calibration
    /// metronome.</summary>
    public void Correct() => Retrigger(_correct);

    /// <summary>The failure sound, pitch-coded: above 1 for an early
    /// press, below 1 for a late one, 1 for a plain miss.</summary>
    public void Wrong(float pitch = 1f)
    {
        _wrong.Pitch = pitch;
        Retrigger(_wrong);
    }

    /// <summary>Silence the judgment sounds mid-ring — pausing or
    /// leaving a level shouldn't carry a wrong into the next screen.</summary>
    public void HushFeedback()
    {
        _wrong.Stop();
        _correct.Stop();
    }

    private static void Retrigger(Sound sound)
    {
        sound.Stop();
        sound.Play();
    }
}

/// <summary>Plays the navigation sound for every perceived movement the
/// widgets announce: focus moves and list selection changes. Boundary
/// bumps stay silent — nothing moved — and sliders play from their
/// Changed handlers instead, at the volume they denote.</summary>
public sealed class EarconReader(Earcons earcons) : IReader
{
    public void OnEvent(AccessibilityEvent e)
    {
        switch (e)
        {
            case AccessibilityEvent.Focused:
            case AccessibilityEvent.ItemNav { BoundaryHit: null }:
                earcons.Move();
                break;
        }
    }
}

/// <summary>Pooled one-shot voices for gameplay sounds: a few Sound
/// instances per file, round-robin, so overlapping cues reusing a file
/// don't cut each other off. The engine caches decoded data, so the
/// extra voices cost no decoding. Playback is allocation-free.</summary>
public sealed class SoundBank(SoundManager audio, SoundGroup bus) : IDisposable
{
    private const int VoicesPerFile = 4;

    private sealed class Voices
    {
        public required Sound[] Sounds { get; init; }
        public int Next;
    }

    private readonly Dictionary<string, Voices> _pool = new();

    public void Preload(string file)
    {
        if (_pool.ContainsKey(file))
            return;
        var sounds = new Sound[VoicesPerFile];
        for (var i = 0; i < sounds.Length; i++)
        {
            sounds[i] = audio.CreateSound(bus);
            sounds[i].Load(Earcons.AssetPath(file));
        }
        _pool[file] = new Voices { Sounds = sounds };
    }

    public void Play(string file)
    {
        if (!_pool.TryGetValue(file, out var voices))
        {
            Preload(file);
            voices = _pool[file];
        }
        var sound = voices.Sounds[voices.Next];
        voices.Next = (voices.Next + 1) % voices.Sounds.Length;
        sound.Stop();
        sound.Play();
    }

    /// <summary>Stop every playing voice — pausing or leaving a level.</summary>
    public void StopAll()
    {
        foreach (var voices in _pool.Values)
            foreach (var sound in voices.Sounds)
                sound.Stop();
    }

    public void Dispose()
    {
        foreach (var voices in _pool.Values)
            foreach (var sound in voices.Sounds)
                sound.Dispose();
        _pool.Clear();
    }
}
