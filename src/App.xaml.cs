using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;

namespace PomodoroFlip;

public partial class App : Application
{
    private Mutex? _singleInstance;

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr FindWindow(string? className, string windowName);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern bool PostMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

    protected override void OnStartup(StartupEventArgs e)
    {
        // Modo de diagnostico: genera los sonidos del cuenco y sale. Va antes de la
        // comprobacion de instancia unica para poder usarlo con la app abierta.
        var dumpIndex = Array.FindIndex(e.Args, a => string.Equals(a, "--dump-bowl", StringComparison.OrdinalIgnoreCase));
        if (dumpIndex >= 0)
        {
            var folder = dumpIndex + 1 < e.Args.Length
                ? e.Args[dumpIndex + 1]
                : System.IO.Path.Combine(System.IO.Path.GetTempPath(), "pomodoro-bowl");

            var watch = System.Diagnostics.Stopwatch.StartNew();
            Services.SingingBowl.DumpTo(folder);
            watch.Stop();
            var ok = Services.SingingBowl.PlayAndReport(Services.BowlVoice.Finish, 35);
            System.IO.File.WriteAllText(System.IO.Path.Combine(folder, "generacion.txt"),
                $"generar los tres sonidos: {watch.ElapsedMilliseconds} ms{Environment.NewLine}" +
                $"Windows acepta el audio (volumen 35): {ok}");
            Shutdown();
            return;
        }

        _singleInstance = new Mutex(true, @"Local\PomodoroFlip.SingleInstance", out var isFirst);
        if (!isFirst)
        {
            // Ya hay una copia abierta (quiza escondida en la bandeja). En vez de
            // cerrarse en silencio -que dejaba al usuario sin poder abrir la app-
            // se le pide que saque su ventana.
            AskRunningInstanceToShow();
            Shutdown();
            return;
        }

        base.OnStartup(e);

        var startInTray = e.Args.Any(a => string.Equals(a, "--tray", StringComparison.OrdinalIgnoreCase));

        // Traza opcional para diagnostico: --diag <ruta del archivo>
        var diagIndex = Array.FindIndex(e.Args, a => string.Equals(a, "--diag", StringComparison.OrdinalIgnoreCase));
        if (diagIndex >= 0 && diagIndex + 1 < e.Args.Length)
        {
            PomodoroFlip.MainWindow.DiagPath = e.Args[diagIndex + 1];
        }
        var window = new MainWindow(startInTray);
        MainWindow = window;
        window.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _singleInstance?.Dispose();
        base.OnExit(e);
    }

    /// <summary>
    /// Busca la ventana de la copia que ya esta corriendo y le pide que se muestre.
    /// Se reintenta un par de veces por si todavia no habia creado la ventana.
    /// </summary>
    private static void AskRunningInstanceToShow()
    {
        for (var intento = 0; intento < 12; intento++)
        {
            var handle = FindWindow(null, "Flip Focus");
            if (handle != IntPtr.Zero)
            {
                PostMessage(handle, PomodoroFlip.MainWindow.ShowAppMessage, IntPtr.Zero, IntPtr.Zero);
                return;
            }

            Thread.Sleep(150);
        }
    }
}
