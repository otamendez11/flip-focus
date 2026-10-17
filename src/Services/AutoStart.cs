using Microsoft.Win32;

namespace PomodoroFlip.Services;

/// <summary>Alta y baja en el arranque de Windows (HKCU\...\Run).</summary>
public static class AutoStart
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "PomodoroFlip";

    /// <summary>Ruta del ejecutable; en desarrollo puede ser dotnet.exe, y entonces no se toca el registro.</summary>
    public static string? ExecutablePath
    {
        get
        {
            var path = Environment.ProcessPath;
            if (string.IsNullOrEmpty(path)) return null;
            return path.EndsWith("PomodoroFlip.exe", StringComparison.OrdinalIgnoreCase) ? path : null;
        }
    }

    public static bool IsSupported => ExecutablePath is not null;

    public static bool IsEnabled()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey, false);
            return key?.GetValue(ValueName) is string s && s.Length > 0;
        }
        catch
        {
            return false;
        }
    }

    public static bool Apply(bool enabled)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey, true);
            if (key is null) return false;

            if (enabled)
            {
                var exe = ExecutablePath;
                if (exe is null) return false;
                key.SetValue(ValueName, $"\"{exe}\" --tray");
            }
            else
            {
                key.DeleteValue(ValueName, false);
            }

            return true;
        }
        catch
        {
            return false;
        }
    }
}
