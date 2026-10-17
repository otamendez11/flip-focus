FLIP FOCUS
==========

A flip-clock focus timer for Windows. Small window in the corner of your screen,
always on top, around 60 MB of memory. No account, no telemetry, no ads.

--------------------------------------------------------------------------
HOW TO RUN IT
--------------------------------------------------------------------------
1. Unzip this folder anywhere you like.
2. Double click FlipFocus.exe.

That is it. The app is self-contained: you do not need to install .NET.

The first time, Windows may show a blue warning because the app is not signed
yet. Click "More info" and then "Run anyway". It will not ask again.

To have it always at hand, right click FlipFocus.exe and choose "Pin to Start",
or make a shortcut wherever you prefer.

--------------------------------------------------------------------------
THE THREE SIZES
--------------------------------------------------------------------------
  Mini        Small window anchored to a corner, always on top.
  Normal      Comfortable size to glance at.
  Fullscreen  Black screen, very large clock.

Switch with the buttons in the top right corner (they appear when you move the
mouse over the window), from the tray icon menu, or with the shortcuts.

--------------------------------------------------------------------------
SHORTCUTS
--------------------------------------------------------------------------
With the window focused:

    Space      start or pause
    Esc        leave fullscreen
    F          fullscreen
    M / N      mini / normal
    A          settings
    R          restart the current phase
    S          skip phase
    C          restart the cycle (back to session 1)

From any program (the modifier can be changed to Ctrl+Shift in settings):

    Ctrl + Alt + P    start or pause
    Ctrl + Alt + M    mini / normal
    Ctrl + Alt + F    fullscreen
    Ctrl + Alt + C    restart cycle
    Ctrl + Alt + A    settings
    Ctrl + Alt + R    restart phase
    Ctrl + Alt + N    notifications on or off
    Ctrl + Alt + H    hide or show the window

Mouse: click the clock to start or pause, drag it to move it, right click for the
full menu, click the taskbar icon to bring it back or minimise it.

--------------------------------------------------------------------------
LANGUAGE
--------------------------------------------------------------------------
The interface comes in English. If you prefer Spanish, open Settings, section
LANGUAGE, and pick "Espanol". It changes straight away.

--------------------------------------------------------------------------
SETTINGS
--------------------------------------------------------------------------
Gear button, Ctrl + Alt + A, or the right click menu.

    TIMES        Focus 25 min, short break 5, long break 15, sessions per
                 cycle, and a button to start the cycle over.
    BEHAVIOUR    Chain breaks and focus, singing bowl and volume, blunt
                 quotes, notifications.
    WINDOW       Mode, anchor corner, sizes, opacity, always on top.
    CLOCK        Seconds, flip effect, controls on hover.
    SYSTEM       Start with Windows, global shortcuts, Play/Pause key.

Settings are stored in %APPDATA%\FlipFocus\settings.json and are saved as you
change them.

--------------------------------------------------------------------------
SOUND AND NOTIFICATIONS
--------------------------------------------------------------------------
The chime is a singing bowl synthesised by the app itself, no audio files: a low
note when a session ends and another when the next one starts. It plays soft and
low (35 per cent by default).

Notifications have three states: never, only when the window is hidden in the
tray (the useful one: close it, go do something else, get told when the session
ends), or always.

--------------------------------------------------------------------------
THE CYCLE
--------------------------------------------------------------------------
A cycle is several focus sessions (4 by default) separated by short breaks.
After the last one comes the long break, and when that break ends the count
starts again by itself.

The dots under the clock show how many sessions of the cycle you have done, and
the label tells you which one you are on (for example "FOCUS 2/4").

To start over early: press C, or Ctrl + Alt + C, or use "Start over" in Settings,
or the right click menu.

--------------------------------------------------------------------------
RESOURCE USE
--------------------------------------------------------------------------
Measured on an 8-core Windows 11 machine, mini mode:

    idle ................ 47 MB   0.1 % of the processor
    running, flip on .... 61 MB   1.8 %
    running, flip off ... 58 MB   0.8 %

The flip effect and showing seconds are the only things that use the processor,
because they redraw the clock. Turn both off in Settings, section CLOCK, for the
minimum.

For comparison: an equivalent app built on a browser engine (Electron or
WebView2) would use between 250 and 400 MB.

--------------------------------------------------------------------------
UNINSTALL
--------------------------------------------------------------------------
It was not installed, so there is nothing to remove: quit from the tray menu and
delete this folder. If you enabled "Start with Windows", turn it off first, and
you can also delete %APPDATA%\FlipFocus, where your settings live.
