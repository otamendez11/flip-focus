using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using PomodoroFlip.Controls;
using PomodoroFlip.Models;
using PomodoroFlip.Services;
using WF = System.Windows.Forms;

namespace PomodoroFlip;

public partial class MainWindow : Window
{
    private const double ClockInset = 32;
    private const double EdgeMargin = 14;
    private const double PanelWidth = 520;
    private const double PanelHeight = 620;

    private readonly bool _startHidden;
    private readonly AppSettings _settings;
    private readonly PomodoroEngine _engine;
    private readonly DispatcherTimer _timer;
    private readonly DispatcherTimer _saveTimer;

    private TrayIcon? _tray;
    private GlobalHotKeys? _hotKeys;
    private Size _contentSize = new(484, 316);
    private int _lastShownSeconds = -1;
    private double _lastProgress = -1;
    private (PhaseKind Phase, bool Paused)? _lastPhaseState;
    private bool _settingsOpen;
    private bool _exiting;
    private bool _startedOnce;
    private bool _hovering;
    private bool _warnedAboutTray;
    private Point _pressScreen;
    private bool _pressedOnClock;
    private bool _draggedClock;
    private WindowMode? _hotKeyMode;

    /// <summary>Hay una sesion nueva esperando a arrancar: le toca cuenco de apertura.</summary>
    private bool _pendingPhaseStart = true;

    private readonly List<(Quote Quote, TimeSpan At, TimeSpan Visible)> _quotePlan = new();
    private readonly DispatcherTimer _quoteTimer;
    private readonly DispatcherTimer _topMostTimer;
    private int _quoteIndex;

    public MainWindow(bool startHidden)
    {
        _startHidden = startHidden;

        InitializeComponent();

        _settings = SettingsStore.Load();
        _engine = new PomodoroEngine(_settings);
        DataContext = _settings;
        Settings.DataContext = _settings;

        _timer = new DispatcherTimer(DispatcherPriority.Background) { Interval = TimeSpan.FromSeconds(1) };
        _timer.Tick += OnTick;

        _saveTimer = new DispatcherTimer(DispatcherPriority.Background) { Interval = TimeSpan.FromMilliseconds(600) };
        _saveTimer.Tick += (_, _) =>
        {
            _saveTimer.Stop();
            SettingsStore.Save(_settings);
        };

        _quoteTimer = new DispatcherTimer(DispatcherPriority.Background);
        _quoteTimer.Tick += (_, _) => HideQuote(instant: false);

        // Otras aplicaciones tambien se ponen arriba (algunas son topmost), asi que
        // se reafirma "siempre encima" cada pocos segundos. Cuesta practicamente nada.
        _topMostTimer = new DispatcherTimer(DispatcherPriority.Background) { Interval = TimeSpan.FromSeconds(2.5) };
        _topMostTimer.Tick += (_, _) => ApplyTopMostStyle();

        _engine.StateChanged += (_, _) => UpdateClock(animate: true);
        _engine.PhaseCompleted += OnPhaseCompleted;
        _settings.PropertyChanged += OnSettingChanged;

        Settings.CloseRequested += CloseSettings;
        Settings.ResetRequested += RestoreDefaults;
        Settings.RestartCycleRequested += RestartCycle;

        Icon = AppIcon.GetImageSource(32);

        Loaded += OnLoaded;
        SourceInitialized += OnSourceInitialized;
        Closing += OnClosing;
        LocationChanged += OnLocationChanged;
        StateChanged += OnStateChanged;

        MouseEnter += (_, _) => SetHover(true);
        MouseLeave += (_, _) => SetHover(false);

        ClockHost.MouseLeftButtonDown += OnClockPressed;
        ClockHost.MouseMove += OnClockMoved;
        ClockHost.MouseLeftButtonUp += OnClockReleased;
        RootGrid.MouseLeftButtonDown += OnBackgroundPressed;

        // La frase se quita en cuanto el raton la toca.
        QuoteBox.MouseEnter += (_, _) => HideQuote(instant: true);
        QuoteBox.MouseMove += (_, _) => HideQuote(instant: true);
    }

    // =====================================================================
    //  Arranque y cierre
    // =====================================================================

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        ConfigureHotKeys();

        var handle = new WindowInteropHelper(this).Handle;
        NativeWindowStyle.EnableMinimize(handle);

        // Como un widget de escritorio: el clic trabaja sin activar la ventana, asi
        // no se necesita un primer clic solo para traerla al frente y ademas nunca
        // le roba el foco a lo que el usuario este escribiendo.
        // La excepcion es el clic en la barra de tareas: ahi hay que dejar que
        // Windows active la ventana, porque ese clic es el que alterna entre
        // traerla al frente y minimizarla.
        var source = (HwndSource?)PresentationSource.FromVisual(this);
        source?.AddHook(WindowProc);
    }

    private IntPtr WindowProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        const int WM_MOUSEACTIVATE = 0x0021;
        const int MA_NOACTIVATE = 3;
        const int MA_ACTIVATE = 1;

        // Otra instancia pide que saquemos la ventana (al abrir el acceso directo
        // estando ya la app en la bandeja).
        if (msg == ShowAppMessage)
        {
            ShowWindow();
            handled = true;
            return IntPtr.Zero;
        }

        if (msg == WM_MOUSEACTIVATE && !_settingsOpen && _settings.Mode != WindowMode.Fullscreen)
        {
            handled = true;
            return new IntPtr(ShellClick.ComesFromShell() ? MA_ACTIVATE : MA_NOACTIVATE);
        }

        return IntPtr.Zero;
    }

    /// <summary>Mensaje propio: "saca la ventana, que han vuelto a abrir la app".</summary>
    public const int ShowAppMessage = 0x8001;

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        MeasureContent();

        _tray = new TrayIcon();
        _tray.ShowHideRequested += ToggleVisibility;
        _tray.PlayPauseRequested += ToggleTimer;
        _tray.ResetRequested += () =>
        {
            _engine.Reset();
            UpdateClock(animate: true);
        };
        _tray.MiniRequested += () => SetMode(WindowMode.Mini);
        _tray.NormalRequested += () => SetMode(WindowMode.Normal);
        _tray.FullscreenRequested += () => SetMode(WindowMode.Fullscreen);
        _tray.SettingsRequested += () =>
        {
            ShowWindow();
            OpenSettings();
        };
        _tray.ExitRequested += ExitApp;
        _tray.NotificationsToggleRequested += ToggleNotifications;

        Settings.RefreshHints(AutoStart.IsSupported || _settings.StartWithWindows, _hotKeys?.Failed ?? new List<string>());

        _engine.Reset();
        SyncNotifyMenu();
        // Primero el estado visual (fija si se muestran los segundos y por tanto el
        // ancho real del reloj) y despues se mide y se coloca la ventana.
        UpdateClock(animate: false);
        MeasureContent();
        ApplyLayout();
        _timer.Start();
        _topMostTimer.Start();

        if (_startHidden) HideToTray(silent: true);
    }

    private void OnClosing(object? sender, CancelEventArgs e)
    {
        if (_exiting) return;
        e.Cancel = true;
        HideToTray(silent: false);
    }

    private void ExitApp()
    {
        _exiting = true;
        _saveTimer.Stop();
        SettingsStore.Save(_settings);
        _timer.Stop();
        _hotKeys?.Dispose();
        _tray?.Dispose();
        Application.Current.Shutdown();
    }

    private void HideToTray(bool silent)
    {
        Hide();
        ShowInTaskbar = false;
        if (!silent && !_warnedAboutTray && _settings.NotificationMode != NotificationMode.Never)
        {
            _warnedAboutTray = true;
            _tray?.ShowBalloon("Pomodoro Flip sigue activo",
                "Se quedo en la bandeja. Te aviso cuando termine la sesion.");
        }
    }

    private void ShowWindow()
    {
        ShowInTaskbar = true;
        if (WindowState == WindowState.Minimized) WindowState = WindowState.Normal;
        if (!IsVisible) Show();
        Activate();
        ApplyLayout();
        ApplyTopMostStyle();
    }

    /// <summary>
    /// La propiedad Topmost de WPF no basta: el estilo se pierde y el reloj queda
    /// detras de la ventana activa. Se refuerza con SetWindowPos.
    /// </summary>
    private void ApplyTopMostStyle()
    {
        var handle = new WindowInteropHelper(this).Handle;
        if (handle == IntPtr.Zero) return;
        if (!IsVisible) return;

        WindowTopMost.Apply(handle, Topmost);
    }

    private void ToggleVisibility()
    {
        if (IsVisible) HideToTray(silent: true);
        else ShowWindow();
    }

    // =====================================================================
    //  Reloj
    // =====================================================================

    private void OnTick(object? sender, EventArgs e)
    {
        if (!_engine.IsRunning) return;
        _engine.Tick();
        CheckQuotes();
        UpdateClock(animate: true);
    }

    // =====================================================================
    //  Frases motivadoras
    // =====================================================================

    /// <summary>Prepara las tres frases de la sesion de foco con sus momentos al azar.</summary>
    private void PlanQuotesForSession()
    {
        HideQuote(instant: true);
        _quoteIndex = 0;

        if (!_settings.MotivationalQuotes || _engine.Phase != PhaseKind.Focus)
        {
            _quotePlan.Clear();
            return;
        }

        _quotePlan.Clear();
        _quotePlan.AddRange(Quotes.PlanSession(3, _engine.Total));
    }

    private void CheckQuotes()
    {
        if (_settingsOpen || !_settings.MotivationalQuotes) return;
        if (_engine.Phase != PhaseKind.Focus) return;
        if (_quoteIndex >= _quotePlan.Count) return;

        var elapsed = _engine.Total - _engine.Remaining;
        var next = _quotePlan[_quoteIndex];
        if (elapsed < next.At) return;

        _quoteIndex++;
        ShowQuote(next.Quote, next.Visible);
    }

    private void ShowQuote(Quote quote, TimeSpan visible)
    {
        QuoteText.Text = quote.Text;
        QuoteAuthor.Text = "— " + quote.Author;
        Diag($"FRASE mostrada en el segundo {(_engine.Total - _engine.Remaining).TotalSeconds:0.0}: \"{quote.Text}\" — {quote.Author} (visible {visible.TotalSeconds:0.0}s)");

        QuoteBox.Visibility = Visibility.Visible;
        QuoteBox.IsHitTestVisible = true;
        QuoteBox.BeginAnimation(OpacityProperty, null);

        var fadeIn = new DoubleAnimation(1, TimeSpan.FromMilliseconds(260))
        {
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
        };
        fadeIn.Completed += (_, _) =>
        {
            QuoteBox.BeginAnimation(OpacityProperty, null);
            QuoteBox.Opacity = 1;
        };
        QuoteBox.BeginAnimation(OpacityProperty, fadeIn);

        _quoteTimer.Stop();
        _quoteTimer.Interval = visible;
        _quoteTimer.Start();
    }

    /// <summary>La quita con fundido; instantaneo cuando la toca el raton.</summary>
    private void HideQuote(bool instant)
    {
        _quoteTimer.Stop();
        if (QuoteBox.Visibility != Visibility.Visible) return;

        Diag(instant ? "FRASE quitada por el raton" : "FRASE quitada al cumplir su tiempo");

        if (instant)
        {
            QuoteBox.BeginAnimation(OpacityProperty, null);
            QuoteBox.Opacity = 0;
            QuoteBox.Visibility = Visibility.Collapsed;
            QuoteBox.IsHitTestVisible = false;
            return;
        }

        var fadeOut = new DoubleAnimation(0, TimeSpan.FromMilliseconds(420))
        {
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn }
        };
        fadeOut.Completed += (_, _) =>
        {
            // Soltar el reloj de animacion: si no, WPF sigue repintando la ventana
            // a 60 fps aunque la frase ya no se vea.
            QuoteBox.BeginAnimation(OpacityProperty, null);

            if (QuoteBox.Opacity > 0.5) return;   // se mostro otra frase entretanto

            QuoteBox.Opacity = 0;
            QuoteBox.Visibility = Visibility.Collapsed;
            QuoteBox.IsHitTestVisible = false;
        };

        QuoteBox.BeginAnimation(OpacityProperty, fadeOut);
    }

    private void UpdateClock(bool animate)
    {
        var seconds = (int)Math.Ceiling(_engine.Remaining.TotalSeconds);
        if (seconds < 0) seconds = 0;

        var changed = seconds != _lastShownSeconds;
        Clock.SetTime(seconds, animate && _lastShownSeconds >= 0 && _settings.FlipAnimation, _settings.ShowSeconds);

        if (Math.Abs(_engine.Progress - _lastProgress) > 0.0015 || _lastProgress < 0)
        {
            Clock.SetProgress(_engine.Progress);
            _lastProgress = _engine.Progress;
        }

        var phaseState = (_engine.Phase, Paused: !_engine.IsRunning);
        if (_lastPhaseState is null || !_lastPhaseState.Value.Equals(phaseState))
        {
            Clock.SetPhase(_engine.Phase, !_engine.IsRunning, animate: _lastPhaseState is not null);
            _lastPhaseState = phaseState;
        }

        Clock.SetLabel(BuildLabel());
        var cycleTotal = _settings.CyclesBeforeLongBreak;
        Clock.SetCycle(Math.Min(_engine.FocusDoneInCycle, cycleTotal), cycleTotal);
        Settings.SetCycleInfo(Math.Min(_engine.FocusDoneInCycle, cycleTotal), cycleTotal);

        PlayPauseItem.Header = _engine.IsRunning ? "Pausar" : "Iniciar";

        // El tooltip de la bandeja se actualiza solo cuando cambia el segundo: hacerlo
        // en cada tick son cuatro llamadas por segundo al shell sin necesidad.
        if (changed) _tray?.Update(_engine.IsRunning, _engine.Phase, FormatClock(seconds));

        if (changed) _lastShownSeconds = seconds;
        UpdateTimerInterval();
    }

    private string BuildLabel()
    {
        if (!_engine.IsRunning && !_startedOnce) return "LISTO";

        return _engine.Phase switch
        {
            PhaseKind.Focus => _engine.IsRunning
                ? $"FOCO {Math.Min(_engine.FocusDoneInCycle + 1, _settings.CyclesBeforeLongBreak)}/{_settings.CyclesBeforeLongBreak}"
                : "EN PAUSA",
            PhaseKind.ShortBreak => _engine.IsRunning ? "DESCANSO" : "EN PAUSA",
            _ => _engine.IsRunning ? "DESCANSO LARGO" : "EN PAUSA"
        };
    }

    private static string FormatClock(int totalSeconds)
    {
        var minutes = totalSeconds / 60;
        var seconds = totalSeconds % 60;
        return $"{minutes:00}:{seconds:00}";
    }

    private void UpdateTimerInterval()
    {
        var wanted = _engine.IsRunning && _settings.ShowSeconds
            ? TimeSpan.FromMilliseconds(250)
            : TimeSpan.FromSeconds(1);

        if (_timer.Interval != wanted) _timer.Interval = wanted;
    }

    private void ToggleTimer()
    {
        if (!_engine.IsRunning) _startedOnce = true;
        _engine.Toggle();
        PlayPhaseStartIfPending();
        UpdateClock(animate: true);
    }

    /// <summary>
    /// Cuenco al empezar una sesion nueva. Suena cuando la fase arranca por
    /// primera vez (sea sola o al pulsar el usuario), no al reanudar una pausa.
    /// </summary>
    private void PlayPhaseStartIfPending()
    {
        if (!_pendingPhaseStart) return;
        _pendingPhaseStart = false;

        if (_settings.SoundEnabled)
        {
            var voice = _engine.Phase == PhaseKind.Focus ? BowlVoice.StartFocus : BowlVoice.StartBreak;
            SingingBowl.Play(voice, _settings.VolumePercent);
        }

        PlanQuotesForSession();
    }

    /// <summary>Decide si toca avisar con un globo, segun el modo elegido.</summary>
    private bool ShouldNotify() => _settings.NotificationMode switch
    {
        NotificationMode.Always => true,
        NotificationMode.OnlyWhenHidden => !IsVisible,
        _ => false
    };

    private void OnPhaseCompleted(object? sender, PhaseCompletedEventArgs e)
    {
        var inFullscreen = _settings.Mode == WindowMode.Fullscreen;
        var allowSound = _settings.SoundEnabled && (!inFullscreen || _settings.PlaySoundInFullscreen);

        // Al cerrar una sesion suena el cuenco; si la siguiente arranca sola, van
        // los dos golpes juntos en un mismo sonido (PlaySound no superpone).
        _pendingPhaseStart = !e.AutoStarted;

        if (allowSound)
        {
            var startVoice = e.Next == PhaseKind.Focus ? BowlVoice.StartFocus : BowlVoice.StartBreak;
            if (e.AutoStarted) SingingBowl.PlayPair(BowlVoice.Finish, startVoice, 1.7, _settings.VolumePercent);
            else SingingBowl.Play(BowlVoice.Finish, _settings.VolumePercent);
        }

        // La sesion nueva lleva sus propias tres frases.
        if (e.AutoStarted) PlanQuotesForSession();
        else HideQuote(instant: true);

        var notify = ShouldNotify();
        Diag($"fin de fase: modo={_settings.NotificationMode} ventanaVisible={IsVisible} -> aviso={(notify ? "SI" : "no")}");

        if (notify)
        {
            var title = e.Finished == PhaseKind.Focus ? "Sesion completada" : "Descanso terminado";
            var body = e.Next == PhaseKind.Focus
                ? "Vuelve al foco: el reloj ya cuenta."
                : e.Next == PhaseKind.ShortBreak
                    ? "Toca descanso corto."
                    : "Toca descanso largo.";
            if (!e.AutoStarted) body += " Esta en pausa.";
            _tray?.ShowBalloon(title, body);
        }

        UpdateClock(animate: true);
    }

    // =====================================================================
    //  Interaccion con el mouse
    // =====================================================================

    private void OnClockPressed(object sender, MouseButtonEventArgs e)
    {
        if (_settingsOpen) return;

        _pressScreen = ScreenCursor;
        _pressedOnClock = true;
        _draggedClock = false;
        ClockHost.CaptureMouse();
    }

    /// <summary>Arrastra la ventana solo cuando el raton se mueve de verdad; si no, es un clic.</summary>
    private void OnClockMoved(object sender, MouseEventArgs e)
    {
        if (!_pressedOnClock || _draggedClock) return;

        if (e.LeftButton != MouseButtonState.Pressed)
        {
            _pressedOnClock = false;
            ClockHost.ReleaseMouseCapture();
            return;
        }

        // Se compara la posicion del cursor en pantalla, no la relativa al elemento:
        // con el raton capturado, WPF puede informar de un movimiento inexistente.
        var delta = ScreenCursor - _pressScreen;
        if (Math.Abs(delta.X) < 6 && Math.Abs(delta.Y) < 6) return;

        _draggedClock = true;
        _pressedOnClock = false;
        ClockHost.ReleaseMouseCapture();

        if (_settings.Mode == WindowMode.Fullscreen) return;
        try
        {
            DragMove();
        }
        catch
        {
            // DragMove puede fallar si el boton ya se solto: se ignora.
        }
    }

    private void OnClockReleased(object sender, MouseButtonEventArgs e)
    {
        if (!_pressedOnClock) return;

        _pressedOnClock = false;
        ClockHost.ReleaseMouseCapture();
        if (_draggedClock) return;

        ToggleTimer();
    }

    private static Point ScreenCursor
    {
        get
        {
            var p = WF.Cursor.Position;
            return new Point(p.X, p.Y);
        }
    }

    /// <summary>Ruta de traza opcional (se activa con el argumento --diag).</summary>
    internal static string? DiagPath { get; set; }

    private static void Diag(string message)
    {
        if (DiagPath is null) return;

        try
        {
            File.AppendAllText(DiagPath, $"{DateTime.Now:HH:mm:ss.fff} {message}{Environment.NewLine}");
        }
        catch
        {
            // sin traza si no se puede escribir
        }
    }

    private void OnBackgroundPressed(object sender, MouseButtonEventArgs e)
    {
        if (_settingsOpen) return;

        // Solo arrastra si el clic no vino de un boton o del reloj.
        if (e.OriginalSource is DependencyObject source && IsInsideInteractive(source)) return;
        if (_settings.Mode == WindowMode.Fullscreen) return;

        try
        {
            DragMove();
        }
        catch
        {
            // ignorado
        }
    }

    private static bool IsInsideInteractive(DependencyObject? source)
    {
        while (source is not null)
        {
            if (source is System.Windows.Controls.Button) return true;
            if (source is FlipClock) return true;
            if (source is System.Windows.Controls.Primitives.ButtonBase) return true;
            source = System.Windows.Media.VisualTreeHelper.GetParent(source);
        }

        return false;
    }

    private void SetHover(bool hovering)
    {
        _hovering = hovering;
        UpdateBarOpacity();
    }

    // =====================================================================
    //  Atajos con la ventana activa
    // =====================================================================

    /// <summary>
    /// Teclas sueltas, sin combinaciones: solo cuando la ventana tiene el foco.
    /// Los atajos globales siguen funcionando desde cualquier programa.
    /// </summary>
    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        var key = e.Key == Key.System ? e.SystemKey : e.Key;

        switch (key)
        {
            case Key.Escape:
                if (_settingsOpen) CloseSettings();
                else if (_settings.Mode == WindowMode.Fullscreen) ExitFullscreen();
                else return;
                break;

            case Key.Space:
                if (_settingsOpen) return;
                ToggleTimer();
                break;

            case Key.F:
                if (_settingsOpen) return;
                ToggleFullscreen();
                break;

            case Key.M:
                if (_settingsOpen) return;
                SetMode(WindowMode.Mini);
                break;

            case Key.N:
                if (_settingsOpen) return;
                SetMode(WindowMode.Normal);
                break;

            case Key.A:
                if (_settingsOpen) CloseSettings();
                else OpenSettings();
                break;

            case Key.R:
                if (_settingsOpen) return;
                _engine.Reset();
                _lastShownSeconds = -1;
                UpdateClock(animate: true);
                break;

            case Key.C:
                if (_settingsOpen) return;
                RestartCycle();
                break;

            case Key.S:
                if (_settingsOpen) return;
                _engine.Skip();
                _lastShownSeconds = -1;
                UpdateClock(animate: true);
                break;

            default:
                return;
        }

        e.Handled = true;
    }

    private void ExitFullscreen()
    {
        if (_settings.Mode != WindowMode.Fullscreen) return;
        SetMode(WindowMode.Normal);
    }

    /// <summary>
    /// Enciende o apaga los avisos. Apagados pasan a "solo si la ventana esta
    /// oculta", que es el caso de cerrarla y ponerse a trabajar en otra cosa.
    /// </summary>
    private void ToggleNotifications()
    {
        _settings.ToggleNotifications();
        SyncNotifyMenu();
    }

    private void SyncNotifyMenu()
    {
        var on = _settings.NotificationMode != NotificationMode.Never;
        NotifyItem.IsChecked = on;
        _tray?.SetNotificationsChecked(on);
    }

    /// <summary>Vuelve al primer foco del ciclo y borra la cuenta de sesiones.</summary>
    private void RestartCycle()
    {
        _engine.ResetAll();
        _pendingPhaseStart = true;
        _lastShownSeconds = -1;
        PlanQuotesForSession();
        UpdateClock(animate: true);

        if (ShouldNotify())
        {
            _tray?.ShowBalloon("Ciclo reiniciado",
                $"Empiezas de cero: sesion 1 de {_settings.CyclesBeforeLongBreak}.");
        }
    }

    /// <summary>Aviso breve y discreto: aparece al entrar en pantalla completa y se va solo.</summary>
    private void ShowShortcutHint()
    {
        var mod = _settings.HotKeyModifier == HotKeyModifier.CtrlShift ? "Ctrl+Shift" : "Ctrl+Alt";
        ShortcutHintTitle.Text = "Esc para salir de pantalla completa";
        ShortcutHintBody.Text = $"Espacio pausa  ·  F sale  ·  M reduce  ·  {mod}+P desde cualquier programa";

        ShortcutHint.Visibility = Visibility.Visible;
        ShortcutHint.BeginAnimation(OpacityProperty, null);

        var fade = new DoubleAnimationUsingKeyFrames();
        fade.KeyFrames.Add(new LinearDoubleKeyFrame(1, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(200))));
        fade.KeyFrames.Add(new LinearDoubleKeyFrame(1, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(3.6))));
        fade.KeyFrames.Add(new LinearDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(4.4))));
        fade.Completed += (_, _) =>
        {
            ShortcutHint.BeginAnimation(OpacityProperty, null);
            ShortcutHint.Opacity = 0;
            ShortcutHint.Visibility = Visibility.Collapsed;
        };
        ShortcutHint.BeginAnimation(OpacityProperty, fade);
    }

    private void UpdateBarOpacity()
    {
        if (_settingsOpen)
        {
            HoverBar.Opacity = 0;
            HoverBar.IsHitTestVisible = false;
            return;
        }

        var show = !_settings.ShowControlsOnHover || _hovering;
        HoverBar.IsHitTestVisible = show;
        var target = show ? 1.0 : 0.0;
        if (Math.Abs(HoverBar.Opacity - target) < 0.01) return;

        var anim = new DoubleAnimation(target, TimeSpan.FromMilliseconds(140))
        {
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
        };
        anim.Completed += (_, _) =>
        {
            HoverBar.BeginAnimation(OpacityProperty, null);
            HoverBar.Opacity = target;
        };
        HoverBar.BeginAnimation(OpacityProperty, anim);
    }

    // =====================================================================
    //  Tamano y posicion
    // =====================================================================

    private void MeasureContent()
    {
        Clock.InvalidateMeasure();
        Clock.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        var desired = Clock.DesiredSize;
        _contentSize = new Size(desired.Width + ClockInset * 2, desired.Height + ClockInset * 2);
    }

    private void ApplyLayout()
    {
        if (_settingsOpen)
        {
            ApplySettingsLayout();
            return;
        }

        switch (_settings.Mode)
        {
            case WindowMode.Mini:
                ApplyMiniLayout();
                break;
            case WindowMode.Normal:
                ApplyNormalLayout();
                break;
            default:
                ApplyFullscreenLayout();
                break;
        }

        var fullscreen = _settings.Mode == WindowMode.Fullscreen;
        Root.CornerRadius = new CornerRadius(fullscreen ? 0 : 20);
        Root.BorderThickness = new Thickness(fullscreen ? 0 : 1);
        Root.Background = fullscreen
            ? System.Windows.Media.Brushes.Black
            : (System.Windows.Media.Brush)FindResource("ShellBg");

        Topmost = _settings.AlwaysOnTop || fullscreen;
        ApplyTopMostStyle();
        Opacity = Math.Clamp(_settings.OpacityPercent, 30, 100) / 100.0;

        FullButton.Content = fullscreen ? "\uE73F" : "\uE740";
        PinButton.Content = _settings.AlwaysOnTop ? "\uE718" : "\uE77A";
        ApplyQuoteSize(fullscreen);
        UpdateBarOpacity();

        // Esc solo se secuestra mientras dura la pantalla completa.
        if (_hotKeyMode != _settings.Mode)
        {
            var previous = _hotKeyMode;
            _hotKeyMode = _settings.Mode;
            ConfigureHotKeys();
            if (fullscreen && previous is not null) ShowShortcutHint();
        }
    }

    private void ApplyMiniLayout()
    {
        var scale = _settings.MiniScalePercent / 100.0;
        SetClockScale(scale);

        var width = Math.Round(_contentSize.Width * scale);
        var height = Math.Round(_contentSize.Height * scale);
        Width = width;
        Height = height;

        var area = GetMonitorRect(workArea: true);
        Left = _settings.AnchorCorner switch
        {
            AnchorCorner.TopLeft => area.Left + EdgeMargin,
            AnchorCorner.BottomLeft => area.Left + EdgeMargin,
            _ => area.Left + area.Width - width - EdgeMargin
        };
        Top = _settings.AnchorCorner switch
        {
            AnchorCorner.TopLeft or AnchorCorner.TopRight => area.Top + EdgeMargin,
            _ => area.Top + area.Height - height - EdgeMargin
        };
    }

    private void ApplyNormalLayout()
    {
        var scale = _settings.NormalScalePercent / 100.0;
        SetClockScale(scale);

        var width = Math.Round(_contentSize.Width * scale);
        var height = Math.Round(_contentSize.Height * scale);
        Width = width;
        Height = height;

        var area = GetMonitorRect(workArea: true);
        if (!double.IsNaN(_settings.WindowLeft) && !double.IsNaN(_settings.WindowTop) &&
            IsPositionVisible(_settings.WindowLeft, _settings.WindowTop, width, height, area))
        {
            Left = _settings.WindowLeft;
            Top = _settings.WindowTop;
        }
        else
        {
            Left = area.Left + (area.Width - width) / 2;
            Top = area.Top + (area.Height - height) / 2;
        }
    }

    private void ApplyFullscreenLayout()
    {
        var area = GetMonitorRect(workArea: false);
        var available = new Size(Math.Max(320, area.Width - 140), Math.Max(200, area.Height - 180));
        var scale = Math.Min(available.Width / _contentSize.Width, available.Height / _contentSize.Height);
        scale = Math.Clamp(scale, 0.5, 8);

        SetClockScale(scale);
        Left = area.Left;
        Top = area.Top;
        Width = area.Width;
        Height = area.Height;

        // En pantalla completa conviene que la ventana tenga el foco: asi funcionan
        // las teclas sueltas (Espacio, Esc, F, M...).
        try
        {
            Activate();
        }
        catch
        {
            // sin foco, los atajos globales siguen estando disponibles
        }
    }

    private void ApplySettingsLayout()
    {
        var area = GetMonitorRect(workArea: true);
        var height = Math.Min(PanelHeight, Math.Max(360, area.Height - 60));
        Width = PanelWidth;
        Height = height;
        Left = area.Left + (area.Width - Width) / 2;
        Top = area.Top + (area.Height - Height) / 2;
        Topmost = _settings.AlwaysOnTop;
        Opacity = 1;
    }

    /// <summary>La frase crece con la ventana para que se lea siempre.</summary>
    private void ApplyQuoteSize(bool fullscreen)
    {
        double size;
        double maxWidth;

        if (fullscreen)
        {
            size = 30;
            maxWidth = 1000;
        }
        else if (_settings.Mode == WindowMode.Mini)
        {
            size = 11.5;
            maxWidth = 235;
        }
        else
        {
            size = 15;
            maxWidth = 420;
        }

        QuoteText.FontSize = size;
        QuoteText.LineHeight = size * 1.4;
        QuoteAuthor.FontSize = Math.Max(9, size * 0.75);
        QuoteBox.MaxWidth = maxWidth;
        QuoteBox.Padding = new Thickness(size * 1.6, size * 1.2, size * 1.6, size * 1.2);
        QuoteBox.CornerRadius = new CornerRadius(fullscreen ? 22 : 16);
    }

    private void SetClockScale(double scale)
    {
        ClockScale.ScaleX = scale;
        ClockScale.ScaleY = scale;
    }

    private static bool IsPositionVisible(double left, double top, double width, double height, (double Left, double Top, double Width, double Height) area)
    {
        var right = left + width;
        var bottom = top + height;
        return right > area.Left + 40 && left < area.Left + area.Width - 40 &&
               bottom > area.Top + 20 && top < area.Top + area.Height - 20;
    }

    private (double Left, double Top, double Width, double Height) GetMonitorRect(bool workArea)
    {
        var handle = new WindowInteropHelper(this).Handle;
        var screen = handle != IntPtr.Zero
            ? WF.Screen.FromHandle(handle)
            : WF.Screen.PrimaryScreen;

        if (screen is null) return (0, 0, SystemParameters.PrimaryScreenWidth, SystemParameters.PrimaryScreenHeight);

        var rect = workArea ? screen.WorkingArea : screen.Bounds;
        var source = PresentationSource.FromVisual(this);
        if (source?.CompositionTarget is { } target)
        {
            var topLeft = target.TransformFromDevice.Transform(new Point(rect.Left, rect.Top));
            var bottomRight = target.TransformFromDevice.Transform(new Point(rect.Right, rect.Bottom));
            return (topLeft.X, topLeft.Y, bottomRight.X - topLeft.X, bottomRight.Y - topLeft.Y);
        }

        return (rect.Left, rect.Top, rect.Width, rect.Height);
    }

    private void OnLocationChanged(object? sender, EventArgs e)
    {
        if (_settingsOpen || _settings.Mode != WindowMode.Normal) return;
        if (double.IsNaN(Left) || double.IsNaN(Top)) return;

        _settings.WindowLeft = Left;
        _settings.WindowTop = Top;
        ScheduleSave();
    }

    /// <summary>
    /// Al restaurar desde la barra de tareas, la ventana vuelve a su sitio: en modo
    /// mini a su esquina, y en pantalla completa recupera la pantalla entera.
    /// </summary>
    private void OnStateChanged(object? sender, EventArgs e)
    {
        if (WindowState != WindowState.Normal) return;

        ApplyLayout();
    }

    private void ScheduleSave()
    {
        _saveTimer.Stop();
        _saveTimer.Start();
    }

    // =====================================================================
    //  Ajustes
    // =====================================================================

    private void OnSettingChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(AppSettings.Mode):
                if (!_settingsOpen) ApplyLayout();
                break;

            case nameof(AppSettings.MiniScalePercent):
            case nameof(AppSettings.NormalScalePercent):
            case nameof(AppSettings.AnchorCorner):
            case nameof(AppSettings.AlwaysOnTop):
            case nameof(AppSettings.OpacityPercent):
                ApplyLayout();
                break;

            case nameof(AppSettings.ShowSeconds):
                _lastShownSeconds = -1;
                UpdateClock(animate: false);
                MeasureContent();
                ApplyLayout();
                break;

            case nameof(AppSettings.FocusMinutes):
            case nameof(AppSettings.ShortBreakMinutes):
            case nameof(AppSettings.LongBreakMinutes):
                _engine.ApplySettings(resetCurrentPhase: true);
                _lastShownSeconds = -1;
                UpdateClock(animate: true);
                break;

            case nameof(AppSettings.CyclesBeforeLongBreak):
                UpdateClock(animate: false);
                break;

            case nameof(AppSettings.StartWithWindows):
                if (!AutoStart.Apply(_settings.StartWithWindows))
                {
                    _settings.StartWithWindows = AutoStart.IsEnabled();
                }
                break;

            case nameof(AppSettings.HotKeysEnabled):
            case nameof(AppSettings.HotKeyModifier):
            case nameof(AppSettings.MediaKeyPlayPause):
                ConfigureHotKeys();
                break;

            case nameof(AppSettings.ShowControlsOnHover):
                UpdateBarOpacity();
                break;

            case nameof(AppSettings.MotivationalQuotes):
                PlanQuotesForSession();
                break;

            case nameof(AppSettings.NotificationMode):
                SyncNotifyMenu();
                break;
        }

        ScheduleSave();
    }

    private void OpenSettings()
    {
        if (_settingsOpen) return;
        _settingsOpen = true;
        HideQuote(instant: true);
        Settings.Visibility = Visibility.Visible;
        UpdateBarOpacity();
        ApplySettingsLayout();
    }

    private void CloseSettings()
    {
        if (!_settingsOpen) return;
        _settingsOpen = false;
        Settings.Visibility = Visibility.Collapsed;
        ApplyLayout();
        UpdateClock(animate: false);
        ScheduleSave();
    }

    private void RestoreDefaults()
    {
        var defaults = new AppSettings();
        _settings.FocusMinutes = defaults.FocusMinutes;
        _settings.ShortBreakMinutes = defaults.ShortBreakMinutes;
        _settings.LongBreakMinutes = defaults.LongBreakMinutes;
        _settings.CyclesBeforeLongBreak = defaults.CyclesBeforeLongBreak;
        _settings.AutoStartBreaks = defaults.AutoStartBreaks;
        _settings.AutoStartFocus = defaults.AutoStartFocus;
        _settings.SoundEnabled = defaults.SoundEnabled;
        _settings.VolumePercent = defaults.VolumePercent;
        _settings.NotificationMode = defaults.NotificationMode;
        _settings.PlaySoundInFullscreen = defaults.PlaySoundInFullscreen;
        _settings.AlwaysOnTop = defaults.AlwaysOnTop;
        _settings.ShowSeconds = defaults.ShowSeconds;
        _settings.ShowControlsOnHover = defaults.ShowControlsOnHover;
        _settings.MotivationalQuotes = defaults.MotivationalQuotes;
        _settings.FlipAnimation = defaults.FlipAnimation;
        _settings.OpacityPercent = defaults.OpacityPercent;
        _settings.AnchorCorner = defaults.AnchorCorner;
        _settings.MiniScalePercent = defaults.MiniScalePercent;
        _settings.NormalScalePercent = defaults.NormalScalePercent;
        _settings.HotKeyModifier = defaults.HotKeyModifier;
        _settings.MediaKeyPlayPause = defaults.MediaKeyPlayPause;
        SettingsStore.Save(_settings);
    }

    // =====================================================================
    //  Atajos globales
    // =====================================================================

    private void ConfigureHotKeys()
    {
        _hotKeys?.Dispose();
        _hotKeys = null;

        if (!_settings.HotKeysEnabled)
        {
            Settings.RefreshHints(AutoStart.IsSupported || _settings.StartWithWindows, new List<string>());
            return;
        }

        var ctrlShift = _settings.HotKeyModifier == HotKeyModifier.CtrlShift;
        var mods = ctrlShift ? GlobalHotKeys.ComboCtrlShift : GlobalHotKeys.ComboCtrlAlt;
        var label = ctrlShift ? "Ctrl+Shift" : "Ctrl+Alt";

        try
        {
            _hotKeys = new GlobalHotKeys(new WindowInteropHelper(this).Handle);
            _hotKeys.Register($"{label}+P", 0x50, ToggleTimer, mods);
            _hotKeys.Register($"{label}+R", 0x52, () =>
            {
                _engine.Reset();
                _lastShownSeconds = -1;
                UpdateClock(animate: true);
            }, mods);
            _hotKeys.Register($"{label}+M", 0x4D, ToggleMiniNormal, mods);
            _hotKeys.Register($"{label}+F", 0x46, ToggleFullscreen, mods);
            _hotKeys.Register($"{label}+C", 0x43, RestartCycle, mods);
            _hotKeys.Register($"{label}+N", 0x4E, ToggleNotifications, mods);
            _hotKeys.Register($"{label}+H", 0x48, ToggleVisibility, mods);
            _hotKeys.Register($"{label}+A", 0x41, () =>
            {
                ShowWindow();
                if (_settingsOpen) CloseSettings();
                else OpenSettings();
            }, mods);

            // Esc sale de la pantalla completa. Solo se secuestra mientras dura,
            // para no quitarle la tecla a nadie el resto del tiempo.
            if (_settings.Mode == WindowMode.Fullscreen)
            {
                _hotKeys.Register("Esc", 0x1B, ExitFullscreen, 0);
            }

            // Opcional: la tecla Play/Pausa del teclado. Apagada de fabrica porque
            // se la quita a Spotify, YouTube y demas reproductores.
            if (_settings.MediaKeyPlayPause)
            {
                _hotKeys.Register("Play/Pausa", 0xB3, ToggleTimer, 0);
            }
        }
        catch
        {
            _hotKeys = null;
        }

        Settings.RefreshHints(AutoStart.IsSupported || _settings.StartWithWindows, _hotKeys?.Failed ?? new List<string>());
        Settings.SetShortcutText(label);
    }

    private void ToggleMiniNormal()
    {
        SetMode(_settings.Mode == WindowMode.Mini ? WindowMode.Normal : WindowMode.Mini);
    }

    private void ToggleFullscreen()
    {
        SetMode(_settings.Mode == WindowMode.Fullscreen ? WindowMode.Normal : WindowMode.Fullscreen);
    }

    private void SetMode(WindowMode mode)
    {
        _settings.Mode = mode;
        if (_settingsOpen)
        {
            CloseSettings();
            return;
        }

        ApplyLayout();
    }

    // =====================================================================
    //  Handlers de la interfaz
    // =====================================================================

    private void PlayPause_Click(object sender, RoutedEventArgs e) => ToggleTimer();

    private void Notify_Click(object sender, RoutedEventArgs e) => ToggleNotifications();

    private void Reset_Click(object sender, RoutedEventArgs e)
    {
        _engine.Reset();
        _pendingPhaseStart = true;
        _lastShownSeconds = -1;
        PlanQuotesForSession();
        UpdateClock(animate: true);
    }

    private void ResetAll_Click(object sender, RoutedEventArgs e) => RestartCycle();

    private void Skip_Click(object sender, RoutedEventArgs e)
    {
        _engine.Skip();
        _pendingPhaseStart = true;
        _lastShownSeconds = -1;
        PlanQuotesForSession();
        UpdateClock(animate: true);
    }

    private void Pin_Click(object sender, RoutedEventArgs e)
    {
        _settings.AlwaysOnTop = !_settings.AlwaysOnTop;
    }

    private void Mini_Click(object sender, RoutedEventArgs e)
    {
        if (_settingsOpen) CloseSettings();
        SetMode(WindowMode.Mini);
    }

    private void Normal_Click(object sender, RoutedEventArgs e)
    {
        if (_settingsOpen) CloseSettings();
        SetMode(WindowMode.Normal);
    }

    private void Full_Click(object sender, RoutedEventArgs e)
    {
        if (_settingsOpen) CloseSettings();
        SetMode(_settings.Mode == WindowMode.Fullscreen ? WindowMode.Normal : WindowMode.Fullscreen);
    }

    private void Settings_Click(object sender, RoutedEventArgs e)
    {
        if (_settingsOpen) CloseSettings();
        else OpenSettings();
    }

    private void Hide_Click(object sender, RoutedEventArgs e) => HideToTray(silent: false);

    private void Exit_Click(object sender, RoutedEventArgs e) => ExitApp();
}
