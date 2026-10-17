using System.IO;
using System.Runtime.InteropServices;

namespace PomodoroFlip.Services;

/// <summary>Voz del cuenco: cierre de fase o apertura de la siguiente.</summary>
public enum BowlVoice
{
    Finish,
    StartFocus,
    StartBreak
}

/// <summary>
/// Cuenco tibetano sintetizado, sin archivos de audio.
///
/// Un cuenco no es un "ding": tiene un ataque suave, parciales inarmonicos
/// (relaciones 1 · 2 · 2.92 · 4.15 · 5.43 · 7.10, no multiplos exactos), un
/// batido lento por la ligera desafinacion entre parciales y un decaimiento
/// largo donde los agudos se apagan antes que el fundamental.
///
/// Los dos golpes (cierre y apertura) se mezclan en un solo buffer porque
/// PlaySound no superpone sonidos: si se llamara dos veces, el segundo cortaria
/// al primero.
/// </summary>
public static class SingingBowl
{
    private const int Rate = 44100;
    private const uint SND_ASYNC = 0x0001;
    private const uint SND_MEMORY = 0x0004;
    private const uint SND_NODEFAULT = 0x0002;

    /// <summary>Parcial: relacion con el fundamental, peso y tiempo de caida en segundos.</summary>
    private readonly record struct Partial(double Ratio, double Amplitude, double Decay);

    private static readonly Partial[] Partials =
    {
        new(1.00, 1.00, 5.6),
        new(2.00, 0.44, 4.4),
        new(2.92, 0.32, 3.5),
        new(4.15, 0.20, 2.6),
        new(5.43, 0.12, 1.9),
        new(7.10, 0.07, 1.3)
    };

    /// <summary>Fundamental de cada voz: grave y larga al cerrar, distinta al abrir.</summary>
    private static double BaseFrequency(BowlVoice voice) => voice switch
    {
        BowlVoice.Finish => 196.0,      // G3, la nota del cierre
        BowlVoice.StartFocus => 261.6,  // C4, mas brillante para volver al trabajo
        _ => 174.6                      // F3, mas grave para el descanso
    };

    private static double VoiceGain(BowlVoice voice) => voice switch
    {
        BowlVoice.Finish => 1.0,
        BowlVoice.StartFocus => 0.78,
        _ => 0.85
    };

    private static IntPtr _buffer = IntPtr.Zero;
    private static readonly object Gate = new();

    [DllImport("winmm.dll", SetLastError = true)]
    private static extern bool PlaySound(IntPtr data, IntPtr hMod, uint flags);

    /// <summary>Un solo golpe (por ejemplo, solo el cierre cuando la siguiente fase no arranca sola).</summary>
    public static void Play(BowlVoice voice, int volumePercent)
        => PlaySequence(new[] { (voice, 0.0) }, volumePercent);

    /// <summary>Cierre y apertura seguidos, mezclados en un mismo sonido.</summary>
    public static void PlayPair(BowlVoice first, BowlVoice second, double gapSeconds, int volumePercent)
        => PlaySequence(new[] { (first, 0.0), (second, gapSeconds) }, volumePercent);

    /// <summary>Solo para diagnostico: escribe los sonidos generados en una carpeta.</summary>
    public static void DumpTo(string folder)
    {
        Directory.CreateDirectory(folder);
        File.WriteAllBytes(Path.Combine(folder, "a-cierre.wav"), Build(new[] { (BowlVoice.Finish, 0.0) }, 1.0));
        File.WriteAllBytes(Path.Combine(folder, "b-apertura-foco.wav"), Build(new[] { (BowlVoice.StartFocus, 0.0) }, 1.0));
        File.WriteAllBytes(Path.Combine(folder, "c-transicion.wav"),
            Build(new[] { (BowlVoice.Finish, 0.0), (BowlVoice.StartFocus, 1.7) }, 1.0));
    }

    private static void PlaySequence((BowlVoice Voice, double Start)[] voices, int volumePercent)
    {
        var volume = Math.Clamp(volumePercent, 0, 100) / 100.0;
        if (volume <= 0) return;

        // La sintesis son ~130 ms: se hace en segundo plano para no cortar la
        // animacion del volteo mientras suena.
        Task.Run(() =>
        {
            var wav = Build(voices, volume);

            lock (Gate)
            {
                if (_buffer != IntPtr.Zero)
                {
                    Marshal.FreeHGlobal(_buffer);
                    _buffer = IntPtr.Zero;
                }

                _buffer = Marshal.AllocHGlobal(wav.Length);
                Marshal.Copy(wav, 0, _buffer, wav.Length);
                PlaySound(_buffer, IntPtr.Zero, SND_ASYNC | SND_MEMORY | SND_NODEFAULT);
            }
        });
    }

    /// <summary>Para el diagnostico: reproduce y devuelve si Windows acepto el audio.</summary>
    public static bool PlayAndReport(BowlVoice voice, int volumePercent)
    {
        var volume = Math.Clamp(volumePercent, 0, 100) / 100.0;
        var wav = Build(new[] { (voice, 0.0) }, volume);

        lock (Gate)
        {
            if (_buffer != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(_buffer);
                _buffer = IntPtr.Zero;
            }

            _buffer = Marshal.AllocHGlobal(wav.Length);
            Marshal.Copy(wav, 0, _buffer, wav.Length);
            return PlaySound(_buffer, IntPtr.Zero, SND_ASYNC | SND_MEMORY | SND_NODEFAULT);
        }
    }

    private static byte[] Build((BowlVoice Voice, double Start)[] voices, double volume)
    {
        var tail = Partials.Max(p => p.Decay) + 0.5;
        var length = voices.Max(v => v.Start + tail);
        var samples = new double[(int)(length * Rate) + 1];

        foreach (var (voice, start) in voices)
        {
            AddBowl(samples, BaseFrequency(voice), start, VoiceGain(voice));
        }

        // Normaliza al pico y aplica el volumen. La curva potencia mantiene los
        // valores bajos realmente suaves en lugar de casi lineales.
        var peak = 0.0;
        foreach (var s in samples) peak = Math.Max(peak, Math.Abs(s));
        var gain = peak > 0 ? 0.82 / peak * Math.Pow(volume, 1.3) : 0;

        using var ms = new MemoryStream();
        using var w = new BinaryWriter(ms);
        var dataBytes = samples.Length * 2;

        w.Write(new[] { 'R', 'I', 'F', 'F' });
        w.Write(36 + dataBytes);
        w.Write(new[] { 'W', 'A', 'V', 'E' });
        w.Write(new[] { 'f', 'm', 't', ' ' });
        w.Write(16);
        w.Write((short)1);
        w.Write((short)1);
        w.Write(Rate);
        w.Write(Rate * 2);
        w.Write((short)2);
        w.Write((short)16);
        w.Write(new[] { 'd', 'a', 't', 'a' });
        w.Write(dataBytes);

        foreach (var s in samples)
        {
            var value = Math.Clamp(s * gain, -1, 1);
            w.Write((short)(value * short.MaxValue));
        }

        w.Flush();
        return ms.ToArray();
    }

    /// <summary>Suma un golpe al buffer: cada parcial son dos osciladores levemente desafinados.</summary>
    private static void AddBowl(double[] buffer, double fundamental, double startSeconds, double gain)
    {
        var offset = (int)(startSeconds * Rate);

        foreach (var partial in Partials)
        {
            var f1 = fundamental * partial.Ratio;
            var f2 = f1 * 1.0035;                       // el batido lento del cuenco
            var span = (int)((partial.Decay + 0.5) * Rate);

            for (var i = 0; i < span; i++)
            {
                var index = offset + i;
                if (index < 0 || index >= buffer.Length) break;

                var t = (double)i / Rate;
                var attack = 1 - Math.Exp(-t / 0.045);          // ataque suave, sin golpe
                var decay = Math.Exp(-t / partial.Decay);
                var envelope = attack * decay;

                // Solo se corta cuando el sonido ya decayo de verdad: al principio la
                // envolvente tambien es pequena porque el ataque arranca desde cero.
                if (t > 0.5 && envelope < 0.0004) break;

                var shimmer = 1 + 0.05 * Math.Sin(2 * Math.PI * 4.3 * t);
                var tone = (Math.Sin(2 * Math.PI * f1 * t) + Math.Sin(2 * Math.PI * f2 * t)) * 0.5;

                buffer[index] += tone * envelope * partial.Amplitude * shimmer * gain;
            }
        }
    }
}
