using Srui;
using Srui.Audio;

namespace XGRhythm;

/// <summary>The music clock and beat math. The playing track's
/// PlaybackPosition is authoritative but integer-ms and quantized near
/// device-period resolution, so the conductor anchors it against
/// SruiApp.PreciseNow and serves smooth music time between device
/// updates: real jumps re-anchor hard, small drift bleeds away
/// gradually. While paused the clock is frozen at the pause point.</summary>
public sealed class Conductor(SruiApp app, Sound music, double bpm, double beatOneOffsetMs)
{
    private const double SnapThresholdMs = 15.0;
    private const double DriftGain = 0.05;

    private double _anchorMusicMs;
    private TimeSpan _anchorAt;
    private bool _paused;
    private double _pausedMusicMs;

    public void Start()
    {
        music.Play();
        Anchor();
    }

    /// <summary>Music time now, in ms.</summary>
    public double NowMs => _paused
        ? _pausedMusicMs
        : _anchorMusicMs + (app.PreciseNow - _anchorAt).TotalMilliseconds;

    /// <summary>Reconcile the estimate with the device cursor; called
    /// once per loop tick.</summary>
    public void Sample()
    {
        if (_paused)
            return;
        var diff = music.PlaybackPosition - NowMs;
        if (Math.Abs(diff) > SnapThresholdMs)
            Anchor();
        else
            _anchorMusicMs += diff * DriftGain;
    }

    public void Pause()
    {
        if (_paused)
            return;
        _pausedMusicMs = NowMs;
        music.Pause();
        _paused = true;
    }

    public void Resume()
    {
        if (!_paused)
            return;
        music.Play();
        Anchor();
        _paused = false;
    }

    public bool Ended => music.AtEnd;

    public double BeatToMs(double beat) => beatOneOffsetMs + beat * 60000.0 / bpm;

    private void Anchor()
    {
        _anchorMusicMs = music.PlaybackPosition;
        _anchorAt = app.PreciseNow;
    }
}
