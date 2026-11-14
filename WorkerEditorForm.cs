using System.Data;
using System.Globalization;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.CompilerServices;
using System.Windows.Forms;

namespace ProWrestlingRevolution;

/// <summary>
/// Fixed, one-screen worker record layout using the approved PWR blue reference.
/// Every displayed value comes from the native worker profile. Section tabs
/// replace the central content rather than repeating data on General.
/// Database writes remain the responsibility of the separate editing layer.
/// </summary>
internal sealed class WorkerEditorForm : UserControl
{
    private static readonly Color Ink = Color.FromArgb(9, 18, 31);
    private static readonly Color Surface = Color.FromArgb(17, 34, 53);
    private static readonly Color Dark = Color.FromArgb(11, 26, 42);
    private static readonly Color Header = Color.FromArgb(27, 64, 101);
    private static readonly Color Edge = Color.FromArgb(65, 132, 183);
    private static readonly Color Accent = Color.FromArgb(39, 177, 232);
    private static readonly Color TextInk = Color.FromArgb(225, 239, 248);
    private static readonly Color FieldInk = Color.FromArgb(240, 247, 251);

    private readonly AppPaths _paths;
    private readonly SkinPalette _palette;
    private PrwWorkerProfile _profile;
    private readonly string? _databaseFile;
    private readonly string? _password;
    private sealed record WorkerFieldBinding(DataRow? Row, string Field, string? Table = null);
    private string _initialValueSnapshot = "";
    private readonly Label _saveStatus = new()
    {
        Name = "WorkerSaveStatus", Location = new Point(8, 510),
        Size = new Size(142, 120), ForeColor = Color.FromArgb(255, 160, 152),
        BackColor = Color.Transparent, Font = new Font("Segoe UI", 8f),
        AutoEllipsis = true
    };
    private readonly Dictionary<string, Button> _tabs = new(StringComparer.OrdinalIgnoreCase);
    private readonly Panel _content = new() { Name = "WorkerSectionContent", BackColor = Color.Transparent };
    private readonly Panel _sectionNavigation = new() { Name = "WorkerSectionNavigation", Dock = DockStyle.Fill, Margin = Padding.Empty,BackColor = Color.Transparent };
    private readonly Panel _body = new() { Name = "WorkerEditorBody", Dock = DockStyle.Fill, Margin = Padding.Empty, BackColor = Color.Transparent };
    private TableLayoutPanel? _workerPage;
    private readonly Dictionary<string, Control> _sectionViews =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, int> _sectionRecordIndices =
        new(StringComparer.OrdinalIgnoreCase);
    private Dictionary<string, object> _loadedSkillValues = new(StringComparer.Ordinal);

    internal int CachedSectionCount => _sectionViews.Count;
    internal int SectionViewBuildCount { get; private set; }
    internal long LastSectionSwitchMilliseconds { get; private set; }
    private readonly ToolTip _tips = new() { InitialDelay = 350, ReshowDelay = 80 };
    private string _active = "General";
    private int _recordIndex;
    private readonly Font _railNormal = new("Segoe UI", 10f);
    private readonly Font _railActive = new("Segoe UI", 10f, FontStyle.Bold);
    private readonly Dictionary<string, SidebarMenuRow> _mainMenuIcons = new();
    private readonly Panel _mainArea = new() { Name = "WorkerGeneralCanvas", BackColor = Color.Transparent };
    private readonly Panel _actionArea = new() { Name = "WorkerActionArea", BackColor = Color.Transparent };
    private TableLayoutPanel? _identityBiography;
    private ContextMenuStrip? _fieldMenu;
    internal ContextMenuStrip? FieldMenuForSmoke => _fieldMenu;

    internal string? RequestedNavigation { get; private set; }
    internal string ActiveSection => _active;
    internal event Action<string>? SectionChanged;
    internal event EventHandler? CloseRequested;

    internal void Close()
    {
        if (_active == "Skills")
        {
            ShowSection("General");
            return;
        }
        if (HasPendingChanges() &&
            MessageBox.Show(FindForm(),
                "Discard unsaved worker changes and return to the Database Editor?",
                "Unsaved Worker Changes", MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning) != DialogResult.Yes)
            return;
        CloseRequested?.Invoke(this, EventArgs.Empty);
    }

    private static string SnapshotValues(PrwWorkerProfile profile) =>
        string.Join("\u001e", profile.Sections.SelectMany(section =>
            section.Records.Rows.Cast<DataRow>().Select(row =>
                section.Table + ":" + string.Join("\u001f",
                    row.ItemArray.Select(value =>
                        value is DBNull ? "<null>" :
                        Convert.ToString(value, CultureInfo.InvariantCulture) ?? "")))));
    
    private bool HasPendingChanges() =>
        !string.Equals(SnapshotValues(_profile), _initialValueSnapshot,
            StringComparison.Ordinal) ||
        ActiveSkillsEditor()?.HasUncommittedInvalidInput() == true;

    internal WorkerEditorForm(AppPaths paths, SkinPalette palette,
        PrwWorkerProfile profile, string? databaseFile = null, string? password = null)
    {
        _paths = paths;
        _palette = palette;
        _profile = profile;
        _loadedSkillValues = CaptureSkillValues(profile);
        _databaseFile = databaseFile;
        _password = password;

        Name = "WorkerEditorForm";
        Text = "Pro Wrestling Revolution | Worker Editor";
        ClientSize = new Size(SkinArt.WindowWidth, SkinArt.WindowHeight);
        AutoScaleMode = AutoScaleMode.None;
        Font = new Font("Segoe UI", 9f);
        BackColor = Ink;
        ForeColor = TextInk;
        DoubleBuffered = true;
        KeyDown += (_, e) =>
        {
            if (e.KeyCode != Keys.Escape) return;
            Close();
            e.Handled = true;
        };

        var timer = System.Diagnostics.Stopwatch.StartNew();

        BuildFrame();

        System.Diagnostics.Debug.WriteLine(
            $"Worker Editor BuildFrame: {timer.ElapsedMilliseconds} ms");

        timer.Restart();

        ShowSection("General");
        _initialValueSnapshot = SnapshotValues(_profile);

        System.Diagnostics.Debug.WriteLine(
            $"Worker Editor ShowSection: {timer.ElapsedMilliseconds} ms");
        Disposed += (_, _) =>
        {
            foreach (var view in _sectionViews.Values)
                if (!view.IsDisposed) view.Dispose();
            _sectionViews.Clear();
            _fieldMenu?.Dispose();
            _tips.Dispose();
            _railNormal.Dispose();
            _railActive.Dispose();
        };
    }

    /// <summary>
    /// This is a child control hosted in the running game, never another
    /// desktop window. The surrounding Database Editor remains intact.
    /// </summary>
    internal void FitInside(Control host)
    {
        Location = Point.Empty;
        Size = host.ClientSize;
    }

    internal void LoadProfile(PrwWorkerProfile profile)
    {
        // All cached controls hold DataRows from the previous worker. Discard
        // them before swapping profiles so no changes can leak across workers.
        InvalidateSectionViews();
        _profile = profile;
        _loadedSkillValues = CaptureSkillValues(profile);
        RequestedNavigation = null;

        if (_identityBiography is TableLayoutPanel top)
        {
            top.SuspendLayout();

            foreach (Control old in top.Controls.Cast<Control>().ToArray())
            {
                var portrait = old.Controls
                    .Find("WorkerPicture", true)
                    .OfType<PictureBox>()
                    .FirstOrDefault();

                portrait?.Image?.Dispose();

                top.Controls.Remove(old);
                old.Dispose();
            }

            BuildIdentity(top);
            BuildBiography(top);

            top.ResumeLayout(true);
        }

        ShowSection("General");
        _saveStatus.Text = "";
        _initialValueSnapshot = SnapshotValues(_profile);
    }

    private void RequestMainMenu(string key)
    {
        RequestedNavigation = key;
        Close();
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (keyData == Keys.Escape)
        {
            Close();
            return true;
        }
        return base.ProcessCmdKey(ref msg, keyData);
    }

    private static Panel Card(string title, string name)
    {
        var card = new Panel
        {
            Name = name,
            Dock = DockStyle.Fill,
            Margin = new Padding(3),
            Padding = new Padding(1, 27, 1, 1),
            BackColor = Color.FromArgb(11, 26, 42)
        };

        card.Paint += (_, e) =>
        {
            var rect = new Rectangle(
                0, 0, card.ClientSize.Width - 1,
                card.ClientSize.Height - 1);

            if (rect.Width < 13 || rect.Height < 13)
                return;

            const int radius = 6;
            int diameter = radius * 2;

            using var path = new GraphicsPath();

            path.AddArc(rect.Left, rect.Top,
                diameter, diameter, 180, 90);

            path.AddArc(rect.Right - diameter, rect.Top,
                diameter, diameter, 270, 90);

            path.AddArc(rect.Right - diameter, rect.Bottom - diameter,
                diameter, diameter, 0, 90);

            path.AddArc(rect.Left, rect.Bottom - diameter,
                diameter, diameter, 90, 90);

            path.CloseFigure();

            using var gradient = new LinearGradientBrush(
                rect,
                Color.FromArgb(210, 28, 35, 47),
                Color.FromArgb(220, 14, 22, 34),
                LinearGradientMode.Vertical);

            e.Graphics.FillPath(gradient, path);

            using var pen = new Pen(Edge);

            e.Graphics.DrawLine(pen,
                rect.Left, rect.Top + radius,
                rect.Left, rect.Bottom - radius);

            e.Graphics.DrawArc(pen,
                rect.Left, rect.Bottom - diameter,
                diameter, diameter, 90, 90);

            e.Graphics.DrawLine(pen,
                rect.Left + radius, rect.Bottom,
                rect.Right - radius, rect.Bottom);

            e.Graphics.DrawArc(pen,
                rect.Right - diameter, rect.Bottom - diameter,
                diameter, diameter, 0, 90);

            e.Graphics.DrawLine(pen,
                rect.Right, rect.Bottom - radius,
                rect.Right, rect.Top + radius);
        };

        var heading = new Panel
        {
            Dock = DockStyle.None,
            Location = Point.Empty,
            Height = 30,
            Width = Math.Max(1, card.Width),
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
            BackColor = Color.Transparent,
            Name = name + "Heading"
        };
        heading.Paint += (_, e) =>
        {
            int width = heading.ClientSize.Width;
            int height = heading.ClientSize.Height;

            if (width < 12 || height < 12)
                return;

            const int diameter = 10;

            using var path = new GraphicsPath();

            path.AddArc(0, 0, diameter, diameter, 180, 90);
            path.AddArc(width - diameter - 1, 0,
                diameter, diameter, 270, 90);

            path.AddLine(width - 1, height - 1, 0, height - 1);
            path.CloseFigure();

            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

            using var gradient = new LinearGradientBrush(
                new Rectangle(0, 0, width, height),
                Color.FromArgb(36, 91, 144),
                Color.FromArgb(23, 54, 85),
                LinearGradientMode.Vertical);

            e.Graphics.FillPath(gradient, path);

            using var pen = new Pen(Accent);
            e.Graphics.DrawLine(
                pen, 0, height - 1, width - 1, height - 1);
            using var edgePen = new Pen(Edge);

            e.Graphics.DrawArc(
                edgePen, 0, 0,
                diameter, diameter, 180, 90);

            e.Graphics.DrawArc(
                edgePen, width - diameter - 1, 0,
                diameter, diameter, 270, 90);

            e.Graphics.DrawLine(
                edgePen, 0, diameter / 2,
                0, height - 1);

            e.Graphics.DrawLine(
                edgePen, width - 1, diameter / 2,
                width - 1, height - 1);
        };
  
        string symbol = title switch
        {
            "Personal Data" => "▤",
            "Roles" => "♟",
            "Physical Health" => "♥",
            "Default & Age-Related Settings" => "⚙",
            "Active Areas" => "◎",
            "Languages" => "▣",
            _ => ""
        };
        var label = new Label
        {
            Text = string.IsNullOrEmpty(symbol) ? title : symbol + "  " + title,
            Dock = DockStyle.Fill,
            Padding = new Padding(11, 0, 0, 0),
            TextAlign = ContentAlignment.MiddleLeft,
            Font = new Font("Segoe UI", 9.7f, FontStyle.Bold),
            ForeColor = Color.White,
            BackColor = Color.Transparent
        };
        heading.Controls.Add(label);
        card.Controls.Add(heading);
        return card;
    }

    /// <summary>
    /// Fixed-height section buttons from the approved mockup. The blue
    /// selection is painted rather than delegated to OS theme rendering.
    /// </summary>
    private sealed class WorkerSectionButton : Button
    {
        [System.ComponentModel.DesignerSerializationVisibility(
            System.ComponentModel.DesignerSerializationVisibility.Hidden)]
        internal bool Active { get; set; }

        private bool _hover;

        internal WorkerSectionButton()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint |
    ControlStyles.OptimizedDoubleBuffer |
    ControlStyles.UserPaint, true);
            BackColor = Color.FromArgb(11, 24, 39);
            BackColor = Color.Transparent;
            FlatStyle = FlatStyle.Flat;
            FlatAppearance.BorderSize = 0;
            Cursor = Cursors.Hand;
            Font = new Font("Segoe UI", 9.3f, FontStyle.Bold);
            ForeColor = Color.White;
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

        protected override void OnPaint(PaintEventArgs e)
        {
            if (Width < 3 || Height < 3) return;

            Control? page = Parent;

            while (page != null && page.Name != "WorkerPage")
                page = page.Parent;

            if (page?.BackgroundImage is Image artwork &&
                page.ClientSize.Width > 0 &&
                page.ClientSize.Height > 0)
            {
                int x = Left;
                int y = Top;

                for (Control? current = Parent;
                     current != null && current != page;
                     current = current.Parent)
                {
                    x += current.Left;
                    y += current.Top;
                }

                PaintPageBackdrop(e.Graphics, artwork, page,
                    ClientRectangle, x, y);
            }
            else
            {
                e.Graphics.Clear(BackColor);
            }

            var area = new Rectangle(0, 0, Width, Height);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            var bounds = new Rectangle(1, 1, Width - 3, Height - 3);
            int radius = 6;
            int diameter = radius * 2;

            using var rounded = new GraphicsPath();

            rounded.AddArc(bounds.Left, bounds.Top,
                diameter, diameter, 180, 90);

            rounded.AddArc(bounds.Right - diameter, bounds.Top,
                diameter, diameter, 270, 90);

            rounded.AddArc(bounds.Right - diameter, bounds.Bottom - diameter,
                diameter, diameter, 0, 90);

            rounded.AddArc(bounds.Left, bounds.Bottom - diameter,
                diameter, diameter, 90, 90);

            rounded.CloseFigure();
            var upper = Active
                ? Color.FromArgb(35, 155, 217)
                : _hover ? Color.FromArgb(40, 69, 101)
                    : Color.FromArgb(34, 52, 73);
            var lower = Active
                ? Color.FromArgb(7, 91, 150)
                : _hover ? Color.FromArgb(18, 47, 76)
                    : Color.FromArgb(13, 25, 40);
            using (var brush = new LinearGradientBrush(area, upper, lower,
                LinearGradientMode.Vertical))
                e.Graphics.FillPath(brush, rounded);
            using (var outline = new Pen(Active ? Accent : Edge, Active ? 1.5f : 1f))
                e.Graphics.DrawPath(outline, rounded);
            TextRenderer.DrawText(e.Graphics, Text, Font, area, Color.White,
                TextFormatFlags.HorizontalCenter |
                TextFormatFlags.VerticalCenter |
                TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix);
        }
    }

    private static Label TextLabel(string label) => new()
    {
        Text = label,
        Dock = DockStyle.Fill,
        TextAlign = ContentAlignment.MiddleLeft,
        Padding = new Padding(7, 0, 1, 0),
        ForeColor = TextInk,
        BackColor = Color.Transparent,
        Font = new Font("Segoe UI", 8.7f, FontStyle.Bold),
        AutoEllipsis = true
    };

    private sealed class WorkerValue : Control
    {
        [System.ComponentModel.DesignerSerializationVisibility(
            System.ComponentModel.DesignerSerializationVisibility.Hidden)]
        internal bool DropDownIndicator { get; set; }

        [System.ComponentModel.DesignerSerializationVisibility(
            System.ComponentModel.DesignerSerializationVisibility.Hidden)]
        internal bool CalendarIndicator { get; set; }

        [System.ComponentModel.DesignerSerializationVisibility(
            System.ComponentModel.DesignerSerializationVisibility.Hidden)]
        internal bool SpinnerIndicator { get; set; }

        internal WorkerValue()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer | ControlStyles.Selectable, true);
            AccessibleRole = AccessibleRole.PushButton;
            Cursor = Cursors.Hand;
            BackColor = Color.FromArgb(11, 24, 39);
            ForeColor = Color.FromArgb(11, 28, 42);
            Font = new Font("Segoe UI", 9.15f, FontStyle.Bold);
        }

        internal void Edit()
        {
            Control? current = Parent;
            while (current is not null && current is not WorkerEditorForm)
                current = current.Parent;
            if (current is WorkerEditorForm editor &&
                Tag is WorkerFieldBinding binding)
            {
                if (DropDownIndicator)
                    editor.OpenFieldChoices(this, binding);
                else
                    editor.EditField(this, binding);
            }
        }

        protected override void OnClick(EventArgs e)
        {
            base.OnClick(e);
            Edit();
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.KeyCode is Keys.Enter or Keys.Space)
            {
                Edit();
                e.Handled = true;
                e.SuppressKeyPress = true;
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        { 
            var rect = ClientRectangle;

            if (rect.Width < 14 || rect.Height < 14) return;

            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

            var bounds = new Rectangle(
                1, 1,
                rect.Width - 3,
                rect.Height - 3);

            const int diameter = 10;

            using var path = new GraphicsPath();

            path.AddArc(bounds.Left, bounds.Top,
                diameter, diameter, 180, 90);

            path.AddArc(bounds.Right - diameter, bounds.Top,
                diameter, diameter, 270, 90);

            path.AddArc(bounds.Right - diameter,
                bounds.Bottom - diameter,
                diameter, diameter, 0, 90);

            path.AddArc(bounds.Left,
                bounds.Bottom - diameter,
                diameter, diameter, 90, 90);

            path.CloseFigure();

            using var gradient = new LinearGradientBrush(bounds,
                Color.FromArgb(255,255,255),
                Color.FromArgb(205,220,235),
                LinearGradientMode.Vertical);

            e.Graphics.FillPath(gradient, path);

            using var border = new Pen(
                Color.FromArgb(110, 145, 170), 1f);

            e.Graphics.DrawPath(border, path);

            bool indicator = DropDownIndicator || CalendarIndicator || SpinnerIndicator;
            var textArea = new Rectangle(5, 0,
                Math.Max(1, rect.Width - (indicator ? 20 : 8)), rect.Height);
            TextRenderer.DrawText(e.Graphics, Text, Font, textArea, ForeColor,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter |
                TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis |
                TextFormatFlags.SingleLine);
            int x = rect.Width - 11, y = rect.Height / 2 + 1;
            using var arrow = new SolidBrush(Color.FromArgb(15, 35, 53));
            if (DropDownIndicator)
                e.Graphics.FillPolygon(arrow,
                [
                    new Point(x - 4, y - 3), new Point(x + 4, y - 3),
                    new Point(x, y + 2)
                ]);
            if (SpinnerIndicator)
            {
                e.Graphics.FillPolygon(arrow,
                [
                    new Point(x - 4, y - 2), new Point(x + 4, y - 2),
                    new Point(x, y - 6)
                ]);
                e.Graphics.FillPolygon(arrow,
                [
                    new Point(x - 4, y + 3), new Point(x + 4, y + 3),
                    new Point(x, y + 7)
                ]);
            }
            if (CalendarIndicator)
            {
                using var pen = new Pen(Color.FromArgb(32, 62, 84), 1f);
                e.Graphics.DrawRectangle(pen, rect.Width - 20, (rect.Height - 14) / 2, 14, 13);
                e.Graphics.DrawLine(pen, rect.Width - 20, (rect.Height - 14) / 2 + 4,
                    rect.Width - 6, (rect.Height - 14) / 2 + 4);
                e.Graphics.DrawLine(pen, rect.Width - 17, (rect.Height - 14) / 2 - 2,
                    rect.Width - 17, (rect.Height - 14) / 2 + 2);
            }
        }
    }

    private static WorkerValue ValueBox(string value, string name,
        bool dropDown = true) => new()
    {
        Name = name, Text = value, DropDownIndicator = dropDown,
        Dock = DockStyle.Fill,
        Margin = new Padding(2, 1, 5, 1),
        TabStop = true
    };

    private static void Bind(WorkerValue value, DataRow? row, string field,
        string? fallbackTable = null)
    {
        if (row is not null && row.Table.Columns.Contains(field))
            value.Tag = new WorkerFieldBinding(row, field, row.Table.TableName);
        else if (row is null && fallbackTable is not null)
            value.Tag = new WorkerFieldBinding(null, field, fallbackTable);
    }

    private static bool IsTrue(object? value)
    {
        if (value is null or DBNull) return false;
        if (value is bool b) return b;
        return string.Equals(Convert.ToString(value), "Yes", StringComparison.OrdinalIgnoreCase)
            || string.Equals(Convert.ToString(value), "True", StringComparison.OrdinalIgnoreCase)
            || string.Equals(Convert.ToString(value), "1", StringComparison.OrdinalIgnoreCase)
            || string.Equals(Convert.ToString(value), "-1", StringComparison.OrdinalIgnoreCase);
    }

    private static string Display(DataRow? record, string field)
    {
        if (record is null || !record.Table.Columns.Contains(field)) return "";
        object? raw = record[field];
        if (raw is null or DBNull) return "";
        if (raw is DateTime dt) return dt.ToString("yyyy-MM-dd");
        if (raw is bool b) return b ? "Yes" : "No";
        return Convert.ToString(raw) ?? "";
    }

    private static Dictionary<string, object> CaptureSkillValues(
        PrwWorkerProfile profile)
    {
        var originals = new Dictionary<string, object>(StringComparer.Ordinal);
        var record = profile.Sections.FirstOrDefault(section =>
                section.Table == "tblWorkerSkills")?.Records.Rows
            .Cast<DataRow>().FirstOrDefault();
        foreach (var field in PrwSchema.Tables.Single(t =>
                     t.Name == "tblWorkerSkills").Columns.Where(t => t != "WorkerUID"))
            originals[field] = record is not null && record.Table.Columns.Contains(field)
                ? record[field] : DBNull.Value;
        return originals;
    }

    private WorkerSkillsEditor? ActiveSkillsEditor() =>
        _content.Controls.OfType<WorkerSkillsEditor>().FirstOrDefault();

    private DataRow? First(string table) =>
        _profile.Sections.FirstOrDefault(s => s.Table.Equals(table, StringComparison.OrdinalIgnoreCase))
            ?.Records.Rows.Cast<DataRow>().FirstOrDefault();

    private static void PaintPageBackdrop(Graphics graphics, Image artwork,
        Control page, Rectangle destination, int originX, int originY)
    {
        // The image cache now prepares an exact-size bitmap. Do a direct
        // 1:1 blit of the correct portion instead of invoking GDI+ resampling
        // individually for every dropdown, card, and action panel.
        if (artwork.Size == page.ClientSize)
        {
            var state = graphics.Save();
            graphics.SetClip(destination);
            graphics.DrawImageUnscaled(artwork, -originX, -originY);
            graphics.Restore(state);
            return;
        }

        // This fallback is needed only during a resize while a newly
        // prepared bitmap is still being generated in the background.
        float scaleX = (float)artwork.Width / page.ClientSize.Width;
        float scaleY = (float)artwork.Height / page.ClientSize.Height;
        graphics.DrawImage(artwork, destination,
            originX * scaleX, originY * scaleY,
            destination.Width * scaleX, destination.Height * scaleY,
            GraphicsUnit.Pixel);
    }

    private void BuildFrame()
    {
        // An explicit fixed logical canvas matches the approved 1366x768
        // screenshot. The game uses DPI-unaware GDI scaling at runtime.
        var shell = new TableLayoutPanel
        {
            Name = "WorkerPopupLayout",
            Dock = DockStyle.Fill,
            Margin = Padding.Empty,
            Padding = Padding.Empty,
            RowCount = 1,
            ColumnCount = 1,
            BackColor = Ink
        };

        shell.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        shell.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        Controls.Add(shell);

        var page = new TableLayoutPanel
        {
            Name = "WorkerPage",
            ColumnCount = 2,
            RowCount = 1,
            Dock = DockStyle.Fill,
            Margin = Padding.Empty,
            Padding = Padding.Empty,
            BackColor = Ink
        };
        string backgroundPath = _palette.Asset(
            Path.Combine("Backgrounds", "Worker_Editor.jpg"));
        // Decompress once off the UI thread, then reuse a scaled bitmap when
        // navigating between worker profiles.
        page.HandleCreated += (_, _) =>
            _ = RestoreBackgroundAsync(page, backgroundPath);
        page.SizeChanged += (_, _) =>
        {
            if (page.IsHandleCreated && page.ClientSize.Width > 10 &&
                page.ClientSize.Height > 10 &&
                page.BackgroundImage?.Size != page.ClientSize)
                _ = RestoreBackgroundAsync(page, backgroundPath);
        };

        _workerPage = page;
        page.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 158));
        page.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        page.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        shell.Controls.Add(page, 0, 0);

        _sectionNavigation.BackColor = Color.Transparent;
        var sections = new[]
        {
            "Contracts", "Skills", "Popularity", "Attributes", "Business Skills",
            "Job History", "Business History", "Legacy", "Alter Ego"
        };
        for (int i = 0; i < sections.Length; i++)
        {
            string section = sections[i];
            var button = new WorkerSectionButton
            {
                Name = "WorkerSection_" + section.Replace(" ", ""),
                Text = section,
                Location = new Point(8, 13 + i * 44),
                Size = new Size(142, 39)
            };
            button.Click += (_, _) => ShowSection(section);
            _tabs[section] = button;
            _sectionNavigation.Controls.Add(button);
        }
        _sectionNavigation.Controls.Add(_saveStatus);
        page.Controls.Add(_sectionNavigation, 0, 0);

        _body.Paint += (_, e) =>
        {
            var area = new Rectangle(0, 0, Math.Max(1, _body.Width), Math.Max(1, _body.Height));
            using var fill = new LinearGradientBrush(area,
                Color.Transparent, Color.Transparent,
                LinearGradientMode.Vertical);
            e.Graphics.FillRectangle(fill, area);
        };
        page.Controls.Add(_body, 1, 0);

        var top = new TableLayoutPanel
        {
            Name = "WorkerIdentityBiographyRow",
            ColumnCount = 2, RowCount = 1,
            Margin = Padding.Empty,
            Padding = new Padding(3),
            BackColor = Color.Transparent
        };
        top.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 29.5f));
        top.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 70.5f));
        top.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        top.Paint += (_, e) =>
        {

            var rect = new Rectangle(
                0, 0,
                top.ClientSize.Width - 3,
                top.ClientSize.Height - 3);

            if (rect.Width < 13 || rect.Height < 13)
                return;

            const int diameter = 12;
            // Draw the correct portion of the Worker Editor background
            // across the entire identity and biography row.
            if (page.BackgroundImage is Image artwork &&
                page.ClientSize.Width > 0 &&
                page.ClientSize.Height > 0)
            {
                Point origin = Point.Empty;

                for (Control? current = top;
                     current != null && current != page;
                     current = current.Parent)
                {
                    origin.Offset(current.Left, current.Top);
                }

                PaintPageBackdrop(e.Graphics, artwork, page,
                    top.ClientRectangle, origin.X, origin.Y);

                using var glassTint = new SolidBrush(
                    Color.FromArgb(90, 9, 18, 31));

                e.Graphics.FillRectangle(glassTint, top.ClientRectangle);
            }

            using var path = new GraphicsPath();

            path.AddArc(rect.Left, rect.Top,
                diameter, diameter, 180, 90);

            path.AddArc(rect.Right - diameter, rect.Top,
                diameter, diameter, 270, 90);

            path.AddArc(rect.Right - diameter,
                rect.Bottom - diameter,
                diameter, diameter, 0, 90);

            path.AddArc(rect.Left,
                rect.Bottom - diameter,
                diameter, diameter, 90, 90);

            path.CloseFigure();

            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

            using var pen = new Pen(Edge, 1.5f);
            e.Graphics.DrawPath(pen, path);
        };

        _identityBiography = top;
        BuildIdentity(top);
        BuildBiography(top);

        _mainArea.Name = "WorkerGeneralCanvas";
        _content.Name = "WorkerSectionContent";
        _mainArea.Controls.Add(_content);
        _mainArea.Paint += (_, e) =>
        {
            var paintTime = System.Diagnostics.Stopwatch.StartNew();
            if (page.BackgroundImage is not Image artwork ||
            page.ClientSize.Width <= 0 ||
                page.ClientSize.Height <= 0 ||
                _mainArea.ClientSize.Width <= 0 ||
                _mainArea.ClientSize.Height <= 0)
                return;

            Point origin = Point.Empty;

            for (Control? current = _mainArea;
                 current != null && current != page;
                 current = current.Parent)
            {
                origin.Offset(current.Left, current.Top);
            }

            PaintPageBackdrop(e.Graphics, artwork, page,
                _mainArea.ClientRectangle, origin.X, origin.Y);

            using var glassTint = new SolidBrush(
                Color.FromArgb(90, 9, 18, 31));

            e.Graphics.FillRectangle(
                glassTint,
                _mainArea.ClientRectangle);
            PwrPerformanceLog.Record("Paint.WorkerCanvas", paintTime);
        };
        _body.Controls.Add(top);
        _body.Controls.Add(_mainArea);

        _body.Resize += (_, _) => LayoutWorkerCanvas();
        LayoutWorkerCanvas();
        BuildActions();
    }

    private void LayoutWorkerCanvas()
    {
        if (_identityBiography is null || _body.Width < 200) return;
        // Align the left edge and the right edge to the approved 1366x768
        // screenshot: a 3px inset and a total 8px horizontal gutter.
        int contentX = 3;
        int width = Math.Max(100, _body.ClientSize.Width - 8);
        int height = _body.ClientSize.Height;
        var timer = System.Diagnostics.Stopwatch.StartNew();

        bool skills = _active == "Skills";
        _identityBiography.Visible = !skills;
        _identityBiography.Bounds = new Rectangle(contentX, 10, width, 152);
        long topTime = timer.ElapsedMilliseconds;

        int generalY = skills ? 7 : 165;
        int generalHeight = skills
            ? Math.Max(300, height - generalY - 7)
            : Math.Max(340, Math.Min(520, height - generalY - 12));

        _mainArea.Bounds = new Rectangle(contentX, generalY, width, generalHeight);
        long mainTime = timer.ElapsedMilliseconds;

        _content.Bounds = _mainArea.ClientRectangle;
        long contentTime = timer.ElapsedMilliseconds;

        System.Diagnostics.Debug.WriteLine(
            $"Canvas Layout: Top={topTime} ms, " +
            $"Main={mainTime - topTime} ms, " +
            $"Content={contentTime - mainTime} ms, " +
            $"Total={contentTime} ms");
    }

    // The top row is a label-and-field editor, not a banner card.
    private void BuildIdentity(TableLayoutPanel top)
    {
        var card = new Panel
        {
            Name = "WorkerIdentity",
            Dock = DockStyle.Fill, Margin = Padding.Empty,
            BackColor = Color.Transparent
        };
        top.Controls.Add(card, 0, 0);
        var worker = First("tblWorker");

        var name = new Label
        {
            Name = "WorkerNameTitle",
            Text = "Name",
            Location = new Point(7, 3),
            Size = new Size(89, 22),
            ForeColor = Color.White, BackColor = Color.Transparent,
            Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleLeft
        };
        var shortName = new Label
        {
            Text = "Short Name",
            Location = new Point(7, 30),
            Size = new Size(89, 22),
            ForeColor = Color.White, BackColor = Color.Transparent,
            Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleLeft
        };
        var pictureLabel = new Label
        {
            Text = "Picture",
            Location = new Point(7, 65),
            Size = new Size(84, 22),
            ForeColor = Color.White, BackColor = Color.Transparent,
            Font = new Font("Segoe UI", 9.5f, FontStyle.Bold)
        };
        card.Controls.Add(name);
        card.Controls.Add(shortName);
        card.Controls.Add(pictureLabel);
        var identityLabels = new[] { name, shortName, pictureLabel };

        foreach (var fieldLabel in identityLabels)
        {
            fieldLabel.Visible = false;
        }

        card.Paint += (_, e) =>
        {
            foreach (var fieldLabel in identityLabels)
            {
                TextRenderer.DrawText(
                    e.Graphics,
                    fieldLabel.Text,
                    fieldLabel.Font,
                    fieldLabel.Bounds,
                    fieldLabel.ForeColor,
                    TextFormatFlags.Left |
                    TextFormatFlags.VerticalCenter |
                    TextFormatFlags.NoPrefix |
                    TextFormatFlags.NoPadding);
            }
        };
        var first = ValueBox(Display(worker, "Name"), "WorkerField_Name", false);
        first.Dock = DockStyle.None;
        first.Location = new Point(100, 3);
        first.Size = new Size(210, 22);
        var second = ValueBox(Display(worker, "ShortName"), "WorkerField_ShortName", false);
        second.Dock = DockStyle.None;
        second.Location = new Point(100, 30);
        second.Size = new Size(210, 22);
        Bind(first, worker, "Name");
        Bind(second, worker, "ShortName");
        card.Controls.Add(first);
        card.Controls.Add(second);

        var portrait = new PictureBox
        {
            Name = "WorkerPicture",
            Location = new Point(100, 55),
            Size = new Size(91,91),
            BackColor = Color.FromArgb(21, 41, 61),
            BorderStyle = BorderStyle.FixedSingle,
            SizeMode = PictureBoxSizeMode.Zoom
        };
        card.Controls.Add(portrait);

        // Picture Type belongs alongside the portrait, not in the
        // biography panel. This matches the reference editor grouping.
        var pictureTypeLabel = new Label
        {
            Name = "WorkerPictureTypeLabel",
            Text = "Picture Type",
            Location = new Point(205, 62),
            Size = new Size(138, 20),
            ForeColor = Color.White, BackColor = Color.Transparent,
            Font = new Font("Segoe UI", 8.8f, FontStyle.Bold)
        };
        var pictureType = ValueBox(Display(worker, "BiographyType"),
            "WorkerField_BiographyType", true);
        pictureType.Dock = DockStyle.None;
        pictureType.Location = new Point(205, 85);
        pictureType.Size = new Size(132, 23);
        Bind(pictureType, worker, "BiographyType");
        card.Controls.Add(pictureTypeLabel);
        card.Controls.Add(pictureType);
        string picture = Display(worker, "Picture");
        string safeName = Path.GetFileName(picture);

        if (string.IsNullOrWhiteSpace(safeName))
        {
            string workerName = Display(worker, "Name");

            string workerFolder = Path.Combine(
                _paths.Pictures, "Default", "Workers");

            foreach (string extension in new[]
            {
        ".gif", ".jpg", ".jpeg", ".png", ".bmp"
    })
            {
                string candidate = Path.Combine(
                    workerFolder, workerName + extension);

                if (File.Exists(candidate))
                {
                    safeName = Path.GetFileName(candidate);
                    break;
                }
            }
        }

        if (!string.IsNullOrWhiteSpace(safeName))
        {
            var paths = new[]
            {
                Path.Combine(_paths.Pictures, safeName),
                Path.Combine(_paths.Pictures, "Default", "Workers", safeName),
                Path.Combine(_paths.Pictures, "Workers", safeName),
                Path.Combine(_paths.Pictures, "People", safeName),
                Path.Combine(_paths.Pictures, "Worker", safeName)
            };
            var file = paths.FirstOrDefault(File.Exists);
            if (file is not null)
            {
                try
                {
                    using var stream = File.OpenRead(file);
                    using var image = Image.FromStream(stream);
                    // Only one detached bitmap is needed for each portrait.
                    portrait.Image = new Bitmap(image);
                }
                catch (Exception ex) when (
                    ex is IOException or ArgumentException or OutOfMemoryException)
                {
                    // Missing or invalid worker photographs are optional.
                }
            }
        }
        if (portrait.Image is null)
        {
            portrait.Controls.Add(new Label
            {
                Text = "NO IMAGE", Dock = DockStyle.Fill,
                ForeColor = Color.FromArgb(144, 178, 200),
                Font = new Font("Segoe UI", 7.8f, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleCenter
            });
        }

      
        
        card.Resize += (_, _) =>

        {
            int valueWidth = Math.Max(100, card.ClientSize.Width - 107);
            first.Width = valueWidth;
            second.Width = valueWidth;
            pictureType.Width = Math.Max(80,
                card.ClientSize.Width - pictureType.Left - 7);
        };
    }

    private void BuildBiography(TableLayoutPanel top)
    {
        var card = new Panel
        {
            Name = "WorkerBiographyPanel",
            Dock = DockStyle.Fill, Margin = Padding.Empty,
            BackColor = Color.Transparent
        };
        top.Controls.Add(card, 1, 0);
        var heading = new Label
        {
            Text = "Biography",
            Location = new Point(5, 5),
            Size = new Size(89, 22),
            Font = new Font("Segoe UI", 9.7f, FontStyle.Bold),
            ForeColor = Color.White, BackColor = Color.Transparent
        };
        var text = Display(First("tblWorkerBio"), "Biography");
        var bio = new RichTextBox
        {
            Name = "WorkerBiography",
            Location = new Point(96, 8),
            Size = new Size(605, 100),
            BorderStyle = BorderStyle.None,
            Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
            ReadOnly = _databaseFile is null,
            DetectUrls = false,
            TabStop = false,
            ScrollBars = RichTextBoxScrollBars.None,
            BackColor = FieldInk,
            ForeColor = Color.FromArgb(16, 28, 41),
            Text = string.IsNullOrWhiteSpace(text) ?
                "No biography has been entered for this worker." : text
        };
        card.Controls.Add(heading);
        heading.Visible = false;

        card.Paint += (_, e) =>
        {
            TextRenderer.DrawText(
                e.Graphics,
                heading.Text,
                heading.Font,
                heading.Bounds,
                heading.ForeColor,
                TextFormatFlags.Left |
                TextFormatFlags.VerticalCenter |
                TextFormatFlags.NoPrefix |
                TextFormatFlags.NoPadding);
        };

        var bioFrame = new Panel
        {
            Name = "WorkerBiographyFrame",
            Location = bio.Location,
            Size = bio.Size,
            Padding = new Padding(6, 5, 6, 5),
            BackColor = Color.Transparent
        };

        bioFrame.Paint += (_, e) =>
        {
            var rect = new Rectangle(
                1, 1,
                bioFrame.ClientSize.Width - 3,
                bioFrame.ClientSize.Height - 3);

            if (rect.Width < 13 || rect.Height < 13)
                return;

            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

            const int diameter = 12;

            using var path = new GraphicsPath();

            path.AddArc(rect.Left, rect.Top,
                diameter, diameter, 180, 90);

            path.AddArc(rect.Right - diameter, rect.Top,
                diameter, diameter, 270, 90);

            path.AddArc(rect.Right - diameter,
                rect.Bottom - diameter,
                diameter, diameter, 0, 90);

            path.AddArc(rect.Left,
                rect.Bottom - diameter,
                diameter, diameter, 90, 90);

            path.CloseFigure();

            using var brush = new SolidBrush(FieldInk);
            e.Graphics.FillPath(brush, path);

            using var pen = new Pen(Edge);
            e.Graphics.DrawPath(pen, path);
        };

        bio.Dock = DockStyle.Fill;
        bioFrame.Controls.Add(bio);
        card.Controls.Add(bioFrame);
        bio.TextChanged += (_, _) =>
        {
            if (_databaseFile is null) return;
            var section = _profile.Sections.FirstOrDefault(s => s.Table == "tblWorkerBio");
            if (section is null) return;
            var row = section.Records.Rows.Cast<DataRow>().FirstOrDefault();
            if (row is null)
            {
                row = section.Records.NewRow();
                row["WorkerUID"] = _profile.Identity.Uid;
                row["Biography"] = bio.Text;
                section.Records.Rows.Add(row);
            }
            else if (!Equals(Convert.ToString(row["Biography"]), bio.Text))
                row["Biography"] = bio.Text;
        };


        card.Resize += (_, _) =>
        {
            bioFrame.Width = Math.Max(100, card.ClientSize.Width - bioFrame.Left - 6);
            bioFrame.Height = Math.Max(70, card.ClientSize.Height - bioFrame.Top - 6);
        };
    }

    private static (string Label, string Field)[] Pairs(params string[] fields) =>
        fields.Select(x => (x.Replace("ID", " ID").Replace("Pounds", " Weight").Replace("Inches", "").Replace("WorkerUID", " Worker"), x)).ToArray();

    /// <summary>
    /// Fixed-height rows, matching the screenshot's evenly spaced controls.
    /// Neither textboxes nor checkboxes may be compressed below the legible
    /// height used by the original mockup.
    /// </summary>
    private static TableLayoutPanel MakeRows(Panel card,
        IReadOnlyList<(string Label, string Field)> fields, DataRow? source,
        int rowHeight = 22, float labelShare = 39f, int topPad = 2)
    {
        var grid = new TableLayoutPanel
        {
            Name = card.Name + "Fields",
            Dock = DockStyle.Top,
            Height = fields.Count * rowHeight + topPad + 1,
            ColumnCount = 2, RowCount = fields.Count,
            Margin = Padding.Empty, Padding = new Padding(1, topPad, 2, 0),
            BackColor = Color.FromArgb(11, 26, 42)
        };
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, labelShare));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f - labelShare));
        for (int i = 0; i < fields.Count; i++)
        {
            grid.RowStyles.Add(new RowStyle(SizeType.Absolute, rowHeight));
            grid.Controls.Add(TextLabel(fields[i].Label), 0, i);
            bool isWeight = fields[i].Field.Contains("Weight", StringComparison.OrdinalIgnoreCase);
            bool isDate = fields[i].Field.Contains("Date", StringComparison.OrdinalIgnoreCase) ||
                          fields[i].Field is "Born" or "SetToDebut";
            var value = ValueBox(Display(source, fields[i].Field),
                "WorkerField_" + fields[i].Field, !isWeight && !isDate);
            Bind(value, source, fields[i].Field,
                card.Name == "WorkerPhysical" ? "tblWorkerPhysical" : null);
            
value.CalendarIndicator = false;
value.SpinnerIndicator = isWeight;

if (isDate)
{
    var dateRow = new TableLayoutPanel
    {
        Dock = DockStyle.Fill,
        ColumnCount = 2,
        RowCount = 1,
        Margin = Padding.Empty,
        Padding = Padding.Empty,
        BackColor = Color.Transparent
    };

    dateRow.ColumnStyles.Add(
        new ColumnStyle(SizeType.Percent, 100));

    dateRow.ColumnStyles.Add(
        new ColumnStyle(SizeType.Absolute, 32));

    value.Dock = DockStyle.Fill;
    value.Margin = new Padding(2, 1, 4, 1);

    var calendarButton = new Button
    {
        Text = "▦",
        Dock = DockStyle.Fill,
        Margin = new Padding(0, 1, 2, 1),
        FlatStyle = FlatStyle.Flat,
        BackColor = Color.FromArgb(235, 242, 248),
        ForeColor = Color.FromArgb(15, 35, 53),
        Font = new Font("Segoe UI Symbol", 9f),
        TabStop = false
    };

    calendarButton.FlatAppearance.BorderColor =
        Color.FromArgb(110, 145, 170);

    dateRow.Controls.Add(value, 0, 0);
    calendarButton.Click += (_, _) => value.Edit();
    dateRow.Controls.Add(calendarButton, 1, 0);

    grid.Controls.Add(dateRow, 1, i);
}
else
{
    if (isWeight)
    {
        value.Dock = DockStyle.Left;
        value.Width = 98;
    }

    grid.Controls.Add(value, 1, i);
}

        }
        card.Controls.Add(grid);
        grid.BringToFront();
        return grid;
    }

    private static TableLayoutPanel MakeChecks(Panel card,
        IReadOnlyList<(string Label, string Field)> fields, DataRow? source,
        int rowHeight = 22)
    {
        var grid = new TableLayoutPanel
        {
            Name = card.Name + "Fields",
            Dock = DockStyle.Top,
            Height = fields.Count * rowHeight + 3,
            ColumnCount = 1, RowCount = fields.Count,
            Padding = new Padding(11, 2, 2, 0),
            Margin = Padding.Empty,
            BackColor = Color.Transparent
        };

        grid.Paint += (_, e) =>
        {
            if (card.Name == "WorkerRoles")
                return;
           
            var rect = grid.ClientRectangle;

            if (rect.Width <= 0 || rect.Height <= 0)
                return;

            using var gradient = new LinearGradientBrush(
                rect,
                Color.FromArgb(29, 49, 73),
                Color.FromArgb(12, 23, 39),
                LinearGradientMode.Vertical);

            e.Graphics.FillRectangle(gradient, rect);
        };
        grid.Paint += (_, e) =>
        {
            var rect = grid.ClientRectangle;

            if (rect.Width <= 0 || rect.Height <= 0)
                return;

            using var gradient = new LinearGradientBrush(
                rect,
                Color.FromArgb(29, 49, 73),
                Color.FromArgb(12, 23, 39),
                LinearGradientMode.Vertical);

            e.Graphics.FillRectangle(gradient, rect);
        };


        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        for (int i = 0; i < fields.Count; i++)
        {
            var (label, field) = fields[i];
            grid.RowStyles.Add(new RowStyle(SizeType.Absolute, rowHeight));
            var tick = new CheckBox
            {
                Name = "WorkerField_" + field,
                Text = label,
                Dock = DockStyle.Fill,
                AutoCheck = true,
                TabStop = true,
                Checked = source is not null && source.Table.Columns.Contains(field) &&
                    IsTrue(source[field]),
                ForeColor = TextInk,
                Font = new Font("Segoe UI", 9.1f, FontStyle.Regular),
                Margin = Padding.Empty,
                BackColor = Color.Transparent
            };
            if (source is not null && source.Table.Columns.Contains(field))
            {
                tick.CheckedChanged += (_, _) =>
                {
                    var col = source.Table.Columns[field]!;
                    if (col.DataType == typeof(bool))
                        source[field] = tick.Checked;
                    else if (col.DataType == typeof(string))
                        source[field] = tick.Checked ? "Yes" : "No";
                    else
                        source[field] = Convert.ChangeType(tick.Checked ? 1 : 0,
                            col.DataType, CultureInfo.InvariantCulture);
                };
            }
            grid.Controls.Add(tick, 0, i);
        }
        card.Controls.Add(grid);
        grid.BringToFront();
        return grid;
    }

    private static TableLayoutPanel Stack(params (Panel Panel, int Weight)[] groups)
    {
        var result = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = groups.Length,
            BackColor = Color.Transparent
        };
        result.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        int sum = groups.Sum(x => x.Weight);
        for (int i = 0; i < groups.Length; i++)
        {
            result.RowStyles.Add(new RowStyle(SizeType.Percent, groups[i].Weight * 100f / sum));
            result.Controls.Add(groups[i].Panel, 0, i);
        }
        return result;
    }

    private void BuildGeneral()
    {
        var worker = First("tblWorker");
        // Two structural rows make the 4-button action strip part of the
        // layout instead of a floating overlay that clips in DrawToBitmap.
        // The left columns span both rows; the right columns reserve their
        // second row for buttons, exactly as in the approved mockup.
        var grid = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, Name = "WorkerGeneralGrid",
            ColumnCount = 4, RowCount = 2, Margin = Padding.Empty,
            BackColor = Color.Transparent
        };
        foreach (float percent in new[] { 26f, 24f, 28f, 22f })
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, percent));
        grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 455));
        grid.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var personal = Card("Personal Data", "WorkerPersonalData");
        MakeRows(personal, [
            ("Born", "Born"), ("Set To Debut", "SetToDebut"),
            ("Death Date", "DeathDate"), ("Regenerates", "Regenerates"),
            ("Gender ID", "GenderID"), ("Pronouns", "Pronouns"),
            ("Sexuality", "Sexuality"), ("Relationship", "Relationship"),
            ("Race", "Race"), ("Nationality", "Nationality"),
            ("Based In", "BasedIn"), ("Game Status", "GameStatus"),
            ("In Ring Status", "InRingStatus"), ("Celebrity", "Celebrity"),
            ("Style", "Style"), ("Body Type", "BodyType"),
            ("Height", "HeightInches"), ("Weight", "WeightPounds"),
            ("Min Weight", "MinWeightPounds"), ("Max Weight", "MaxWeightPounds")
        ], worker, rowHeight: 23);
        personal.Dock = DockStyle.Top;
        personal.Height = 503;
        grid.Controls.Add(personal, 0, 0);
        grid.SetRowSpan(personal, 2);

        var loyalty = Card("", "WorkerLoyalty");
        MakeRows(loyalty, [
            ("Loyalty", "LoyaltyWorkerUID"), ("Loyalty Type", "LoyaltyType"),
            ("Freelance", "Freelance"), ("User Character", "UserCharacter")
        ], worker, labelShare: 48f);
        var roles = Card("Roles", "WorkerRoles");
        var roleChecks = MakeChecks(roles, [
            ("Wrestler", "RoleWrestler"), ("Occasional Wrestler", "RolePartTimeWrestler"),
            ("Manager", "RoleManager"), ("On-Screen Personality", "RoleOnScreenPersonality"),
            ("Play By Play", "RolePlayByPlay"), ("Colour Commentator", "RoleColour"),
            ("Referee", "RoleReferee"), ("Road Agent", "RoleProducer")
        ], worker, rowHeight: 20);
        var roleBoxes = roleChecks.Controls
    .OfType<CheckBox>()
    .ToArray();

        for (int i = 0; i < roleBoxes.Length; i++)
        {
            var tick = roleBoxes[i];

            roleChecks.Controls.Remove(tick);

            tick.Dock = DockStyle.None;
            tick.Location = new Point(14, 55 + i * 20);
            tick.Size = new Size(250, 20);

            roles.Controls.Add(tick);
        }

        roles.Controls.Remove(roleChecks);
        roleChecks.Dispose();
        var competitor = new Panel
        {
            Name = "WorkerCompetesVs", Dock = DockStyle.None,
            Location = new Point(3, 28),
            Height = 26,
            BackColor = Color.Transparent,
            Padding = new Padding(3, 3, 4, 2)
        };

        var competitorLabel = TextLabel("Competes Vs.");
        competitorLabel.Dock = DockStyle.None;
        competitorLabel.Location = new Point(-3,32);
        competitorLabel.Size = new Size(90, 22);

        var competitorValue = ValueBox(
            Display(worker, "CompetesVs"),
            "WorkerField_CompetesVs");

        competitorValue.Dock = DockStyle.None;
        competitorValue.Location = new Point(102, 32);
        competitorValue.Size = new Size(160, 25);

        roles.Controls.Add(competitorLabel);
        Bind(competitorValue, worker, "CompetesVs");
        roles.Controls.Add(competitorValue);

        competitorLabel.BringToFront();
        competitorValue.BringToFront();

        roles.Resize += (_, _) =>
        {
            competitorValue.Width = Math.Max(
                100,
                roles.ClientSize.Width - competitorValue.Left - 6);

            foreach (var tick in roleBoxes)
            {
                tick.Width = Math.Max(
                    100,
                    roles.ClientSize.Width - tick.Left - 6);
            }
        };

        var physical = Card("Physical Health", "WorkerPhysical");
        MakeRows(physical, [
            ("Head Condition", "HeadCondition"), ("Body Condition", "BodyCondition"),
            ("Arms Condition", "ArmsCondition"), ("Legs Condition", "LegsCondition")
        ], First("tblWorkerPhysical"), labelShare: 60f);
        var middleColumn = Stack((loyalty, 27), (roles, 47), (physical, 26));
        middleColumn.Dock = DockStyle.Top;
        middleColumn.Height = 503;
        grid.Controls.Add(middleColumn, 1, 0);
        grid.SetRowSpan(middleColumn, 2);

        var settings = Card("Default & Age-Related Settings", "WorkerSettings");
        MakeRows(settings, [
            ("Move Set", "DefaultMoveSet"),
            ("Face Gimmick", "FaceGimmick"),
            ("...And Basis", "FaceGimmickBasis"),
            ("Heel Gimmick", "HeelGimmick"),
            ("...And Basis", "HeelGimmickBasis"),
            ("Mask", "Mask"),
            ("Hair", "Hair"),
            ("Maturity Age", "MaturityAge"),
            ("Decline Age", "DeclineAge"),
            ("Relevancy Age", "RelevancyAge"),
            ("Retirement Age", "RetirementAge"),
            ("Career Goal", "CareerGoal")
        ], worker, rowHeight: 27);
        settings.Dock = DockStyle.Fill;
        grid.Controls.Add(settings, 2, 0);

        var regions = Card("Active Areas", "WorkerActiveAreas");
        MakeChecks(regions, [
            ("USA", "ActiveUSA"), ("Canada", "ActiveCanada"),
            ("Mexico", "ActiveMexico"), ("British Isles", "ActiveBritishIsles"),
            ("Japan", "ActiveJapan"), ("Europe", "ActiveEurope"),
            ("Oceania", "ActiveOceania"), ("India", "ActiveIndia")
        ], worker, rowHeight: 21);
        var languages = Card("Languages", "WorkerLanguages");
        MakeRows(languages, [
            ("English", "LanguageEnglish"), ("Japanese", "LanguageJapanese"),
            ("French", "LanguageFrench"), ("Spanish", "LanguageSpanish"),
            ("Mediterranean", "LanguageMediterranean"),
            ("Germanic", "LanguageGermanic"), ("Slavic", "LanguageSlavic"),
            ("Hindi", "LanguageHindi")
        ], worker, rowHeight: 23, labelShare: 48f, topPad: 6);
        var rightColumn = Stack((regions, 47), (languages, 53));
        rightColumn.Dock = DockStyle.Fill;
        grid.Controls.Add(rightColumn, 3, 0);

        _actionArea.Dock = DockStyle.Fill;
        _actionArea.Margin = Padding.Empty;
        // The approved action strip occupies y=686..722 on a 1366x768
        // logical canvas. The action area is 65px high, including padding.
        _actionArea.Padding = new Padding(8, 26, 3, 3);
        grid.Controls.Add(_actionArea, 2, 1);
        grid.SetColumnSpan(_actionArea, 2);
        _content.Controls.Add(grid);
    }

    /// <summary>
    /// Tab views are built once per worker, then detached and reattached
    /// without destroying hundreds of WinForms controls on every visit.
    /// Caches are invalidated whenever a different worker is loaded.
    /// </summary>
    private void ShowSection(string section)
    {
        if (section == _active && _sectionViews.TryGetValue(section, out var displayed) &&
            displayed.Parent == _content)
            return;

        if (_active == "Skills" &&
            ActiveSkillsEditor()?.ValidateBeforeLeaving() == false)
            return;

        var timer = System.Diagnostics.Stopwatch.StartNew();
        bool existingPage = _sectionViews.TryGetValue(section, out var target) &&
            !target.IsDisposed;
        _sectionRecordIndices[_active] = _recordIndex;
        _active = section;
        _recordIndex = _sectionRecordIndices.GetValueOrDefault(section, 0);

        foreach (var (name, button) in _tabs)
        {
            // Preserve the exact v0.6.2 General tab highlight behavior.
            bool selected = section == "General"
                ? name == "Alter Ego"
                : name.Equals(section, StringComparison.OrdinalIgnoreCase);
            if (button is WorkerSectionButton tab)
            {
                tab.Active = selected;
                tab.Invalidate();
            }
        }

        _body.SuspendLayout();
        _content.SuspendLayout();
        try
        {
            // _actionArea is shared by General and secondary sections. It
            // must never be disposed with a tab, or the Save action vanishes.
            if (_actionArea.Parent is Control actionParent)
                actionParent.Controls.Remove(_actionArea);

            foreach (Control previous in _content.Controls.Cast<Control>().ToArray())
                _content.Controls.Remove(previous);

            if (_sectionViews.TryGetValue(section, out var cached) &&
                !cached.IsDisposed)
            {
                _content.Controls.Add(cached);
                RestoreCachedActions(cached, section);
            }
            else
            {
                if (section == "General") BuildGeneral();
                else if (section == "Skills") BuildSkills();
                else BuildSecondary(section);
                var rendered = _content.Controls.Cast<Control>().Single();
                _sectionViews[section] = rendered;
                SectionViewBuildCount++;
            }

            SetImmersiveSkillsLayout(section == "Skills");
        }
        finally
        {
            _content.ResumeLayout(true);
            _body.ResumeLayout(true);
        }
        LayoutWorkerCanvas();
        LastSectionSwitchMilliseconds = timer.ElapsedMilliseconds;
        System.Diagnostics.Trace.WriteLine(
            $"Worker tab '{section}': {LastSectionSwitchMilliseconds} ms; " +
            $"cached={_sectionViews.Count}, builds={SectionViewBuildCount}");
        SectionChanged?.Invoke(section);
        PwrPerformanceLog.Record("Tab." + section, timer,
            existingPage ? "cached=true" : "cached=false");
    }

    private void RestoreCachedActions(Control view, string section)
    {
        if (section == "Skills") return;
        if (section == "General" && view is TableLayoutPanel general)
        {
            _actionArea.Dock = DockStyle.Fill;
            _actionArea.Margin = Padding.Empty;
            _actionArea.Padding = new Padding(8, 26, 3, 3);
            general.Controls.Add(_actionArea, 2, 1);
            general.SetColumnSpan(_actionArea, 2);
        }
        else if (view.Controls.Find("WorkerSecondaryActionsHost", true)
                     .OfType<Panel>().FirstOrDefault() is Panel footer)
        {
            _actionArea.Dock = DockStyle.Right;
            _actionArea.Width = 574;
            _actionArea.Padding = Padding.Empty;
            footer.Controls.Add(_actionArea);
        }
    }

    private void InvalidateSectionViews()
    {
        if (_actionArea.Parent is Control parent)
            parent.Controls.Remove(_actionArea);

        _content.SuspendLayout();
        try
        {
            foreach (Control previous in _content.Controls.Cast<Control>().ToArray())
                _content.Controls.Remove(previous);
            foreach (var view in _sectionViews.Values)
                if (!view.IsDisposed) view.Dispose();
            _sectionViews.Clear();
            _sectionRecordIndices.Clear();
            _recordIndex = 0;
        }
        finally { _content.ResumeLayout(false); }
    }

    private void SetImmersiveSkillsLayout(bool skills)
    {
        if (_workerPage is null || _workerPage.IsDisposed) return;
        _workerPage.SuspendLayout();
        _sectionNavigation.Visible = !skills;
        _workerPage.ColumnStyles[0].Width = skills ? 0 : 158;
        _workerPage.ResumeLayout(true);
    }

    private void BuildSkills()
    {
        _content.Controls.Add(new WorkerSkillsEditor(_profile,
            _databaseFile is not null, SaveWorker, _loadedSkillValues));
    }

    private static string? SectionTable(string section) => section switch
    {
        "Contracts" => "tblContract",
        "Skills" => "tblWorkerSkills",
        "Popularity" => "tblWorkerOver",
        "Attributes" => "tblWorkerAttributes",
        "Business Skills" => "tblWorkerBusiness",
        "Alter Ego" => "tblAlternative",
        _ => null
    };

    private void BuildSecondary(string section)
    {
        var root = new Panel { Dock = DockStyle.Fill, Name = "WorkerSecondarySection" };
        _content.Controls.Add(root);
        // The action strip is retained on other worker sections as well.
        var secondaryFooter = new Panel
        {
            Name = "WorkerSecondaryActionsHost",
            Dock = DockStyle.Bottom, Height = 72,
            BackColor = Ink,
            Padding = new Padding(0, 24, 5, 6)
        };
        _actionArea.Dock = DockStyle.Right;
        _actionArea.Width = 574;
        _actionArea.Padding = Padding.Empty;
        secondaryFooter.Controls.Add(_actionArea);
        root.Controls.Add(secondaryFooter);
        string? tableName = SectionTable(section);
        var data = _profile.Sections.FirstOrDefault(x => x.Table == tableName)?.Records;
        var header = new Label
        {
            Name = "WorkerSecondaryTitle",
            Text = section.ToUpperInvariant(),
            Dock = DockStyle.Top, Height = 44,
            ForeColor = Color.White,
            BackColor = Surface,
            Padding = new Padding(16, 0, 0, 0),
            Font = new Font("Segoe UI", 14, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleLeft
        };
        root.Controls.Add(header);
        if (tableName is null)
        {
            root.Controls.Add(new Label
            {
                Name = "WorkerSectionEmpty",
                Text = "This section does not have a dedicated table in the current PWR worker database.",
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter,
                ForeColor = Color.FromArgb(169, 193, 215),
                Font = new Font("Segoe UI", 11)
            });
            header.BringToFront();
            return;
        }
        if (data is null || data.Rows.Count == 0)
        {
            root.Controls.Add(new Label
            {
                Name = "WorkerSectionEmpty",
                Text = "No " + section.ToLowerInvariant() + " records were found for this worker.",
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter,
                ForeColor = Color.FromArgb(169, 193, 215),
                Font = new Font("Segoe UI", 11)
            });
            header.BringToFront();
            return;
        }

        if (data.Rows.Count > 1)
        {
            var stepper = new FlowLayoutPanel
            {
                Dock = DockStyle.Right, Width = 210,
                FlowDirection = FlowDirection.LeftToRight,
                Padding = new Padding(0, 4, 4, 2),
                BackColor = Surface
            };
            var previous = ActionButton("◀", "PreviousRecord", false, 38);
            var next = ActionButton("▶", "NextRecord", false, 38);
            var count = new Label
            {
                Name = "WorkerRecordCount",
                Text = "1 / " + data.Rows.Count,
                Width = 82, Height = 33,
                ForeColor = Color.White,
                TextAlign = ContentAlignment.MiddleCenter
            };
            previous.Click += (_, _) =>
            {
                _recordIndex = (_recordIndex + data.Rows.Count - 1) % data.Rows.Count;
                count.Text = (_recordIndex + 1) + " / " + data.Rows.Count;
                RenderSecondaryFields(root, data.Rows[_recordIndex], section);
            };
            next.Click += (_, _) =>
            {
                _recordIndex = (_recordIndex + 1) % data.Rows.Count;
                count.Text = (_recordIndex + 1) + " / " + data.Rows.Count;
                RenderSecondaryFields(root, data.Rows[_recordIndex], section);
            };
            stepper.Controls.Add(previous);
            stepper.Controls.Add(count);
            stepper.Controls.Add(next);
            header.Controls.Add(stepper);
        }
        RenderSecondaryFields(root, data.Rows[0], section);
        header.BringToFront();
    }

    private static void RenderSecondaryFields(Panel parent, DataRow record, string section)
    {
        Control? previous = parent.Controls.Find("WorkerSecondaryFields", false).FirstOrDefault();
        if (previous is not null)
        {
            parent.Controls.Remove(previous);
            previous.Dispose();
        }
        var fields = record.Table.Columns.Cast<DataColumn>()
            .Where(x => !x.ColumnName.Equals("UID", StringComparison.OrdinalIgnoreCase) &&
                        !x.ColumnName.EndsWith("WorkerUID", StringComparison.OrdinalIgnoreCase))
            .Select(x => (Label: x.ColumnName, Field: x.ColumnName)).ToArray();
        if (fields.Length == 0)
        {
            parent.Controls.Add(new Label
            {
                Name = "WorkerSecondaryFields", Dock = DockStyle.Fill,
                Text = "No editable detail fields are defined for this record.",
                ForeColor = TextInk,
                TextAlign = ContentAlignment.MiddleCenter
            });
            return;
        }
        var grid = new TableLayoutPanel
        {
            Name = "WorkerSecondaryFields", Dock = DockStyle.Fill,
            ColumnCount = 3, RowCount = 1, Padding = new Padding(8, 8, 8, 12)
        };
        for (int i = 0; i < 3; i++) grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f / 3));
        grid.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        int perGroup = (int)Math.Ceiling(fields.Length / 3d);
        for (int i = 0; i < 3; i++)
        {
            var group = Card(section + (fields.Length > perGroup ? " " + (i + 1) : ""), "WorkerDetailGroup" + i);
            var block = fields.Skip(i * perGroup).Take(perGroup).ToArray();
            if (block.Length != 0) MakeRows(group, block, record);
            grid.Controls.Add(group, i, 0);
        }
        parent.Controls.Add(grid);
        parent.Controls.OfType<Label>()
            .First(x => x.Name == "WorkerSecondaryTitle").BringToFront();
    }

    // Disabled workflow buttons remain readable, instead of Windows Forms
    // rendering black-on-navy disabled text. Saving is still unavailable.
    // Buttons draw their own icon and caption. This works in the running app
    // and in DrawToBitmap, unlike transparent label controls over siblings.
    private sealed class WorkerActionButton : Button
    {
        internal WorkerActionButton()
        {
            SetStyle(ControlStyles.UserPaint |
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.SupportsTransparentBackColor, true);

        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var bounds = ClientRectangle;
            if (bounds.Width <= 2 || bounds.Height <= 2) return;
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using (var fill = new LinearGradientBrush(
    bounds,
    Name == "WorkerSave"
        ? Color.FromArgb(30, 145, 210)
        : Color.FromArgb(33, 52, 72),
    Name == "WorkerSave"
        ? Color.FromArgb(10, 100, 165)
        : Color.FromArgb(12, 24, 40),
    LinearGradientMode.Vertical))
            {
                g.FillRectangle(fill, bounds);
            }
            using var outline = new Pen(FlatAppearance.BorderColor, 1f);
            g.DrawRectangle(outline, 0, 0,
                bounds.Width - 1, bounds.Height - 1);

            var textSize = TextRenderer.MeasureText(g, Text, Font,
                new Size(int.MaxValue, Height),
                TextFormatFlags.NoPadding | TextFormatFlags.SingleLine);
            int iconSize = 19, gap = 6;
            int labelWidth = textSize.Width + iconSize + gap;
            int x = Math.Max(4, (Width - labelWidth) / 2);
            int y = (Height - iconSize) / 2;

            using var pen = new Pen(Color.White, 1.6f)
            {
                LineJoin = LineJoin.Round
            };
            using var brush = new SolidBrush(Color.White);
            switch (Name)
            {
                case "WorkerCreateMoveset":
                    g.DrawPolygon(pen, [
                        new Point(x, y + 5), new Point(x + 9, y + 1),
                        new Point(x + 18, y + 5), new Point(x + 9, y + 9)
                    ]);
                    g.DrawPolygon(pen, [
                        new Point(x, y + 10), new Point(x + 9, y + 14),
                        new Point(x + 18, y + 10), new Point(x + 9, y + 6)
                    ]);
                    g.DrawLine(pen, x, y + 5, x, y + 13);
                    g.DrawLine(pen, x + 18, y + 5, x + 18, y + 13);
                    break;
                case "WorkerSuggestName":
                    g.DrawRectangle(pen, x + 1, y + 1, 16, 16);
                    foreach (var dot in new[] {
                        new Point(5, 5), new Point(12, 5),
                        new Point(9, 9), new Point(5, 13),
                        new Point(12, 13)
                    })
                        g.FillEllipse(brush, x + dot.X, y + dot.Y, 2, 2);
                    break;
                case "WorkerMakeBiography":
                    g.DrawRectangle(pen, x + 2, y + 1, 14, 17);
                    g.DrawLine(pen, x + 5, y + 6, x + 13, y + 6);
                    g.DrawLine(pen, x + 5, y + 10, x + 13, y + 10);
                    g.DrawLine(pen, x + 5, y + 14, x + 11, y + 14);
                    break;
                case "WorkerSave":
                    g.DrawRectangle(pen, x + 1, y + 1, 16, 16);
                    g.DrawRectangle(pen, x + 5, y + 1, 8, 6);
                    g.DrawRectangle(pen, x + 5, y + 11, 8, 6);
                    break;
            }
            TextRenderer.DrawText(g, Text, Font,
                new Rectangle(x + iconSize + gap, 0,
                    Math.Max(1, Width - (x + iconSize + gap) - 2), Height),
                Enabled ? Color.White : Color.FromArgb(221, 237, 248),
                TextFormatFlags.Left |
                TextFormatFlags.VerticalCenter |
                TextFormatFlags.SingleLine |
                TextFormatFlags.NoPadding |
                TextFormatFlags.EndEllipsis);
        }
    }

    private static Button ActionButton(string label, string name, bool primary = false, int width = 148)
    {
        var button = new WorkerActionButton
        {
            Name = name, Text = label,
            Size = new Size(width, 35),
            FlatStyle = FlatStyle.Flat,
            BackColor = primary ? Color.FromArgb(13, 112, 178) : Surface,
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 9f, FontStyle.Bold),
            Cursor = Cursors.Hand,
            Margin = new Padding(4, 2, 4, 2)
        };
        button.FlatAppearance.BorderColor = primary ? Accent : Edge;
        button.FlatAppearance.MouseOverBackColor = Color.FromArgb(28, 81, 126);
        return button;
    }



    private DataRow? ResolveRow(WorkerFieldBinding binding)
    {
        if (binding.Row is not null) return binding.Row;
        if (_databaseFile is null || binding.Table != "tblWorkerPhysical")
            return null;
        var section = _profile.Sections.FirstOrDefault(x =>
            x.Table.Equals(binding.Table, StringComparison.OrdinalIgnoreCase));
        if (section is null || !section.Records.Columns.Contains(binding.Field))
            return null;
        DataRow? row = section.Records.Rows.Cast<DataRow>().FirstOrDefault();
        if (row is not null) return row;
        row = section.Records.NewRow();
        row["WorkerUID"] = _profile.Identity.Uid;
        section.Records.Rows.Add(row);
        return row;
    }

    private void ApplyValue(WorkerValue control, DataRow row,
        string field, object? candidate)
    {
        if (Equals(row[field], candidate)) return;
        try
        {
            row[field] = candidate ?? DBNull.Value;
            control.Text = Display(row, field);
            control.Invalidate();
            _saveStatus.Text = "";
        }
        catch (Exception ex) when (ex is ArgumentException or ConstraintException ||
                                   ex is NoNullAllowedException)
        {
            _saveStatus.Text = "Invalid " + field + ": " + ex.Message;
        }
    }

    private async Task RestoreBackgroundAsync(TableLayoutPanel page, string preferredFile)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            // Prefer current skin; use PWR's generated Obsidian background
            // when a custom skin does not yet supply Worker_Editor.jpg.
            string fallback = Path.Combine(_paths.SkinsRoot,
                "Obsidian", "Backgrounds", "Worker_Editor.jpg");
            string source = File.Exists(preferredFile) ? preferredFile : fallback;
            Size target = page.ClientSize;
            if (target.Width < 10 || target.Height < 10) return;
            Bitmap? art = await WorkerBackgroundCache.GetPreparedAsync(source, target);
            if (IsDisposed || Disposing || page.IsDisposed || !page.IsHandleCreated ||
                art is null || page.ClientSize != target)
                return;
            // The prepared bitmap is already exactly the page size.
            // Tile does a 1:1 copy instead of repeatedly stretching JPG
            // artwork through every nested transparent WinForms control.
            page.BackgroundImageLayout = ImageLayout.Tile;
            page.BackgroundImage = art;
            page.Invalidate(true);
            PwrPerformanceLog.Record("Paint.PrepareBackdrop", stopwatch);
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException)
        {
            System.Diagnostics.Debug.WriteLine(
                "Worker background was unavailable: " + ex.Message);
        }
    }

    private ContextMenuStrip GetFieldMenu()
    {
        if (_fieldMenu is { IsDisposed: false } existing)
        {
            if (existing.Visible) existing.Close();
            foreach (ToolStripItem previous in existing.Items.Cast<ToolStripItem>().ToArray())
            {
                existing.Items.Remove(previous);
                previous.Dispose();
            }
            return existing;
        }

        _fieldMenu = new ContextMenuStrip
        {
            Name = "WorkerFieldChoices",
            ShowImageMargin = false,
            ShowCheckMargin = true,
            Renderer = new ToolStripProfessionalRenderer(new WorkerMenuColorTable(_palette)),
            BackColor = Color.FromArgb(10, 24, 42),
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 9.2f, FontStyle.Bold),
            MaximumSize = new Size(380, 390),
            DropShadowEnabled = true,
            Padding = new Padding(2, 4, 2, 4)
        };
        // Never dispose a ContextMenuStrip while handling Closed. The native
        // WinForms message loop may still reference it after that callback.
        return _fieldMenu;
    }

    private void AfterMenuInteraction(Control control, Action change)
    {
        if (IsDisposed || Disposing || control.IsDisposed || !IsHandleCreated)
            return;
        BeginInvoke((Action)(() =>
        {
            if (!IsDisposed && !Disposing && !control.IsDisposed)
                change();
        }));
    }

    private void OpenFieldChoices(WorkerValue control, WorkerFieldBinding binding)
    {
        if (_databaseFile is null) return;
        var row = binding.Row ?? ResolveRow(binding);
        if (row is null) return;
        string field = binding.Field;
        var table = PrwSchema.Tables.FirstOrDefault(t =>
            t.Name.Equals(row.Table.TableName, StringComparison.OrdinalIgnoreCase));
        if (table is null || table.KeyColumns.Contains(field,
                StringComparer.OrdinalIgnoreCase) ||
            (field.EndsWith("UID", StringComparison.OrdinalIgnoreCase) &&
             field != "LoyaltyWorkerUID"))
        {
            _saveStatus.Text = field + " is a protected record identifier.";
            return;
        }

        var choices = PrwWorkerFieldOptions.Get(
            _paths, _databaseFile, _password, row, field);
        var menu = GetFieldMenu();
        foreach (var choice in choices)
        {
            var selectedValue = choice.Value;
            var item = new ToolStripMenuItem(choice.Label)
            {
                Checked = Equals(row[field], selectedValue),
                AutoSize = true,
                ForeColor = Color.White,
                BackColor = Color.FromArgb(14, 34, 57),
                Padding = new Padding(8, 4, 8, 4)
            };
            item.Click += (_, _) =>
                AfterMenuInteraction(control, () =>
                    ApplyValue(control, row, field, selectedValue));
            menu.Items.Add(item);
        }
        if (choices.Count > 0) menu.Items.Add(new ToolStripSeparator());
        var column = row.Table.Columns[field]!;
        if (column.AllowDBNull)
        {
            var clear = new ToolStripMenuItem("(Clear)")
            {
                ForeColor = Color.White,
                BackColor = Color.FromArgb(14, 34, 57)
            };
            clear.Click += (_, _) =>
                AfterMenuInteraction(control, () =>
                    ApplyValue(control, row, field, DBNull.Value));
            menu.Items.Add(clear);
        }
        if (field != "LoyaltyWorkerUID")
        {
            var manual = new ToolStripMenuItem("Enter a custom value...")
            {
                ForeColor = Color.White,
                BackColor = Color.FromArgb(14, 34, 57)
            };
            manual.Click += (_, _) =>
                AfterMenuInteraction(control, () =>
                    EditField(control,
                        new WorkerFieldBinding(row, field, row.Table.TableName)));
            menu.Items.Add(manual);
        }
        if (menu.Items.Count == 0)
        {
            _saveStatus.Text = "No selectable values are available for " + field + ".";
            return;
        }
        try
        {
            menu.Show(control, new Point(0, control.Height));
        }
        catch (InvalidOperationException ex)
        {
            _saveStatus.Text = "Could not open " + field + ": " + ex.Message;
        }
    }

    private void EditField(WorkerValue control, WorkerFieldBinding binding)
    {
        if (_databaseFile is null) return;
        var row = ResolveRow(binding);
        if (row is null) return;
        var field = binding.Field;
        if ((field.EndsWith("UID", StringComparison.OrdinalIgnoreCase) &&
             field != "LoyaltyWorkerUID") ||
            PrwSchema.Tables.FirstOrDefault(t => t.Name == row.Table.TableName)?
                .KeyColumns.Contains(field, StringComparer.OrdinalIgnoreCase) == true)
        {
            MessageBox.Show(FindForm(), "Linked IDs and record identities cannot be changed here.",
                "Protected Worker Field", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var column = row.Table.Columns[field]!;
        object? candidate;
        if (column.DataType == typeof(bool))
        {
            var choice = MessageBox.Show(FindForm(),
                "Set " + field + " to Yes? Choose No to set it to No.",
                "Edit " + field, MessageBoxButtons.YesNoCancel,
                MessageBoxIcon.Question);
            if (choice == DialogResult.Cancel) return;
            candidate = choice == DialogResult.Yes;
        }
        else if (column.DataType == typeof(DateTime))
        {
            using var picker = new Form
            {
                Text = "Edit " + field, ClientSize = new Size(365, 130),
                StartPosition = FormStartPosition.CenterParent,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                MaximizeBox = false, MinimizeBox = false
            };
            var date = new DateTimePicker
            {
                Left = 18, Top = 16, Width = 325,
                Format = DateTimePickerFormat.Custom,
                CustomFormat = "yyyy-MM-dd", ShowCheckBox = true,
                Checked = row[field] is DateTime
            };
            if (row[field] is DateTime existing)
                date.Value = existing;
            var okay = new Button { Text = "Apply", Left = 155, Top = 68,
                Width = 90, DialogResult = DialogResult.OK };
            var cancel = new Button { Text = "Cancel", Left = 253, Top = 68,
                Width = 90, DialogResult = DialogResult.Cancel };
            picker.Controls.AddRange([date, okay, cancel]);
            picker.AcceptButton = okay;
            picker.CancelButton = cancel;
            if (picker.ShowDialog(FindForm()) != DialogResult.OK) return;
            candidate = date.Checked ? date.Value.Date : DBNull.Value;
        }
        else
        {
            string? answer = PromptText("Edit " + field,
                "Enter a value for " + field + ". Leave blank to clear an optional field.",
                Display(row, field));
            if (answer is null) return;
            try
            {
                candidate = answer.Length == 0 && column.AllowDBNull
                    ? DBNull.Value
                    : column.DataType == typeof(string) ? answer
                    : Convert.ChangeType(answer, column.DataType, CultureInfo.InvariantCulture);
            }
            catch (Exception ex) when (ex is FormatException or OverflowException
                                       or InvalidCastException)
            {
                MessageBox.Show(FindForm(), "The value must be a valid " +
                    column.DataType.Name + ".", "Invalid Value",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
        }

        ApplyValue(control, row, field, candidate);
    }

    private string? PromptText(string title, string prompt, string initial)
    {
        using var dialog = new Form
        {
            Text = title, ClientSize = new Size(450, 185),
            StartPosition = FormStartPosition.CenterParent,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            BackColor = Dark, ForeColor = Color.White,
            MaximizeBox = false, MinimizeBox = false
        };
        var label = new Label
        {
            Text = prompt, Left = 18, Top = 15,
            Width = 414, Height = 60, ForeColor = Color.White
        };
        var input = new TextBox
        {
            Left = 18, Top = 78, Width = 414,
            Text = initial, BackColor = FieldInk, ForeColor = Ink
        };
        var okay = new Button
        {
            Text = "Apply", Left = 230, Top = 125, Width = 93,
            DialogResult = DialogResult.OK
        };
        var cancel = new Button
        {
            Text = "Cancel", Left = 336, Top = 125, Width = 96,
            DialogResult = DialogResult.Cancel
        };
        dialog.Controls.AddRange([label, input, okay, cancel]);
        dialog.AcceptButton = okay;
        dialog.CancelButton = cancel;
        return dialog.ShowDialog(FindForm()) == DialogResult.OK ? input.Text : null;
    }

    private void SetWorkerValue(string field, string text)
    {
        var row = First("tblWorker");
        if (row is null || !row.Table.Columns.Contains(field)) return;
        var column = row.Table.Columns[field]!;
        try
        {
            object value = column.DataType == typeof(string)
                ? text
                : Convert.ChangeType(text, column.DataType, CultureInfo.InvariantCulture);
            row[field] = value;
        }
        catch (Exception ex) when (ex is FormatException or OverflowException
                                   or InvalidCastException or ArgumentException)
        {
            MessageBox.Show(FindForm(),
                "The field " + field + " expects a value of type " +
                column.DataType.Name + ". No changes were made.",
                "Invalid Worker Field", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        foreach (var control in Controls.Find("WorkerField_" + field, true)
                     .OfType<WorkerValue>())
        {
            control.Text = Display(row, field);
            control.Invalidate();
        }
    }

    private void CreateMoveset()
    {
        string current = Display(First("tblWorker"), "DefaultMoveSet");
        string? name = PromptText("Create Moveset Reference",
            "Enter a moveset name. The current database stores a moveset reference, " +
            "not individual move definitions.", current);
        if (name is null) return;
        SetWorkerValue("DefaultMoveSet", name.Trim());
    }

    private void SuggestName()
    {
        string name = Display(First("tblWorker"), "Name");
        string shortName = Display(First("tblWorker"), "ShortName");
        var words = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        string suggestion = words.Length >= 2
            ? words[0] + " \"" + (shortName.Length > 0 ? shortName : words[0]) +
              "\" " + words[^1]
            : shortName.Length > 0 ? shortName : name;
        string? chosen = PromptText("Suggest a Ring Name",
            "A possible ring name based on this worker's existing name. " +
            "You can revise it before applying.", suggestion);
        if (chosen is null || string.IsNullOrWhiteSpace(chosen)) return;
        SetWorkerValue("Name", chosen.Trim());
    }

    private void MakeBiography()
    {
        var worker = First("tblWorker");
        string name = Display(worker, "Name");
        string style = Display(worker, "Style");
        string based = Display(worker, "BasedIn");
        string nationality = Display(worker, "Nationality");
        var description = name + " is a professional wrestling personality.";
        if (!string.IsNullOrWhiteSpace(style))
            description += " Their wrestling style is listed as " + style + ".";
        if (!string.IsNullOrWhiteSpace(nationality))
            description += " Their recorded nationality is " + nationality + ".";
        if (!string.IsNullOrWhiteSpace(based))
            description += " They are based in " + based + ".";
        var biography = Controls.Find("WorkerBiography", true)
            .OfType<RichTextBox>().FirstOrDefault();
        if (biography is null) return;
        if (!string.IsNullOrWhiteSpace(biography.Text) &&
            biography.Text != "No biography has been entered for this worker." &&
            MessageBox.Show(FindForm(),
                "Replace the current biography with a draft built from the worker's recorded details?",
                "Make Biography", MessageBoxButtons.YesNo,
                MessageBoxIcon.Question) != DialogResult.Yes)
            return;
        biography.Text = description;
        biography.Focus();
    }

    private void SaveWorker()
    {
        if (_databaseFile is null) return;
        try
        {
            PrwWorkerEditorService.Save(_databaseFile, _profile, _password);
            _initialValueSnapshot = SnapshotValues(_profile);
            _saveStatus.Text = "";
            // Save closes the overlay directly. The Database Editor refreshes
            // its worker list in the existing CloseRequested handler.
            CloseRequested?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            // Keep the editor open with the unsaved inputs intact. No modal
            // success/failure popup interrupts the desired workflow.
            _saveStatus.Text = "Save failed: " + ex.Message;
            ActiveSkillsEditor()?.ShowSaveError(ex.Message);
            _tips.SetToolTip(_saveStatus, ex.Message);
        }
    }

    private void BuildActions()
    {
        _actionArea.Padding = Padding.Empty;
        _actionArea.Margin = Padding.Empty;
        _actionArea.BackColor = Color.FromArgb(22, 32, 46);
        _actionArea.Paint += (_, e) =>
        {
            Control? page = _actionArea;

            while (page != null && page.Name != "WorkerPage")
                page = page.Parent;

            if (page?.BackgroundImage is not Image background ||
                page.ClientSize.Width <= 0 ||
                page.ClientSize.Height <= 0 ||
                _actionArea.ClientSize.Width <= 0 ||
                _actionArea.ClientSize.Height <= 0)
                return;

            Point origin = Point.Empty;

            for (Control? current = _actionArea;
                 current != null && current != page;
                 current = current.Parent)
            {
                origin.Offset(current.Left, current.Top);
            }

            PaintPageBackdrop(e.Graphics, background, page,
                _actionArea.ClientRectangle, origin.X, origin.Y);

            using var tint = new SolidBrush(Color.FromArgb(90, 9, 18, 31));
            e.Graphics.FillRectangle(tint, _actionArea.ClientRectangle);
        };
        var buttons = new TableLayoutPanel
        {
            Name = "WorkerEditorActions",
            Dock = DockStyle.Fill,
            ColumnCount = 4, RowCount = 1,
            Padding = Padding.Empty, Margin = Padding.Empty,
            BackColor = Color.Transparent
        };
        buttons.Paint += (_, e) =>
        {
            Control? page = buttons.Parent;

            while (page != null && page.Name != "WorkerPage")
                page = page.Parent;

            if (page?.BackgroundImage is not Image artwork ||
                page.ClientSize.Width <= 0 ||
                page.ClientSize.Height <= 0 ||
                buttons.ClientSize.Width <= 0 ||
                buttons.ClientSize.Height <= 0)
                return;

            Point origin = Point.Empty;

            for (Control? current = buttons;
                 current != null && current != page;
                 current = current.Parent)
            {
                origin.Offset(current.Left, current.Top);
            }

            PaintPageBackdrop(e.Graphics, artwork, page,
                buttons.ClientRectangle, origin.X, origin.Y);

            using var tint = new SolidBrush(
                Color.FromArgb(90, 9, 18, 31));

            e.Graphics.FillRectangle(tint, buttons.ClientRectangle);
        };
        buttons.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        foreach (int width in new[] { 28, 25, 27, 20 })
            buttons.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, width));

        var moveset = ActionButton("Create Moveset", "WorkerCreateMoveset");
        var suggest = ActionButton("Suggest Name", "WorkerSuggestName");
        var biography = ActionButton("Make Biography", "WorkerMakeBiography");
        var save = ActionButton("Save", "WorkerSave", primary: true);
        moveset.Click += (_, _) => CreateMoveset();
        suggest.Click += (_, _) => SuggestName();
        biography.Click += (_, _) => MakeBiography();
        save.Click += (_, _) => SaveWorker();
        var actions = new[] { moveset, suggest, biography, save };
        for (int i = 0; i < actions.Length; i++)
        {
            var button = actions[i];
            button.Dock = DockStyle.Fill;
            button.Size = Size.Empty;
            button.Height = 38;
            button.Margin = new Padding(3, 2, 3, 2);
            button.Enabled = _databaseFile is not null;
            _tips.SetToolTip(button, _databaseFile is null
                ? "Open a native PWR universe to edit workers."
                : "Changes are staged until Save; writes use a database transaction.");
            var holder = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.Transparent, Margin = Padding.Empty
            };
            button.Dock = DockStyle.Fill;
            holder.Controls.Add(button);

            buttons.Controls.Add(holder, i, 0);
        }
        _actionArea.Controls.Add(buttons);
    }

}
