using Srui;
using XGRhythm;

var settings = Settings.Load();
using var app = new SruiApp("XGRhythm");
_ = new Label(app, "XGRhythm");
app.Run();
