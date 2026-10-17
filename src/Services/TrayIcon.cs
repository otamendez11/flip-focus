using System.Drawing;
using PomodoroFlip.Models;
using WF = System.Windows.Forms;

namespace PomodoroFlip.Services;

/// <summary>
/// Icono en la bandeja del sistema con menu propio en tema oscuro.
/// Usa el NotifyIcon de WinForms: es el mecanismo nativo y no cuesta RAM apreciable.
/// </summary>
public sealed class TrayIcon : IDisposable
{
    private readonly WF.NotifyIcon _icon;
    private readonly WF.ToolStripMenuItem _toggleItem;
    private readonly WF.ToolStripMenuItem _showItem;
    private readonly WF.ToolStripMenuItem _notifyItem;

    public TrayIcon()
    {
        var menu = new WF.ContextMenuStrip
        {
            Renderer = new DarkRenderer(),
            ShowImageMargin = false,
            Font = new Font("Segoe UI", 9.5f),
            BackColor = Color.FromArgb(28, 28, 30),
            ForeColor = Color.FromArgb(240, 240, 238)
        };

        _showItem = new WF.ToolStripMenuItem("Mostrar u ocultar", null, (_, _) => ShowHideRequested?.Invoke());
        _toggleItem = new WF.ToolStripMenuItem("Iniciar", null, (_, _) => PlayPauseRequested?.Invoke());
        var resetItem = new WF.ToolStripMenuItem("Reiniciar fase", null, (_, _) => ResetRequested?.Invoke());
        var miniItem = new WF.ToolStripMenuItem("Modo mini (esquina)", null, (_, _) => MiniRequested?.Invoke());
        var normalItem = new WF.ToolStripMenuItem("Modo normal", null, (_, _) => NormalRequested?.Invoke());
        var fullItem = new WF.ToolStripMenuItem("Pantalla completa", null, (_, _) => FullscreenRequested?.Invoke());
        var settingsItem = new WF.ToolStripMenuItem("Ajustes...", null, (_, _) => SettingsRequested?.Invoke());
        _notifyItem = new WF.ToolStripMenuItem("Avisarme al terminar", null, (_, _) => NotificationsToggleRequested?.Invoke())
        {
            CheckOnClick = false
        };
        var exitItem = new WF.ToolStripMenuItem("Salir", null, (_, _) => ExitRequested?.Invoke());

        menu.Items.AddRange(new WF.ToolStripItem[]
        {
            _showItem,
            new WF.ToolStripSeparator(),
            _toggleItem,
            resetItem,
            new WF.ToolStripSeparator(),
            _notifyItem,
            new WF.ToolStripSeparator(),
            miniItem,
            normalItem,
            fullItem,
            new WF.ToolStripSeparator(),
            settingsItem,
            exitItem
        });

        foreach (WF.ToolStripItem item in menu.Items)
        {
            item.ForeColor = Color.FromArgb(240, 240, 238);
        }

        _icon = new WF.NotifyIcon
        {
            Icon = AppIcon.Get(32),
            Text = "Flip Focus",
            Visible = true,
            ContextMenuStrip = menu
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

    public void Update(bool running, PhaseKind phase, string timeText)
    {
        var phaseName = phase switch
        {
            PhaseKind.Focus => "Foco",
            PhaseKind.ShortBreak => "Descanso",
            _ => "Descanso largo"
        };

        var text = $"Pomodoro Flip - {timeText} - {phaseName}";
        if (text.Length > 62) text = text[..62];
        _icon.Text = text;
        _toggleItem.Text = running ? "Pausar" : "Iniciar";
        _showItem.Text = "Mostrar u ocultar";
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
