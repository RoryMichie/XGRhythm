using Srui;

namespace XGRhythm;

/// <summary>The input-latency calibration dialog: correct.ogg ticks as a
/// metronome, the player taps Space along with it, and a running
/// mean/deviation (Welford) over the tap errors becomes the latency
/// offset. Stops by itself once the standard error settles or at the tap
/// cap. A negative mean means the player anticipates the beat: latency
/// clamps to 0 and says so. The measurement covers the whole loop —
/// audio out, human, input path — which is exactly the offset judging
/// against what the player hears needs.</summary>
public static class Calibration
{
    private const uint IntervalMs = 600;
    private const int DiscardTaps = 3;
    private const int MinTaps = 8;
    private const int MaxTaps = 25;
    private const double TargetStandardErrorMs = 4.0;

    public static void Open(SruiApp app, Earcons earcons, Settings settings)
    {
        var dialog = app.OpenDialog();
        _ = new Label(
            dialog,
            "Calibration. Tap Space in time with the metronome; it stops by itself "
                + "once the measurement settles.");
        var pad = new CustomWidget(dialog, "Tap pad");
        pad.Description = "Tap Space in time with the metronome.";
        var cancel = new Button(dialog, "Cancel");
        cancel.Activated += dialog.Close;
        app.SetCancel(cancel);

        var ticker = app.StartTicker(IntervalMs);
        dialog.Closed += ticker.Stop;
        var lastTick = TimeSpan.Zero;
        var ticked = false;
        ticker.Tick += () =>
        {
            earcons.Correct();
            lastTick = app.PreciseNow;
            ticked = true;
        };

        var discards = DiscardTaps;
        var n = 0;
        var mean = 0.0;
        var m2 = 0.0;
        var done = false;
        pad.BindKey(KeyCombo.Plain(Key.Space), KeyPhase.Press, () =>
        {
            if (done || !ticked)
                return;
            var error = (app.PreciseNow - lastTick).TotalMilliseconds;
            if (error > IntervalMs / 2.0)
                error -= IntervalMs; // early for the next tick, not late for this one
            if (discards > 0)
            {
                discards--; // let the player lock in first
                return;
            }
            n++;
            var delta = error - mean;
            mean += delta / n;
            m2 += delta * (error - mean);
            var sigma = n > 1 ? Math.Sqrt(m2 / (n - 1)) : double.PositiveInfinity;
            if ((n >= MinTaps && sigma / Math.Sqrt(n) <= TargetStandardErrorMs) || n >= MaxTaps)
            {
                done = true;
                Finish(app, settings, dialog, mean, n > 1 ? sigma : 0.0, n);
            }
        });
        dialog.AnnounceOpened();
    }

    private static void Finish(
        SruiApp app, Settings settings, Dialog dialog, double mean, double sigma, int taps)
    {
        settings.InputLatencyMs = Math.Max(0.0, mean);
        settings.Save();
        dialog.Close();
        var verdict = mean < 0
            ? $"Measured {mean:0.#} ms: you are tapping before the beat, so real latency "
                + "has been set to 0 ms."
            : $"Input latency set to {mean:0.#} ms.";
        app.ShowStatus(
            "Calibration result",
            $"{verdict}\nMu {mean:0.#} ms, sigma {sigma:0.#} ms, over {taps} taps.");
    }
}
