namespace XGRhythm;

/// <summary>A playable level: music at a fixed tempo plus its cues.
/// Levels are CSV files in the levels folder beside the executable
/// (parsed by <see cref="CsvLevel"/>), discovered at startup by
/// <see cref="LevelRegistry"/>. Tracks are produced at perfect tempo,
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

/// <summary>Every level the levels list offers: the levels folder's CSV
/// files in name order. A file that fails to parse appears as a
/// <see cref="BrokenLevel"/> carrying its error.</summary>
public static class LevelRegistry
{
    public static IReadOnlyList<Level> All { get; } = Load();

    private static List<Level> Load()
    {
        var levels = new List<Level>();
        var dir = Path.Combine(AppContext.BaseDirectory, "levels");
        if (!Directory.Exists(dir))
            return levels;
        foreach (var path in Directory.GetFiles(dir, "*.csv")
            .OrderBy(p => p, StringComparer.OrdinalIgnoreCase))
        {
            var name = Path.GetFileNameWithoutExtension(path);
            try
            {
                levels.Add(CsvLevel.Parse(path));
            }
            catch (Exception e) when (e is FormatException or IOException)
            {
                levels.Add(new BrokenLevel(name, e.Message));
            }
        }
        return levels;
    }
}
