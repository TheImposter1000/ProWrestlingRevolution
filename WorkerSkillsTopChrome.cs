using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace ProWrestlingRevolution;

/// <summary>
/// The approved dedicated Skills screen uses a PWR-branded, blue-metal
/// navigation plaque rather than the normal version-label title bar.
/// This paints only the Skills header and leaves the real window controls
/// attached to the existing launcher chrome.
/// </summary>
internal sealed class WorkerSkillsTopChrome : Control
{
    private static readonly Color Cyan = Color.FromArgb(0, 218, 255);
    private static readonly Color White = Color.FromArgb(239, 248, 255);

    internal WorkerSkillsTopChrome()
    {
        Name = "WorkerSkillsTopChrome";
        SetStyle(ControlStyles.UserPaint |
                 ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.OptimizedDoubleBuffer |
                 ControlStyles.ResizeRedraw |
                 ControlStyles.SupportsTransparentBackColor, true);
        BackColor = Color.Transparent;
        AccessibleName = "PWR Skills, Worker Skill Editor";
    }

    // One continuous gradient, facet and cyan accent behind the title
    // AND all four actual interactive window controls.
    internal static void PaintBackdrop(Graphics g, Rectangle rect)
    {
        if (rect.Width <= 0 || rect.Height <= 0) return;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        using (var background = new LinearGradientBrush(rect,
            Color.FromArgb(4, 15, 30), Color.FromArgb(11, 56, 95),
            LinearGradientMode.Horizontal))
        {
            background.InterpolationColors = new ColorBlend
            {
                Colors = [
                    Color.FromArgb(4, 16, 32),
                    Color.FromArgb(6, 23, 43),
                    Color.FromArgb(10, 43, 77),
                    Color.FromArgb(1, 76, 130)],
                Positions = [0f, 0.49f, 0.80f, 1f]
            };
            g.FillRectangle(background, rect);
        }
        using (var brushed = new LinearGradientBrush(
            new Rectangle(0, 0, rect.Width, Math.Max(2, rect.Height / 2)),
            Color.FromArgb(37, 19, 111, 194),
            Color.FromArgb(0, 8, 42, 101), LinearGradientMode.Vertical))
            g.FillRectangle(brushed, 0, 0, rect.Width, rect.Height / 2);
        using (var facet = new SolidBrush(Color.FromArgb(56, 28, 111, 183)))
            g.FillPolygon(facet,
            [
                new Point(rect.Width * 76 / 100, 0),
                new Point(rect.Width, 0),
                new Point(rect.Width * 95 / 100, rect.Height)
            ]);
        using (var flare = new Pen(Color.FromArgb(220, Cyan), 2.2f))
            g.DrawLine(flare, 0, rect.Height - 2,
                rect.Width, rect.Height - 2);
        using (var soft = new Pen(Color.FromArgb(83, 0, 219, 255), 6f))
            g.DrawLine(soft, rect.Width * 43 / 100, rect.Height - 3,
                rect.Width * 89 / 100, rect.Height - 3);

    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var rect = ClientRectangle;
        if (rect.Width < 450 || rect.Height < 28) return;
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        // The background is painted once across the full MetalTopBar.
        // This overlay paints only the title information.
        // The PWR monogram and three angular blue swooshes are drawn from
        // primitives, not a brittle screenshot cropped into a bitmap.
        using var swoosh = new Pen(Color.FromArgb(8, 159, 235), 3.4f);
        g.DrawBezier(swoosh, 21, 26, 36, 5, 62, 0, 139, 8);
        g.DrawBezier(swoosh, 22, 35, 42, 15, 76, 12, 151, 17);
        g.DrawBezier(swoosh, 22, 42, 44, 28, 83, 23, 154, 23);

        float logoSize = rect.Height >= 51 ? 31f : 25f;
        using var logoFont = new Font("Arial", logoSize,
            FontStyle.Bold | FontStyle.Italic);
        TextRenderer.DrawText(g, "PWR", logoFont,
            new Rectangle(47, 0, 148, rect.Height),
            Color.FromArgb(60, 120, 166), TextFormatFlags.VerticalCenter |
            TextFormatFlags.NoPrefix);
        TextRenderer.DrawText(g, "PWR", logoFont,
            new Rectangle(44, -2, 148, rect.Height),
            White, TextFormatFlags.VerticalCenter |
            TextFormatFlags.NoPrefix);

        using var brandFont = new Font("Segoe UI", 12.3f,
            FontStyle.Bold | FontStyle.Italic);
        TextRenderer.DrawText(g, "PRO WRESTLING", brandFont,
            new Rectangle(167, 6, 215, rect.Height / 2),
            White, TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix);
        TextRenderer.DrawText(g, "REVOLUTION", brandFont,
            new Rectangle(167, rect.Height / 2 - 1, 215, rect.Height / 2),
            White, TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix);

        int dividerX = 400;
        using (var divider = new Pen(Cyan, 2.2f))
            g.DrawLine(divider, dividerX, 11, dividerX, rect.Height - 12);
        using var sectionFont = new Font("Segoe UI", 22.0f, FontStyle.Bold);
        TextRenderer.DrawText(g, "Skills", sectionFont,
            new Rectangle(dividerX + 43, 1, 115, rect.Height - 1),
            White, TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
        using var slashFont = new Font("Segoe UI", 20f, FontStyle.Italic);
        TextRenderer.DrawText(g, "/", slashFont,
            new Rectangle(dividerX + 164, 1, 27, rect.Height - 1),
            SkillChrome.Muted,
            TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
        using var subtitle = new Font("Segoe UI", 15.0f, FontStyle.Italic);
        TextRenderer.DrawText(g, "Worker Skill Editor", subtitle,
            new Rectangle(dividerX + 202, 1,
                Math.Max(130, rect.Width - dividerX - 216), rect.Height - 1),
            Color.FromArgb(223, 232, 248),
            TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
    }
}
