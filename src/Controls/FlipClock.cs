using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using PomodoroFlip.Models;

namespace PomodoroFlip.Controls;

/// <summary>
/// El reloj completo: tarjeta de minutos, tarjeta de segundos, etiqueta de fase
/// (en el sitio del "PM" del diseno), barra de progreso y contador de ciclos.
/// </summary>
public sealed class FlipClock : Grid
{
    private readonly FlipCard _minutes = new(digits: 2, roundLeft: true, roundRight: true);
    private readonly FlipCard _seconds = new(digits: 2, roundLeft: true, roundRight: true);
    private readonly TextBlock _phaseLabel;
    private readonly Grid _secondsHost;
    private readonly Rectangle _progressFill;
    private readonly Rectangle _progressTrack;
    private readonly StackPanel _dots;
    private readonly Grid _progressRow;
    private readonly ScaleTransform _progressScale = new(0, 1);

    private bool _showSeconds = true;
    private int _cycleDots = -1;
    private int _cycleDone;
    private double _lastProgress = -1;

    public static readonly Color FocusCard = Color.FromRgb(28, 28, 30);
    public static readonly Color FocusDigit = Color.FromRgb(242, 242, 240);
    public static readonly Color BreakCard = Color.FromRgb(21, 33, 27);
    public static readonly Color BreakDigit = Color.FromRgb(233, 247, 238);
    public static readonly Color LongBreakCard = Color.FromRgb(20, 27, 38);
    public static readonly Color LongBreakDigit = Color.FromRgb(230, 240, 251);
    public static readonly Color PausedDigit = Color.FromRgb(158, 158, 156);
    public static readonly Color FocusAccent = Color.FromRgb(242, 242, 240);
    public static readonly Color BreakAccent = Color.FromRgb(111, 211, 160);
    public static readonly Color LongBreakAccent = Color.FromRgb(111, 176, 232);

    public FlipClock()
    {
        Height = ClockMetrics.CardHeight + 24;
        SnapsToDevicePixels = true;
        UseLayoutRounding = true;

        _phaseLabel = new TextBlock
        {
            Text = "LISTO",
            FontFamily = ClockMetrics.FontFamily,
            FontSize = 15,
            FontWeight = FontWeights.SemiBold,
            Foreground = new SolidColorBrush(Color.FromArgb(190, 242, 242, 240)),
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Bottom,
            Margin = new Thickness(18, 0, 0, 16),
            IsHitTestVisible = false
        };

        var minutesHost = new Grid { Width = ClockMetrics.CardWidth, Height = ClockMetrics.CardHeight };
        minutesHost.Children.Add(_minutes);
        minutesHost.Children.Add(_phaseLabel);

        _secondsHost = new Grid
        {
            Width = ClockMetrics.CardWidth,
            Height = ClockMetrics.CardHeight,
            Margin = new Thickness(ClockMetrics.CardGap, 0, 0, 0)
        };
        _secondsHost.Children.Add(_seconds);

        var cards = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Height = ClockMetrics.CardHeight,
            VerticalAlignment = VerticalAlignment.Top
        };
        cards.Children.Add(minutesHost);
        cards.Children.Add(_secondsHost);

        // --- fila inferior: progreso + puntos de ciclo ---
        _progressTrack = new Rectangle
        {
            Height = 3,
            RadiusX = 2,
            RadiusY = 2,
            Fill = new SolidColorBrush(Color.FromArgb(28, 255, 255, 255)),
            VerticalAlignment = VerticalAlignment.Top
        };

        _progressFill = new Rectangle
        {
            Height = 3,
            RadiusX = 2,
            RadiusY = 2,
            Fill = new SolidColorBrush(FocusAccent),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Top,
            RenderTransform = _progressScale,
            RenderTransformOrigin = new Point(0, 0.5)
        };

        _dots = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Bottom,
            Margin = new Thickness(0, 0, 2, 0)
        };

        _progressRow = new Grid { Margin = new Thickness(0, 9, 0, 0) };
        _progressRow.Children.Add(_progressTrack);
        _progressRow.Children.Add(_progressFill);
        _progressRow.Children.Add(_dots);

        RowDefinitions.Add(new RowDefinition { Height = new GridLength(ClockMetrics.CardHeight) });
        RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        SetRow(cards, 0);
        SetRow(_progressRow, 1);
        Children.Add(cards);
        Children.Add(_progressRow);

        SetTime(25 * 60, animate: false, showSeconds: true);
    }

    public void SetTime(int totalSeconds, bool animate, bool showSeconds)
    {
        if (totalSeconds < 0) totalSeconds = 0;
        var minutes = totalSeconds / 60;
        var seconds = totalSeconds % 60;

        if (minutes > 99)
        {
            minutes = 99;
            seconds = 59;
        }

        _minutes.SetValue(minutes, animate);

        if (showSeconds != _showSeconds)
        {
            _showSeconds = showSeconds;
            _secondsHost.Visibility = showSeconds ? Visibility.Visible : Visibility.Collapsed;
        }

        if (showSeconds) _seconds.SetValue(seconds, animate);
    }

    public void SetPhase(PhaseKind phase, bool paused, bool animate)
    {
        var card = phase switch
        {
            PhaseKind.Focus => FocusCard,
            PhaseKind.ShortBreak => BreakCard,
            _ => LongBreakCard
        };

        var digit = phase switch
        {
            PhaseKind.Focus => FocusDigit,
            PhaseKind.ShortBreak => BreakDigit,
            _ => LongBreakDigit
        };

        var accent = phase switch
        {
            PhaseKind.Focus => FocusAccent,
            PhaseKind.ShortBreak => BreakAccent,
            _ => LongBreakAccent
        };

        if (paused) digit = Blend(digit, PausedDigit, 0.72);

        _minutes.ApplyColors(card, digit, animate);
        _seconds.ApplyColors(card, digit, animate);

        _phaseLabel.Foreground = new SolidColorBrush(Color.FromArgb(paused ? (byte)145 : (byte)210, digit.R, digit.G, digit.B));
        SetAccent(accent);
    }

    /// <summary>Texto pequeno de la esquina (donde el "PM" del diseno).</summary>
    public void SetLabel(string text)
    {
        if (_phaseLabel.Text != text) _phaseLabel.Text = text;
    }

    private void SetAccent(Color accent)
    {
        if (_progressFill.Fill is SolidColorBrush b && b.Color == accent) return;
        _progressFill.Fill = new SolidColorBrush(accent);
    }

    public void SetProgress(double progress)
    {
        progress = Math.Clamp(progress, 0, 1);
        if (Math.Abs(progress - _lastProgress) < 0.0015) return;
        _lastProgress = progress;

        var anim = new DoubleAnimation(progress, TimeSpan.FromMilliseconds(240))
        {
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
        };

        // Al terminar hay que soltar el reloj de animacion: si se queda con
        // FillBehavior, WPF sigue repintando la ventana a 60 fps para siempre
        // (era la causa del consumo de CPU en reposo).
        anim.Completed += (_, _) =>
        {
            _progressScale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
            _progressScale.ScaleX = progress;
        };

        _progressScale.BeginAnimation(ScaleTransform.ScaleXProperty, anim);
    }

    public void SetCycle(int done, int total)
    {
        if (total == _cycleDots && done == _cycleDone) return;
        _cycleDone = done;

        if (total != _cycleDots)
        {
            _cycleDots = total;
            _dots.Children.Clear();
            for (var i = 0; i < total; i++)
            {
                var dot = new Ellipse
                {
                    Width = 5,
                    Height = 5,
                    Margin = new Thickness(3, 0, 0, 0),
                    Fill = new SolidColorBrush(Color.FromArgb(38, 255, 255, 255))
                };
                _dots.Children.Add(dot);
            }
        }

        UpdateDots();
    }

    private void UpdateDots()
    {
        if (_dots.Children.Count == 0) return;

        var accent = _progressFill.Fill is SolidColorBrush b ? b.Color : FocusAccent;
        for (var i = 0; i < _dots.Children.Count; i++)
        {
            if (_dots.Children[i] is Ellipse dot)
            {
                dot.Fill = new SolidColorBrush(i < _cycleDone ? accent : Color.FromArgb(38, 255, 255, 255));
            }
        }
    }

    public void SetProgressVisible(bool visible)
        => _progressRow.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;

    private static Color Blend(Color a, Color b, double amount)
    {
        amount = Math.Clamp(amount, 0, 1);
        return Color.FromRgb(
            (byte)(a.R + (b.R - a.R) * amount),
            (byte)(a.G + (b.G - a.G) * amount),
            (byte)(a.B + (b.B - a.B) * amount));
    }
}
