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
}

internal sealed class MetalTopBar : Panel
{
    private SkinPalette _palette;

    internal MetalTopBar(SkinPalette palette)
    {
        _palette = palette;
        Dock = DockStyle.Top;
        Height = 36;
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
            MetalColors.Mix(_palette.Surface, Color.FromArgb(75, 88, 106), 0.27),
            MetalColors.Mix(_palette.Surface, Color.FromArgb(5, 9, 19), 0.36),
            LinearGradientMode.Vertical);
        e.Graphics.FillRectangle(brushed, ClientRectangle);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;

        // An etched reflection at the top and a continuous cool edge below.
        using var shine = new Pen(Color.FromArgb(78, 195, 214, 230));
        using var line = new Pen(Color.FromArgb(160, _palette.AccentSecondary));
        g.DrawLine(shine, 0, 0, Width, 0);
        g.DrawLine(line, 0, Height - 1, Width, Height - 1);

        // Small original red slash emblem, distinct from the title JPG.
        using var red = new SolidBrush(Color.FromArgb(237, 39, 65));
        using var gleam = new Pen(Color.FromArgb(255, 128, 141), 1);
        g.FillPolygon(red,
        [
            new Point(17, 9), new Point(24, 9),
            new Point(17, 27), new Point(10, 27)
        ]);
        g.DrawLine(gleam, 17, 9, 24, 9);

        // Thin engraved separator between the app name and its version.
        using var divider = new Pen(Color.FromArgb(115, 170, 189, 205));
        g.DrawLine(divider, 342, 9, 342, 27);
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
            MetalColors.Mix(_palette.Sidebar, Color.FromArgb(39, 51, 70), 0.30),
            MetalColors.Mix(_palette.Sidebar, Color.FromArgb(2, 7, 15), 0.26),
            LinearGradientMode.Horizontal);
        e.Graphics.FillRectangle(brushed, ClientRectangle);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        using var edge = new Pen(Color.FromArgb(105, _palette.Border));
        using var light = new Pen(Color.FromArgb(32, _palette.AccentSecondary));
        e.Graphics.DrawLine(edge, Width - 1, 0, Width - 1, Height);
        e.Graphics.DrawLine(light, 0, 0, 0, Height);

        // Very subtle angled steel detail near the lower corner.
        using var facet = new Pen(Color.FromArgb(24, 180, 197, 215));
        e.Graphics.DrawLine(facet, 0, Height - 54, 54, Height);
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
        Size = new Size(44, 36);
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
            ControlStyles.OptimizedDoubleBuffer | ControlStyles.Selectable, true);
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

        var baseColor = MetalColors.Mix(_palette.Surface,
            Color.FromArgb(5, 9, 17), _hovered ? 0.18 : 0.58);
        g.Clear(baseColor);

        if (_hovered)
        {
            using var hover = new SolidBrush(Color.FromArgb(36, _palette.AccentSecondary));
            g.FillRectangle(hover, ClientRectangle);
        }

        using var divider = new Pen(Color.FromArgb(75, 159, 179, 198));
        using var bottom = new Pen(Color.FromArgb(160, _palette.AccentSecondary));
        g.DrawLine(divider, 0, 7, 0, Height - 7);
        g.DrawLine(bottom, 0, Height - 1, Width, Height - 1);

        var color = _kind == "close" ? Color.FromArgb(248, 62, 78)
            : _hovered ? _palette.AccentSecondary : Color.FromArgb(235, 240, 247);
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
                g.DrawEllipse(pen, 13, 8, 19, 19);
                using (var font = new Font("Segoe UI", 11, FontStyle.Bold))
                    TextRenderer.DrawText(g, "?", font,
                        new Rectangle(15, 8, 16, 20), color,
                        TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter |
                        TextFormatFlags.NoPadding);
                break;

            case "minimize":
                g.DrawLine(pen, 15, 21, 30, 21);
                break;

            case "close":
                g.DrawLine(pen, 16, 12, 29, 25);
                g.DrawLine(pen, 29, 12, 16, 25);
                break;
        }

        if (Focused)
        {
            using var focus = new Pen(_palette.AccentSecondary, 1)
                { DashStyle = DashStyle.Dot };
            g.DrawRectangle(focus, 3, 3, Width - 7, Height - 7);
        }
    }
}
