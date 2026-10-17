using System.Runtime.InteropServices;
using System.Windows.Interop;

namespace PomodoroFlip.Services;

/// <summary>
/// Refuerza "siempre encima" a nivel de Windows. La propiedad Topmost de WPF no
/// siempre deja el estilo puesto (basta con que otra ventana se active para que
/// el reloj quede detras), asi que se aplica tambien con SetWindowPos.
/// </summary>
public static class WindowTopMost
{
    private static readonly IntPtr HWND_TOPMOST = new(-1);
    private static readonly IntPtr HWND_NOTOPMOST = new(-2);
    private const uint SWP_NOMOVE = 0x0002;
    private const uint SWP_NOSIZE = 0x0001;
    private const uint SWP_NOACTIVATE = 0x0010;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr after, int x, int y, int cx, int cy, uint flags);

    public static bool Apply(IntPtr handle, bool topMost)
    {
        if (handle == IntPtr.Zero) return false;

        return SetWindowPos(handle, topMost ? HWND_TOPMOST : HWND_NOTOPMOST, 0, 0, 0, 0,
            SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
    }
}

/// <summary>
/// Utilidades de estilo de ventana. La app se crea sin bordes, y eso deja la
/// ventana sin boton de minimizar: la barra de tareas entonces no puede
/// alternar (traer al frente / minimizar) como hace con cualquier programa.
/// </summary>
public static class NativeWindowStyle
{
    private const int GWL_STYLE = -16;
    private const int WS_MINIMIZEBOX = 0x00020000;
    private const int WS_SYSMENU = 0x00080000;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int GetWindowLong(IntPtr hWnd, int index);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int SetWindowLong(IntPtr hWnd, int index, int value);

    /// <summary>Permite minimizar la ventana desde la barra de tareas.</summary>
    public static void EnableMinimize(IntPtr handle)
    {
        if (handle == IntPtr.Zero) return;

        var style = GetWindowLong(handle, GWL_STYLE);
        SetWindowLong(handle, GWL_STYLE, style | WS_MINIMIZEBOX | WS_SYSMENU);
    }
}

/// <summary>Dice si el clic que activa la ventana viene del escritorio o de la barra de tareas.</summary>
public static class ShellClick
{
    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int X;
        public int Y;
    }

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out POINT point);

    [DllImport("user32.dll")]
    private static extern IntPtr WindowFromPoint(POINT point);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

    /// <summary>
    /// True si lo que hay bajo el cursor pertenece al explorador de Windows
    /// (barra de tareas, bandeja o menu de inicio). En ese caso conviene dejar que
    /// Windows active la ventana, para que el clic alterna como en cualquier app.
    /// </summary>
    public static bool ComesFromShell()
    {
        if (!GetCursorPos(out var point)) return false;

        var under = WindowFromPoint(point);
        if (under == IntPtr.Zero) return false;

        GetWindowThreadProcessId(under, out var processId);
        if (processId == 0) return false;

        try
        {
            using var process = System.Diagnostics.Process.GetProcessById((int)processId);
            return string.Equals(process.ProcessName, "explorer", StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }
}

/// <summary>
/// Atajos de teclado globales (funcionan con la app en segundo plano) usando
/// RegisterHotKey, que no necesita hooks ni consume CPU.
/// </summary>
public sealed class GlobalHotKeys : IDisposable
{
    private const int WM_HOTKEY = 0x0312;
    public const uint MOD_ALT = 0x0001;
    public const uint MOD_CONTROL = 0x0002;
    public const uint MOD_SHIFT = 0x0004;
    private const uint MOD_NOREPEAT = 0x4000;

    /// <summary>Combinaciones que el usuario puede elegir en los ajustes.</summary>
    public const uint ComboCtrlAlt = MOD_CONTROL | MOD_ALT;
    public const uint ComboCtrlShift = MOD_CONTROL | MOD_SHIFT;

    private readonly HwndSource _source;
    private readonly Dictionary<int, Action> _actions = new();
    private int _nextId = 0x4100;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    public GlobalHotKeys(IntPtr windowHandle)
    {
        _source = HwndSource.FromHwnd(windowHandle)
                  ?? throw new InvalidOperationException("La ventana no tiene HwndSource.");
        _source.AddHook(WndProc);
    }

    public List<string> Registered { get; } = new();
    public List<string> Failed { get; } = new();

    public bool Register(string label, uint virtualKey, Action action, uint modifiers = ComboCtrlAlt)
    {
        var id = _nextId++;
        var ok = RegisterHotKey(_source.Handle, id, modifiers | MOD_NOREPEAT, virtualKey);
        if (ok)
        {
            _actions[id] = action;
            Registered.Add(label);
        }
        else
        {
            Failed.Add(label);
        }

        return ok;
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_HOTKEY && _actions.TryGetValue(wParam.ToInt32(), out var action))
        {
            action();
            handled = true;
        }

        return IntPtr.Zero;
    }

    public void Dispose()
    {
        foreach (var id in _actions.Keys)
        {
            UnregisterHotKey(_source.Handle, id);
        }

        _actions.Clear();
        _source.RemoveHook(WndProc);
    }
}
