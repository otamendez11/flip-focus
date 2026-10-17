using System.Windows;
using System.Windows.Controls;
using PomodoroFlip.Models;
using PomodoroFlip.Services;

namespace PomodoroFlip.Controls;

public partial class SettingsPanel : UserControl
{
    public SettingsPanel()
    {
        InitializeComponent();
        ApplyLanguage();
    }

    public event Action? CloseRequested;
    public event Action? ResetRequested;
    public event Action? RestartCycleRequested;

    private List<string> _failedHotKeys = new();
    private string _modifier = "Ctrl + Alt";
    private int _cycleDone = -1;
    private int _cycleTotal = -1;
    private bool _autoStartSupported = true;

    private AppSettings? S => DataContext as AppSettings;

    /// <summary>Vuelve a poner los textos que se generan desde codigo.</summary>
    public void ApplyLanguage()
    {
        FooterText.Text = Lang.Format("L.set.footer", "1.0", SettingsStore.FilePath);
        RefreshHints(_autoStartSupported, _failedHotKeys);
        SetCycleInfo(_cycleDone < 0 ? 0 : _cycleDone, _cycleTotal < 0 ? 4 : _cycleTotal);
    }

    /// <summary>Refleja en que sesion del ciclo esta el reloj.</summary>
    public void SetCycleInfo(int done, int total)
    {
        if (done == _cycleDone && total == _cycleTotal && CycleStatusText.Text.Length > 0) return;
        _cycleDone = done;
        _cycleTotal = total;

        var current = Math.Min(done + 1, total);
        CycleStatusText.Text = done >= total && total > 0
            ? Lang.Format("L.set.cycle.done", total)
            : Lang.Format("L.set.cycle.status", current, total);
    }

    private void RestartCycle_Click(object sender, RoutedEventArgs e) => RestartCycleRequested?.Invoke();

    /// <summary>Avisos del sistema: autostart no disponible y atajos que no se pudieron registrar.</summary>
    public void RefreshHints(bool autoStartSupported, IReadOnlyList<string> failedHotKeys)
    {
        _autoStartSupported = autoStartSupported;

        if (autoStartSupported)
        {
            AutoStartHint.Text = Lang.Get("L.set.autostart.hint");
        }
        else
        {
            AutoStartHint.Text = Lang.Get("L.set.autostart.unsupported");
            AutoStartToggle.IsEnabled = false;
            AutoStartToggle.Opacity = 0.45;
        }

        _failedHotKeys = failedHotKeys.ToList();
        UpdateHotKeyText();
    }

    /// <summary>Muestra la combinacion activa segun el modificador elegido.</summary>
    public void SetShortcutText(string modifier)
    {
        _modifier = modifier;
        UpdateHotKeyText();
    }

    private void UpdateHotKeyText()
    {
        var combo = Lang.Format("L.set.hotkeys.combo", _modifier);
        HotKeyHint.Text = _failedHotKeys.Count == 0
            ? combo
            : combo + "  ·  " + Lang.Format("L.set.hotkeys.busy", string.Join(", ", _failedHotKeys));
    }

    private void Close_Click(object sender, RoutedEventArgs e) => CloseRequested?.Invoke();

    private void Reset_Click(object sender, RoutedEventArgs e) => ResetRequested?.Invoke();

    private void FocusMinus_Click(object sender, RoutedEventArgs e) => Step(s => s.FocusMinutes -= 5);
    private void FocusPlus_Click(object sender, RoutedEventArgs e) => Step(s => s.FocusMinutes += 5);
    private void ShortMinus_Click(object sender, RoutedEventArgs e) => Step(s => s.ShortBreakMinutes -= 5);
    private void ShortPlus_Click(object sender, RoutedEventArgs e) => Step(s => s.ShortBreakMinutes += 5);
    private void LongMinus_Click(object sender, RoutedEventArgs e) => Step(s => s.LongBreakMinutes -= 5);
    private void LongPlus_Click(object sender, RoutedEventArgs e) => Step(s => s.LongBreakMinutes += 5);
    private void CyclesMinus_Click(object sender, RoutedEventArgs e) => Step(s => s.CyclesBeforeLongBreak -= 1);
    private void CyclesPlus_Click(object sender, RoutedEventArgs e) => Step(s => s.CyclesBeforeLongBreak += 1);
    private void VolumeMinus_Click(object sender, RoutedEventArgs e) => Step(s => s.VolumePercent -= 5);
    private void VolumePlus_Click(object sender, RoutedEventArgs e) => Step(s => s.VolumePercent += 5);
    private void MiniMinus_Click(object sender, RoutedEventArgs e) => Step(s => s.MiniScalePercent -= 2);
    private void MiniPlus_Click(object sender, RoutedEventArgs e) => Step(s => s.MiniScalePercent += 2);
    private void NormalMinus_Click(object sender, RoutedEventArgs e) => Step(s => s.NormalScalePercent -= 5);
    private void NormalPlus_Click(object sender, RoutedEventArgs e) => Step(s => s.NormalScalePercent += 5);
    private void OpacityMinus_Click(object sender, RoutedEventArgs e) => Step(s => s.OpacityPercent -= 5);
    private void OpacityPlus_Click(object sender, RoutedEventArgs e) => Step(s => s.OpacityPercent += 5);

    private void Step(Action<AppSettings> change)
    {
        if (S is null) return;
        change(S);
    }
}
