using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Windows.Forms;

namespace ProWrestlingRevolution;

// Owner-drawn, resolution-independent controls for the approved PWR Skills design.
// All accents are drawn from vectors: no screenshot assets, per-profile JPG loads,
// or native bright Windows controls are used.
internal static class SkillChrome
{
    internal static readonly Color Ink = Color.FromArgb(5, 19, 36);
    internal static readonly Color Deep = Color.FromArgb(5, 17, 33);
    internal static readonly Color Edge = Color.FromArgb(0, 207, 251);
    internal static readonly Color Cyan = Color.FromArgb(0, 229, 255);
    internal static readonly Color White = Color.FromArgb(242, 248, 255);
    internal static readonly Color Muted = Color.FromArgb(192, 218, 239);

    internal static GraphicsPath Rounded(RectangleF rect, float radius)
    {
        float size = Math.Min(Math.Min(rect.Width, rect.Height), radius * 2);
        var path = new GraphicsPath();
        path.AddArc(rect.Left, rect.Top, size, size, 180, 90);
        path.AddArc(rect.Right - size, rect.Top, size, size, 270, 90);
        path.AddArc(rect.Right - size, rect.Bottom - size, size, size, 0, 90);
        path.AddArc(rect.Left, rect.Bottom - size, size, size, 90, 90);
        path.CloseFigure();
        return path;
    }

    internal static void FillGradient(Graphics g, Rectangle rect,
        Color top, Color bottom)
    {
        if (rect.Width < 2 || rect.Height < 2) return;
        using var brush = new LinearGradientBrush(rect, top, bottom,
            LinearGradientMode.Vertical);
        g.FillRectangle(brush, rect);
    }

    internal static void DrawIcon(Graphics g, string icon, Rectangle rect, Color color)
    {
        g.SmoothingMode = SmoothingMode.AntiAlias;
        using var pen = new Pen(color, Math.Max(1.5f, rect.Width / 19f))
        { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round };
        using var thick = new Pen(color, Math.Max(2.4f, rect.Width / 12f))
        { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round };
        using var brush = new SolidBrush(color);
        var state = g.Save();
        g.TranslateTransform(rect.X, rect.Y);
        g.ScaleTransform(rect.Width / 40f, rect.Height / 40f);

        switch (icon)
        {
            case "ring":
                // Three ropes, four posts and an angled mat.
                g.DrawLine(pen, 5, 12, 34, 12);
                g.DrawLine(pen, 5, 18, 34, 18);
                g.DrawLine(pen, 5, 24, 34, 24);
                g.DrawPolygon(thick, [new Point(5, 12), new Point(34, 12),
                    new Point(34, 29), new Point(5, 29)]);
                g.DrawLine(pen, 5, 29, 20, 36);
                g.DrawLine(pen, 34, 29, 20, 36);
                g.DrawLine(pen, 5, 12, 5, 5);
                g.DrawLine(pen, 34, 12, 34, 5);
                g.FillEllipse(brush, 2, 3, 6, 6);
                g.FillEllipse(brush, 31, 3, 6, 6);
                break;
            case "microphone":
                using (var capsule = Rounded(new RectangleF(13, 2, 14, 25), 7))
                    g.FillPath(brush, capsule);
                using (var dark = new Pen(Color.FromArgb(35, 87, 136), 1.2f))
                {
                    g.DrawLine(dark, 15, 10, 25, 10);
                    g.DrawLine(dark, 15, 15, 25, 15);
                    g.DrawLine(dark, 15, 20, 25, 20);
                }
                g.DrawArc(thick, 8, 11, 24, 22, 0, 180);
                g.DrawLine(thick, 20, 33, 20, 37);
                g.DrawLine(thick, 11, 38, 29, 38);
                break;
            case "physical":
                // Bold bicep silhouette, not a Unicode character.
                using (var path = new GraphicsPath())
                {
                    path.AddBezier(3, 30, 5, 23, 7, 16, 13, 18);
                    path.AddBezier(13, 18, 18, 20, 18, 14, 17, 9);
                    path.AddBezier(17, 9, 20, 5, 27, 5, 29, 10);
                    path.AddBezier(29, 10, 31, 14, 28, 17, 27, 21);
                    path.AddBezier(27, 21, 34, 18, 37, 22, 36, 29);
                    path.AddBezier(36, 29, 32, 37, 9, 38, 3, 30);
                    g.FillPath(brush, path);
                }
                break;
            case "clipboard":
                g.DrawRectangle(thick, 7, 7, 26, 31);
                g.FillRectangle(brush, 15, 2, 10, 8);
                for (int y = 15; y <= 29; y += 7)
                {
                    g.DrawLine(pen, 12, y + 2, 15, y + 4);
                    g.DrawLine(pen, 15, y + 4, 19, y);
                    g.DrawLine(pen, 22, y + 3, 29, y + 3);
                }
                break;
            case "balance":
                g.DrawLine(thick, 20, 4, 20, 34);
                g.DrawLine(thick, 7, 10, 33, 10);
                g.DrawLine(thick, 9, 34, 31, 34);
                g.DrawLine(pen, 11, 11, 6, 24);
                g.DrawLine(pen, 11, 11, 17, 24);
                g.DrawArc(pen, 5, 22, 13, 8, 0, 180);
                g.DrawLine(pen, 29, 11, 23, 24);
                g.DrawLine(pen, 29, 11, 34, 24);
                g.DrawArc(pen, 22, 22, 13, 8, 0, 180);
                break;
            case "reset":
                g.DrawArc(thick, 5, 5, 29, 29, -82, 300);
                g.FillPolygon(brush, [new Point(5, 7),
                    new Point(18, 8), new Point(8, 20)]);
                break;
            case "save":
                using (var path = Rounded(new RectangleF(6, 4, 28, 32), 3))
                    g.DrawPath(thick, path);
                g.FillRectangle(brush, 12, 4, 15, 12);
                using (var dark = new SolidBrush(Color.FromArgb(10, 60, 110)))
                    g.FillRectangle(dark, 18, 5, 5, 8);
                g.FillRectangle(brush, 12, 23, 16, 12);
                break;
            default:
                g.DrawEllipse(pen, 7, 7, 26, 26);
                break;
        }
        g.Restore(state);
    }
}

/// <summary>Deep, illuminated navy card with softened corners and cyan edges.</summary>
internal sealed class SkillGlassPanel : Panel
{
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    internal bool Footer { get; init; }

    internal SkillGlassPanel()
    {
        DoubleBuffered = true;
        SetStyle(ControlStyles.ResizeRedraw |
                 ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.OptimizedDoubleBuffer, true);
        BackColor = SkillChrome.Ink;
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        if (ClientSize.Width < 12 || ClientSize.Height < 12) return;
        // The reference has genuinely rounded corners, not a square child
        // header painted on top of an ornamental rounded border.
        using var outline = SkillChrome.Rounded(
            new RectangleF(1, 1, ClientSize.Width - 2, ClientSize.Height - 2),
            Footer ? 9 : 10);
        var previous = Region;
        Region = new Region(outline);
        previous?.Dispose();
    }

    protected override void OnPaintBackground(PaintEventArgs e)
    {
        var rect = ClientRectangle;
        if (rect.Width < 8 || rect.Height < 8) return;
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        e.Graphics.Clear(SkillChrome.Deep);

        var shapeBounds = new RectangleF(2, 2, rect.Width - 4, rect.Height - 4);
        using var path = SkillChrome.Rounded(shapeBounds, Footer ? 9 : 10);
        using var background = new LinearGradientBrush(rect,
            Color.FromArgb(12, 39, 65), Color.FromArgb(4, 14, 29),
            LinearGradientMode.Vertical);
        background.InterpolationColors = new ColorBlend
        {
            Colors = [Color.FromArgb(11, 41, 72),
                      Color.FromArgb(6, 22, 43),
                      Color.FromArgb(3, 15, 32)],
            Positions = [0f, 0.34f, 1f]
        };
        e.Graphics.FillPath(background, path);

        // Reference: metallic navy with a soft reflected cyan highlight.
        // Keep the glow restrained at the field level and vivid at the rim.
        var shine = new Rectangle(3, 3, rect.Width - 6,
            Math.Max(3, rect.Height / (Footer ? 3 : 4)));
        using (var gleam = new LinearGradientBrush(shine,
            Color.FromArgb(55, 37, 120, 186),
            Color.FromArgb(0, 18, 63, 116), LinearGradientMode.Vertical))
            e.Graphics.FillRectangle(gleam, shine);

        using (var highlight = new Pen(Color.FromArgb(65, 0, 220, 255), 5f))
            e.Graphics.DrawPath(highlight, path);
        using (var edge = new Pen(SkillChrome.Edge, 1.65f))
            e.Graphics.DrawPath(edge, path);
        // A very fine inner bevel keeps the panel border dimensional.
        using (var innerLine = new Pen(Color.FromArgb(68, 150, 225, 255), 1f))
            e.Graphics.DrawLine(innerLine, 12, 4, rect.Right - 12, 4);
        if (Footer)
        {
            using var topLine = new Pen(Color.FromArgb(130, 85, 229, 255), 1f);
            e.Graphics.DrawLine(topLine, 12, 4, rect.Right - 12, 4);
        }
    }
}

/// <summary>Glossy, diagonally faceted header with bespoke illustrated icons.</summary>
internal sealed class SkillGlassHeader : Control
{
    private readonly string _heading;
    private readonly string _description;
    private readonly string _icon;

    internal SkillGlassHeader(string title, string description, string icon)
    {
        _heading = title;
        _description = description;
        _icon = icon;
        SetStyle(ControlStyles.UserPaint |
                 ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.OptimizedDoubleBuffer |
                 ControlStyles.ResizeRedraw, true);
        BackColor = Color.FromArgb(11, 40, 77);
        AccessibleRole = AccessibleRole.Grouping;
        AccessibleName = title;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var rect = ClientRectangle;
        if (rect.Width < 100 || rect.Height < 32) return;
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        SkillChrome.FillGradient(g, rect,
            Color.FromArgb(36, 118, 185), Color.FromArgb(5, 33, 67));
        // Top-half brushed-blue specular reflection, followed by a sharper
        // diagonal steel facet as shown in the approved mockup.
        using (var glint = new LinearGradientBrush(
            new Rectangle(0, 0, rect.Width, Math.Max(2, rect.Height / 2)),
            Color.FromArgb(70, 51, 151, 218),
            Color.FromArgb(0, 18, 61, 122), LinearGradientMode.Vertical))
            g.FillRectangle(glint, 0, 0, rect.Width, rect.Height / 2);
        using (var facet = new SolidBrush(Color.FromArgb(92, 20, 76, 134)))
        {
            g.FillPolygon(facet, [new Point(rect.Width * 63 / 100, 0),
                new Point(rect.Width * 86 / 100, 0),
                new Point(rect.Width * 75 / 100, rect.Bottom)]);
        }
        using (var facet = new SolidBrush(Color.FromArgb(55, 3, 19, 39)))
        {
            g.FillPolygon(facet, [new Point(rect.Width * 86 / 100, 0),
                new Point(rect.Width, 0),
                new Point(rect.Width, rect.Height),
                new Point(rect.Width * 75 / 100, rect.Height)]);
        }

        using var upper = new LinearGradientBrush(
            new Rectangle(0, 0, rect.Width, 3),
            Color.FromArgb(180, 0, 220, 255), Color.FromArgb(10, 0, 220, 255), 0f);
        g.FillRectangle(upper, 0, 0, rect.Width, 2);
        using var underline = new Pen(Color.FromArgb(170, 0, 206, 253), 1f);
        g.DrawLine(underline, 0, rect.Bottom - 1, rect.Right, rect.Bottom - 1);

        // Header typography shares one left inset with all other PWR cards.
        // Category logos are intentionally omitted for visual consistency.
        // Retain the clean metallic gradient and cyan dividing line.
        int left = 25;
        bool longHeading = _icon == "clipboard";
        float headingSize = longHeading
            ? (rect.Width >= 730 ? 15.5f : 13.4f)
            : rect.Width >= 720 ? 18.7f : 15.7f;
        using var titleFont = new Font(
            longHeading ? "Arial Narrow" : "Segoe UI",
            headingSize, FontStyle.Bold);
        using var subtitleFont = new Font("Segoe UI",
            rect.Width >= 730 ? 10.9f : 9f, FontStyle.Italic);
        Size measured = TextRenderer.MeasureText(g, _heading, titleFont,
            new Size(1800, rect.Height), TextFormatFlags.NoPadding |
            TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine);
        int preferredGap = _icon switch
        {
            "ring" => 30,
            "microphone" => 31,
            "physical" => 48,
            _ => 10
        };
        int maximumTitleWidth = rect.Width - left -
            (longHeading ? 215 : 135);
        bool fits = measured.Width <= maximumTitleWidth;
        int titleWidth = Math.Min(measured.Width + 4,
            Math.Max(50, maximumTitleWidth));
        int dividerX = left + titleWidth + preferredGap;
        int subtitleLeft = dividerX + 22;
        int subtitleWidth = rect.Width - subtitleLeft - 13;
        bool sideBySide = fits && subtitleWidth >=
            (longHeading ? 155 : 120);

        if (sideBySide)
        {
            TextRenderer.DrawText(g, _heading, titleFont,
                new Rectangle(left, 1, titleWidth, rect.Height - 2),
                SkillChrome.White, TextFormatFlags.VerticalCenter |
                TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix);
            using var split = new Pen(SkillChrome.Cyan, 2f);
            g.DrawLine(split, dividerX, rect.Height / 4,
                dividerX, rect.Height * 3 / 4);
            TextRenderer.DrawText(g, _description, subtitleFont,
                new Rectangle(subtitleLeft, 1, subtitleWidth, rect.Height - 2),
                SkillChrome.Muted, TextFormatFlags.VerticalCenter |
                TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix);
        }
        else
        {
            // Narrow windows keep the complete heading and description,
            // rather than drawing a divider through a clipped title.
            TextRenderer.DrawText(g, _heading, titleFont,
                new Rectangle(left, 2, rect.Width - left - 8,
                    Math.Max(17, rect.Height / 2)),
                SkillChrome.White, TextFormatFlags.VerticalCenter |
                TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
            using var split = new Pen(SkillChrome.Cyan, 1.6f);
            g.DrawLine(split, left, rect.Height / 2 + 3,
                left + 15, rect.Height / 2 + 3);
            TextRenderer.DrawText(g, _description, subtitleFont,
                new Rectangle(left + 23, rect.Height / 2 - 1,
                    rect.Width - left - 30, rect.Height / 2),
                SkillChrome.Muted, TextFormatFlags.VerticalCenter |
                TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
        }
    }
}

/// <summary>Glowing 0-100 rail with white/blue grip and full mouse/keyboard access.</summary>
internal sealed class SkillGlowSlider : Control
{
    internal event EventHandler? RatingChanged;
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    internal int? Rating { get; private set; }
    private bool _dragging;
    private bool _hover;

    internal SkillGlowSlider()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.Selectable |
                 ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.OptimizedDoubleBuffer |
                 ControlStyles.ResizeRedraw, true);
        TabStop = true;
        Cursor = Cursors.Hand;
        AccessibleRole = AccessibleRole.Slider;
        BackColor = SkillChrome.Ink;
    }

    internal void SetValue(int? value)
    {
        Rating = value;
        AccessibleDescription = value.HasValue
            ? "Skill rating " + value.Value : "Skill rating unset";
        Invalidate();
    }

    private void UpdateMouse(int mouseX)
    {
        int width = Math.Max(1, ClientSize.Width - 24);
        int rating = (int)Math.Round(100d *
            Math.Clamp(mouseX - 12, 0, width) / width);
        if (Rating == rating) return;
        SetValue(rating);
        RatingChanged?.Invoke(this, EventArgs.Empty);
    }

    protected override void OnMouseEnter(EventArgs e)
    {
        base.OnMouseEnter(e);
        _hover = true;
        Invalidate();
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        _hover = false;
        Invalidate();
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (!Enabled || e.Button != MouseButtons.Left) return;
        Focus();
        Capture = true;
        _dragging = true;
        UpdateMouse(e.X);
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (_dragging) UpdateMouse(e.X);
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        _dragging = false;
        Capture = false;
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        int? next = e.KeyCode switch
        {
            Keys.Left or Keys.Down => Math.Max(0, (Rating ?? 0) - 1),
            Keys.Right or Keys.Up => Math.Min(100, (Rating ?? 0) + 1),
            Keys.PageDown => Math.Max(0, (Rating ?? 0) - 10),
            Keys.PageUp => Math.Min(100, (Rating ?? 0) + 10),
            Keys.Home => 0,
            Keys.End => 100,
            _ => null
        };
        if (!next.HasValue) return;
        if (Rating != next)
        {
            SetValue(next);
            RatingChanged?.Invoke(this, EventArgs.Empty);
        }
        e.Handled = true;
        e.SuppressKeyPress = true;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(SkillChrome.Ink);
        if (Width < 35 || Height < 15) return;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        int cy = Height / 2;
        float left = 12f;
        float width = Width - 24f;
        var track = new RectangleF(left, cy - 4f, width, 8f);
        using (var shadow = SkillChrome.Rounded(
            new RectangleF(left - 1, cy - 6, width + 2, 12), 5))
        using (var shadowBrush = new SolidBrush(Color.FromArgb(42, 0, 118, 196)))
            g.FillPath(shadowBrush, shadow);

        using (var railPath = SkillChrome.Rounded(track, 3))
        using (var rail = new LinearGradientBrush(
            new Rectangle(0, cy - 4, Math.Max(1, Width), 9),
            Color.FromArgb(26, 72, 109),
            Color.FromArgb(10, 39, 71), LinearGradientMode.Vertical))
            g.FillPath(rail, railPath);
        using (var outline = new Pen(Color.FromArgb(150, 130, 191, 236), 1.1f))
        using (var railPath = SkillChrome.Rounded(track, 3))
            g.DrawPath(outline, railPath);

        float thumbX = Rating.HasValue
            ? left + width * Rating.Value / 100f : left;
        if (Rating.HasValue && Rating.Value > 0)
        {
            float length = Math.Max(1, thumbX - left);
            var fill = new RectangleF(left, cy - 3.7f, length, 7.4f);
            using var aura = new Pen(Color.FromArgb(55, 0, 217, 255), 13f)
            { StartCap = LineCap.Round, EndCap = LineCap.Round };
            g.DrawLine(aura, left + 3, cy, thumbX, cy);
            using var halo = new Pen(Color.FromArgb(125, 0, 215, 255), 9f)
            { StartCap = LineCap.Round, EndCap = LineCap.Round };
            g.DrawLine(halo, left + 3, cy, thumbX, cy);
            using var bright = new LinearGradientBrush(
                Rectangle.Round(fill), Color.FromArgb(48, 251, 255),
                SkillChrome.Cyan, LinearGradientMode.Vertical);
            using var fillPath = SkillChrome.Rounded(fill, 3);
            g.FillPath(bright, fillPath);
            using var highlight = new Pen(Color.FromArgb(190, 230, 255, 255), 1f);
            g.DrawLine(highlight, left + 3, cy - 2, thumbX, cy - 2);
        }

        float radius = Rating.HasValue ? _hover || Focused ? 10f : 8.7f : 7f;
        var glowRect = new RectangleF(thumbX - radius - 3, cy - radius - 3,
            (radius + 3) * 2, (radius + 3) * 2);
        using (var glow = new SolidBrush(Color.FromArgb(68, 0, 217, 255)))
            g.FillEllipse(glow, glowRect);
        var circle = new RectangleF(thumbX - radius, cy - radius,
            radius * 2, radius * 2);
        using (var gradient = new LinearGradientBrush(
            Rectangle.Round(circle),
            Color.FromArgb(255, 255, 255),
            Color.FromArgb(179, 216, 250),
            LinearGradientMode.Vertical))
            g.FillEllipse(gradient, circle);
        using var rim = new Pen(Color.FromArgb(24, 112, 178), 1.8f);
        g.DrawEllipse(rim, circle);
    }
}

/// <summary>Metallic numeric entry with real up/down spinners and nullable ratings.</summary>
internal sealed class SkillNumberSpinner : UserControl
{
    internal event Action<int?>? RatingChanged;
    private readonly TextBox _editor;
    private int? _rating;
    private bool _syncing;
    private bool _invalid;
    private bool _hover;

    internal SkillNumberSpinner(string field, int? value, bool canEdit)
    {
        Name = "SkillSpinner_" + field;
        AccessibleName = field + " numerical rating";
        BackColor = SkillChrome.Ink;
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        _editor = new TextBox
        {
            Name = "SkillNumber_" + field,
            BorderStyle = BorderStyle.None,
            TextAlign = HorizontalAlignment.Center,
            ForeColor = Color.White,
            BackColor = Color.FromArgb(12, 34, 59),
            Font = new Font("Segoe UI", 12.1f, FontStyle.Bold),
            TabStop = canEdit, ReadOnly = !canEdit
        };
        Controls.Add(_editor);
        Enabled = canEdit;
        _editor.KeyPress += (_, e) =>
        {
            if (!char.IsControl(e.KeyChar) && !char.IsDigit(e.KeyChar))
                e.Handled = true;
        };
        _editor.TextChanged += (_, _) =>
        {
            if (_syncing) return;
            string valueText = _editor.Text.Trim();
            if (valueText.Length == 0)
            {
                _invalid = false;
                _rating = null;
                RatingChanged?.Invoke(null);
            }
            else if (int.TryParse(valueText, NumberStyles.None,
                    CultureInfo.InvariantCulture, out int amount) &&
                     amount is >= 0 and <= 100)
            {
                _invalid = false;
                _rating = amount;
                RatingChanged?.Invoke(amount);
            }
            else _invalid = true;
            Invalidate();
        };
        _editor.KeyDown += (_, e) =>
        {
            if (e.KeyCode is Keys.Up or Keys.Down)
            {
                Step(e.KeyCode == Keys.Up ? 1 : -1);
                e.SuppressKeyPress = true;
            }
        };
        _editor.Enter += (_, _) => Invalidate();
        _editor.Leave += (_, _) => Invalidate();
        Resize += (_, _) => PlaceEditor();
        SetRating(value);
    }

    private void PlaceEditor()
    {
        int width = Math.Max(1, Width - 27);
        _editor.Bounds = new Rectangle(5,
            Math.Max(2, (Height - _editor.PreferredHeight) / 2),
            width, Math.Max(17, _editor.PreferredHeight));
    }

    internal void SetRating(int? value)
    {
        _syncing = true;
        try
        {
            _rating = value;
            _invalid = false;
            _editor.Text = value?.ToString(CultureInfo.InvariantCulture) ?? "";
        }
        finally { _syncing = false; }
        Invalidate();
    }

    internal string? ValidateNumber() => _invalid
        ? "Skill ratings must be whole numbers between 0 and 100."
        : null;

    private void Step(int offset)
    {
        if (!Enabled) return;
        int next = Math.Clamp((_rating ?? 0) + offset, 0, 100);
        SetRating(next);
        RatingChanged?.Invoke(next);
    }

    protected override void OnMouseEnter(EventArgs e)
    {
        base.OnMouseEnter(e);
        _hover = true; Invalidate();
    }
    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        _hover = false; Invalidate();
    }
    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (!Enabled) return;
        if (e.X >= Width - 22)
            Step(e.Y < Height / 2 ? 1 : -1);
        else _editor.Focus();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(SkillChrome.Ink);
        var rect = new Rectangle(1, 1, Math.Max(3, Width - 3),
            Math.Max(3, Height - 3));
        using (var path = SkillChrome.Rounded(rect, 4))
        using (var gradient = new LinearGradientBrush(rect,
            Color.FromArgb(23, 61, 95), Color.FromArgb(9, 25, 46),
            LinearGradientMode.Vertical))
            g.FillPath(gradient, path);
        using var edge = new Pen(_invalid
            ? Color.FromArgb(255, 115, 117)
            : _hover || _editor.Focused
                ? SkillChrome.Edge : Color.FromArgb(111, 169, 216), 1.1f);
        using var path2 = SkillChrome.Rounded(rect, 4);
        g.DrawPath(edge, path2);
        int x = Width - 11;
        using var arrow = new SolidBrush(_hover ? SkillChrome.Cyan : Color.White);
        g.FillPolygon(arrow, [new Point(x - 4, Height / 2 - 3),
            new Point(x + 4, Height / 2 - 3),
            new Point(x, Height / 2 - 8)]);
        g.FillPolygon(arrow, [new Point(x - 4, Height / 2 + 3),
            new Point(x + 4, Height / 2 + 3),
            new Point(x, Height / 2 + 8)]);
    }
}

/// <summary>Bright, bevelled and icon-bearing controls for the footer.</summary>
internal sealed class SkillChromeButton : Button
{
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    internal string IconKind { get; }
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    internal bool Primary { get; }
    private bool _hover;
    private bool _pressed;

    internal SkillChromeButton(string icon, bool primary)
    {
        IconKind = icon;
        Primary = primary;
        FlatStyle = FlatStyle.Flat;
        FlatAppearance.BorderSize = 0;
        SetStyle(ControlStyles.UserPaint |
                 ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.OptimizedDoubleBuffer |
                 ControlStyles.ResizeRedraw, true);
        BackColor = SkillChrome.Ink;
        ForeColor = Color.White;
        Font = new Font("Segoe UI", 12.7f, FontStyle.Bold);
        Cursor = Cursors.Hand;
    }

    protected override void OnMouseEnter(EventArgs e)
    { base.OnMouseEnter(e); _hover = true; Invalidate(); }
    protected override void OnMouseLeave(EventArgs e)
    { base.OnMouseLeave(e); _hover = false; Invalidate(); }
    protected override void OnMouseDown(MouseEventArgs e)
    { base.OnMouseDown(e); _pressed = true; Invalidate(); }
    protected override void OnMouseUp(MouseEventArgs e)
    { base.OnMouseUp(e); _pressed = false; Invalidate(); }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        var rect = ClientRectangle;
        g.Clear(SkillChrome.Ink);
        if (rect.Width < 12 || rect.Height < 12) return;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        var bounds = new Rectangle(3, 3, rect.Width - 6, rect.Height - 6);
        using var path = SkillChrome.Rounded(bounds, 6);
        Color top = Primary ? Color.FromArgb(19, 174, 240)
            : Color.FromArgb(35, 73, 115);
        Color bottom = Primary ? Color.FromArgb(2, 90, 159)
            : Color.FromArgb(7, 24, 48);
        if (_hover) top = Primary ? Color.FromArgb(63, 213, 255)
            : Color.FromArgb(55, 119, 175);
        if (_pressed) bottom = Color.FromArgb(12, 48, 84);
        using var gradient = new LinearGradientBrush(bounds, top, bottom,
            LinearGradientMode.Vertical);
        g.FillPath(gradient, path);
        using var outer = new Pen(
            Primary ? SkillChrome.Cyan : Color.FromArgb(144, 200, 246),
            Primary ? 2.4f : 1.2f);
        g.DrawPath(outer, path);

        if (Primary)
        {
            using var neon = new Pen(Color.FromArgb(92, 0, 221, 255), 4.4f);
            g.DrawPath(neon, path);
            using var inside = new Pen(Color.FromArgb(190, 168, 255, 255), 1f);
            g.DrawLine(inside, bounds.Left + 9, bounds.Top + 2,
                bounds.Right - 9, bounds.Top + 2);
        }
        int iconSize = Math.Clamp(rect.Height - 20, 22, 32);
        var flags = TextFormatFlags.NoPrefix | TextFormatFlags.VerticalCenter |
            TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis;
        int tw = TextRenderer.MeasureText(g, Text, Font, rect.Size, flags).Width;
        int total = Math.Min(rect.Width - 16, tw + iconSize + 9);
        int left = (rect.Width - total) / 2;
        SkillChrome.DrawIcon(g, IconKind,
            new Rectangle(left, (rect.Height - iconSize) / 2, iconSize, iconSize),
            Enabled ? SkillChrome.White : Color.FromArgb(123, 156, 181));
        var textBox = new Rectangle(left + iconSize + 9, 0,
            Math.Max(1, rect.Right - left - iconSize - 12), rect.Height);
        TextRenderer.DrawText(g, Text, Font, textBox,
            Enabled ? SkillChrome.White : Color.FromArgb(130, 162, 184), flags);
    }
}

/// <summary>Metallic pale-blue Preset selection using a safe reusable menu.</summary>
internal sealed class SkillChromePreset : Control
{
    internal event Action<string>? Selected;
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    internal string Selection { get; private set; } = "None";
    private readonly ContextMenuStrip _menu = new();
    private readonly string[] _items;
    private bool _hover;

    internal SkillChromePreset(params string[] items)
    {
        _items = items;
        Name = "SkillsPreset";
        Cursor = Cursors.Hand;
        TabStop = true;
        AccessibleRole = AccessibleRole.ComboBox;
        AccessibleName = "Skills preset";
        SetStyle(ControlStyles.UserPaint | ControlStyles.Selectable |
                 ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.OptimizedDoubleBuffer |
                 ControlStyles.ResizeRedraw, true);
        _menu.ShowImageMargin = false;
        _menu.BackColor = Color.FromArgb(10, 27, 48);
        _menu.ForeColor = Color.White;
        _menu.Font = new Font("Segoe UI", 10f);
        _menu.Renderer = new ToolStripProfessionalRenderer(
            new ProfessionalColorTable { UseSystemColors = false });
        // The menu is disposed only with this selector, never in Closed.
        foreach (string itemName in _items)
        {
            var item = new ToolStripMenuItem(itemName)
            {
                ForeColor = Color.White,
                BackColor = Color.FromArgb(11, 33, 58)
            };
            item.Click += (_, _) =>
            {
                if (!IsHandleCreated || IsDisposed) return;
                BeginInvoke((Action)(() =>
                {
                    if (IsDisposed) return;
                    SelectValue(itemName, notify: true);
                }));
            };
            _menu.Items.Add(item);
        }
    }

    internal void SelectValue(string value, bool notify = false)
    {
        if (!_items.Contains(value, StringComparer.OrdinalIgnoreCase))
            return;
        Selection = value;
        Invalidate();
        if (notify) Selected?.Invoke(value);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) _menu.Dispose();
        base.Dispose(disposing);
    }

    protected override void OnMouseEnter(EventArgs e)
    { base.OnMouseEnter(e); _hover = true; Invalidate(); }
    protected override void OnMouseLeave(EventArgs e)
    { base.OnMouseLeave(e); _hover = false; Invalidate(); }
    protected override void OnClick(EventArgs e)
    {
        base.OnClick(e);
        if (Enabled && !_menu.IsDisposed)
            _menu.Show(this, new Point(0, Height));
    }
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.KeyCode is Keys.Enter or Keys.Space or Keys.Down)
        {
            OnClick(EventArgs.Empty);
            e.Handled = true;
            e.SuppressKeyPress = true;
        }
    }
    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(SkillChrome.Ink);
        if (Width < 18 || Height < 12) return;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        var bounds = new Rectangle(1, 2, Width - 3, Height - 4);
        using var path = SkillChrome.Rounded(bounds, 5);
        using var fill = new LinearGradientBrush(bounds,
            Color.FromArgb(255, 255, 255),
            _hover ? Color.FromArgb(187, 226, 254) :
                Color.FromArgb(201, 219, 236), LinearGradientMode.Vertical);
        g.FillPath(fill, path);
        using var outline = new Pen(Color.FromArgb(104, 158, 201), 1f);
        g.DrawPath(outline, path);
        using var textFont = new Font("Segoe UI", 10.5f, FontStyle.Bold);
        TextRenderer.DrawText(g, Selection, textFont,
            new Rectangle(9, 0, Width - 33, Height),
            Color.FromArgb(9, 30, 52), TextFormatFlags.NoPrefix |
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
        int x = Width - 17, y = Height / 2;
        using var arrow = new SolidBrush(Color.FromArgb(5, 26, 48));
        g.FillPolygon(arrow, [new Point(x - 6, y - 4),
            new Point(x + 6, y - 4), new Point(x, y + 4)]);
    }
}
