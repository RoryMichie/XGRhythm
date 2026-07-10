# XGRhythm Design

# 1: Overview

XGRhythm is an audio-only rhythm game and the first game built on SRUI (c:\srui), consumed from source via project references to Srui.Net and Srui.Audio. The player navigates a spoken menu, picks a level, and answers rhythmic calls with timed key presses. All feedback is earcons; speech is reserved for menus and announcements. The game targets .NET 10 with Native AOT publishing, so all serialization uses source-generated System.Text.Json and levels are plain C# code.

Sound assets live in c:\admin\XGRhythm\asset as OGG. The shared sounds are:

* menumusic.ogg — looping menu music.
* menumove.ogg — any navigation: list movement, slider adjustment, focus (tab) movement.
* menuenter.ogg — activating something that actually acts (entering a level, pressing a button).
* correct.ogg — the completion ding when every hit in a cue was correct; also the calibration metronome.
* wrong.ogg — any wrong input: wrong key, stray press, or a missed hit.

# 2: Main Screen

A single panel of widgets, in this order, with these mnemonics:

* Levels (list), alt+l.
* Music volume (slider, 0 to 100), alt+u.
* Master volume (slider, 0 to 100), alt+m.
* Tolerance window (list), alt+t.
* Audio engine buffer size (list), alt+b.
* Calibrate real input latency (button), alt+c.

The levels list is the layer's primary widget: Enter anywhere on the main screen starts the selected level through the list's Activated event, which plays menuenter. Shift+Enter (SecondaryActivated) is reserved for later per-level actions. The settings lists and sliders update live as the selection or value moves, so Enter on them is a genuine no-op and plays nothing.

Every navigation plays menumove: moving within a list, adjusting a slider, and focus moving between widgets. Focus moves are caught centrally by a small IReader that plays menumove on Focused accessibility events; list and slider sounds come from thin widget subclasses in the SruiDemo SoundListBox style (override OnChanged, keep the base call). When a volume slider is adjusted, its menumove preview plays at the volume the slider now denotes — the preview sound's BaseVolume is set from the slider before playing — so the user hears what they are choosing. The master slider's preview simply plays through the master bus, which scales it naturally.

The tolerance list offers named difficulties, where the value is the half-width of the hit window (100 means plus or minus 100 ms around the target):

* Beginner, 300 ms.
* Easy, 200 ms.
* Moderate, 100 ms (default).
* Hard, 50 ms.
* Expert, 25 ms.
* Insane, 10 ms.

The buffer-size list offers 128, 256, 512, and 1024 frames. Each item's text includes the millisecond equivalent computed live from the device sample rate (frames * 1000 / SruiApp.AudioSampleRate). A selection change applies immediately by writing SruiApp.AudioPeriodFrames — no debounce; the brief device-rebuild gap is accepted. After applying, the granted period is read back, and if it differs from the request the granted value is announced.

Menu music loops on the main screen, stops when a level starts, and resumes when the player returns.

# 3: Audio and Mixing

One SoundManager (SruiApp.Audio) with nested groups: a master group holding a music group and an SFX group. The master volume slider drives the master group, the music slider drives the music group. Menu and level music route through the music group; earcons, cue sounds, and hit feedback route through the SFX group.

# 4: Settings and Persistence

Settings persist in a JSON file next to the executable: music volume, master volume, tolerance in ms, requested buffer period in frames, and calibrated input latency in ms. Loaded at startup, saved on change. Serialization goes through a source-generated JsonSerializerContext for AOT compatibility.

# 5: Input Latency Calibration

The calibrate button opens a dialog layer with correct.ogg ticking as a metronome at a fixed 600 ms interval. The player taps Space along with it. The first few taps are discarded while the player locks in; after that, each tap's error is its time minus the nearest tick. Calibration maintains a running mean (mu) and sample standard deviation (sigma) of the errors and stops when the standard error (sigma / sqrt(n)) falls below a small threshold, or after 25 counted taps, whichever comes first. The resulting mu becomes the input latency offset, subtracted from every gameplay press before judging.

A mu below zero means the player anticipates the beat rather than reacting to it: latency clamps to 0 ms and the player is told they are tapping before the beat and that real latency has been set to 0. Afterward a status box reports mu and sigma. Because the metronome is an audible sound, the measured offset covers the whole loop — audio output latency, human reaction, and input path — which is exactly the offset needed when judging against what the player hears.

# 6: Levels

A level is defined in C# code, one class per level under the Levels directory, registered in a static list the levels list reads. A level carries a title, the music file name (in the asset folder), the tempo in BPM, the offset in ms of bar 1 beat 1 within the music file, and its cues. Tracks are produced at perfect tempo, so a single BPM and offset suffice; there is no tempo map.

Until real tracks are added, the first test level uses a synthesized track with a plainly audible beat at a known BPM.

# 7: Cues

A cue is anchored at a position in the track: bar (1-based), beat (1 to 4 in 4/4), and percent through that beat. It contains a timeline of elements at beat offsets from the anchor, of two kinds, interleavable in any order (a call may follow a response that has already completed):

* Call sounds: a sound scheduled at an offset, always played. These telegraph the rhythm the player answers.
* Response hits: an expected key at an expected time. Each hit carries three sounds — an ambient sound played at the scheduled time whether or not the player succeeds, a correct sound, and a wrong sound.

The cue also carries its key sound: the feedback played for the player's actual press when a press is judged as belonging to this cue. When every hit in a cue is judged correct, correct.ogg dings as the final hit lands; a single miss anywhere in the cue forfeits the ding.

Multiple cues may be active at once, each with its own interleaved calls and hits.

# 8: Timing

The playing music is the authoritative clock. Sound.PlaybackPosition (integer ms, quantized near device-period resolution) is sampled each loop iteration together with SruiApp.PreciseNow; between samples, music time is estimated as the last position plus the precise time elapsed since it was sampled, re-anchored gently when the estimate drifts more than a few ms from a fresh reading. Beat math: beat = (musicMs - beatOneOffsetMs) * bpm / 60000, and a cue anchor converts as beatIndex = (bar - 1) * 4 + (beat - 1) + percent / 100.

SRUI's loop runs at a 2 ms idle cadence, so scheduled sounds (calls, ambients) and input timestamps land within a few ms — well inside even the Insane 10 ms window's useful range.

# 9: Judging

A press arrives from a BindKey handler, is timestamped with PreciseNow, converted to music time, and reduced by the calibrated input latency. With tolerance t, a hit expected at time e has the open window [e - t, e + t]; the window is the hit's whole lifetime for judging purposes.

* The press's key is matched against the pending hits of all active cues whose window contains the press time. The nearest such hit by expected time claims the press: the hit is correct, and the cue's key sound plus the hit's correct sound play.
* A press matching no open window on its key is a stray: wrong.ogg plays and nothing else changes. Wrong keys and too-early or too-late presses all manifest as strays.
* A hit whose window closes unclaimed is a miss: wrong.ogg and the hit's wrong sound play at window close, and the cue's completion ding is forfeited. A press after the window has closed is an ordinary stray — the opportunity has already vanished.

Ambient sounds fire at every hit's scheduled time regardless of outcome.

# 10: Gameplay Screen

Starting a level pushes a modal layer containing a role-less play-field CustomWidget. At level start, the keys the level uses are bound on it via BindKey press handlers; window focus loss zeroes held-key state. Closing the layer restores menu focus to the levels list.

The play-field claims Escape itself (ahead of automatic dialog dismissal) and opens a pause layer: level music pauses in place (Sound.Pause keeps the position), a status line reports the running stats, a Resume button is both primary and cancel so Enter and Escape both resume, and a Quit level button abandons the run and returns to the menu.

Running stats: correct hits, misses, strays, cues completed out of total, and mean absolute timing error of correct hits. When the track ends, a status box (SruiDialogs.ShowStatus) shows the same stats as the level's result, and dismissing it returns to the menu.

# 11: Project Layout

* c:\admin\XGRhythm\XGRhythm.csproj — net10.0, ProjectReferences to c:\srui\Srui.Net\Srui.Net.csproj and c:\srui\Srui.Audio\Srui.Audio.csproj, native DLLs copied beside the exe.
* c:\admin\XGRhythm\Program.cs — bootstrap: app, buses, settings, main menu.
* c:\admin\XGRhythm\MainMenu.cs — the main-screen panel and its widget wiring.
* c:\admin\XGRhythm\Earcons.cs — earcon loading, the focus-move reader, sound-augmented widget subclasses.
* c:\admin\XGRhythm\GameScreen.cs — the play-field layer, pause layer, and results.
* c:\admin\XGRhythm\Conductor.cs — the music clock and beat math.
* c:\admin\XGRhythm\Judge.cs — hit windows, press matching, stats.
* c:\admin\XGRhythm\Cue.cs — cue, call, and hit models.
* c:\admin\XGRhythm\Level.cs — the level base and registry.
* c:\admin\XGRhythm\Levels\ — one file per level.
* c:\admin\XGRhythm\Calibration.cs — the calibration dialog and estimator.
* c:\admin\XGRhythm\Settings.cs — the settings model, load/save, JSON context.
* c:\admin\XGRhythm\asset\ — all OGG sounds and level music.

# 12: Conventions

Commits are small and frequent — every working increment. Publishing targets Native AOT, so reflection-dependent libraries are avoided throughout. The game is not throwaway: modularity and maintainability are assumed.
