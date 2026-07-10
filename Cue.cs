using Srui;

namespace XGRhythm;

/// <summary>A sound the cue plays on its own — the call side of call and
/// response. Offsets are in beats from the cue's anchor.</summary>
public sealed record CueCall(double OffsetBeats, string SoundFile);

/// <summary>A response the player owes: a key at a time, offset in beats
/// from the cue's anchor. The ambient sound plays at the scheduled time
/// whether or not the player succeeds; the correct and wrong sounds
/// accompany the judgment, on top of the game-wide feedback.</summary>
public sealed record CueHit(
    double OffsetBeats,
    Key Key,
    string? AmbientFile = null,
    string? CorrectFile = null,
    string? WrongFile = null);

/// <summary>A rhythmic exchange anchored at a bar (1-based), beat (1 to 4
/// in 4/4), and percentage through that beat. Calls and hits interleave
/// freely — a call may follow a response that has already passed.
/// KeySoundFile is the sound the player's own presses make when a press
/// is judged as belonging to this cue. Completing every hit correctly
/// earns the game-wide ding; one miss anywhere forfeits it.</summary>
public sealed class Cue
{
    public required int Bar { get; init; }
    public int Beat { get; init; } = 1;
    public double Percent { get; init; }
    public string? KeySoundFile { get; init; }
    public IReadOnlyList<CueCall> Calls { get; init; } = [];
    public required IReadOnlyList<CueHit> Hits { get; init; }

    /// <summary>The anchor as a beat index counted from bar 1 beat 1.</summary>
    public double AnchorBeat => (Bar - 1) * 4 + (Beat - 1) + Percent / 100.0;
}
