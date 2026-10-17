using System.Diagnostics;
using PomodoroFlip.Models;

namespace PomodoroFlip.Services;

public sealed class PhaseCompletedEventArgs : EventArgs
{
    public PhaseKind Finished { get; init; }
    public PhaseKind Next { get; init; }
    public bool AutoStarted { get; init; }
}

/// <summary>
/// Motor del pomodoro. No usa timers propios: el DispatcherTimer de la ventana
/// llama a Tick() y aquí solo se comparan tiempos con un Stopwatch, que no se
/// desajusta si cambia el reloj del sistema.
/// </summary>
public sealed class PomodoroEngine
{
    private readonly Stopwatch _watch = new();
    private TimeSpan _remaining;
    private TimeSpan _total;
    private bool _running;
    private int _focusDoneInCycle;

    public PomodoroEngine(AppSettings settings)
    {
        Settings = settings;
        Phase = PhaseKind.Focus;
        _total = TimeSpan.FromSeconds(settings.FocusSeconds);
        _remaining = _total;
    }

    public AppSettings Settings { get; }

    public PhaseKind Phase { get; private set; }

    public bool IsRunning => _running;

    /// <summary>Tiempo restante ya descontado (se calcula en vivo, no en cada tick).</summary>
    public TimeSpan Remaining
    {
        get
        {
            if (!_running) return _remaining;
            var left = _remaining - _watch.Elapsed;
            return left > TimeSpan.Zero ? left : TimeSpan.Zero;
        }
    }

    public TimeSpan Total => _total;

    public double Progress
    {
        get
        {
            if (_total.TotalSeconds <= 0) return 0;
            var done = 1 - Remaining.TotalSeconds / _total.TotalSeconds;
            return done < 0 ? 0 : done > 1 ? 1 : done;
        }
    }

    /// <summary>Ciclos de foco completados dentro del ciclo actual (0..N-1).</summary>
    public int FocusDoneInCycle => _focusDoneInCycle;

    public int CycleLength => Settings.CyclesBeforeLongBreak;

    public event EventHandler? StateChanged;
    public event EventHandler<PhaseCompletedEventArgs>? PhaseCompleted;

    public void Start()
    {
        if (_running) return;
        if (_remaining <= TimeSpan.Zero) _remaining = _total;
        _watch.Restart();
        _running = true;
        RaiseState();
    }

    public void Pause()
    {
        if (!_running) return;
        var left = _remaining - _watch.Elapsed;
        _remaining = left > TimeSpan.Zero ? left : TimeSpan.Zero;
        _watch.Reset();
        _running = false;
        RaiseState();
    }

    public void Toggle()
    {
        if (_running) Pause();
        else Start();
    }

    public void Reset()
    {
        _running = false;
        _watch.Reset();
        _total = TimeSpan.FromSeconds(SecondsFor(Phase));
        _remaining = _total;
        RaiseState();
    }

    /// <summary>Reinicia todo el ciclo: vuelve a foco y borra la cuenta de ciclos.</summary>
    public void ResetAll()
    {
        Phase = PhaseKind.Focus;
        _focusDoneInCycle = 0;
        Reset();
    }

    public void Skip()
    {
        var next = NextPhase(Phase);
        GoTo(next, autoStart: false);
    }

    /// <summary>Recalcula duraciones cuando el usuario edita los ajustes.</summary>
    public void ApplySettings(bool resetCurrentPhase)
    {
        if (resetCurrentPhase)
        {
            Reset();
        }
        else
        {
            var seconds = SecondsFor(Phase);
            if (seconds != (int)_total.TotalSeconds)
            {
                var wasRunning = _running;
                var ratio = _total.TotalSeconds <= 0 ? 1 : Remaining.TotalSeconds / _total.TotalSeconds;
                _total = TimeSpan.FromSeconds(seconds);
                _remaining = TimeSpan.FromSeconds(seconds * ratio);
                _running = false;
                _watch.Reset();
                if (wasRunning) Start();
            }
        }
        RaiseState();
    }

    /// <summary>Devuelve true si hay que refrescar la UI (cambió el segundo mostrado o terminó la fase).</summary>
    public bool Tick()
    {
        if (!_running) return false;

        if (Remaining > TimeSpan.Zero) return true;

        _running = false;
        _watch.Reset();
        _remaining = TimeSpan.Zero;

        var finished = Phase;
        var next = AdvanceFrom(finished);
        var autoStart = finished == PhaseKind.Focus ? Settings.AutoStartBreaks : Settings.AutoStartFocus;

        _total = TimeSpan.FromSeconds(SecondsFor(next));
        _remaining = _total;
        Phase = next;

        if (autoStart) Start();
        else RaiseState();

        PhaseCompleted?.Invoke(this, new PhaseCompletedEventArgs
        {
            Finished = finished,
            Next = next,
            AutoStarted = autoStart
        });

        return true;
    }

    public bool PhaseEndedThisTick(TimeSpan previousRemaining) => previousRemaining > TimeSpan.Zero && Remaining <= TimeSpan.Zero;

    /// <summary>
    /// Cierra la fase indicada y devuelve la siguiente. Al terminar el descanso
    /// largo el ciclo vuelve a cero, que es lo que evita que la cuenta se pase
    /// de largo (el clasico "FOCO 6/4").
    /// </summary>
    private PhaseKind AdvanceFrom(PhaseKind finished)
    {
        if (finished == PhaseKind.Focus) _focusDoneInCycle++;

        var next = NextPhase(finished);

        if (finished == PhaseKind.LongBreak) _focusDoneInCycle = 0;

        return next;
    }

    private PhaseKind NextPhase(PhaseKind current)
    {
        if (current != PhaseKind.Focus) return PhaseKind.Focus;
        return _focusDoneInCycle >= Settings.CyclesBeforeLongBreak ? PhaseKind.LongBreak : PhaseKind.ShortBreak;
    }

    private int SecondsFor(PhaseKind phase) => phase switch
    {
        PhaseKind.Focus => Settings.FocusSeconds,
        PhaseKind.ShortBreak => Settings.ShortBreakSeconds,
        _ => Settings.LongBreakSeconds
    };

    private void GoTo(PhaseKind next, bool autoStart)
    {
        if (Phase == PhaseKind.Focus && next != PhaseKind.Focus)
        {
            // Saltar a un descanso cuenta igual que haber terminado el foco.
            _focusDoneInCycle++;
        }

        if (Phase == PhaseKind.LongBreak && next == PhaseKind.Focus)
        {
            // Saltar el descanso largo tambien cierra el ciclo.
            _focusDoneInCycle = 0;
        }

        _total = TimeSpan.FromSeconds(SecondsFor(next));
        _remaining = _total;
        Phase = next;
        _running = false;
        _watch.Reset();
        RaiseState();
        if (autoStart) Start();
    }

    private void RaiseState() => StateChanged?.Invoke(this, EventArgs.Empty);
}
