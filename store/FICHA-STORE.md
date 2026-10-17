# Ficha para Microsoft Store — Flip Focus

Todo listo para copiar y pegar en el Partner Center. No cambies las mayúsculas de los nombres propios.

## Identidad del producto (lo que te da el Partner Center al reservar el nombre)

```
Identity Name:  (te lo asigna el Partner Center, algo como 12345EduardoOtamendez.FlipFocus)
Publisher:      (te lo asigna el Partner Center, empieza por CN=)
```

Esos dos valores hay que pegármelos en el chat para generar el paquete definitivo.

## Nombre

```
Flip Focus
```

Si está cogido, alternativas por orden:

```
Flip Focus Timer
FlipFocus
Focus Flip
```

## Descripción corta (máximo 270 caracteres)

```
A flip-clock focus timer that lives in the corner of your screen. Small, always on top, under 60 MB, nothing to sign up for. Singing bowl chime and blunt quotes to keep you going.
```

## Descripción larga

```
Flip Focus is a focus timer built around a split-flap clock. The digits really flip, like the departure boards at an old train station.

Leave it small in a corner of your desktop and forget about it until it chimes.

WHAT IT DOES

- Mini mode: a small window anchored to any corner, always on top, that never steals your keyboard focus while you type.
- Fullscreen mode: black screen, very large clock, nothing else.
- Three focus sessions with short breaks, then a long break. The cycle counts itself and starts over when the long break ends.
- A soft singing bowl at the end and at the start of every session, synthesised inside the app.
- Three blunt quotes per session, with their author. They appear, wait a few seconds and leave. Move the mouse over one and it goes.
- Notifications only when you want them: never, only if you close the window, or always.
- Tray icon, global hotkeys and settings applied live.
- English and Spanish, switched in the settings.

WHY IT IS LIGHT

Around 47 MB of memory while idle and 61 MB while running, using under 2 per cent of one processor core. An equivalent app built on a browser engine would use ten times that.

PRIVACY

No account, no telemetry, no advertising, no network access. Your settings live in one file you can delete.

FREE AND OPEN SOURCE

MIT licensed. The source code is public at github.com/otamendez11/flip-focus.
```

## Categoría

```
Productivity
```

## Palabras clave (7 máximo)

```
pomodoro
focus timer
flip clock
productivity
minimal
```

## Requisitos del sistema

```
Windows 10 version 1809 (build 17763) or later
Windows 11
x64
```

## Política de privacidad

```
https://otamendez11.github.io/flip-focus/privacy.html
```

## Sitio web

```
https://otamendez11.github.io/flip-focus/
```

## Soporte

```
https://github.com/otamendez11/flip-focus/issues
```

## Declaración de capacidades

La app declara `runFullTrust`, que es lo normal en apps de escritorio empaquetadas.

Si el Partner Center pregunta por capacidades especiales, la respuesta es que la app
no accede a cámara, micrófono, ubicación, contactos, cuentas ni datos personales.
Escribe exactamente: "The app only runs as a desktop application and stores its
settings locally. No personal data is accessed."

## Capturas para la ficha

Están en el repositorio, en la carpeta `docs/`. Las que valen:

```
01-normal.png      reloj en modo normal
02-mini.png        modo mini en la esquina
03-fullscreen.png  pantalla completa
04-settings.png    ajustes
05-quote.png       una frase en pantalla
```

Microsoft pide al menos una, 1366x768 o mayor. La 01 y la 03 son las más vistosas.

## Notas para el envío

Al subir el paquete, el Partner Center pedirá el archivo `.msix`. Se genera con:

```
.\tools\generar-msix.ps1 -IdentityName "<el tuyo>" -Publisher "<el tuyo>"
```

Y queda en `msix\FlipFocus-1.0.0.0-x64.msix`.

## Punto a comprobar antes de publicar

La opción "Arrancar con Windows" escribe en el registro (`HKCU\...\Run`). En una app
empaquetada como MSIX, Windows puede redirigir esa escritura y no arrancar. Hay que
probarlo en la versión empaquetada antes de dejar la función activa en la ficha, o
quitarla de esa versión. No está verificado: hay que comprobarlo.
