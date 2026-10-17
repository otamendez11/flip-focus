using System.Drawing;
using PomodoroFlip.Models;
using WF = System.Windows.Forms;

namespace PomodoroFlip.Services;

/// <summary>
/// Icono en la bandeja del sistema con menu propio en tema oscuro.
/// Usa el NotifyIcon de WinForms: es el mecanismo nativo y no cuesta RAM apreciable.
/// Los textos se piden a Localization para que el menu siga el idioma elegido.
/// </summary>
public sealed class TrayIcon : IDisposable
{
    private readonly WF.NotifyIcon _icon;
    private readonly WF.ContextMenuStrip _menu;
    private readonly WF.ToolStripMenuItem _toggleItem;
    private readonly WF.ToolStripMenuItem _showItem;
    private readonly WF.ToolStripMenuItem _resetItem;
    private readonly WF.ToolStripMenuItem _notifyItem;
    private readonly WF.ToolStripMenuItem _miniItem;
    private readonly WF.ToolStripMenuItem _normalItem;
    private readonly WF.ToolStripMenuItem _fullItem;
    private readonly WF.ToolStripMenuItem _settingsItem;
    private readonly WF.ToolStripMenuItem _exitItem;

    public TrayIcon()
    {
        _menu = new WF.ContextMenuStrip
        {
            Renderer = new DarkRenderer(),
            ShowImageMargin = false,
            Font = new Font("Segoe UI", 9.5f),
            BackColor = Color.FromArgb(28, 28, 30),
            ForeColor = Color.FromArgb(240, 240, 238)
        };

        _showItem = new WF.ToolStripMenuItem(Lang.Get("L.tray.showhide"), null, (_, _) => ShowHideRequested?.Invoke());
        _toggleItem = new WF.ToolStripMenuItem(Lang.Get("L.menu.play"), null, (_, _) => PlayPauseRequested?.Invoke());
        _resetItem = new WF.ToolStripMenuItem(Lang.Get("L.tray.reset"), null, (_, _) => ResetRequested?.Invoke());
        _miniItem = new WF.ToolStripMenuItem(Lang.Get("L.tray.mini"), null, (_, _) => MiniRequested?.Invoke());
        _normalItem = new WF.ToolStripMenuItem(Lang.Get("L.tray.normal"), null, (_, _) => NormalRequested?.Invoke());
        _fullItem = new WF.ToolStripMenuItem(Lang.Get("L.tray.fullscreen"), null, (_, _) => FullscreenRequested?.Invoke());
        _settingsItem = new WF.ToolStripMenuItem(Lang.Get("L.tray.settings"), null, (_, _) => SettingsRequested?.Invoke());
        _notifyItem = new WF.ToolStripMenuItem(Lang.Get("L.tray.notify"), null, (_, _) => NotificationsToggleRequested?.Invoke())
        {
            CheckOnClick = false
        };
        _exitItem = new WF.ToolStripMenuItem(Lang.Get("L.tray.exit"), null, (_, _) => ExitRequested?.Invoke());

        _menu.Items.AddRange(new WF.ToolStripItem[]
        {
            _showItem,
            new WF.ToolStripSeparator(),
            _toggleItem,
            _resetItem,
            new WF.ToolStripSeparator(),
            _notifyItem,
            new WF.ToolStripSeparator(),
            _miniItem,
            _normalItem,
            _fullItem,
            new WF.ToolStripSeparator(),
            _settingsItem,
            _exitItem
        });

        foreach (WF.ToolStripItem item in _menu.Items)
        {
            item.ForeColor = Color.FromArgb(240, 240, 238);
        }

        _icon = new WF.NotifyIcon
        {
            Icon = AppIcon.Get(32),
            Text = "Flip Focus",
            Visible = true,
            ContextMenuStrip = _menu
        };

        _icon.DoubleClick += (_, _) => ShowHideRequested?.Invoke();
        _icon.MouseClick += (_, e) =>
        {
            if (e.Button == WF.MouseButtons.Left) ShowHideRequested?.Invoke();
        };
    }

    public event Action? ShowHideRequested;
    public event Action? PlayPauseRequested;
    public event Action? ResetRequested;
    public event Action? MiniRequested;
    public event Action? NormalRequested;
    public event Action? FullscreenRequested;
    public event Action? SettingsRequested;
    public event Action? NotificationsToggleRequested;
    public event Action? ExitRequested;

    /// <summary>Marca o desmarca la entrada de avisos del menu de la bandeja.</summary>
    public void SetNotificationsChecked(bool on) => _notifyItem.Checked = on;

    /// <summary>Vuelve a poner los textos del menu en el idioma actual.</summary>
    public void ApplyLanguage()
    {
        _showItem.Text = Lang.Get("L.tray.showhide");
        _resetItem.Text = Lang.Get("L.tray.reset");
        _notifyItem.Text = Lang.Get("L.tray.notify");
        _miniItem.Text = Lang.Get("L.tray.mini");
        _normalItem.Text = Lang.Get("L.tray.normal");
        _fullItem.Text = Lang.Get("L.tray.fullscreen");
        _settingsItem.Text = Lang.Get("L.tray.settings");
        _exitItem.Text = Lang.Get("L.tray.exit");
    }

    public void Update(bool running, PhaseKind phase, string timeText)
    {
        var phaseName = phase switch
        {
            PhaseKind.Focus => Lang.Get("L.tray.phase.focus"),
            PhaseKind.ShortBreak => Lang.Get("L.tray.phase.short"),
            _ => Lang.Get("L.tray.phase.long")
        };

        var text = $"Flip Focus - {timeText} - {phaseName}";
        if (text.Length > 62) text = text[..62];
        _icon.Text = text;
        _toggleItem.Text = Lang.Get(running ? "L.menu.pause" : "L.menu.play");
    }

    public void ShowBalloon(string title, string text)
    {
        try
        {
            _icon.BalloonTipTitle = title;
            _icon.BalloonTipText = text;
            _icon.BalloonTipIcon = WF.ToolTipIcon.Info;
            _icon.ShowBalloonTip(6000);
        }
        catch
        {
            // sin notificaciones del sistema: no es critico
        }
    }

    public void Dispose()
    {
        _icon.Visible = false;
        _icon.Dispose();
        _menu.Dispose();
    }

    /// <summary>Paleta oscura para que el menu de la bandeja no desentone con la app.</summary>
    private sealed class DarkRenderer : WF.ToolStripProfessionalRenderer
    {
        public DarkRenderer() : base(new DarkColors()) => RoundedEdges = false;

        protected override void OnRenderItemText(WF.ToolStripItemTextRenderEventArgs e)
        {
            e.TextColor = e.Item.Selected ? Color.White : Color.FromArgb(232, 232, 230);
            base.OnRenderItemText(e);
        }
    }

    private sealed class DarkColors : WF.ProfessionalColorTable
    {
        public override Color MenuItemSelected => Color.FromArgb(58, 58, 62);
        public override Color MenuItemSelectedGradientBegin => Color.FromArgb(58, 58, 62);
        public override Color MenuItemSelectedGradientEnd => Color.FromArgb(58, 58, 62);
        public override Color MenuItemBorder => Color.FromArgb(70, 70, 74);
        public override Color ToolStripDropDownBackground => Color.FromArgb(28, 28, 30);
        public override Color ImageMarginGradientBegin => Color.FromArgb(28, 28, 30);
        public override Color ImageMarginGradientMiddle => Color.FromArgb(28, 28, 30);
        public override Color ImageMarginGradientEnd => Color.FromArgb(28, 28, 30);
        public override Color SeparatorDark => Color.FromArgb(48, 48, 52);
        public override Color SeparatorLight => Color.FromArgb(48, 48, 52);
    }
}
