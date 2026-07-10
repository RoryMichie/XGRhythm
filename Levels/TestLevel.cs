using Srui;

namespace XGRhythm.Levels;

/// <summary>The synthesized proving ground: 120 BPM, 32 bars, every cue
/// shape the engine supports — simple echoes, an eighth-note run,
/// interleaved call and response, and two cues active at once.</summary>
public sealed class TestLevel : Level
{
    public override string Title => "Test pattern";

    public override string MusicFile => "testtrack.ogg";

    public override double Bpm => 120;

    public override double BeatOneOffsetMs => 0;

    public override IReadOnlyList<Cue> BuildCues()
    {
        var f = Key.Char('f');
        var j = Key.Char('j');
        var cues = new List<Cue>();

        // Simple echoes: two calls on beats 1 and 2, the same key
        // answers on 3 and 4. Alternating hands, every other bar.
        for (var i = 0; i < 4; i++)
        {
            cues.Add(new Cue
            {
                Bar = 2 + i * 2,
                KeySoundFile = "tap.ogg",
                Calls = [new(0, "call.ogg"), new(1, "call.ogg")],
                Hits = [new(2, i % 2 == 0 ? f : j), new(3, i % 2 == 0 ? f : j)],
            });
        }

        // An eighth-note run: four calls at eighth spacing, answered at
        // the same spacing, switching hands halfway.
        cues.Add(new Cue
        {
            Bar = 11,
            KeySoundFile = "tap.ogg",
            Calls =
            [
                new(0, "call.ogg"), new(0.5, "call.ogg"),
                new(1, "call.ogg"), new(1.5, "call.ogg"),
            ],
            Hits = [new(2, f), new(2.5, f), new(3, j), new(3.5, j)],
        });

        // Interleaved: call, answer, call, answer — a call arrives after
        // the previous response has already completed. The answers carry
        // an ambient sound at their scheduled moment.
        cues.Add(new Cue
        {
            Bar = 14,
            KeySoundFile = "tap.ogg",
            Calls = [new(0, "call.ogg"), new(2, "call.ogg")],
            Hits = [new(1, f, AmbientFile: "amb.ogg"), new(3, j, AmbientFile: "amb.ogg")],
        });

        // Two cues at once: f answers on the beat while j answers
        // halfway off it.
        cues.Add(new Cue
        {
            Bar = 17,
            KeySoundFile = "tap.ogg",
            Calls = [new(0, "call.ogg"), new(1, "call.ogg")],
            Hits = [new(2, f), new(3, f)],
        });
        cues.Add(new Cue
        {
            Bar = 17,
            Percent = 50,
            KeySoundFile = "tap.ogg",
            Hits = [new(2, j), new(3, j)],
        });

        return cues;
    }
}
