using System.Text.Json;
using System.Text.Json.Serialization;

namespace XGRhythm;

/// <summary>User configuration, persisted as JSON beside the executable.
/// Volumes are percentages (0..100); the period is the requested frame
/// count, not the granted one; input latency is the calibrated offset
/// subtracted from every press before judging, never negative.</summary>
public sealed class Settings
{
    public int MusicVolume { get; set; } = 80;
    public int MasterVolume { get; set; } = 80;
    public int ToleranceMs { get; set; } = 100;
    public uint PeriodFrames { get; set; } = 128;
    public double InputLatencyMs { get; set; }

    private static string FilePath => Path.Combine(AppContext.BaseDirectory, "settings.json");

    /// <summary>Load the settings, or defaults when the file is missing
    /// or unreadable — the next save rewrites it.</summary>
    public static Settings Load()
    {
        try
        {
            if (File.Exists(FilePath))
            {
                var loaded = JsonSerializer.Deserialize(
                    File.ReadAllText(FilePath), SettingsContext.Default.Settings);
                if (loaded is not null)
                    return loaded;
            }
        }
        catch (Exception e) when (e is IOException or JsonException or UnauthorizedAccessException)
        {
        }
        return new Settings();
    }

    public void Save()
    {
        try
        {
            File.WriteAllText(
                FilePath, JsonSerializer.Serialize(this, SettingsContext.Default.Settings));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
        }
    }
}

[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(Settings))]
internal sealed partial class SettingsContext : JsonSerializerContext;
