using Srui;
using Srui.Audio;

namespace XGRhythm;

/// <summary>A running level: a modal layer holding the play-field, the
/// music, the conductor, and the judge. Escape (or the window losing
/// focus) pauses; the track ending shows the results and returns to the
/// menu, restoring focus to the levels list.</summary>
public sealed class GameScreen
{
    private readonly SruiApp _app;
    private readonly Earcons _earcons;
    private readonly Level _level;
    private readonly Dialog _dialog;
    private readonly Sound _music;
    private readonly SoundBank _bank;
    private readonly Conductor _conductor;
    private readonly Judge _judge;
    private readonly Ticker _ticker;
    private bool _paused;
    private bool _finished;

    public GameScreen(SruiApp app, Level level, Earcons earcons, Settings settings, Action closed)
    {
        _app = app;
        _earcons = earcons;
        _level = level;
        _dialog = app.OpenDialog();
        _ = new Label(_dialog, $"Playing {level.Title}.");
        var field = new PlayField(_dialog, level.Title, RequestPause);
        field.Description = "Answer the calls. Escape pauses.";

        _music = app.Audio.CreateSound(earcons.Music);
        _music.Load(Earcons.AssetPath(level.MusicFile));
        _bank = new SoundBank(app.Audio, earcons.Sfx);
        var cues = level.BuildCues();
        PreloadCueSounds(cues);
        _conductor = new Conductor(app, _music, level.Bpm, level.BeatOneOffsetMs);
        _judge = new Judge(
            cues, _conductor, _bank, earcons, settings.ToleranceMs, settings.InputLatencyMs);

        foreach (var key in cues.SelectMany(c => c.Hits).Select(h => h.Key).Distinct())
        {
            var bound = key;
            field.BindKey(KeyCombo.Plain(bound), KeyPhase.Press, () =>
            {
                if (!_paused && !_finished)
                    _judge.OnPress(bound, _conductor.NowMs);
            });
        }

        _ticker = app.StartTicker(1);
        _ticker.Tick += OnTick;
        app.FocusLost = RequestPause; // pausing blind on focus loss beats missing cues
        _dialog.Closed += () =>
        {
            _ticker.Stop();
            app.FocusLost = null;
            earcons.HushFeedback();
            _music.Dispose();
            _bank.Dispose();
            closed();
        };
        _dialog.AnnounceOpened();
        _conductor.Start();
    }

    private void PreloadCueSounds(IReadOnlyList<Cue> cues)
    {
        foreach (var cue in cues)
        {
            if (cue.KeySoundFile is string keySound)
                _bank.Preload(keySound);
            foreach (var call in cue.Calls)
                _bank.Preload(call.SoundFile);
            foreach (var hit in cue.Hits)
            {
                if (hit.AmbientFile is string ambient)
                    _bank.Preload(ambient);
                if (hit.CorrectFile is string correct)
                    _bank.Preload(correct);
                if (hit.WrongFile is string wrong)
                    _bank.Preload(wrong);
            }
        }
    }

    private void OnTick()
    {
        if (_paused || _finished)
            return;
        _conductor.Sample();
        _judge.Tick(_conductor.NowMs);
        if (_conductor.Ended)
            Finish();
    }

    private void RequestPause()
    {
        if (_paused || _finished)
            return;
        _paused = true;
        _conductor.Pause();
        _bank.StopAll();
        _earcons.HushFeedback();

        var pause = _app.OpenDialog();
        _ = new Label(pause, $"Paused. {_judge.Stats.Describe()}");
        var resume = new Button(pause, "Resume");
        var quit = new Button(pause, "Quit level");
        resume.AddShortcut(KeyCombo.WithAlt(Key.Char('r')), ShortcutAction.Activate);
        quit.AddShortcut(KeyCombo.WithAlt(Key.Char('q')), ShortcutAction.Activate);
        resume.Activated += pause.Close;
        quit.Activated += _dialog.Close; // closes the pause layer with it
        pause.Closed += () =>
        {
            // Absent when quitting: the whole screen is going away.
            if (_dialog.IsOpen)
            {
                _paused = false;
                _conductor.Resume();
            }
        };
        _app.SetPrimary(resume);
        _app.SetCancel(resume);
        pause.AnnounceOpened();
    }

    private void Finish()
    {
        _finished = true;
        _ticker.Stop();
        var results = _app.ShowStatus($"{_level.Title} complete", _judge.Stats.Describe());
        results.Closed += _dialog.Close;
    }

    /// <summary>The gameplay surface: role-less, no built-in behavior;
    /// cue keys attach via BindKey and Escape asks for the pause screen.</summary>
    private sealed class PlayField(IWidgetContainer parent, string name, Action pauseRequested)
        : Widget(parent, name)
    {
        protected override bool OnInput(in InputEvent input)
        {
            if (input.Kind == InputKind.Dismiss)
            {
                Post(pauseRequested);
                return true;
            }
            return false;
        }
    }
}
