namespace XGRhythm;

/// <summary>A playable level: music at a fixed tempo plus its cues.
/// Levels are C# classes, one file each under Levels\, registered in
/// <see cref="LevelRegistry.All"/>. Tracks are produced at perfect tempo,
/// so a single BPM and beat-one offset suffice.</summary>
public abstract class Level
{
    public abstract string Title { get; }

    /// <summary>Music file name inside the asset folder.</summary>
    public abstract string MusicFile { get; }

    public abstract double Bpm { get; }

    /// <summary>Where bar 1 beat 1 sits in the music file, in ms.</summary>
    public abstract double BeatOneOffsetMs { get; }

    public abstract IReadOnlyList<Cue> BuildCues();
}

/// <summary>Every level the levels list offers, in menu order.</summary>
public static class LevelRegistry
{
    public static IReadOnlyList<Level> All { get; } = [new Levels.TestLevel()];
}
