using System.Windows;
using PomodoroFlip.Models;

namespace PomodoroFlip.Services;

/// <summary>
/// Textos de la interfaz en dos idiomas. Los XAML los leen con DynamicResource,
/// asi que al cambiar el diccionario la ventana se actualiza sola. Los textos que
/// se generan desde codigo (etiqueta del reloj, bandeja, avisos) se piden con Get.
/// </summary>
public static class Lang
{
    private const string EnglishPack = "pack://application:,,,/Resources/Strings.en.xaml";
    private const string SpanishPack = "pack://application:,,,/Resources/Strings.es.xaml";

    private static ResourceDictionary? _loaded;

    public static AppLanguage Current { get; private set; } = AppLanguage.English;

    /// <summary>Se dispara despues de cambiar de idioma, para refrescar lo generado en codigo.</summary>
    public static event Action? Changed;

    public static void Apply(AppLanguage language, bool notify = true)
    {
        var pack = language == AppLanguage.Spanish ? SpanishPack : EnglishPack;
        var dictionary = new ResourceDictionary { Source = new Uri(pack, UriKind.Absolute) };

        var app = Application.Current;
        if (app is not null)
        {
            if (_loaded is not null) app.Resources.MergedDictionaries.Remove(_loaded);
            app.Resources.MergedDictionaries.Add(dictionary);
        }

        _loaded = dictionary;
        var changed = Current != language;
        Current = language;

        if (notify && changed) Changed?.Invoke();
    }

    public static string Get(string key)
        => Application.Current?.TryFindResource(key) as string ?? key;

    public static string Format(string key, params object?[] args)
    {
        var text = Get(key);
        try
        {
            return string.Format(text, args);
        }
        catch
        {
            return text;
        }
    }
}
