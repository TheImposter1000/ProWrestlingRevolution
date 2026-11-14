using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace ProWrestlingRevolution;

/// <summary>
/// The General Worker Editor is PWR's single visual source of truth.
/// Other worker sections may lay out different data, but must use these
/// same field, biography, portrait, card, header and action surfaces.
/// </summary>
internal static class WorkerEditorTheme
{
    internal static readonly Color Ink = Color.FromArgb(9, 18, 31);
    internal static readonly Color Surface = Color.FromArgb(17, 34, 53);
    internal static readonly Color Dark = Color.FromArgb(11, 26, 42);
    internal static readonly Color Edge = Color.FromArgb(65, 132, 183);
    internal static readonly Color Accent = Color.FromArgb(39, 177, 232);
    internal static readonly Color FieldInk = Color.FromArgb(240, 247, 251);
    internal static readonly Color FieldText = Color.FromArgb(11, 28, 42);

    private static GraphicsPath Rounded(Rectangle rect, int diameter)
    {
        var path = new GraphicsPath();
        path.AddArc(rect.Left, rect.Top, diameter, diameter, 180, 90);
        path.AddArc(rect.Right - diameter, rect.Top, diameter, diameter, 270, 90);
        path.AddArc(rect.Right - diameter, rect.Bottom - diameter,
            diameter, diameter, 0, 90);
        path.AddArc(rect.Left, rect.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        return path;
    }

    internal static void PaintField(Graphics graphics, Rectangle client,
        bool enabled = true)
    {
        if (client.Width < 14 || client.Height < 14) return;
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        var bounds = new Rectangle(1, 1, client.Width - 3, client.Height - 3);
        using var path = Rounded(bounds, 10);
        using var gradient = new LinearGradientBrush(bounds,
            Color.FromArgb(255, 255, 255),
            Color.FromArgb(205, 220, 235), LinearGradientMode.Vertical);
        graphics.FillPath(gradient, path);
        using var border = new Pen(enabled
            ? Color.FromArgb(110, 145, 170)
            : Color.FromArgb(125, 149, 162), 1f);
        graphics.DrawPath(border, path);
    }

    internal static void PaintCard(Graphics graphics, Rectangle bounds)
    {
        if (bounds.Width < 13 || bounds.Height < 13) return;
        const int radius = 6, diameter = radius * 2;
        using var path = Rounded(bounds, diameter);
        using var gradient = new LinearGradientBrush(bounds,
            Color.FromArgb(210, 28, 35, 47),
            Color.FromArgb(220, 14, 22, 34), LinearGradientMode.Vertical);
        graphics.FillPath(gradient, path);
        using var pen = new Pen(Edge);
        graphics.DrawLine(pen, bounds.Left, bounds.Top + radius,
            bounds.Left, bounds.Bottom - radius);
        graphics.DrawArc(pen, bounds.Left, bounds.Bottom - diameter,
            diameter, diameter, 90, 90);
        graphics.DrawLine(pen, bounds.Left + radius, bounds.Bottom,
            bounds.Right - radius, bounds.Bottom);
        graphics.DrawArc(pen, bounds.Right - diameter, bounds.Bottom - diameter,
            diameter, diameter, 0, 90);
        graphics.DrawLine(pen, bounds.Right, bounds.Bottom - radius,
            bounds.Right, bounds.Top + radius);
    }

    internal static void PaintHeader(Graphics graphics, Rectangle bounds)
    {
        if (bounds.Width < 12 || bounds.Height < 12) return;
        const int diameter = 10;
        using var path = new GraphicsPath();
        path.AddArc(bounds.Left, bounds.Top, diameter, diameter, 180, 90);
        path.AddArc(bounds.Right - diameter - 1, bounds.Top,
            diameter, diameter, 270, 90);
        path.AddLine(bounds.Right - 1, bounds.Bottom - 1,
            bounds.Left, bounds.Bottom - 1);
        path.CloseFigure();
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using var gradient = new LinearGradientBrush(bounds,
            Color.FromArgb(36, 91, 144),
            Color.FromArgb(23, 54, 85), LinearGradientMode.Vertical);
        graphics.FillPath(gradient, path);
        using var pen = new Pen(Accent);
        graphics.DrawLine(pen, bounds.Left, bounds.Bottom - 1,
            bounds.Right - 1, bounds.Bottom - 1);
        using var edgePen = new Pen(Edge);
        graphics.DrawArc(edgePen, bounds.Left, bounds.Top,
            diameter, diameter, 180, 90);
        graphics.DrawArc(edgePen, bounds.Right - diameter - 1, bounds.Top,
            diameter, diameter, 270, 90);
        graphics.DrawLine(edgePen, bounds.Left, bounds.Top + diameter / 2,
            bounds.Left, bounds.Bottom - 1);
        graphics.DrawLine(edgePen, bounds.Right - 1, bounds.Top + diameter / 2,
            bounds.Right - 1, bounds.Bottom - 1);
    }

    /// <summary>
    /// General and Contracts must resolve the SAME photograph, including
    /// Default/Workers and legacy People/Worker picture pack folders.
    /// The returned detached bitmap belongs to the calling view.
    /// </summary>
    internal static Bitmap? LoadWorkerPortrait(AppPaths paths, string? storedPicture,
        string workerName)
    {
        string picture = Path.GetFileName(storedPicture ?? "");
        string[] folders =
        [
            paths.Pictures,
            Path.Combine(paths.Pictures, "Default", "Workers"),
            Path.Combine(paths.Pictures, "Workers"),
            Path.Combine(paths.Pictures, "People"),
            Path.Combine(paths.Pictures, "Worker")
        ];
        var candidates = new List<string>();
        if (!string.IsNullOrWhiteSpace(picture))
            candidates.AddRange(folders.Select(folder => Path.Combine(folder, picture)));
        if (!string.IsNullOrWhiteSpace(workerName))
            foreach (string extension in new[] { ".gif", ".jpg", ".jpeg", ".png", ".bmp" })
                candidates.AddRange(folders.Select(folder =>
                    Path.Combine(folder, workerName + extension)));

        foreach (string file in candidates.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (!File.Exists(file)) continue;
            try
            {
                using var stream = File.OpenRead(file);
                using var original = Image.FromStream(stream);
                return new Bitmap(original);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException
                                       or ArgumentException or OutOfMemoryException)
            {
                // An optional damaged picture does not prevent editing.
            }
        }
        return null;
    }

    internal static PictureBox CreatePortrait(string name)
    {
        return new PictureBox
        {
            Name = name,
            Size = new Size(91, 91),
            BackColor = Color.FromArgb(21, 41, 61),
            BorderStyle = BorderStyle.FixedSingle,
            SizeMode = PictureBoxSizeMode.Zoom
        };
    }

    internal static RichTextBox CreateBiography(string name, string text, bool readOnly)
    {
        return new RichTextBox
        {
            Name = name,
            BorderStyle = BorderStyle.None,
            Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
            ReadOnly = readOnly,
            DetectUrls = false,
            ScrollBars = RichTextBoxScrollBars.Vertical,
            BackColor = FieldInk,
            ForeColor = Color.FromArgb(16, 28, 41),
            Text = text
        };
    }

    internal static void PaintBiographyFrame(Graphics graphics, Rectangle rect)
    {
        if (rect.Width < 13 || rect.Height < 13) return;
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using var path = Rounded(rect, 12);
        using var brush = new SolidBrush(FieldInk);
        graphics.FillPath(brush, path);
        using var pen = new Pen(Edge);
        graphics.DrawPath(pen, path);
    }

    internal static void PaintAction(Graphics graphics, Rectangle bounds, bool primary,
        Color border)
    {
        if (bounds.Width <= 2 || bounds.Height <= 2) return;
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using var gradient = new LinearGradientBrush(bounds,
            primary ? Color.FromArgb(30, 145, 210) : Color.FromArgb(33, 52, 72),
            primary ? Color.FromArgb(10, 100, 165) : Color.FromArgb(12, 24, 40),
            LinearGradientMode.Vertical);
        graphics.FillRectangle(gradient, bounds);
        using var outline = new Pen(border, 1f);
        graphics.DrawRectangle(outline, 0, 0,
            bounds.Width - 1, bounds.Height - 1);
    }
}
