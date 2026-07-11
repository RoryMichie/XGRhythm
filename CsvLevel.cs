using System.Globalization;
using Srui;

namespace XGRhythm;

/// <summary>A level parsed from a CSV file; see plan.md section 6 for
/// the format.</summary>
internal sealed class ParsedLevel(
    string title, string musicFile, double bpm, double beatOneOffsetMs, IReadOnlyList<Cue> cues)
    : Level
{
    public override string Title => title;

    public override string MusicFile => musicFile;

    public override double Bpm => bpm;

    public override double BeatOneOffsetMs => beatOneOffsetMs;

    public override IReadOnlyList<Cue> BuildCues() => cues;
}

/// <summary>Stands in for a level file that failed to load: it stays
/// selectable in the list, and starting it announces the error instead
/// of playing.</summary>
public sealed class BrokenLevel(string fileName, string error) : Level
{
    public string Error { get; } = error;

    public override string Title => $"{fileName}, failed to load";

    public override string MusicFile => "";

    public override double Bpm => 120;

    public override double BeatOneOffsetMs => 0;

    public override IReadOnlyList<Cue> BuildCues() => [];
}

/// <summary>The CSV level parser. Line-oriented: blank lines and lines
/// starting with # are ignored, fields are comma-separated and trimmed,
/// the first field names the record type (level, cue, call, hit), and
/// trailing optional fields may be omitted. Every referenced sound file
/// is checked against the asset folder, so a missing asset surfaces as
/// a load failure rather than a crash mid-level.</summary>
public static class CsvLevel
{
    public static Level Parse(string path)
    {
        string title = "";
        string? music = null;
        var bpm = 0.0;
        var offset = 0.0;
        var cues = new List<Cue>();
        var soundFiles = new HashSet<string>();

        // The cue under construction; flushed when the next one starts.
        int? bar = null;
        var beat = 1;
        var percent = 0.0;
        string? keySound = null;
        var calls = new List<CueCall>();
        var hits = new List<CueHit>();

        void FlushCue(int lineNo)
        {
            if (bar is not int b)
                return;
            if (hits.Count == 0)
                throw new FormatException($"line {lineNo}: the cue at bar {b} has no hits");
            cues.Add(new Cue
            {
                Bar = b,
                Beat = beat,
                Percent = percent,
                KeySoundFile = keySound,
                Calls = calls.ToList(),
                Hits = hits.ToList(),
            });
            calls.Clear();
            hits.Clear();
            bar = null;
        }

        var lines = File.ReadAllLines(path);
        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i].Trim();
            if (line.Length == 0 || line.StartsWith('#'))
                continue;
            var lineNo = i + 1;
            var fields = line.Split(',');
            for (var f = 0; f < fields.Length; f++)
                fields[f] = fields[f].Trim();

            string Req(int index, string what) =>
                index < fields.Length && fields[index].Length > 0
                    ? fields[index]
                    : throw new FormatException($"line {lineNo}: missing {what}");
            string? Opt(int index) =>
                index < fields.Length && fields[index].Length > 0 ? fields[index] : null;
            double Num(int index, string what)
            {
                var raw = Req(index, what);
                return double.TryParse(raw, CultureInfo.InvariantCulture, out var value)
                    ? value
                    : throw new FormatException($"line {lineNo}: {what} is not a number: {raw}");
            }
            string? Sound(int index)
            {
                var file = Opt(index);
                if (file is not null)
                    soundFiles.Add(file);
                return file;
            }
            string ReqSound(int index, string what)
            {
                var file = Req(index, what);
                soundFiles.Add(file);
                return file;
            }

            switch (fields[0].ToLowerInvariant())
            {
                case "level":
                    if (music is not null)
                        throw new FormatException($"line {lineNo}: a second level record");
                    title = Req(1, "title");
                    music = ReqSound(2, "music file");
                    bpm = Num(3, "bpm");
                    if (bpm <= 0)
                        throw new FormatException($"line {lineNo}: bpm must be positive");
                    offset = Opt(4) is null ? 0.0 : Num(4, "beat-one offset");
                    break;
                case "cue":
                    if (music is null)
                        throw new FormatException($"line {lineNo}: a cue before the level record");
                    FlushCue(lineNo);
                    var newBar = (int)Num(1, "bar");
                    if (newBar < 1)
                        throw new FormatException($"line {lineNo}: bars start at 1");
                    beat = Opt(2) is null ? 1 : (int)Num(2, "beat");
                    if (beat is < 1 or > 4)
                        throw new FormatException($"line {lineNo}: beat must be 1 to 4");
                    percent = Opt(3) is null ? 0.0 : Num(3, "percent");
                    if (percent is < 0 or >= 100)
                        throw new FormatException(
                            $"line {lineNo}: percent must be at least 0 and below 100");
                    keySound = Sound(4);
                    bar = newBar;
                    break;
                case "call":
                    if (bar is null)
                        throw new FormatException($"line {lineNo}: a call outside any cue");
                    calls.Add(new CueCall(Num(1, "offset"), ReqSound(2, "sound file")));
                    break;
                case "hit":
                    if (bar is null)
                        throw new FormatException($"line {lineNo}: a hit outside any cue");
                    var keyName = Req(2, "key");
                    var key = Key.FromConfigName(keyName)
                        ?? throw new FormatException($"line {lineNo}: unknown key: {keyName}");
                    hits.Add(new CueHit(Num(1, "offset"), key, Sound(3), Sound(4), Sound(5)));
                    break;
                default:
                    throw new FormatException($"line {lineNo}: unknown record type: {fields[0]}");
            }
        }
        FlushCue(lines.Length);
        if (music is null)
            throw new FormatException("no level record");
        if (cues.Count == 0)
            throw new FormatException("no cues");
        foreach (var file in soundFiles)
        {
            if (!File.Exists(Earcons.AssetPath(file)))
                throw new FormatException($"sound file not in the asset folder: {file}");
        }
        return new ParsedLevel(title, music, bpm, offset, cues);
    }
}
