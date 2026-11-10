using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Windows.Forms;

namespace ProWrestlingRevolution;

/// <summary>
/// Title-screen layout based on the approved 1672 x 941 reference.
/// The artwork is always the selected skin's unmodified Backgrounds/Title_Screen.jpg.
/// All surrounding chrome is drawn here, instead of reusing the interior sidebar.
/// The coordinate system scales as one piece to the game's 1366 x 768 client area.
/// </summary>
internal sealed class TitleScreenView : Control
{
    private const float DesignWidth = 1672f;
    private const float DesignHeight = 941f;

    private readonly record struct MenuItem(
        string Key, string Label, string Icon, RectangleF Bounds, float TextX);

    private static readonly MenuItem[] Menu =
    [
        new("new", "New Game", "new", new RectangleF(88, 667, 318, 83), 227),
        new("load", "Load Game", "load", new RectangleF(404, 667, 316, 83), 535),
        new("database", "Database", "database", new RectangleF(714, 667, 298, 83), 851),
        new("editor", "Editor", "editor", new RectangleF(1007, 667, 294, 83), 1164),
        new("skins", "Skins", "skins", new RectangleF(76, 771, 319, 80), 225),
        new("achievements", "Achievements", "achievements", new RectangleF(397, 771, 350, 80), 552),
        new("credits", "Credits", "credits", new RectangleF(738, 771, 341, 80), 892)
    ];

    private string? _hoverKey;
    private int _keyboardIndex;
    private Image? _artwork;
    private Image? _exactLayout;

    // Optional pixel-exact approved menu image. Buttons still use live hit testing.
    // This is not the skin background: it is a full-sized title-screen UI composite.
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    [Browsable(false)]
    internal Image? ExactLayout
    {
        get => _exactLayout;
        set { _exactLayout = value; Invalidate(); }
    }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    [Browsable(false)]
    internal Image? Artwork
    {
        get => _artwork;
        set { _artwork = value; Invalidate(); }
    }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    [Browsable(false)]
    internal string VersionText { get; set; } = "v0.5.4";
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    [Browsable(false)]
    internal string UpdateText { get; set; } = "Check for Updates";
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    [Browsable(false)]
    internal bool UpdateEnabled { get; set; } = true;
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    [Browsable(false)]
    internal bool SoundEnabled { get; set; } = true;
    internal event Action<string>? CommandInvoked;

    internal TitleScreenView()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw |
            ControlStyles.Selectable, true);
        TabStop = true;
        AccessibleRole = AccessibleRole.Client;
        AccessibleName = "Pro Wrestling Revolution title screen";
        Cursor = Cursors.Default;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        Graphics g = e.Graphics;
        g.Clear(Color.FromArgb(7, 18, 31));
        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;

        // Painting the supplied reference composite is pixel accurate, including
        // the crowd, logo, interface geometry, typography, and button textures.
        // Interaction regions below remain real and usable.
        if (_exactLayout is not null)
        {
            g.DrawImage(_exactLayout, ClientRectangle);
            return;
        }

        // Keep the crowd image and the large logo exactly as supplied in the skin.
        // No second logo, wallpaper substitution, crop of the logo, or tint over it.
        if (_artwork is not null && Width > 0 && Height > 0)
        {
            float scale = Math.Max((float)Width / _artwork.Width,
                                   (float)Height / _artwork.Height);
            int w = (int)Math.Ceiling(_artwork.Width * scale);
            int h = (int)Math.Ceiling(_artwork.Height * scale);
            g.DrawImage(_artwork, new Rectangle((Width - w) / 2, (Height - h) / 2, w, h));
        }

        var original = g.Save();
        g.ScaleTransform(Width / DesignWidth, Height / DesignHeight);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;

        DrawIdentity(g);
        DrawUtility(g);
        DrawLowerArchitecture(g);

        foreach (MenuItem item in Menu)
            DrawMenuButton(g, item,
                ((_hoverKey is null || Array.FindIndex(Menu, m => m.Key == _hoverKey) < 0)
                  && Menu[_keyboardIndex].Key == item.Key) || _hoverKey == item.Key);

        DrawUpdate(g, _hoverKey == "updates");
        g.Restore(original);
    }

    private static GraphicsPath Polygon(params PointF[] points)
    {
        var path = new GraphicsPath();
        path.AddPolygon(points);
        return path;
    }

    private static void Stroke(Graphics g, GraphicsPath path, Color color, float width = 1f)
    {
        using var pen = new Pen(color, width) { LineJoin = LineJoin.Round };
        g.DrawPath(pen, path);
    }

    private static void FillGradient(Graphics g, GraphicsPath path,
        RectangleF rect, Color top, Color bottom)
    {
        using var brush = new LinearGradientBrush(rect, top, bottom, LinearGradientMode.Vertical);
        g.FillPath(brush, path);
    }

    private static void DrawText(Graphics g, string value, float x, float y,
        float size, Color color, FontStyle style = FontStyle.Regular, string family = "Segoe UI")
    {
        using var font = new Font(family, size, style, GraphicsUnit.Pixel);
        using var brush = new SolidBrush(color);
        g.DrawString(value, font, brush, new PointF(x, y),
            StringFormat.GenericTypographic);
    }

    private void DrawIdentity(Graphics g)
    {
        PointF[] corners =
        [
            new(70, 55), new(390, 55), new(328, 156), new(23, 156)
        ];
        using var badge = Polygon(corners);
        FillGradient(g, badge, new RectangleF(23, 55, 367, 101),
            Color.FromArgb(203, 7, 19, 29), Color.FromArgb(198, 12, 25, 39));
        Stroke(g, badge, Color.FromArgb(170, 177, 216, 229), 1.3f);

        using (var upper = new Pen(Color.FromArgb(155, 151, 218, 238), 2f))
            g.DrawLine(upper, 70, 55, 387, 55);
        using (var highlight = new Pen(Color.FromArgb(95, 110, 217, 245), 9))
            g.DrawLine(highlight, 330, 155, 387, 56);

        using (var slash = new SolidBrush(Color.FromArgb(253, 255, 34, 77)))
        {
            using var slashPath = Polygon(new PointF(80, 72), new PointF(98, 72),
                new PointF(77, 124), new PointF(59, 124));
            g.FillPath(slash, slashPath);
        }

        DrawText(g, "PRO WRESTLING", 114, 69, 22, Color.White, FontStyle.Bold | FontStyle.Italic);
        DrawText(g, "REVOLUTION", 114, 93, 22, Color.White, FontStyle.Bold | FontStyle.Italic);
        DrawText(g, VersionText, 114, 127, 16, Color.FromArgb(185, 204, 219));
    }


    private void DrawUtility(Graphics g)
    {
        using var rail = Polygon(new PointF(1385, 25), new PointF(1630, 25),
            new PointF(1607, 72), new PointF(1357, 72));
        FillGradient(g, rail, new RectangleF(1357, 25, 273, 47),
            Color.FromArgb(210, 12, 31, 43), Color.FromArgb(210, 7, 15, 27));
        Stroke(g, rail, Color.FromArgb(150, 177, 213, 227), 1.4f);
        using (var shine = new Pen(Color.FromArgb(120, 180, 219, 235), 1))
            g.DrawLine(shine, 1385, 25, 1628, 25);
        using (var divider = new Pen(Color.FromArgb(100, 176, 201, 220), 1))
        {
            g.DrawLine(divider, 1452, 35, 1452, 61);
            g.DrawLine(divider, 1513, 35, 1513, 61);
            g.DrawLine(divider, 1572, 35, 1572, 61);
        }

        DrawIcon(g, "sound", 1416, 49, Color.White);
        DrawIcon(g, "help", 1483, 49, Color.White);
        DrawIcon(g, "minimize", 1546, 49, Color.White);
        DrawIcon(g, "close", 1593, 49, Color.FromArgb(255, 26, 71));
        if (!SoundEnabled)
        {
            using var mutePen = new Pen(Color.FromArgb(255, 33, 74), 3);
            g.DrawLine(mutePen, 1424, 40, 1441, 57);
        }
    }

    private static void DrawLowerArchitecture(Graphics g)
    {
        using var ground = Polygon(
            new PointF(0, 732), new PointF(88, 632), new PointF(182, 632),
            new PointF(1200, 883), new PointF(1672, 880),
            new PointF(1672, 941), new PointF(0, 941));
        using (var fill = new SolidBrush(Color.FromArgb(55, 4, 14, 23)))
            g.FillPath(fill, ground);

        using var bottom = new Pen(Color.FromArgb(140, 121, 166, 187), 1.3f)
            { LineJoin = LineJoin.Round };
        g.DrawLines(bottom,
        [
            new PointF(24, 861), new PointF(49, 889),
            new PointF(454, 889), new PointF(474, 896),
            new PointF(1200, 896), new PointF(1223, 862)
        ]);

        using (var red = new Pen(Color.FromArgb(255, 24, 69), 4)
            { LineJoin = LineJoin.Round })
        {
            g.DrawLines(red,
            [
                new PointF(109, 632), new PointF(176, 632)
            ]);
            g.DrawLines(red,
            [
                new PointF(1168, 879), new PointF(1190, 879), new PointF(1224, 838)
            ]);
        }

        using var blue = new Pen(Color.FromArgb(145, 102, 186, 213), 1.4f);
        g.DrawLine(blue, 472, 888, 1169, 888);
        using var thinRed = new Pen(Color.FromArgb(200, 248, 40, 79), 1.3f);
        g.DrawLine(thinRed, 1052, 882, 1194, 882);
    }

    private static GraphicsPath ButtonOutline(RectangleF r)
    {
        float cut = 25;
        return Polygon(
            new PointF(r.Left + cut, r.Top),
            new PointF(r.Right, r.Top),
            new PointF(r.Right - cut, r.Bottom),
            new PointF(r.Left, r.Bottom),
            new PointF(r.Left + cut - 5, r.Top + 11));
    }

    private static void DrawMenuButton(Graphics g, MenuItem item, bool selected)
    {
        RectangleF r = item.Bounds;
        using var body = ButtonOutline(r);

        if (selected)
        {
            Stroke(g, body, Color.FromArgb(38, 89, 218, 255), 15f);
            Stroke(g, body, Color.FromArgb(85, 120, 223, 255), 7f);
        }

        FillGradient(g, body, r,
            selected ? Color.FromArgb(237, 30, 77, 108) : Color.FromArgb(220, 24, 42, 56),
            selected ? Color.FromArgb(225, 16, 50, 74) : Color.FromArgb(216, 11, 27, 43));
        Stroke(g, body, selected ? Color.FromArgb(244, 133, 226, 255)
                                : Color.FromArgb(175, 158, 199, 218),
            selected ? 2.9f : 1.3f);

        // The reference has a small faceted light reflection at the right edge.
        using (var reflection = Polygon(
            new PointF(r.Right - 40, r.Top + 4),
            new PointF(r.Right - 5, r.Top + 4),
            new PointF(r.Right - 31, r.Bottom - 5),
            new PointF(r.Right - 68, r.Bottom - 5)))
        using (var reflectionFill = new SolidBrush(Color.FromArgb(
            selected ? 45 : 21, 176, 222, 239)))
            g.FillPath(reflectionFill, reflection);

        float cy = r.Top + r.Height / 2f;
        float iconX = item.Key switch
        {
            "new" => 158f, "load" => 470f, "database" => 786f,
            "editor" => 1076f, "skins" => 151f,
            "achievements" => 469f, "credits" => 812f, _ => 150
        };
        DrawIcon(g, item.Icon, iconX, cy, Color.FromArgb(248, 244, 250, 253));

        float separatorX = item.Key switch
        {
            "new" => 204f, "load" => 519f, "database" => 833f,
            "editor" => 1122f, "skins" => 199f,
            "achievements" => 519f, "credits" => 866f, _ => 202
        };
        using (var red = new Pen(Color.FromArgb(254, 239, 25, 70), 2))
            g.DrawLine(red, separatorX, cy - 17, separatorX, cy + 17);

        DrawText(g, item.Label, item.TextX, cy - 13.5f, 22.5f,
            Color.FromArgb(252, 255, 255, 255), FontStyle.Bold);
    }

    private void DrawUpdate(Graphics g, bool hovering)
    {
        RectangleF r = new(1275, 804, 336, 63);
        using var body = ButtonOutline(r);
        if (hovering && UpdateEnabled)
            Stroke(g, body, Color.FromArgb(70, 126, 213, 246), 8);
        FillGradient(g, body, r,
            Color.FromArgb(225, 30, 48, 62),
            Color.FromArgb(215, 13, 28, 43));
        Stroke(g, body, hovering
            ? Color.FromArgb(221, 163, 230, 255)
            : Color.FromArgb(165, 144, 191, 209), 1.5f);
        DrawIcon(g, "updates", 1337, 835, Color.FromArgb(252, 247, 251, 254));
        using (var pen = new Pen(Color.FromArgb(252, 236, 21, 68), 2))
            g.DrawLine(pen, 1384, 821, 1384, 853);
        DrawText(g, UpdateText, 1405, 822, 20, Color.White, FontStyle.Bold);
    }

    private static void DrawIcon(Graphics g, string key, float cx, float cy, Color c)
    {
        var save = g.Save();
        g.TranslateTransform(cx, cy);
        using var pen = new Pen(c, 3f)
        {
            StartCap = LineCap.Round, EndCap = LineCap.Round,
            LineJoin = LineJoin.Round
        };
        using var brush = new SolidBrush(c);

        switch (key)
        {
            case "new":
                g.DrawRectangle(pen, -18, -18, 36, 36);
                g.DrawLine(pen, -11, 0, 11, 0);
                g.DrawLine(pen, 0, -11, 0, 11);
                break;
            case "load":
                g.DrawLines(pen,
                [
                    new PointF(-20, 15), new PointF(-20, -13),
                    new PointF(-8, -13), new PointF(-2, -7),
                    new PointF(19, -7), new PointF(19, 15),
                    new PointF(-20, 15)
                ]);
                break;
            case "database":
                g.DrawEllipse(pen, -18, -18, 36, 12);
                g.DrawArc(pen, -18, -6, 36, 12, 0, 180);
                g.DrawArc(pen, -18, 6, 36, 12, 0, 180);
                g.DrawLine(pen, -18, -12, -18, 12);
                g.DrawLine(pen, 18, -12, 18, 12);
                break;
            case "editor":
                g.DrawLine(pen, -17, -17, 17, 17);
                g.DrawLine(pen, 11, -18, -18, 11);
                g.DrawEllipse(pen, -21, 8, 10, 10);
                g.DrawEllipse(pen, 9, 10, 9, 9);
                g.DrawLine(pen, 8, -19, 16, -16);
                g.DrawLine(pen, 16, -16, 18, -8);
                break;
            case "skins":
                g.DrawPolygon(pen,
                [
                    new PointF(-20, 0), new PointF(-1, -19),
                    new PointF(18, -19), new PointF(18, 0),
                    new PointF(-1, 19)
                ]);
                g.DrawEllipse(pen, 8, -13, 5, 5);
                break;
            case "achievements":
                g.DrawArc(pen, -13, -18, 26, 27, 0, 180);
                g.DrawLine(pen, -13, -18, 13, -18);
                g.DrawArc(pen, -22, -16, 16, 21, 80, 210);
                g.DrawArc(pen, 6, -16, 16, 21, -110, 210);
                g.DrawLine(pen, 0, 9, 0, 18);
                g.DrawLine(pen, -12, 19, 12, 19);
                break;
            case "credits":
                g.DrawEllipse(pen, -19, -19, 38, 38);
                g.FillEllipse(brush, -2, -10, 4, 4);
                g.DrawLine(pen, 0, -1, 0, 11);
                break;
            case "updates":
                g.DrawArc(pen, -17, -17, 34, 34, 212, 200);
                g.DrawArc(pen, -17, -17, 34, 34, 32, 160);
                g.DrawLines(pen,
                [
                    new PointF(17, -9), new PointF(16, -17), new PointF(9, -14)
                ]);
                g.DrawLines(pen,
                [
                    new PointF(-17, 9), new PointF(-16, 17), new PointF(-9, 14)
                ]);
                break;
            case "sound":
                g.DrawPolygon(pen,
                [
                    new PointF(-16, -6), new PointF(-10, -6),
                    new PointF(-2, -13), new PointF(-2, 13),
                    new PointF(-10, 6), new PointF(-16, 6)
                ]);
                g.DrawArc(pen, -3, -13, 22, 26, -65, 130);
                g.DrawArc(pen, -1, -19, 30, 38, -65, 130);
                break;
            case "help":
                g.DrawEllipse(pen, -10, -10, 20, 20);
                using (var font = new Font("Segoe UI", 19, FontStyle.Bold, GraphicsUnit.Pixel))
                using (var format = new StringFormat
                    { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                    g.DrawString("?", font, brush, new RectangleF(-11, -12, 22, 23), format);
                break;
            case "minimize":
                g.DrawLine(pen, -8, 0, 9, 0);
                break;
            case "close":
                g.DrawLine(pen, -8, -8, 8, 8);
                g.DrawLine(pen, 8, -8, -8, 8);
                break;
        }

        g.Restore(save);
    }

    private static string? HitTest(PointF point)
    {
        // Update and utility controls are independent of the seven menu tiles.
        if (new RectangleF(1275, 804, 336, 63).Contains(point)) return "updates";
        if (point.Y >= 25 && point.Y <= 75 && point.X >= 1355 && point.X < 1637)
        {
            if (point.X < 1453) return "sound";
            if (point.X < 1514) return "help";
            if (point.X < 1573) return "minimize";
            return "close";
        }
        foreach (MenuItem item in Menu)
            if (item.Bounds.Contains(point))
                return item.Key;
        return null;
    }

    private PointF ToDesignPoint(Point p) =>
        new(p.X * DesignWidth / Math.Max(1, Width),
            p.Y * DesignHeight / Math.Max(1, Height));

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        string? next = HitTest(ToDesignPoint(e.Location));
        if (_hoverKey == next) return;
        _hoverKey = next;
        Cursor = next is null ? Cursors.Default : Cursors.Hand;
        Invalidate();
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        _hoverKey = null;
        Cursor = Cursors.Default;
        Invalidate();
    }

    protected override void OnMouseClick(MouseEventArgs e)
    {
        base.OnMouseClick(e);
        if (e.Button != MouseButtons.Left) return;
        string? command = HitTest(ToDesignPoint(e.Location));
        if (command != null && (command != "updates" || UpdateEnabled))
            CommandInvoked?.Invoke(command);
    }

    protected override bool IsInputKey(Keys keyData) =>
        (keyData & Keys.KeyCode) is Keys.Up or Keys.Down or Keys.Left or Keys.Right ||
        base.IsInputKey(keyData);

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.KeyCode is Keys.Right or Keys.Down)
            _keyboardIndex = (_keyboardIndex + 1) % Menu.Length;
        else if (e.KeyCode is Keys.Left or Keys.Up)
            _keyboardIndex = (_keyboardIndex + Menu.Length - 1) % Menu.Length;
        else if (e.KeyCode is Keys.Enter or Keys.Space)
        {
            CommandInvoked?.Invoke(Menu[_keyboardIndex].Key);
            e.SuppressKeyPress = true;
        }
        else return;

        e.Handled = true;
        _hoverKey = null;
        Invalidate();
    }
}
