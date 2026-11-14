using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace ProWrestlingRevolution;

/// <summary>
/// Restrained gunmetal chrome for the fixed-size game shell. These controls
/// draw no title-screen artwork and use the selected skin's accent colors.
/// </summary>
internal static class MetalColors
{
    internal static Color Mix(Color first, Color second, double amount)
    {
        amount = Math.Clamp(amount, 0, 1);
        return Color.FromArgb(
            (int)Math.Round(first.R * (1 - amount) + second.R * amount),
            (int)Math.Round(first.G * (1 - amount) + second.G * amount),
            (int)Math.Round(first.B * (1 - amount) + second.B * amount));
    }

    internal static Color SidebarLeft(SkinPalette palette) =>
        Mix(palette.Sidebar, Color.FromArgb(5, 13, 25), 0.72);

    internal static Color SidebarRight(SkinPalette palette) =>
        Mix(palette.Sidebar, Color.FromArgb(12, 33, 57), 0.62);

    internal static Color TopRight(SkinPalette palette) =>
        Mix(palette.Surface, Color.FromArgb(7, 19, 35), 0.78);
}

internal sealed class MetalTopBar : Panel
{
    private SkinPalette _palette;

    internal MetalTopBar(SkinPalette palette)
    {
        _palette = palette;
        Dock = DockStyle.Top;
        Height = 56;
        DoubleBuffered = true;
        ResizeRedraw = true;
    }

    internal void UpdatePalette(SkinPalette palette)
    {
        _palette = palette;
        Invalidate(true);
    }

    protected override void OnPaintBackground(PaintEventArgs e)
    {
        // Every interior page uses exactly the same full-width chrome,
        // including the real sound/help/minimize/close button area.
        PwrSharedHeader.PaintBackdrop(e.Graphics, ClientRectangle);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        // PWR branding and page context are painted by PwrSharedHeader.
        // The red legacy emblem and independent divider no longer exist.
    }
}

internal sealed class MetalSidebar : Panel
{
    private SkinPalette _palette;

    internal MetalSidebar(SkinPalette palette)
    {
        _palette = palette;
        Dock = DockStyle.Left;
        Width = 200;
        DoubleBuffered = true;
        ResizeRedraw = true;
    }

    internal void UpdatePalette(SkinPalette palette)
    {
        _palette = palette;
        Invalidate(true);
    }

    protected override void OnPaintBackground(PaintEventArgs e)
    {
        if (Width <= 0 || Height <= 0) return;
        using var brushed = new LinearGradientBrush(ClientRectangle,
            MetalColors.SidebarLeft(_palette),
            MetalColors.SidebarRight(_palette),
            LinearGradientMode.Horizontal);
        e.Graphics.FillRectangle(brushed, ClientRectangle);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;

        // Geometric, translucent steel facet below the seven menu entries.
        using var facet = new SolidBrush(Color.FromArgb(44, 30, 66, 105));
        g.FillPolygon(facet,
        [
            new Point(0, Height - 1),
            new Point(Width - 1, Height - 182),
            new Point(Width - 1, Height - 1)
        ]);
        using var facetEdge = new Pen(Color.FromArgb(44, 111, 150, 190), 1);
        g.DrawLine(facetEdge, 0, Height - 1, Width - 1, Height - 182);

        using var border = new Pen(Color.FromArgb(100, _palette.Border), 1);
        using var coolEdge = new Pen(Color.FromArgb(35, _palette.AccentSecondary), 1);
        g.DrawLine(border, Width - 1, 0, Width - 1, Height - 1);
        g.DrawLine(coolEdge, 0, 0, 0, Height - 1);
    }
}

/// <summary>Keyboard-accessible, independently clickable chrome action.</summary>
internal sealed class TopUtilityButton : Control
{
    private readonly string _kind;
    private SkinPalette _palette;
    private bool _hovered;
    private bool _soundEnabled = true;
    internal TopUtilityButton(string kind, SkinPalette palette)
    {
        _kind = kind;
        _palette = palette;
        Size = new Size(50, 56);
        BackColor = Color.Transparent;
        Cursor = Cursors.Hand;
        TabStop = true;
        AccessibleRole = AccessibleRole.PushButton;
        AccessibleName = kind switch
        {
            "sound" => "Toggle sound",
            "help" => "Help",
            "minimize" => "Minimize",
            "close" => "Close",
            _ => kind
        };
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer | ControlStyles.Selectable |
            ControlStyles.SupportsTransparentBackColor, true);
    }

    internal void UpdatePalette(SkinPalette palette)
    {
        _palette = palette;
        Invalidate();
    }

    internal void SetSoundEnabled(bool enabled)
    {
        _soundEnabled = enabled;
        Invalidate();
    }

    protected override void OnMouseEnter(EventArgs e)
    {
        base.OnMouseEnter(e);
        _hovered = true;
        Invalidate();
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        _hovered = false;
        Invalidate();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.KeyCode is Keys.Enter or Keys.Space)
        {
            OnClick(EventArgs.Empty);
            e.Handled = true;
            e.SuppressKeyPress = true;
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;

        // Transparent controls inherit the single MetalTopBar background.
        // No independent tile fill or bottom border is drawn.
        if (_hovered)
        {
            using var hover = new SolidBrush(Color.FromArgb(36, _palette.AccentSecondary));
            g.FillRectangle(hover, ClientRectangle);
        }

        using var divider = new Pen(Color.FromArgb(75, 159, 179, 198));
        g.DrawLine(divider, 0, 7, 0, Height - 7);
        // The cyan baseline is painted once by the parent across all cells.

        // All page headers are 56px. Center the common 40px glyph artboard.
        g.TranslateTransform(3, 2 + (Height - 40) / 2f);
        var color = _kind == "close" && _hovered
            ? Color.FromArgb(248, 93, 103)
            : _hovered ? _palette.AccentSecondary : Color.FromArgb(235, 244, 252);
        using var pen = new Pen(color, 1.9f)
        {
            StartCap = LineCap.Round, EndCap = LineCap.Round,
            LineJoin = LineJoin.Round
        };

        switch (_kind)
        {
            case "sound":
                g.DrawPolygon(pen,
                [
                    new Point(12, 15), new Point(17, 15),
                    new Point(23, 10), new Point(23, 26),
                    new Point(17, 21), new Point(12, 21)
                ]);
                if (_soundEnabled)
                {
                    g.DrawArc(pen, 20, 10, 12, 16, -65, 130);
                    g.DrawArc(pen, 19, 6, 19, 24, -55, 110);
                }
                else
                {
                    g.DrawLine(pen, 28, 12, 36, 23);
                    g.DrawLine(pen, 36, 12, 28, 23);
                }
                break;

            case "help":
                // TextRenderer uses Win32 GDI and ignores Graphics.TranslateTransform:
                // in the tall Skills header the old '?' floated above its circle.
                // Draw a vector question mark so it follows the same transform
                // as the circle and the three other utility glyphs.
                g.DrawEllipse(pen, 13, 8, 19, 19);
                g.DrawBezier(pen, new Point(18, 15), new Point(19, 10),
                    new Point(28, 10), new Point(27, 16));
                g.DrawBezier(pen, new Point(27, 16), new Point(26, 19),
                    new Point(22, 18), new Point(22, 21));
                using (var dot = new SolidBrush(color))
                    g.FillEllipse(dot, 21, 23, 3, 3);
                break;

            case "minimize":
                g.DrawLine(pen, 15, 21, 30, 21);
                break;

            case "close":
                g.DrawLine(pen, 16, 12, 29, 25);
                g.DrawLine(pen, 29, 12, 16, 25);
                break;
        }

        g.ResetTransform();

        if (Focused)
        {
            using var focus = new Pen(_palette.AccentSecondary, 1)
                { DashStyle = DashStyle.Dot };
            g.DrawRectangle(focus, 3, 3, Width - 7, Height - 7);
        }
    }
}
