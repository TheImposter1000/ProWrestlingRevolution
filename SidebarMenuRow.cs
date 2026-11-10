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
        Size = new Size(228, 54);
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
        // Align the idle row with the sidebar's subtle metallic finish.
        using (var baseMetal = new LinearGradientBrush(ClientRectangle,
            MetalColors.Mix(_palette.Sidebar, Color.FromArgb(39, 51, 70), 0.30),
            MetalColors.Mix(_palette.Sidebar, Color.FromArgb(2, 7, 15), 0.26),
            LinearGradientMode.Horizontal))
            g.FillRectangle(baseMetal, ClientRectangle);
        bool highlighted = _hovered || _selected;

        if (highlighted)
        {
            using var metal = new LinearGradientBrush(
                ClientRectangle,
                Color.FromArgb(55, 77, 99),
                Color.FromArgb(15, 25, 43),
                LinearGradientMode.Horizontal
            );

            g.FillRectangle(metal, ClientRectangle);

            using var shine = new Pen(
                Color.FromArgb(150, 185, 210, 225), 1
            );

            g.DrawLine(shine, 1, 1, Width - 2, 1);

            using var shadow = new Pen(
                Color.FromArgb(80, 5, 12, 24), 1
            );

            g.DrawLine(
                shadow, 1, Height - 2,
                Width - 2, Height - 2
            );

            using var accent = new Pen(
                _palette.AccentSecondary, 3
            );

            g.DrawLine(accent, 1, 4, 1, Height - 5);
            using var reflectedEdge = new Pen(Color.FromArgb(100, _palette.AccentSecondary), 1);
            g.DrawLine(reflectedEdge, Width - 2, 3, Width - 2, Height - 4);
        }

        var foreground = highlighted ? _palette.AccentSecondary : _palette.Foreground;
        g.TranslateTransform(12, (Height - 26) / 2f);
        DrawIcon(g, _key, foreground);
        g.ResetTransform();
        var textRect = new Rectangle(49, 0, Width - 53, Height);
        TextRenderer.DrawText(g, _caption, highlighted ? _activeFont : _normalFont,
            textRect, foreground,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter |
            TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis);
        if (Focused)
        {
            using var outline = new Pen(_palette.AccentSecondary, 1) { DashStyle = DashStyle.Dot };
            g.DrawRectangle(outline, 5, 4, Width - 11, Height - 9);
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
                g.DrawPolygon(pen,
                [
                    new Point(13, 2), new Point(24, 8),
                    new Point(13, 14), new Point(2, 8)
                ]);
                g.DrawLines(pen,
                [
                    new Point(2, 13), new Point(13, 19), new Point(24, 13)
                ]);
                g.DrawLines(pen,
                [
                    new Point(2, 18), new Point(13, 24), new Point(24, 18)
                ]);
                break;

            case "achievements":
                g.DrawArc(pen, 6, 2, 15, 16, 0, 180);
                g.DrawLine(pen, 6, 5, 2, 5);
                g.DrawArc(pen, 0, 5, 9, 11, 0, 145);
                g.DrawLine(pen, 21, 5, 25, 5);
                g.DrawArc(pen, 18, 5, 9, 11, 35, 145);
                g.DrawLine(pen, 13, 18, 13, 22);
                g.DrawLine(pen, 8, 23, 18, 23);
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
