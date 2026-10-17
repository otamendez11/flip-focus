using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using PomodoroFlip.Models;

namespace PomodoroFlip.Services;

/// <summary>Guarda y lee los ajustes en %APPDATA%\PomodoroFlip\settings.json.</summary>
public static class SettingsStore
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public static string FolderPath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "PomodoroFlip");

    public static string FilePath { get; } = Path.Combine(FolderPath, "settings.json");

    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(FilePath))
            {
                var json = File.ReadAllText(FilePath);
                var loaded = JsonSerializer.Deserialize<AppSettings>(json, Options);
                if (loaded is not null) return loaded;
            }
        }
        catch
        {
            // Ajustes corruptos: se arranca con los valores por defecto en lugar de caerse.
        }

        return new AppSettings();
    }

    public static void Save(AppSettings settings)
    {
        try
        {
            Directory.CreateDirectory(FolderPath);
            var json = JsonSerializer.Serialize(settings, Options);
            File.WriteAllText(FilePath, json);
        }
        catch
        {
            // Nunca bloquear la app por no poder escribir los ajustes.
        }
    }
}
