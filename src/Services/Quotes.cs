using PomodoroFlip.Models;

namespace PomodoroFlip.Services;

/// <summary>Frase con su autor. El autor se muestra siempre debajo.</summary>
public readonly record struct Quote(string Text, string Author);

/// <summary>
/// Frases contundentes para empujar durante la sesion. Todas van con autor:
/// no son inventadas para la app. En ingles van en su idioma original; en espanol,
/// traducidas.
///
/// Procedencia (verificada con fuentes publicas):
///   Senna, Shankly, Jordan, Ali, Kobe Bryant y el discurso de Rocky Balboa
///   estan documentados en recopilaciones y guiones (IMDb, Sky Sports, Billboard).
///   La de Lombardi esta popularizada por el, aunque el original es de Red Sanders.
///   La de "Ganar es ser el primero" circula ampliamente atribuida a Senna, sin
///   entrevista original localizada; se mantiene como atribuida.
/// </summary>
public static class Quotes
{
    private static readonly Quote[] English =
    {
        new("Winning is being first. Everything else is losing.", "Ayrton Senna"),
        new("Being second is to be the first of the ones who lose.", "Ayrton Senna"),
        new("If you no longer go for a gap that exists, you are no longer a racing driver.", "Ayrton Senna"),
        new("If you are first you are first. If you are second, you are nothing.", "Bill Shankly"),
        new("Winning is not the most important thing: it is the only thing.", "Vince Lombardi"),
        new("I have missed more than nine thousand shots. I have lost almost three hundred games. That is why I succeed.", "Michael Jordan"),
        new("Some people want it to happen, some wish it would happen, others make it happen.", "Michael Jordan"),
        new("I hated every minute of training, but I said: do not quit. Suffer now and live the rest of your life as a champion.", "Muhammad Ali"),
        new("It is not about how hard you hit. It is about how hard you can get hit and keep moving forward.", "Rocky Balboa"),
        new("The world is not all sunshine and rainbows. It is a mean and nasty place and it does not care how tough you are.", "Rocky Balboa"),
        new("Rest at the end, not in the middle.", "Kobe Bryant"),
        new("Everything negative, pressure and challenges, is an opportunity for me to rise.", "Kobe Bryant"),
        new("Nobody is coming to save you. Save yourself.", "David Goggins"),
        new("The pain you feel today will be the strength you feel tomorrow.", "Arnold Schwarzenegger")
    };

    private static readonly Quote[] Spanish =
    {
        new("Ganar es ser el primero. Todo lo demas es perder.", "Ayrton Senna"),
        new("Ser segundo es ser el primero de los que pierden.", "Ayrton Senna"),
        new("Si dejas de ir por un hueco que existe, dejas de ser piloto.", "Ayrton Senna"),
        new("Si eres primero, eres primero. Si eres segundo, no eres nada.", "Bill Shankly"),
        new("Ganar no es lo mas importante: es lo unico.", "Vince Lombardi"),
        new("He fallado mas de nueve mil tiros. He perdido casi trescientos partidos. Por eso triunfo.", "Michael Jordan"),
        new("Algunos quieren que pase, otros desean que pase, otros hacen que pase.", "Michael Jordan"),
        new("Odié cada minuto de entrenamiento, pero me dije: no abandones. Sufre ahora y vive el resto de tu vida como campeón.", "Muhammad Ali"),
        new("No importa lo fuerte que golpeas, sino lo fuerte que te golpean y sigues avanzando.", "Rocky Balboa"),
        new("El mundo no es todo sol y arcoiris. Es un lugar sucio y cruel, y no le importa lo duro que seas.", "Rocky Balboa"),
        new("Descansa al final, no a la mitad.", "Kobe Bryant"),
        new("Todo lo negativo, la presión y los retos, es una oportunidad para crecer.", "Kobe Bryant"),
        new("Nadie viene a salvarte. Salvate tu.", "David Goggins"),
        new("El dolor que sientes hoy es la fuerza que tendras manana.", "Arnold Schwarzenegger")
    };

    private static readonly Random Rng = new();

    private static Quote[] Pool => Lang.Current == AppLanguage.Spanish ? Spanish : English;

    public static int Count => Pool.Length;

    /// <summary>Frases distintas para una sesion, sin repetir dentro de la misma.</summary>
    public static List<Quote> PickForSession(int count)
    {
        var pool = Pool.ToList();

        // Barajado Fisher-Yates: reparto uniforme sin repeticiones.
        for (var i = pool.Count - 1; i > 0; i--)
        {
            var j = Rng.Next(i + 1);
            (pool[i], pool[j]) = (pool[j], pool[i]);
        }

        return pool.Take(Math.Clamp(count, 1, pool.Count)).ToList();
    }

    /// <summary>
    /// Reparte las frases en la sesion: una por tercio, en un punto al azar de la
    /// zona central de cada tercio, para que no salgan pegadas ni al principio ni al final.
    /// </summary>
    public static List<(Quote Quote, TimeSpan At, TimeSpan Visible)> PlanSession(int count, TimeSpan total)
    {
        var plan = new List<(Quote, TimeSpan, TimeSpan)>();
        if (total <= TimeSpan.Zero || count <= 0) return plan;

        var picks = PickForSession(count);
        var third = total.TotalSeconds / count;

        for (var i = 0; i < picks.Count; i++)
        {
            var from = (i + 0.28) * third;
            var to = (i + 0.86) * third;
            var second = from + Rng.NextDouble() * Math.Max(1, to - from);
            var visible = 4.5 + Rng.NextDouble() * 2.0;   // entre 4,5 y 6,5 segundos

            plan.Add((picks[i], TimeSpan.FromSeconds(second), TimeSpan.FromSeconds(visible)));
        }

        return plan;
    }
}
