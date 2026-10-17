using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace PomodoroFlip.Models;

public enum PhaseKind
{
    Focus,
    ShortBreak,
    LongBreak
}

public enum WindowMode
{
    Mini,
    Normal,
    Fullscreen
}

public enum AnchorCorner
{
    TopLeft,
    TopRight,
    BottomLeft,
    BottomRight
}

/// <summary>Modificador de los atajos globales; se elige en los ajustes.</summary>
public enum HotKeyModifier
{
    CtrlAlt,
    CtrlShift
}

/// <summary>Cuando avisar con un globo de la bandeja al cambiar de fase.</summary>
public enum NotificationMode
{
    /// <summary>Nunca. Ni al cerrar la ventana.</summary>
    Never,

    /// <summary>Solo si la ventana esta oculta en la bandeja (el caso de trabajar en otra cosa).</summary>
    OnlyWhenHidden,

    /// <summary>Siempre, tambien con el reloj a la vista.</summary>
    Always
}

/// <summary>Idioma de la interfaz.</summary>
public enum AppLanguage
{
    English,
    Spanish
}

/// <summary>
/// Estado persistente de la app. Implementa INotifyPropertyChanged para que los
/// bindings del panel de ajustes funcionen en dos direcciones sin framework MVVM.
/// </summary>
public sealed class AppSettings : INotifyPropertyChanged
{
    private int _focusMinutes = 25;
    private int _shortBreakMinutes = 5;
    private int _longBreakMinutes = 15;
    private int _cyclesBeforeLongBreak = 4;
    private bool _autoStartBreaks = true;
    private bool _autoStartFocus;
    private bool _soundEnabled = true;
    private int _volumePercent = 35;
    private NotificationMode _notificationMode = NotificationMode.Never;
    private AppLanguage _appLanguage = AppLanguage.English;
    private bool _alwaysOnTop = true;
    private bool _showSeconds = true;
    private bool _showControlsOnHover = true;
    private int _opacityPercent = 100;
    private WindowMode _mode = WindowMode.Mini;
    private AnchorCorner _anchorCorner = AnchorCorner.BottomRight;
    private int _miniScalePercent = 46;
    private int _normalScalePercent = 80;
    private double _windowLeft = double.NaN;
    private double _windowTop = double.NaN;
    private bool _startWithWindows;
    private bool _hotKeysEnabled = true;
    private bool _playSoundInFullscreen = true;
    private bool _motivationalQuotes = true;
    private bool _flipAnimation = true;
    private HotKeyModifier _hotKeyModifier = HotKeyModifier.CtrlAlt;
    private bool _mediaKeyPlayPause;

    // El reloj muestra dos digitos de minutos: el tope es 99 para que nunca se recorte.
    public int FocusMinutes { get => _focusMinutes; set => Set(ref _focusMinutes, Clamp(value, 1, 99)); }
    public int ShortBreakMinutes { get => _shortBreakMinutes; set => Set(ref _shortBreakMinutes, Clamp(value, 1, 99)); }
    public int LongBreakMinutes { get => _longBreakMinutes; set => Set(ref _longBreakMinutes, Clamp(value, 1, 99)); }
    public int CyclesBeforeLongBreak { get => _cyclesBeforeLongBreak; set => Set(ref _cyclesBeforeLongBreak, Clamp(value, 1, 12)); }
    public bool AutoStartBreaks { get => _autoStartBreaks; set => Set(ref _autoStartBreaks, value); }
    public bool AutoStartFocus { get => _autoStartFocus; set => Set(ref _autoStartFocus, value); }
    public bool SoundEnabled { get => _soundEnabled; set => Set(ref _soundEnabled, value); }
    public int VolumePercent { get => _volumePercent; set => Set(ref _volumePercent, Clamp(value, 0, 100)); }

    /// <summary>Globos de la bandeja al cambiar de fase (y aviso al mandar la ventana a la bandeja).</summary>
    public NotificationMode NotificationMode
    {
        get => _notificationMode;
        set => Set(ref _notificationMode, value);
    }

    /// <summary>Atajo rapido: alterna entre "nunca" y "solo si esta oculta".</summary>
    public void ToggleNotifications()
        => NotificationMode = NotificationMode == NotificationMode.Never
            ? NotificationMode.OnlyWhenHidden
            : NotificationMode.Never;

    /// <summary>Idioma de la interfaz. Ingles de fabrica.</summary>
    public AppLanguage AppLanguage { get => _appLanguage; set => Set(ref _appLanguage, value); }
    public bool AlwaysOnTop { get => _alwaysOnTop; set => Set(ref _alwaysOnTop, value); }
    public bool ShowSeconds { get => _showSeconds; set => Set(ref _showSeconds, value); }
    public bool ShowControlsOnHover { get => _showControlsOnHover; set => Set(ref _showControlsOnHover, value); }
    public int OpacityPercent { get => _opacityPercent; set => Set(ref _opacityPercent, Clamp(value, 30, 100)); }
    public WindowMode Mode { get => _mode; set => Set(ref _mode, value); }
    public AnchorCorner AnchorCorner { get => _anchorCorner; set => Set(ref _anchorCorner, value); }
    public int MiniScalePercent { get => _miniScalePercent; set => Set(ref _miniScalePercent, Clamp(value, 30, 70)); }
    public int NormalScalePercent { get => _normalScalePercent; set => Set(ref _normalScalePercent, Clamp(value, 55, 130)); }
    public double WindowLeft { get => _windowLeft; set => Set(ref _windowLeft, value); }
    public double WindowTop { get => _windowTop; set => Set(ref _windowTop, value); }
    public bool StartWithWindows { get => _startWithWindows; set => Set(ref _startWithWindows, value); }
    public bool HotKeysEnabled { get => _hotKeysEnabled; set => Set(ref _hotKeysEnabled, value); }
    public bool PlaySoundInFullscreen { get => _playSoundInFullscreen; set => Set(ref _playSoundInFullscreen, value); }

    /// <summary>Frases contundentes durante la sesion de foco: tres por sesion.</summary>
    public bool MotivationalQuotes { get => _motivationalQuotes; set => Set(ref _motivationalQuotes, value); }

    /// <summary>
    /// Efecto de volteo de los digitos. Es lo unico que consume CPU de verdad
    /// (dibuja mientras gira), asi que se puede apagar si se quiere lo minimo.
    /// </summary>
    public bool FlipAnimation { get => _flipAnimation; set => Set(ref _flipAnimation, value); }
    public HotKeyModifier HotKeyModifier { get => _hotKeyModifier; set => Set(ref _hotKeyModifier, value); }

    /// <summary>Tecla Play/Pausa del teclado como atajo global (apagada de fabrica).</summary>
    public bool MediaKeyPlayPause { get => _mediaKeyPlayPause; set => Set(ref _mediaKeyPlayPause, value); }

    public int FocusSeconds => FocusMinutes * 60;
    public int ShortBreakSeconds => ShortBreakMinutes * 60;
    public int LongBreakSeconds => LongBreakMinutes * 60;

    public event PropertyChangedEventHandler? PropertyChanged;

    private static int Clamp(int value, int min, int max) => value < min ? min : value > max ? max : value;

    private void Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
