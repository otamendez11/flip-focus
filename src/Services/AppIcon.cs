using System.Drawing;
using System.Reflection;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using WF = System.Windows.Forms;

namespace PomodoroFlip.Services;

/// <summary>Carga el icono de la app desde el recurso embebido, con respaldo al propio exe.</summary>
public static class AppIcon
{
    private static Icon? _cached;

    public static Icon Get(int size = 32)
    {
        if (_cached is not null) return _cached;

        try
        {
            var asm = Assembly.GetExecutingAssembly();
            var name = asm.GetManifestResourceNames()
                .FirstOrDefault(n => n.EndsWith("app.ico", StringComparison.OrdinalIgnoreCase));
            if (name is not null)
            {
                using var stream = asm.GetManifestResourceStream(name);
                if (stream is not null) _cached = new Icon(stream, new System.Drawing.Size(size, size));
            }
        }
        catch
        {
            // sigue con el respaldo
        }

        if (_cached is null)
        {
            try
            {
                var exe = Environment.ProcessPath;
                if (exe is not null) _cached = Icon.ExtractAssociatedIcon(exe);
            }
            catch
            {
                // respaldo final
            }
        }

        _cached ??= SystemIcons.Application;
        return _cached;
    }

    /// <summary>El mismo icono como ImageSource, para la barra de tareas y el Alt+Tab.</summary>
    public static System.Windows.Media.ImageSource GetImageSource(int size = 32)
    {
        var icon = Get(size);
        var source = Imaging.CreateBitmapSourceFromHIcon(
            icon.Handle,
            Int32Rect.Empty,
            BitmapSizeOptions.FromEmptyOptions());
        source.Freeze();
        return source;
    }
}
