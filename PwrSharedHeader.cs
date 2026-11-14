using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace ProWrestlingRevolution;

/// <summary>
/// One visual identity for every interior PWR page. The full-width
/// MetalTopBar paints the metal and cyan rule; this transparent control
/// renders only the shared logo and contextual title. The real utility
/// buttons remain separate interactive controls in that same top bar.
/// </summary>
internal sealed class PwrSharedHeader : Control
{
    private static readonly Color Cyan = Color.FromArgb(0, 213, 255);
    private static readonly Color White = Color.FromArgb(241, 248, 255);
    private static readonly Color DetailInk = Color.FromArgb(212, 228, 244);

    private string _pageTitle = "Pro Wrestling Revolution";
    private string _pageDetail = "";

    internal string PageTitle => _pageTitle;
    internal string PageDetail => _pageDetail;

    internal PwrSharedHeader()
    {
        Name = "PwrSharedHeader";
        AccessibleName = "PWR header";
        SetStyle(ControlStyles.UserPaint |
                 ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.OptimizedDoubleBuffer |
                 ControlStyles.ResizeRedraw |
                 ControlStyles.SupportsTransparentBackColor, true);
        BackColor = Color.Transparent;
        TabStop = false;
    }

    internal void SetPage(string title, string detail)
    {
        if (string.Equals(title, _pageTitle, StringComparison.Ordinal) &&
            string.Equals(detail, _pageDetail, StringComparison.Ordinal))
            return;
        _pageTitle = title;
        _pageDetail = detail;
        AccessibleName = "PWR / " + title + (detail.Length == 0 ? "" : " / " + detail);
        Invalidate();
    }

    /// <summary>
    /// Used for every interior page, including the utility-button area.
    /// A single full-width background prevents discontinuous colours,
    /// doubled cyan borders or independent tiles at the right.
    /// </summary>
    internal static void PaintBackdrop(Graphics g, Rectangle rect)
    {
        if (rect.Width < 2 || rect.Height < 2) return;
        g.SmoothingMode = SmoothingMode.AntiAlias;

        using (var background = new LinearGradientBrush(rect,
            Color.FromArgb(4, 14, 29),
            Color.FromArgb(5, 29, 56),
            LinearGradientMode.Horizontal))
        {
            background.InterpolationColors = new ColorBlend
            {
                Colors =
                [
                    Color.FromArgb(4, 14, 29),
                    Color.FromArgb(5, 23, 44),
                    Color.FromArgb(13, 50, 84),
                    Color.FromArgb(8, 32, 60)
                ],
                Positions = [0f, 0.34f, 0.77f, 1f]
            };
            g.FillRectangle(background, rect);
        }

        // Brushed-steel highlight and angular blue facet. Geometry is
        // relative to the actual window size, not a baked-in screenshot.
        var upper = new Rectangle(0, 0, rect.Width, Math.Max(2, rect.Height / 2));
        using (var sheen = new LinearGradientBrush(upper,
            Color.FromArgb(41, 27, 106, 173),
            Color.FromArgb(0, 16, 54, 103),
            LinearGradientMode.Vertical))
            g.FillRectangle(sheen, upper);

        using (var facet = new SolidBrush(Color.FromArgb(70, 17, 94, 156)))
            g.FillPolygon(facet,
            [
                new Point(rect.Width * 75 / 100, 0),
                new Point(rect.Width * 89 / 100, 0),
                new Point(rect.Width * 85 / 100, rect.Height - 3),
                new Point(rect.Width * 72 / 100, rect.Height - 3)
            ]);
        using (var edge = new Pen(Color.FromArgb(43, 87, 184, 237), 1f))
            g.DrawLine(edge, rect.Width * 89 / 100, 0,
                rect.Width * 85 / 100, rect.Height - 3);

        using (var top = new Pen(Color.FromArgb(93, 124, 199, 246)))
            g.DrawLine(top, 0, 0, rect.Right - 1, 0);
        using (var glow = new Pen(Color.FromArgb(57, Cyan), 5f))
            g.DrawLine(glow, 0, rect.Bottom - 3, rect.Right - 1, rect.Bottom - 3);
        using (var rule = new Pen(Cyan, 1.6f))
            g.DrawLine(rule, 0, rect.Bottom - 1, rect.Right - 1, rect.Bottom - 1);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        if (Width < 450 || Height < 40) return;

        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        using (var streak = new Pen(Color.FromArgb(0, 173, 239), 3f))
        {
            g.DrawBezier(streak, 20, 27, 40, 3, 74, 1, 138, 10);
            g.DrawBezier(streak, 20, 35, 44, 13, 82, 13, 146, 19);
            g.DrawBezier(streak, 20, 43, 45, 27, 86, 25, 153, 25);
        }

        using (var logoFont = new Font("Arial", 32f,
            FontStyle.Bold | FontStyle.Italic))
        {
            TextRenderer.DrawText(g, "PWR", logoFont,
                new Rectangle(42, -1, 124, Height),
                Color.FromArgb(95, 121, 168, 215),
                TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding |
                TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine);
            TextRenderer.DrawText(g, "PWR", logoFont,
                new Rectangle(39, -4, 124, Height),
                White, TextFormatFlags.VerticalCenter |
                TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix |
                TextFormatFlags.SingleLine);
        }

        using (var brand = new Font("Segoe UI", 11.7f,
            FontStyle.Bold | FontStyle.Italic))
        {
            TextRenderer.DrawText(g, "PRO WRESTLING", brand,
                new Rectangle(168, 7, 161, 21), White,
                TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix |
                TextFormatFlags.SingleLine);
            TextRenderer.DrawText(g, "REVOLUTION", brand,
                new Rectangle(168, 28, 161, 21), White,
                TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix |
                TextFormatFlags.SingleLine);
        }

        const int dividerX = 337;
        using (var divider = new Pen(Cyan, 2f))
            g.DrawLine(divider, dividerX, 12, dividerX, Height - 12);

        using var titleFont = new Font("Segoe UI", 19.5f, FontStyle.Bold);
        using var slashFont = new Font("Segoe UI", 18.2f, FontStyle.Italic);
        using var detailFont = new Font("Segoe UI", 13.2f, FontStyle.Italic);

        const int titleX = 367;
        int titleWidth = Math.Min(240,
            TextRenderer.MeasureText(g, _pageTitle, titleFont,
                new Size(2000, Height),
                TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix |
                TextFormatFlags.SingleLine).Width + 5);
        int available = Math.Max(1, Width - titleX - 4);
        TextRenderer.DrawText(g, _pageTitle, titleFont,
            new Rectangle(titleX, 0, Math.Min(titleWidth, available), Height - 1),
            White, TextFormatFlags.VerticalCenter |
            TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix |
            TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine);

        if (_pageDetail.Length == 0) return;
        int slashX = titleX + titleWidth + 13;
        if (slashX + 42 >= Width) return;
        TextRenderer.DrawText(g, "/", slashFont,
            new Rectangle(slashX, 0, 30, Height - 1), DetailInk,
            TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding |
            TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine);
        int detailX = slashX + 37;
        TextRenderer.DrawText(g, _pageDetail, detailFont,
            new Rectangle(detailX, 0, Math.Max(1, Width - detailX - 10),
                Height - 1), DetailInk, TextFormatFlags.VerticalCenter |
            TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix |
            TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine);
    }
}
