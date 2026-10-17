using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;

namespace PomodoroFlip.Controls;

/// <summary>Medidas base del reloj. Todo el escalado se hace con un LayoutTransform global.</summary>
public static class ClockMetrics
{
    public const double CardHeight = 240;
    public const double CutHeight = 2;
    public const double HalfHeight = (CardHeight - CutHeight) / 2;

    /// <summary>Ancho reservado por digito dentro de una tarjeta.</summary>
    public const double DigitWidth = 112;
    public const double CardWidth = DigitWidth * 2;
    public const double CardGap = 18;
    public const string FontStack = "Segoe UI Variable Display, Segoe UI, Arial";
    public const double CornerRadius = 22;

    /// <summary>Compresion horizontal suave del digito (look display del diseno).</summary>
    public const double DigitSqueeze = 0.9;

    public static readonly FontFamily FontFamily = new(FontStack);
}

/// <summary>
/// Una tarjeta del reloj flip: puede llevar uno o dos digitos (la tarjeta entera
/// voltea, como un flip clock de verdad). Se compone de cuatro capas: mitad
/// superior e inferior estaticas mas dos hojas con bisagra central.
/// </summary>
public sealed class FlipCard : Grid
{
    private readonly Border _topStatic;
    private readonly Border _bottomStatic;
    private readonly Border _topLeaf;
    private readonly Border _bottomLeaf;
    private readonly TextBlock _topStaticText;
    private readonly TextBlock _bottomStaticText;
    private readonly TextBlock _topLeafText;
    private readonly TextBlock _bottomLeafText;
    private readonly ScaleTransform _topScale = new(1, 1);
    private readonly ScaleTransform _bottomScale = new(1, 1);
    private readonly Rectangle _topShade;
    private readonly Rectangle _bottomShade;

    private readonly SolidColorBrush _cardBrush;
    private readonly SolidColorBrush _digitBrush;
    private readonly bool _roundLeft;
    private readonly bool _roundRight;
    private readonly int _digits;
    private readonly double _width;
    private int _value = -1;

    public FlipCard(int digits, bool roundLeft, bool roundRight)
    {
        _digits = Math.Clamp(digits, 1, 3);
        _roundLeft = roundLeft;
        _roundRight = roundRight;
        _width = ClockMetrics.DigitWidth * _digits;
        _cardBrush = new SolidColorBrush(Color.FromRgb(28, 28, 30));
        _digitBrush = new SolidColorBrush(Color.FromRgb(242, 242, 240));

        Width = _width;
        Height = ClockMetrics.CardHeight;
        ClipToBounds = true;
        SnapsToDevicePixels = true;
        UseLayoutRounding = true;

        var (fontSize, glyphOffset) = ComputeTypography(_digits, _width);

        // --- mitad superior estatica (valor nuevo) ---
        _topStaticText = BuildDigitText(fontSize, glyphOffset);
        _topStatic = BuildHalf(isTop: true, _topStaticText);

        // --- mitad inferior estatica (valor viejo hasta que la tapa la hoja) ---
        _bottomStaticText = BuildDigitText(fontSize, glyphOffset);
        _bottomStatic = BuildHalf(isTop: false, _bottomStaticText);

        // --- hoja superior: cae mostrando el valor viejo ---
        _topLeafText = BuildDigitText(fontSize, glyphOffset);
        var topLeafInner = BuildHalf(isTop: true, _topLeafText);
        _topShade = BuildShade(isTop: true);
        var topLeafStack = new Grid { Width = _width, Height = ClockMetrics.HalfHeight };
        topLeafStack.Children.Add(topLeafInner);
        topLeafStack.Children.Add(_topShade);
        _topLeaf = BuildHalf(isTop: true, topLeafStack, withShadow: false);
        _topLeaf.RenderTransformOrigin = new Point(0.5, 1);
        _topLeaf.RenderTransform = _topScale;

        // --- hoja inferior: sube mostrando el valor nuevo ---
        _bottomLeafText = BuildDigitText(fontSize, glyphOffset);
        var bottomLeafInner = BuildHalf(isTop: false, _bottomLeafText);
        _bottomShade = BuildShade(isTop: false);
        var bottomLeafStack = new Grid { Width = _width, Height = ClockMetrics.HalfHeight };
        bottomLeafStack.Children.Add(bottomLeafInner);
        bottomLeafStack.Children.Add(_bottomShade);
        _bottomLeaf = BuildHalf(isTop: false, bottomLeafStack, withShadow: false);
        _bottomLeaf.RenderTransformOrigin = new Point(0.5, 0);
        _bottomLeaf.RenderTransform = _bottomScale;

        _bottomLeaf.Opacity = 0;
        _topShade.Opacity = 0;
        _bottomShade.Opacity = 0;

        // Contenedor con la linea de corte central (el "corte" del flip clock)
        var stack = new Grid { Width = _width, Height = ClockMetrics.CardHeight };
        stack.RowDefinitions.Add(new RowDefinition { Height = new GridLength(ClockMetrics.HalfHeight) });
        stack.RowDefinitions.Add(new RowDefinition { Height = new GridLength(ClockMetrics.CutHeight) });
        stack.RowDefinitions.Add(new RowDefinition { Height = new GridLength(ClockMetrics.HalfHeight) });

        Grid.SetRow(_topStatic, 0);
        Grid.SetRow(_bottomStatic, 2);
        Grid.SetRow(_topLeaf, 0);
        Grid.SetRow(_bottomLeaf, 2);

        var cut = new Rectangle
        {
            Fill = new SolidColorBrush(Color.FromRgb(9, 9, 11)),
            Height = ClockMetrics.CutHeight,
            Width = _width
        };
        Grid.SetRow(cut, 1);

        stack.Children.Add(_topStatic);
        stack.Children.Add(_bottomStatic);
        stack.Children.Add(cut);
        stack.Children.Add(_topLeaf);
        stack.Children.Add(_bottomLeaf);

        Children.Add(stack);

        Settle();
    }

    public int Digits => _digits;

    /// <summary>Color de la tarjeta y del digito; el cambio entre fases se anima.</summary>
    public void ApplyColors(Color card, Color digit, bool animated)
    {
        if (!animated)
        {
            _cardBrush.Color = card;
            _digitBrush.Color = digit;
            return;
        }

        var duration = TimeSpan.FromMilliseconds(450);
        var ease = new CubicEase { EasingMode = EasingMode.EaseInOut };
        _cardBrush.BeginAnimation(SolidColorBrush.ColorProperty,
            new ColorAnimation(card, duration) { EasingFunction = ease });
        _digitBrush.BeginAnimation(SolidColorBrush.ColorProperty,
            new ColorAnimation(digit, duration) { EasingFunction = ease });
    }

    public int Value => _value;

    public void SetValue(int value, bool animate)
    {
        var max = (int)Math.Pow(10, _digits) - 1;
        value = Math.Clamp(value, 0, max);

        if (value == _value)
        {
            if (!animate) Settle();
            return;
        }

        if (!animate || _value < 0)
        {
            _value = value;
            Settle();
            return;
        }

        var old = _value;
        _value = value;

        var newText = Format(value);
        var oldText = Format(old);

        // Estado final inmediato por debajo de las hojas
        _topStaticText.Text = newText;
        _bottomLeafText.Text = newText;

        // Hojas: la de arriba cae mostrando el valor viejo, la de abajo sube con el nuevo
        _topLeafText.Text = oldText;
        _bottomStaticText.Text = oldText;

        _topLeaf.Visibility = Visibility.Visible;
        _bottomLeaf.Visibility = Visibility.Visible;
        _bottomLeaf.Opacity = 1;

        const double topMs = 130;
        const double bottomMs = 150;
        var ease = new CubicEase { EasingMode = EasingMode.EaseIn };

        var topFall = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(topMs)) { EasingFunction = ease };
        var shadeUp = new DoubleAnimation(0, 0.92, TimeSpan.FromMilliseconds(topMs)) { EasingFunction = ease };
        var bottomRise = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(bottomMs))
        {
            BeginTime = TimeSpan.FromMilliseconds(topMs - 12),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };
        var shadeDown = new DoubleAnimation(0.92, 0, TimeSpan.FromMilliseconds(bottomMs))
        {
            BeginTime = TimeSpan.FromMilliseconds(topMs - 12),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };

        bottomRise.Completed += (_, _) => Settle();

        _topScale.BeginAnimation(ScaleTransform.ScaleYProperty, topFall);
        _bottomScale.BeginAnimation(ScaleTransform.ScaleYProperty, bottomRise);
        _topShade.BeginAnimation(OpacityProperty, shadeUp);
        _bottomShade.BeginAnimation(OpacityProperty, shadeDown);
    }

    /// <summary>Fija el estado estable: hojas ocultas y valores definitivos.</summary>
    public void Settle()
    {
        _topScale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
        _bottomScale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
        _topShade.BeginAnimation(OpacityProperty, null);
        _bottomShade.BeginAnimation(OpacityProperty, null);

        _topScale.ScaleY = 1;
        _bottomScale.ScaleY = 1;
        _topShade.Opacity = 0;
        _bottomShade.Opacity = 0;
        _topLeaf.Visibility = Visibility.Collapsed;
        _bottomLeaf.Visibility = Visibility.Collapsed;

        var text = Format(_value < 0 ? 0 : _value);
        _topStaticText.Text = text;
        _bottomStaticText.Text = text;
        _topLeafText.Text = text;
        _bottomLeafText.Text = text;
    }

    private string Format(int value) => value.ToString("D" + _digits, CultureInfo.InvariantCulture);

    private TextBlock BuildDigitText(double fontSize, double glyphOffset) => new()
    {
        Text = new string('0', _digits),
        FontFamily = ClockMetrics.FontFamily,
        FontWeight = FontWeights.Black,
        FontSize = fontSize,
        Foreground = _digitBrush,
        TextAlignment = TextAlignment.Center,
        HorizontalAlignment = HorizontalAlignment.Center,
        VerticalAlignment = VerticalAlignment.Center,
        Width = _width,
        Height = ClockMetrics.CardHeight,
        Margin = new Thickness(0),
        // RenderTransform (no LayoutTransform): comprime el digito sin tocar el layout
        // y lo baja para que el centro visual del glifo caiga en el corte central.
        RenderTransform = new TransformGroup
        {
            Children =
            {
                new ScaleTransform(ClockMetrics.DigitSqueeze, 1),
                new TranslateTransform(0, glyphOffset)
            }
        },
        RenderTransformOrigin = new Point(0.5, 0.5),
        IsHitTestVisible = false
    };

    /// <summary>Media tarjeta: recorta el texto a la mitad superior o inferior.</summary>
    private Border BuildHalf(bool isTop, UIElement content, bool withShadow = true)
    {
        var corner = ClockMetrics.CornerRadius;
        var radius = new CornerRadius(
            isTop && _roundLeft ? corner : 0,
            isTop && _roundRight ? corner : 0,
            !isTop && _roundRight ? corner : 0,
            !isTop && _roundLeft ? corner : 0);

        var inner = new Grid
        {
            Height = ClockMetrics.CardHeight,
            Width = _width,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(0, isTop ? 0 : -ClockMetrics.HalfHeight, 0, 0)
        };
        inner.Children.Add(content);

        if (withShadow)
        {
            // Sombra muy leve en la mitad inferior: profundidad bajo el corte.
            var shade = new Rectangle
            {
                Height = ClockMetrics.HalfHeight,
                Width = _width,
                VerticalAlignment = VerticalAlignment.Bottom,
                IsHitTestVisible = false
            };
            var grad = new LinearGradientBrush
            {
                StartPoint = new Point(0.5, 0),
                EndPoint = new Point(0.5, 1)
            };
            grad.GradientStops.Add(new GradientStop(Color.FromArgb(isTop ? (byte)0 : (byte)26, 0, 0, 0), 0));
            grad.GradientStops.Add(new GradientStop(Color.FromArgb(0, 0, 0, 0), 1));
            shade.Fill = grad;
            inner.Children.Add(shade);
        }

        return new Border
        {
            Width = _width,
            Height = ClockMetrics.HalfHeight,
            Background = _cardBrush,
            CornerRadius = radius,
            ClipToBounds = true,
            SnapsToDevicePixels = true,
            Child = inner
        };
    }

    private Rectangle BuildShade(bool isTop)
    {
        var rect = new Rectangle
        {
            Height = ClockMetrics.HalfHeight + 1,
            Width = _width,
            VerticalAlignment = isTop ? VerticalAlignment.Top : VerticalAlignment.Bottom,
            IsHitTestVisible = false
        };
        var grad = new LinearGradientBrush
        {
            StartPoint = new Point(0.5, isTop ? 1 : 0),
            EndPoint = new Point(0.5, isTop ? 0 : 1)
        };
        grad.GradientStops.Add(new GradientStop(Color.FromArgb(230, 0, 0, 0), 0));
        grad.GradientStops.Add(new GradientStop(Color.FromArgb(0, 0, 0, 0), 1));
        rect.Fill = grad;
        return rect;
    }

    /// <summary>
    /// Tamano de fuente (el mayor que quepa de alto y de ancho) y desplazamiento
    /// vertical para que el centro visual del glifo coincida con el corte central.
    /// </summary>
    private static (double FontSize, double OffsetY) ComputeTypography(int digits, double width)
    {
        var probe = new string('8', digits);
        var typeface = new Typeface(ClockMetrics.FontFamily, FontStyles.Normal, FontWeights.Black, FontStretches.Normal);

        FormattedText Make(double size) => new(
            probe,
            CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight,
            typeface,
            size,
            Brushes.White,
            1.0);

        var probe100 = Make(100);
        var widthPer100 = probe100.WidthIncludingTrailingWhitespace * ClockMetrics.DigitSqueeze;

        var byHeight = 100 * (ClockMetrics.CardHeight * 0.82) / probe100.Height;
        var byWidth = 100 * (width - 12) / widthPer100;
        var fontSize = Math.Min(byHeight, byWidth);

        var measured = Make(fontSize);
        var geometry = measured.BuildGeometry(new Point(0, 0));
        var bounds = geometry.Bounds;
        var glyphCenter = (bounds.Top + bounds.Bottom) / 2;
        var offset = measured.Height / 2 - glyphCenter;

        return (fontSize, offset);
    }
}
