using Srui;

namespace XGRhythm.Levels;

/// <summary>The synthesized proving ground: 120 BPM, 32 bars, every cue
/// shape the engine supports — simple echoes, an eighth-note run,
/// interleaved call and response, and two cues active at once. All
/// responses are Space, and every hit carries an ambient marker at its
/// exact moment, so the timing is learnable by ear.</summary>
public sealed class TestLevel : Level
{
    public override string Title => "Test pattern";

    public override string MusicFile => "testtrack.ogg";

    public override double Bpm => 120;

    public override double BeatOneOffsetMs => 0;

    public override IReadOnlyList<Cue> BuildCues()
    {
        var space = Key.Space;
        var cues = new List<Cue>();

        // Simple echoes: two calls on beats 1 and 2, answered on 3 and 4
        // at the same spacing. Every other bar.
        for (var i = 0; i < 4; i++)
        {
            cues.Add(new Cue
            {
                Bar = 2 + i * 2,
                KeySoundFile = "tap.ogg",
                Calls = [new(0, "call.ogg"), new(1, "call.ogg")],
                Hits =
                [
                    new(2, space, AmbientFile: "amb.ogg"),
                    new(3, space, AmbientFile: "amb.ogg"),
                ],
            });
        }

        // An eighth-note run: four calls at eighth spacing, answered at
        // the same spacing.
        cues.Add(new Cue
        {
            Bar = 11,
            KeySoundFile = "tap.ogg",
            Calls =
            [
                new(0, "call.ogg"), new(0.5, "call.ogg"),
                new(1, "call.ogg"), new(1.5, "call.ogg"),
            ],
            Hits =
            [
                new(2, space, AmbientFile: "amb.ogg"),
                new(2.5, space, AmbientFile: "amb.ogg"),
                new(3, space, AmbientFile: "amb.ogg"),
                new(3.5, space, AmbientFile: "amb.ogg"),
            ],
        });

        // Interleaved: call, answer, call, answer — a call arrives after
        // the previous response has already completed.
        cues.Add(new Cue
        {
            Bar = 14,
            KeySoundFile = "tap.ogg",
            Calls = [new(0, "call.ogg"), new(2, "call.ogg")],
            Hits =
            [
                new(1, space, AmbientFile: "amb.ogg"),
                new(3, space, AmbientFile: "amb.ogg"),
            ],
        });

        // Two cues at once: one answers on the beat, the other halfway
        // off it, weaving a single eighth-note response line between
        // them.
        cues.Add(new Cue
        {
            Bar = 17,
            KeySoundFile = "tap.ogg",
            Calls = [new(0, "call.ogg"), new(1, "call.ogg")],
            Hits =
            [
                new(2, space, AmbientFile: "amb.ogg"),
                new(3, space, AmbientFile: "amb.ogg"),
            ],
        });
        cues.Add(new Cue
        {
            Bar = 17,
            Percent = 50,
            KeySoundFile = "tap.ogg",
            Hits =
            [
                new(2, space, AmbientFile: "amb.ogg"),
                new(3, space, AmbientFile: "amb.ogg"),
            ],
        });

        return cues;
    }
}
