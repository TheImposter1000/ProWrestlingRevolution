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
        Mix(palette.Sidebar, Color.FromArgb(5, 12, 23), 0.72);

    internal static Color SidebarRight(SkinPalette palette) =>
        Mix(palette.Sidebar, Color.FromArgb(8, 19, 33), 0.78);

    internal static Color TopRight(SkinPalette palette) =>
        Mix(palette.Surface, Color.FromArgb(8, 24, 40), 0.82);
}

/// <summary>
/// Optional skin art for the application's actual chrome, separate from the
/// Backgrounds/Title_Screen.jpg artwork. These assets contain background
/// treatments only: real buttons, labels and hit targets remain native controls.
/// The vector reference design is used when a skin omits an asset.
/// </summary>
internal static class SkinChromeArt
{
    internal static bool Draw(Graphics graphics, SkinPalette palette,
        string relativePath, Rectangle bounds)
    {
        var path = palette.Asset(relativePath);
        if (!File.Exists(path)) return false;
        try
        {
            using var art = Image.FromFile(path);
            graphics.DrawImage(art, bounds);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            return false;
        }
    }

    internal static bool Exists(SkinPalette palette, string relativePath) =>
        File.Exists(palette.Asset(relativePath));
}

internal sealed class MetalTopBar : Panel
{
    private SkinPalette _palette;

    internal MetalTopBar(SkinPalette palette)
    {
        _palette = palette;
        Dock = DockStyle.Top;
        Height = 40;
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
        if (Width < 1 || Height < 1) return;
        if (SkinChromeArt.Draw(e.Graphics, _palette,
            Path.Combine("TopBars", "Title_Chrome.png"), ClientRectangle)) return;

        using var metal = new LinearGradientBrush(ClientRectangle,
            MetalColors.Mix(_palette.Surface, Color.FromArgb(6, 16, 27), 0.82),
            MetalColors.TopRight(_palette), LinearGradientMode.Horizontal);
        metal.InterpolationColors = new ColorBlend
        {
            Colors =
            [
                MetalColors.Mix(_palette.Surface, Color.FromArgb(6, 14, 24), 0.84),
                MetalColors.Mix(_palette.Surface, Color.FromArgb(10, 27, 45), 0.80),
                MetalColors.Mix(_palette.Surface, Color.FromArgb(7, 22, 37), 0.83)
            ],
            Positions = [0f, 0.43f, 1f]
        };
        e.Graphics.FillRectangle(metal, ClientRectangle);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;

        using var upperReflection = new Pen(Color.FromArgb(55, 153, 187, 218), 1);
        using var accentLine = new Pen(Color.FromArgb(115, _palette.AccentSecondary), 1);
        g.DrawLine(upperReflection, 0, 0, Width - 1, 0);
        g.DrawLine(accentLine, 0, Height - 1, Width - 1, Height - 1);

        // Only the small chrome emblem is drawn here; the main JPG is untouched.
        using var red = new SolidBrush(Color.FromArgb(239, 36, 65));
        using var gleam = new Pen(Color.FromArgb(245, 144, 155), 1);
        g.FillPolygon(red,
        [
            new Point(34, 12), new Point(43, 12),
            new Point(36, 29), new Point(27, 29)
        ]);
        g.DrawLine(gleam, 34, 12, 43, 12);

        using var divider = new Pen(Color.FromArgb(165, 173, 196, 217), 1);
        g.DrawLine(divider, 307, 11, 307, 29);
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
        if (SkinChromeArt.Draw(e.Graphics, _palette,
            Path.Combine("SideBars", "Title_Chrome.png"), ClientRectangle)) return;

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

        // Lower-left diagonal metal facet from the supplied reference image.
        // Do not paint over optional skin-supplied sidebar chrome.
        if (!SkinChromeArt.Exists(_palette, Path.Combine("SideBars", "Title_Chrome.png")))
        {
            using var facet = new SolidBrush(Color.FromArgb(32, 26, 54, 86));
            g.FillPolygon(facet,
            [
                new Point(0, Height - 68),
                new Point(Width - 1, Height - 261),
                new Point(Width - 1, Height - 1),
                new Point(0, Height - 1)
            ]);
            using var facetEdge = new Pen(Color.FromArgb(44, 94, 139, 178), 1);
            g.DrawLine(facetEdge, 0, Height - 68, Width - 1, Height - 261);
        }

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
        Size = new Size(50, 40);
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

        // Match the surrounding top bar instead of showing four boxed tiles.
        using (var back = new SolidBrush(MetalColors.TopRight(_palette)))
            g.FillRectangle(back, ClientRectangle);

        if (_hovered)
        {
            using var hover = new SolidBrush(Color.FromArgb(36, _palette.AccentSecondary));
            g.FillRectangle(hover, ClientRectangle);
        }

        using var divider = new Pen(Color.FromArgb(78, 137, 160, 181));
        using var bottom = new Pen(Color.FromArgb(115, _palette.AccentSecondary));
        g.DrawLine(divider, 0, 7, 0, Height - 7);
        g.DrawLine(bottom, 0, Height - 1, Width, Height - 1);

        g.TranslateTransform(3, 2);
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
                // A vector question mark stays precisely within its vector circle,
                // independent of font rendering, Windows DPI and font metrics.
                g.DrawEllipse(pen, 13, 8, 20, 20);
                g.DrawBezier(pen,
                    new Point(18, 15), new Point(16, 8),
                    new Point(30, 8), new Point(29, 16));
                g.DrawBezier(pen,
                    new Point(29, 16), new Point(29, 19),
                    new Point(23, 19), new Point(23, 22));
                using (var dot = new SolidBrush(color))
                    g.FillEllipse(dot, 22, 24, 3, 3);
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
