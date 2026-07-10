using Srui;
using XGRhythm;

var settings = Settings.Load();
using var app = new SruiApp("XGRhythm");
// Stored as the request before the manager exists, so the device opens
// with the saved period.
app.AudioPeriodFrames = settings.PeriodFrames;
var earcons = new Earcons(app.Audio);
earcons.Music.Volume = settings.MusicVolume / 100f;
earcons.Master.Volume = settings.MasterVolume / 100f;
app.AddReader(new EarconReader(earcons));
_ = new MainMenu(app, settings, earcons);
app.Run();
