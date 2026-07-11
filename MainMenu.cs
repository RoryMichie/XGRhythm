using Srui;
using Srui.Audio;

namespace XGRhythm;

/// <summary>A level in the levels list.</summary>
public sealed class LevelItem(Level level) : IListItem
{
    public Level Level { get; } = level;

    public string Text => Level.Title;
}

/// <summary>A buffer-size choice; the millisecond figure follows the
/// live device sample rate.</summary>
public sealed class PeriodItem(SruiApp app, uint frames) : IListItem
{
    public uint Frames { get; } = frames;

    public string Text =>
        $"{Frames} frames, {Frames * 1000.0 / app.AudioSampleRate:0.#} milliseconds";
}

/// <summary>The main screen: the levels list (the layer's primary, so
/// Enter anywhere starts the selected level) and the live settings.
/// Settings apply as the selection or value moves; Enter on them is a
/// genuine no-op.</summary>
public sealed class MainMenu
{
    private static readonly (string Name, int Ms)[] Tolerances =
    [
        ("Beginner", 300),
        ("Easy", 200),
        ("Moderate", 100),
        ("Hard", 50),
        ("Expert", 25),
        ("Insane", 10),
    ];

    private static readonly uint[] Periods = [128, 256, 512, 1024];

    private readonly SruiApp _app;
    private readonly Settings _settings;
    private readonly Earcons _earcons;
    private readonly Sound _menuMusic;
    private readonly ListBox<LevelItem> _levels;

    public MainMenu(SruiApp app, Settings settings, Earcons earcons)
    {
        _app = app;
        _settings = settings;
        _earcons = earcons;

        _ = new Label(app, "XGRhythm");
        _levels = new ListBox<LevelItem>(
            app, "Levels",
            LevelRegistry.All.Select(l => new LevelItem(l)).ToList(), numbered: true);
        var musicVolume = new Slider(app, "Music volume", settings.MusicVolume, 0, 100, unit: "%");
        var masterVolume = new Slider(
            app, "Master volume", settings.MasterVolume, 0, 100, unit: "%");
        var tolerance = new ListBox(
            app, "Tolerance window",
            Tolerances.Select(t => $"{t.Name}, {t.Ms} milliseconds").ToList());
        var buffer = new ListBox<PeriodItem>(
            app, "Audio engine buffer size",
            Periods.Select(p => new PeriodItem(app, p)).ToList());
        var calibrate = new Button(app, "Calibrate real input latency");

        _levels.AddShortcut(KeyCombo.WithAlt(Key.Char('l')));
        musicVolume.AddShortcut(KeyCombo.WithAlt(Key.Char('u')));
        masterVolume.AddShortcut(KeyCombo.WithAlt(Key.Char('m')));
        tolerance.AddShortcut(KeyCombo.WithAlt(Key.Char('t')));
        buffer.AddShortcut(KeyCombo.WithAlt(Key.Char('b')));
        calibrate.AddShortcut(KeyCombo.WithAlt(Key.Char('c')), ShortcutAction.Activate);

        var toleranceIndex = Array.FindIndex(Tolerances, t => t.Ms == settings.ToleranceMs);
        tolerance.SelectedIndex = toleranceIndex >= 0 ? toleranceIndex : 2; // Moderate
        var periodIndex = Array.IndexOf(Periods, settings.PeriodFrames);
        buffer.SelectedIndex = periodIndex >= 0 ? periodIndex : 0;

        app.SetPrimary(_levels);
        _levels.Activated += StartSelected;
        musicVolume.Changed += () =>
        {
            settings.MusicVolume = musicVolume.Value;
            earcons.Music.Volume = musicVolume.Value / 100f;
            earcons.Move(musicVolume.Value / 100f); // preview at the chosen volume
            settings.Save();
        };
        masterVolume.Changed += () =>
        {
            settings.MasterVolume = masterVolume.Value;
            earcons.Master.Volume = masterVolume.Value / 100f;
            earcons.Move(); // the master bus itself scales the preview
            settings.Save();
        };
        tolerance.Changed += () =>
        {
            settings.ToleranceMs = Tolerances[tolerance.SelectedIndex].Ms;
            settings.Save();
        };
        buffer.Changed += () =>
        {
            var requested = Periods[buffer.SelectedIndex];
            app.AudioPeriodFrames = requested;
            settings.PeriodFrames = requested;
            settings.Save();
            var granted = app.AudioPeriodFrames;
            if (granted != requested)
                app.Announce($"Device granted {granted} frames.");
        };
        calibrate.Activated += () =>
        {
            earcons.Enter();
            Calibration.Open(app, earcons, settings);
        };

        _menuMusic = app.Audio.CreateSound(earcons.Music);
        _menuMusic.Load(Earcons.AssetPath("menumusic.ogg"));
        _menuMusic.Looping = true;
        _menuMusic.Play();
    }

    private void StartSelected()
    {
        if (_levels.SelectedItem is not LevelItem item)
            return;
        if (item.Level is BrokenLevel broken)
        {
            _earcons.Wrong();
            _app.Announce($"Can't play: {broken.Error}");
            return;
        }
        _earcons.Enter();
        _menuMusic.Pause();
        _ = new GameScreen(_app, item.Level, _earcons, _settings, closed: _menuMusic.Play);
    }
}
