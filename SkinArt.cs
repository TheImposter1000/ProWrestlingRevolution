using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace ProWrestlingRevolution;

/// <summary>
/// Creates original, replaceable skin image files. The launcher reads raster
/// images from disk rather than drawing GUI navigation buttons from a theme INI.
/// Images are generated once if missing; user-supplied image files are preserved.
/// </summary>
public static class SkinArt
{
    public const int WindowWidth = 1366;
    public const int WindowHeight = 768;
    public const int TopHeight = 32;
    public const int SidebarWidth = 57;
    public const int SidebarHeight = 736;
    public const int MainWidth = 1314;
    public const int MainHeight = 649;

    public static readonly (string Key, string Label, int Y)[] MenuIcons =
    [
        ("back", "Back to Main Menu", 88),
        ("new", "New Game", 150),
        ("load", "Load Game", 206),
        ("database", "Database Selector", 262),
        ("editor", "Database Editor", 318),
        ("skins", "Skins", 374),
        ("achievements", "Achievements", 562),
        ("credits", "Credits", 626)
    ];

    private sealed record Colors(Color Background, Color Sidebar, Color Surface, Color Accent,
        Color AccentLight, Color Text, Color Muted);

    private static Colors Theme(string name) => name.Equals("Steel", StringComparison.OrdinalIgnoreCase)
        ? new(Color.FromArgb(16, 20, 28), Color.FromArgb(16, 19, 26),
            Color.FromArgb(34, 39, 49), Color.FromArgb(221, 173, 83),
            Color.FromArgb(255, 222, 157), Color.White, Color.FromArgb(172, 180, 198))
        : new(Color.FromArgb(10, 15, 27), Color.FromArgb(15, 18, 29),
            Color.FromArgb(24, 34, 50), Color.FromArgb(29, 170, 213),
            Color.FromArgb(111, 233, 250), Color.White, Color.FromArgb(170, 196, 213));

    public static void EnsurePack(string folder, string name)
    {
        Directory.CreateDirectory(folder);
        var palette = Theme(name);
        Generate(folder, "Title_Screen(1).jpg", MainWidth, MainHeight, g => DrawMain(g, name, palette));
        Generate(folder, "Title_Screen.jpg", SidebarWidth, SidebarHeight, g => DrawSidebar(g, palette, false, null));
        Generate(folder, "Title_Screen_Alt.jpg", SidebarWidth, SidebarHeight, g => DrawSidebar(g, palette, true, null));
        Generate(folder, "Title_Screen_On.jpg", 1370, TopHeight, g => DrawTop(g, palette, true));
        Generate(folder, "Title_Screen_Off.jpg", 1370, TopHeight, g => DrawTop(g, palette, false));
        foreach (var (key, _, _) in MenuIcons)
        {
            if (key != "back")
                Generate(folder, "Title_Screen_Alt_Hover_" + key + ".jpg", SidebarWidth,
                    SidebarHeight, g => DrawSidebar(g, palette, true, key));
            Generate(folder, "Title_Screen_Hover_" + key + ".jpg", SidebarWidth,
                SidebarHeight, g => DrawSidebar(g, palette, false, key));
        }
    }

    private static void Generate(string folder, string name, int width, int height, Action<Graphics> action)
    {
        string target = Path.Combine(folder, name);
        if (File.Exists(target)) return;
        using var canvas = new Bitmap(width, height, PixelFormat.Format24bppRgb);
        using (var g = Graphics.FromImage(canvas))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
            action(g);
        }
        string temp = target + ".new";
        canvas.Save(temp, ImageFormat.Jpeg);
        try { File.Move(temp, target); }
        catch (IOException) { if (File.Exists(temp)) File.Delete(temp); }
    }

    private static void DrawMain(Graphics g, string skinName, Colors c)
    {
        var canvas = new Rectangle(0, 0, MainWidth, MainHeight);
        using (var gradient = new LinearGradientBrush(canvas, c.Surface, c.Background, 12))
            g.FillRectangle(gradient, canvas);

        using (var haze = new SolidBrush(Color.FromArgb(22, c.Accent)))
        {
            g.FillPolygon(haze,
            [
                new Point(140, 0), new Point(510, 0), new Point(280, MainHeight), new Point(0, MainHeight)
            ]);
            g.FillPolygon(haze,
            [
                new Point(855, 0), new Point(1215, 0), new Point(950, MainHeight), new Point(580, MainHeight)
            ]);
        }
        using (var border = new Pen(Color.FromArgb(75, c.Accent), 2))
        {
            g.DrawLine(border, 170, 0, 0, MainHeight);
            g.DrawLine(border, 1020, 0, 830, MainHeight);
            g.DrawLine(border, 1150, 0, 1350, MainHeight);
        }

        var random = new Random(2026);
        using (var dots = new SolidBrush(Color.FromArgb(43, c.AccentLight)))
            for (int i = 0; i < 390; i++)
            {
                int px = random.Next(0, MainWidth), py = random.Next(0, MainHeight);
                int r = random.Next(1, 3);
                g.FillEllipse(dots, px, py, r, r);
            }

        int cx = MainWidth / 2;
        var shield = new[]
        {
            new Point(cx - 350, 221), new Point(cx + 350, 221),
            new Point(cx + 300, 415), new Point(cx, 480), new Point(cx - 300, 415)
        };
        var inner = new[]
        {
            new Point(cx - 335, 238), new Point(cx + 335, 238),
            new Point(cx + 281, 400), new Point(cx, 462), new Point(cx - 281, 400)
        };
        using (var shadow = new SolidBrush(Color.FromArgb(190, 0, 0, 0)))
            g.FillPolygon(shadow, shield.Select(p => new Point(p.X + 8, p.Y + 10)).ToArray());
        using (var plate = new SolidBrush(c.Accent)) g.FillPolygon(plate, shield);
        using (var plate = new LinearGradientBrush(new Rectangle(cx - 355, 237, 710, 228),
            Color.FromArgb(43, 51, 72), Color.FromArgb(7, 12, 23), 90))
            g.FillPolygon(plate, inner);
        using (var outline = new Pen(Color.FromArgb(215, c.AccentLight), 3))
            g.DrawPolygon(outline, inner);

        using (var small = new Font("Segoe UI", 27, FontStyle.Bold))
            Center(g, "PRO WRESTLING", small, c.AccentLight, cx, 183);
        using (var large = new Font("Segoe UI Black", 135, FontStyle.Bold))
        {
            using var thick = new Pen(Color.FromArgb(18, 24, 34), 9) { LineJoin = LineJoin.Round };
            using var glyphs = new GraphicsPath();
            using var format = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
            glyphs.AddString("PWR", large.FontFamily, (int)FontStyle.Bold, 155f,
                new RectangleF(cx - 340, 252, 680, 147), format);
            g.DrawPath(thick, glyphs);
            using var fill = new LinearGradientBrush(new Rectangle(cx - 340, 252, 680, 150),
                Color.White, Color.FromArgb(168, 191, 209), LinearGradientMode.Vertical);
            g.FillPath(fill, glyphs);
        }
        using (var name = new Font("Segoe UI", 27, FontStyle.Bold))
            Center(g, "R E V O L U T I O N", name, Color.White, cx, 422);
        using (var subtitle = new Font("Segoe UI", 14, FontStyle.Bold))
            Center(g, "BUILD YOUR PROMOTION. SHAPE THE INDUSTRY.", subtitle, c.Muted, cx, 517);
        using (var skin = new Font("Segoe UI", 17, FontStyle.Bold))
            Center(g, skinName.ToUpperInvariant() + "  •  SKIN", skin, c.AccentLight, cx, 608);
    }

    private static void Center(Graphics g, string value, Font font, Color color, float x, float y)
    {
        using var brush = new SolidBrush(color);
        using var format = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
        g.DrawString(value, font, brush, new RectangleF(x - 650, y - 45, 1300, 90), format);
    }

    private static void DrawTop(Graphics g, Colors c, bool soundOn)
    {
        g.Clear(Color.FromArgb(31, 32, 38));
        using (var dark = new SolidBrush(c.Sidebar))
            g.FillRectangle(dark, 0, 15, 1370, 17);
        using (var pen = new Pen(Color.FromArgb(55, c.Accent), 1))
            g.DrawLine(pen, 0, 31, 1370, 31);
        using (var label = new SolidBrush(Color.White))
        {
            g.FillEllipse(label, 529, 5, 21, 22);
            g.FillEllipse(label, 816, 5, 21, 22);
            g.FillRectangle(label, 539, 5, 288, 22);
        }
        using (var font = new Font("Segoe UI", 9, FontStyle.Bold))
            CenterTop(g, "PRO WRESTLING REVOLUTION   •   0.3.0", font,
                Color.FromArgb(24, 24, 32), 682, 16);
        DrawTopCircle(g, 1218, 16, soundOn ? "on" : "off", c);
        DrawTopCircle(g, 1257, 16, "help", c);
        DrawTopCircle(g, 1296, 16, "min", c);
        DrawTopCircle(g, 1337, 16, "close", c);
    }

    private static void CenterTop(Graphics g, string t, Font f, Color color, float cx, float cy)
    {
        using var brush = new SolidBrush(color);
        using var format = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
        g.DrawString(t, f, brush, new RectangleF(cx - 400, cy - 16, 800, 32), format);
    }

    private static void DrawTopCircle(Graphics g, int x, int y, string type, Colors c)
    {
        using (var b = new SolidBrush(Color.White)) g.FillEllipse(b, x - 11, y - 11, 22, 22);
        using var pen = new Pen(type is "close" or "off" ? Color.FromArgb(220, 43, 61) : Color.FromArgb(23, 26, 36), 2.1f);
        pen.StartCap = LineCap.Round; pen.EndCap = LineCap.Round;
        if (type == "close")
        {
            g.DrawLine(pen, x - 4, y - 4, x + 4, y + 4);
            g.DrawLine(pen, x + 4, y - 4, x - 4, y + 4);
        }
        else if (type == "min") g.DrawLine(pen, x - 5, y + 3, x + 5, y + 3);
        else if (type == "help")
        {
            using var f = new Font("Segoe UI", 11, FontStyle.Bold);
            CenterTop(g, "?", f, Color.FromArgb(23, 26, 36), x, y - 1);
        }
        else
        {
            g.DrawPolygon(pen, [new Point(x - 7, y - 3), new Point(x - 3, y - 3),
                new Point(x + 1, y - 6), new Point(x + 1, y + 6),
                new Point(x - 3, y + 3), new Point(x - 7, y + 3)]);
            if (type == "off")
            {
                g.DrawLine(pen, x + 2, y - 5, x + 8, y + 5);
                g.DrawLine(pen, x + 8, y - 5, x + 2, y + 5);
            }
            else
            {
                g.DrawArc(pen, x - 2, y - 7, 15, 14, -55, 110);
            }
        }
    }

    private static void DrawSidebar(Graphics g, Colors c, bool hideBack, string? active)
    {
        g.Clear(c.Sidebar);
        using (var line = new Pen(Color.FromArgb(95, c.Accent), 1))
            g.DrawLine(line, SidebarWidth - 1, 0, SidebarWidth - 1, SidebarHeight);
        using (var thin = new Pen(Color.FromArgb(62, c.Accent), 1))
        {
            g.DrawLine(thin, 11, 445, 45, 445);
            g.DrawLine(thin, 11, 479, 45, 479);
        }
        foreach (var (key, _, cy) in MenuIcons)
        {
            if (hideBack && key == "back") continue;
            if (active == key)
            {
                using var selected = new SolidBrush(Color.FromArgb(74, c.Accent));
                g.FillRectangle(selected, 4, cy - 25, 49, 50);
            }
            DrawIcon(g, key, 28, cy, c.Text);
        }
    }

    private static void DrawIcon(Graphics g, string key, int x, int y, Color color)
    {
        using var pen = new Pen(color, 2.2f) { StartCap = LineCap.Round, EndCap = LineCap.Round,
            LineJoin = LineJoin.Round };
        var r = new Rectangle(x - 12, y - 15, 25, 30);
        using var path = new GraphicsPath();
        path.AddArc(r.Left, r.Top, 7, 7, 180, 90);
        path.AddArc(r.Right - 7, r.Top, 7, 7, 270, 90);
        path.AddArc(r.Right - 7, r.Bottom - 7, 7, 7, 0, 90);
        path.AddArc(r.Left, r.Bottom - 7, 7, 7, 90, 90);
        path.CloseFigure();
        g.DrawPath(pen, path);
        switch (key)
        {
            case "back":
                g.DrawLine(pen, x + 8, y, x - 6, y);
                g.DrawLine(pen, x - 6, y, x - 1, y - 5);
                g.DrawLine(pen, x - 6, y, x - 1, y + 5);
                break;
            case "new":
                g.DrawLine(pen, x - 7, y, x + 7, y);
                g.DrawLine(pen, x, y - 7, x, y + 7);
                break;
            case "load":
                for (int a = 0; a < 8; a++)
                {
                    double angle = a * Math.PI / 4;
                    int px = x + (int)(Math.Cos(angle) * 7);
                    int py = y + (int)(Math.Sin(angle) * 7);
                    g.FillEllipse(Brushes.White, px - 1, py - 1, 3, 3);
                }
                break;
            case "database":
                using (var font = new Font("Segoe UI", 9, FontStyle.Bold))
                    CenterTop(g, "DB", font, color, x, y);
                break;
            case "editor":
                g.DrawLine(pen, x - 7, y - 6, x + 7, y + 6);
                g.DrawLine(pen, x - 7, y + 6, x + 7, y - 6);
                g.DrawEllipse(pen, x - 8, y - 8, 4, 4);
                break;
            case "skins":
                g.DrawLine(pen, x - 8, y - 4, x - 4, y - 8);
                g.DrawLine(pen, x + 8, y - 4, x + 4, y - 8);
                g.DrawLine(pen, x - 4, y - 8, x + 4, y - 8);
                g.DrawLine(pen, x - 8, y - 4, x - 8, y + 8);
                g.DrawLine(pen, x + 8, y - 4, x + 8, y + 8);
                g.DrawLine(pen, x - 8, y + 8, x + 8, y + 8);
                break;
            case "achievements":
                g.DrawArc(pen, x - 7, y - 8, 14, 14, 0, 180);
                g.DrawLine(pen, x - 9, y - 7, x - 5, y + 1);
                g.DrawLine(pen, x + 9, y - 7, x + 5, y + 1);
                g.DrawLine(pen, x, y + 2, x, y + 7);
                g.DrawLine(pen, x - 5, y + 7, x + 5, y + 7);
                break;
            case "credits":
                g.DrawLine(pen, x - 7, y - 6, x + 7, y - 6);
                g.DrawLine(pen, x - 7, y - 1, x + 7, y - 1);
                g.DrawLine(pen, x - 7, y + 4, x + 7, y + 4);
                break;
        }
    }
}
