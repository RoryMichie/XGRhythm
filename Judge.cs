using Srui;

namespace XGRhythm;

/// <summary>Running (and final) results for one level attempt.</summary>
public sealed class LevelStats
{
    public int Correct;
    public int Missed;
    public int Strays;
    public int CuesCompleted;
    public int CuesTotal;
    public int HitsTotal;
    private double _absErrorSumMs;

    public void RecordCorrect(double absErrorMs)
    {
        Correct++;
        _absErrorSumMs += absErrorMs;
    }

    public double MeanAbsErrorMs => Correct == 0 ? 0.0 : _absErrorSumMs / Correct;

    public string Describe() =>
        $"Correct hits: {Correct} of {HitsTotal}. Misses: {Missed}. Stray presses: {Strays}.\n"
        + $"Cues completed: {CuesCompleted} of {CuesTotal}.\n"
        + $"Mean absolute timing error: {MeanAbsErrorMs:0.#} milliseconds.";
}

/// <summary>Schedules a level's sounds and judges its presses. With
/// tolerance t, a hit expected at time e is claimable in [e-t, e+t]; a
/// press claims the nearest pending hit on its key across all active
/// cues, a press matching no open window is a stray (wrong.ogg, nothing
/// consumed), and a window that closes unclaimed is a miss (wrong.ogg
/// plus the hit's wrong sound, cue ding forfeited). Presses arrive
/// already on the music clock; the calibrated input latency is
/// subtracted here, and window closes lag by the same offset so a
/// late-arriving corrected press never targets a closed window.</summary>
public sealed class Judge
{
    private sealed class CueState
    {
        public required Cue Cue { get; init; }
        public int Remaining;
        public bool Spoiled;
    }

    private readonly record struct ScheduledCall(double TimeMs, string File);

    private sealed class TrackedHit
    {
        public required CueState Cue { get; init; }
        public required CueHit Hit { get; init; }
        public required double TimeMs { get; init; }
        public bool Judged;
    }

    private readonly SoundBank _bank;
    private readonly Earcons _earcons;
    private readonly double _toleranceMs;
    private readonly double _latencyMs;

    private readonly ScheduledCall[] _calls; // sorted by time
    private readonly TrackedHit[] _hits;     // sorted by time
    private int _callCursor;
    private int _ambientCursor;
    private int _closeCursor;

    public LevelStats Stats { get; } = new();

    public Judge(
        IReadOnlyList<Cue> cues, Conductor conductor, SoundBank bank, Earcons earcons,
        double toleranceMs, double latencyMs)
    {
        _bank = bank;
        _earcons = earcons;
        _toleranceMs = toleranceMs;
        _latencyMs = latencyMs;

        var calls = new List<ScheduledCall>();
        var hits = new List<TrackedHit>();
        foreach (var cue in cues)
        {
            var state = new CueState { Cue = cue, Remaining = cue.Hits.Count };
            foreach (var call in cue.Calls)
                calls.Add(new ScheduledCall(
                    conductor.BeatToMs(cue.AnchorBeat + call.OffsetBeats), call.SoundFile));
            foreach (var hit in cue.Hits)
                hits.Add(new TrackedHit
                {
                    Cue = state,
                    Hit = hit,
                    TimeMs = conductor.BeatToMs(cue.AnchorBeat + hit.OffsetBeats),
                });
        }
        calls.Sort((a, b) => a.TimeMs.CompareTo(b.TimeMs));
        hits.Sort((a, b) => a.TimeMs.CompareTo(b.TimeMs));
        _calls = calls.ToArray();
        _hits = hits.ToArray();
        Stats.CuesTotal = cues.Count;
        Stats.HitsTotal = hits.Count;
    }

    /// <summary>Advance to the current music time: fire due calls and
    /// ambients, close overdue windows as misses.</summary>
    public void Tick(double nowMs)
    {
        while (_callCursor < _calls.Length && _calls[_callCursor].TimeMs <= nowMs)
            _bank.Play(_calls[_callCursor++].File);
        while (_ambientCursor < _hits.Length && _hits[_ambientCursor].TimeMs <= nowMs)
        {
            if (_hits[_ambientCursor++].Hit.AmbientFile is string ambient)
                _bank.Play(ambient);
        }
        // Judged against latency-corrected press times, so windows close
        // on the corrected clock too.
        while (_closeCursor < _hits.Length
            && _hits[_closeCursor].TimeMs + _toleranceMs + _latencyMs < nowMs)
        {
            var hit = _hits[_closeCursor++];
            if (!hit.Judged)
                Miss(hit);
        }
    }

    public void OnPress(Key key, double pressMusicMs)
    {
        var t = pressMusicMs - _latencyMs;
        TrackedHit? best = null;
        var bestError = double.MaxValue;
        for (var i = _closeCursor; i < _hits.Length; i++)
        {
            var hit = _hits[i];
            if (hit.TimeMs - _toleranceMs > t)
                break; // sorted: everything later opens later still
            if (hit.Judged || hit.Hit.Key != key)
                continue;
            var error = Math.Abs(t - hit.TimeMs);
            if (error <= _toleranceMs && error < bestError)
            {
                best = hit;
                bestError = error;
            }
        }
        if (best is null)
        {
            Stats.Strays++;
            _earcons.Wrong();
            return;
        }
        best.Judged = true;
        best.Cue.Remaining--;
        Stats.RecordCorrect(bestError);
        if (best.Cue.Cue.KeySoundFile is string keySound)
            _bank.Play(keySound);
        if (best.Hit.CorrectFile is string correct)
            _bank.Play(correct);
        if (best.Cue.Remaining == 0 && !best.Cue.Spoiled)
        {
            Stats.CuesCompleted++;
            _earcons.Correct();
        }
    }

    private void Miss(TrackedHit hit)
    {
        hit.Judged = true;
        hit.Cue.Remaining--;
        hit.Cue.Spoiled = true;
        Stats.Missed++;
        _earcons.Wrong();
        if (hit.Hit.WrongFile is string wrong)
            _bank.Play(wrong);
    }
}
