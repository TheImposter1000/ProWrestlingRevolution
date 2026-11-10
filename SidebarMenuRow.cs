using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace ProWrestlingRevolution;

/// <summary>
/// A single owner-drawn navigation target. Icon, text, background and hover
/// highlight all share the same rectangular hit area. No skin sidebar JPGs,
/// emoji fonts, or separate icon controls are required.
/// </summary>
internal sealed class SidebarMenuRow : Control
{
    private readonly string _key;
    private readonly string _caption;
    private readonly Font _normalFont;
    private readonly Font _activeFont;
    private SkinPalette _palette;
    private bool _hovered;
    private bool _selected;

    internal SidebarMenuRow(string key, string caption, SkinPalette palette,
        Font normalFont, Font activeFont)
    {
        _key = key;
        _caption = caption;
        _palette = palette;
        _normalFont = normalFont;
        _activeFont = activeFont;
        BackColor = palette.Sidebar;
        ForeColor = palette.Foreground;
        Cursor = Cursors.Hand;
        Size = new Size(200, 54);
        TabStop = true;
        AccessibleName = caption;
        AccessibleRole = AccessibleRole.PushButton;
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
            ControlStyles.OptimizedDoubleBuffer | ControlStyles.Selectable, true);
    }

    internal void UpdateAppearance(SkinPalette palette, bool hovered, bool selected)
    {
        _palette = palette;
        _hovered = hovered;
        _selected = selected;
        BackColor = palette.Sidebar;
        ForeColor = palette.Foreground;
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

        // Use exactly the same fill as the parent so idle items form one
        // continuous sidebar rather than seven individual dark rectangles.
        using (var baseMetal = new LinearGradientBrush(ClientRectangle,
            MetalColors.SidebarLeft(_palette), MetalColors.SidebarRight(_palette),
            LinearGradientMode.Horizontal))
            g.FillRectangle(baseMetal, ClientRectangle);

        bool highlighted = _hovered || _selected;
        if (highlighted)
        {
            // The reference uses a diagonal metal blade, not a flat rectangle.
            var shape =
                new[]
                {
                    new Point(0, 0),
                    new Point(Width - 1, 0),
                    new Point(Width - 20, Height - 1),
                    new Point(0, Height - 1)
                };

            using var fill = new LinearGradientBrush(ClientRectangle,
                Color.FromArgb(22, 40, 62), Color.FromArgb(27, 85, 135),
                LinearGradientMode.Horizontal);
            fill.InterpolationColors = new ColorBlend
            {
                Colors =
                [
                    MetalColors.Mix(Color.FromArgb(23, 41, 64), _palette.Accent, 0.06),
                    MetalColors.Mix(Color.FromArgb(33, 69, 110), _palette.Accent, 0.10),
                    MetalColors.Mix(Color.FromArgb(28, 91, 151), _palette.Accent, 0.14)
                ],
                Positions = [0f, 0.67f, 1f]
            };
            g.FillPolygon(fill, shape);

            using var topReflection = new Pen(Color.FromArgb(158, 193, 222, 244), 1);
            using var bottomReflection = new Pen(Color.FromArgb(92, 121, 168, 204), 1);
            g.DrawLine(topReflection, 1, 0, Width - 2, 0);
            g.DrawLine(bottomReflection, 1, Height - 1, Width - 20, Height - 1);

            using var outerGlow = new Pen(Color.FromArgb(70, _palette.AccentSecondary), 7);
            using var brightEdge = new Pen(_palette.AccentSecondary, 2);
            g.DrawLine(outerGlow, Width - 2, 1, Width - 20, Height - 2);
            g.DrawLine(brightEdge, Width - 2, 1, Width - 20, Height - 2);

            using var innerSheen = new Pen(Color.FromArgb(95, 235, 248, 255), 1);
            g.DrawLine(innerSheen, Width - 6, 3, Width - 23, Height - 4);
        }

        // Reflected silver remains readable above the bright blue blade.
        var foreground = _palette.Foreground;
        g.TranslateTransform(27, (Height - 26) / 2f);
        DrawIcon(g, _key, foreground);
        g.ResetTransform();

        var textRect = new Rectangle(75, 0, Width - 80, Height);
        TextRenderer.DrawText(g, _caption,
            highlighted ? _activeFont : _normalFont,
            textRect, foreground,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter |
            TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis);

        if (Focused)
        {
            using var focus = new Pen(_palette.AccentSecondary, 1)
            {
                DashStyle = DashStyle.Dot
            };
            g.DrawRectangle(focus, 4, 4, Width - 26, Height - 9);
        }
    }

    private static void DrawIcon(Graphics g, string key, Color color)
    {
        using var pen = new Pen(color, 2.0f)
        {
            StartCap = LineCap.Round, EndCap = LineCap.Round,
            LineJoin = LineJoin.Round
        };
        switch (key)
        {
            case "new":
                g.DrawRectangle(pen, 2, 2, 22, 22);
                g.DrawLine(pen, 13, 7, 13, 19);
                g.DrawLine(pen, 7, 13, 19, 13);
                break;

            case "load":
                g.DrawLines(pen,
                [
                    new Point(2, 7), new Point(9, 7), new Point(12, 10),
                    new Point(24, 10), new Point(24, 23), new Point(2, 23),
                    new Point(2, 7)
                ]);
                break;

            case "database":
                g.DrawEllipse(pen, 3, 3, 20, 7);
                g.DrawArc(pen, 3, 14, 20, 7, 0, 180);
                g.DrawArc(pen, 3, 19, 20, 7, 0, 180);
                g.DrawLine(pen, 3, 7, 3, 22);
                g.DrawLine(pen, 23, 7, 23, 22);
                break;

            case "editor":
                g.DrawLine(pen, 4, 23, 21, 6);
                g.DrawLine(pen, 4, 6, 21, 23);
                g.DrawEllipse(pen, 0, 19, 7, 7);
                g.DrawEllipse(pen, 19, 0, 7, 7);
                g.DrawLine(pen, 1, 5, 7, 0);
                g.DrawLine(pen, 1, 5, 8, 12);
                break;

            case "skins":
                // A tag silhouette mirrors the source mockup.
                g.DrawPolygon(pen,
                [
                    new Point(2, 12), new Point(12, 2),
                    new Point(24, 2), new Point(24, 14),
                    new Point(14, 24)
                ]);
                g.DrawEllipse(pen, 17, 6, 3, 3);
                break;

            case "achievements":
                // Trophy cup with handles, stem and plinth.
                g.DrawLines(pen,
                [
                    new Point(5, 4), new Point(7, 13),
                    new Point(13, 18), new Point(19, 13),
                    new Point(21, 4)
                ]);
                g.DrawLine(pen, 5, 4, 21, 4);
                g.DrawArc(pen, 0, 5, 10, 10, 75, 205);
                g.DrawArc(pen, 16, 5, 10, 10, 260, 205);
                g.DrawLine(pen, 13, 18, 13, 23);
                g.DrawLine(pen, 9, 23, 17, 23);
                break;

            case "credits":
                g.DrawEllipse(pen, 2, 2, 22, 22);
                using (var brush = new SolidBrush(color)) g.FillEllipse(brush, 11, 6, 4, 4);
                g.DrawLine(pen, 13, 12, 13, 19);
                break;

            case "back":
                g.DrawLine(pen, 23, 13, 3, 13);
                g.DrawLine(pen, 3, 13, 11, 5);
                g.DrawLine(pen, 3, 13, 11, 21);
                break;
        }
    }
}
