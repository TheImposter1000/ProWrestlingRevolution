using System.Data;
using System.Drawing;
using System.Drawing.Drawing2D;
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
    private readonly PrwWorkerProfile _profile;
    private readonly Dictionary<string, Button> _tabs = new(StringComparer.OrdinalIgnoreCase);
    private readonly Panel _content = new() { Name = "WorkerSectionContent", BackColor = Ink };
    private readonly Panel _sectionNavigation = new() { Name = "WorkerSectionNavigation", Dock = DockStyle.Fill, Margin = Padding.Empty };
    private readonly Panel _mainRail = new() { Name = "WorkerMainMenuRail", Dock = DockStyle.Fill, Margin = Padding.Empty };
    private readonly Panel _body = new() { Name = "WorkerEditorBody", Dock = DockStyle.Fill, Margin = Padding.Empty };
    private readonly ToolTip _tips = new() { InitialDelay = 350, ReshowDelay = 80 };
    private string _active = "General";
    private int _recordIndex;
    private readonly Font _railNormal = new("Segoe UI", 10f);
    private readonly Font _railActive = new("Segoe UI", 10f, FontStyle.Bold);
    private readonly Dictionary<string, SidebarMenuRow> _mainMenuIcons = new();
    private readonly Panel _mainArea = new() { Name = "WorkerGeneralCanvas", BackColor = Ink };
    private readonly Panel _actionArea = new() { Name = "WorkerActionArea", BackColor = Color.Transparent };
    private TableLayoutPanel? _identityBiography;

    internal string? RequestedNavigation { get; private set; }
    internal event EventHandler? CloseRequested;

    internal void Close() =>
        CloseRequested?.Invoke(this, EventArgs.Empty);

    internal WorkerEditorForm(AppPaths paths, SkinPalette palette, PrwWorkerProfile profile)
    {
        _paths = paths;
        _palette = palette;
        _profile = profile;

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

        BuildFrame();
        ShowSection("General");
        Disposed += (_, _) =>
        {
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
            Padding = new Padding(0, 27, 0, 0),
            BackColor = Dark
        };
        card.Paint += (_, e) =>
        {
            using var pen = new Pen(Edge);
            e.Graphics.DrawRectangle(pen, 0, 0, card.Width - 1, card.Height - 1);
        };
        var heading = new Panel
        {
            Dock = DockStyle.None,
            Location = Point.Empty,
            Height = 27, Width = Math.Max(1, card.Width),
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
            BackColor = Header,
            Name = name + "Heading"
        };
        heading.Paint += (_, e) =>
        {
            var rect = new Rectangle(0, 0, Math.Max(1, heading.Width), Math.Max(1, heading.Height));
            using var gradient = new LinearGradientBrush(rect,
                Color.FromArgb(36, 91, 144), Color.FromArgb(23, 54, 85), LinearGradientMode.Vertical);
            e.Graphics.FillRectangle(gradient, rect);
            using var pen = new Pen(Accent);
            e.Graphics.DrawLine(pen, 0, heading.Height - 1, heading.Width, heading.Height - 1);
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
            var area = new Rectangle(0, 0, Width, Height);
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
                e.Graphics.FillRectangle(brush, area);
            using (var outline = new Pen(Active ? Accent : Edge, Active ? 1.5f : 1f))
                e.Graphics.DrawRectangle(outline, 1, 1, Width - 3, Height - 3);
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
                ControlStyles.OptimizedDoubleBuffer, true);
            AccessibleRole = AccessibleRole.StaticText;
            BackColor = FieldInk;
            ForeColor = Color.FromArgb(11, 28, 42);
            Font = new Font("Segoe UI", 9.15f, FontStyle.Bold);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var rect = ClientRectangle;
            if (rect.Width < 3 || rect.Height < 3) return;
            using var gradient = new LinearGradientBrush(rect,
                Color.FromArgb(251, 252, 253), Color.FromArgb(228, 237, 245),
                LinearGradientMode.Vertical);
            e.Graphics.FillRectangle(gradient, rect);
            using var border = new Pen(Color.FromArgb(110, 145, 170));
            e.Graphics.DrawRectangle(border, 0, 0, rect.Width - 1, rect.Height - 1);
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
        TabStop = false
    };

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

    private DataRow? First(string table) =>
        _profile.Sections.FirstOrDefault(s => s.Table.Equals(table, StringComparison.OrdinalIgnoreCase))
            ?.Records.Rows.Cast<DataRow>().FirstOrDefault();

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
            RowCount = 2,
            ColumnCount = 2,
            BackColor = Ink
        };
        shell.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 61));
        shell.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        shell.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
        shell.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        Controls.Add(shell);

        var title = new Panel
        {
            Name = "WorkerEditorHeader", Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(16, 30, 49),
            Margin = Padding.Empty
        };
        title.Paint += (_, e) =>
        {
            var rect = new Rectangle(0, 0, Math.Max(1, title.Width), Math.Max(1, title.Height));
            using var gradient = new LinearGradientBrush(rect,
                Color.FromArgb(10, 22, 38), Color.FromArgb(32, 63, 97),
                LinearGradientMode.Horizontal);
            e.Graphics.FillRectangle(gradient, rect);
            using var border = new Pen(Accent, 1);
            e.Graphics.DrawLine(border, 0, title.Height - 1, title.Width, title.Height - 1);
        };
        shell.Controls.Add(title, 0, 0);
        shell.SetColumnSpan(title, 2);

        var logo = new Panel
        {
            Name = "WorkerBrand", Dock = DockStyle.Left,
            Width = 300, BackColor = Color.Transparent
        };
        logo.Paint += (_, args) =>
        {
            var g = args.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using var swoosh = new Pen(Color.FromArgb(31, 155, 225), 2.4f);
            g.DrawArc(swoosh, new Rectangle(18, 3, 111, 28), 188, 143);
            using var large = new Font("Segoe UI", 22f, FontStyle.Bold | FontStyle.Italic);
            using var small = new Font("Segoe UI", 9.3f, FontStyle.Bold | FontStyle.Italic);
            using var white = new SolidBrush(Color.White);
            g.DrawString("PWR", large, white, new PointF(19, -4));
            g.DrawString("PRO WRESTLING", small, white, new PointF(133, 5));
            g.DrawString("REVOLUTION", small, white, new PointF(133, 20));
            using var cyan = new Pen(Accent, 1f);
            g.DrawLine(cyan, 283, 6, 283, 34);
        };
        var pageHeading = new Label
        {
            Name = "WorkerEditorHeading",
            Text = "Worker Editor     /     Edit Worker",
            Font = new Font("Segoe UI", 13.2f, FontStyle.Bold),
            ForeColor = Color.White,
            Dock = DockStyle.Left, Width = 460,
            TextAlign = ContentAlignment.MiddleLeft,
            BackColor = Color.Transparent
        };
        var close = new Button
        {
            Name = "WorkerClose", Text = "×",
            Dock = DockStyle.Right, Width = 42,
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(12, 25, 41),
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 17f, FontStyle.Regular),
            Cursor = Cursors.Hand
        };
        close.FlatAppearance.BorderSize = 0;
        close.Click += (_, _) => Close();
        var minimize = new Button
        {
            Name = "WorkerMinimize", Text = "−",
            Dock = DockStyle.Right, Width = 37,
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.Transparent, ForeColor = Color.White,
            Font = new Font("Segoe UI", 15f),
            Cursor = Cursors.Hand
        };
        minimize.FlatAppearance.BorderSize = 0;
        minimize.Click += (_, _) =>
        {
            if (FindForm() is Form owner)
                owner.WindowState = FormWindowState.Minimized;
        };
        var maximize = new Button
        {
            Name = "WorkerMaximize", Text = "□",
            Dock = DockStyle.Right, Width = 37,
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.Transparent, ForeColor = Color.White,
            Font = new Font("Segoe UI", 16f),
            Cursor = Cursors.Hand
        };
        maximize.FlatAppearance.BorderSize = 0;
        maximize.Click += (_, _) =>
        {
            if (FindForm() is Form owner)
                owner.WindowState = owner.WindowState == FormWindowState.Maximized
                    ? FormWindowState.Normal : FormWindowState.Maximized;
        };
        var help = new Button
        {
            Name = "WorkerHelp", Text = "?",
            Dock = DockStyle.Right, Width = 36,
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.Transparent, ForeColor = Color.White,
            Font = new Font("Segoe UI", 13f, FontStyle.Bold),
            Cursor = Cursors.Hand
        };
        help.FlatAppearance.BorderSize = 0;
        help.Click += (_, _) => MessageBox.Show(this,
            "Select a section on the left to view the worker's information. " +
            "Saving and data generation will be enabled when the database " +
            "editing layer is available.", "Worker Editor Help",
            MessageBoxButtons.OK, MessageBoxIcon.Information);
        var settings = new Button
        {
            Name = "WorkerSettingsButton", Text = "⚙",
            Dock = DockStyle.Right, Width = 39,
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.Transparent, ForeColor = Color.White,
            Font = new Font("Segoe UI Symbol", 15f),
            Cursor = Cursors.Hand
        };
        settings.FlatAppearance.BorderSize = 0;
        settings.Click += (_, _) => RequestMainMenu("skins");
        pageHeading.Cursor = Cursors.Hand;
        _tips.SetToolTip(pageHeading, "Return to General worker information");
        pageHeading.Click += (_, _) => ShowSection("General");
        title.Controls.Add(pageHeading);
        title.Controls.Add(logo);
        title.Controls.Add(help);
        title.Controls.Add(settings);
        title.Controls.Add(minimize);
        title.Controls.Add(maximize);
        title.Controls.Add(close);

        _mainRail.BackColor = Color.FromArgb(11, 25, 41);
        var navTips = _tips;
        // Reuse the exact PWR icon artwork and command keys on the rail.
        foreach (var (key, label, y) in SkinArt.MenuIcons)
        {
            var item = new SidebarMenuRow(key, label, _palette, _railNormal, _railActive)
            {
                Name = "WorkerMenu_" + key,
                IconOnly = true,
                Location = new Point(0, Math.Max(0, y - 40 - 27)),
                Width = 61
            };
            item.UpdateAppearance(_palette, false, key == "editor");
            item.Click += (_, _) => RequestMainMenu(key);
            navTips.SetToolTip(item, label);
            _mainRail.Controls.Add(item);
            _mainMenuIcons[key] = item;
        }
        shell.Controls.Add(_mainRail, 0, 1);

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
        page.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 158));
        page.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        page.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        shell.Controls.Add(page, 1, 1);

        _sectionNavigation.BackColor = Color.FromArgb(11, 24, 39);
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
                Location = new Point(5, 13 + i * 44),
                Size = new Size(142, 39)
            };
            button.Click += (_, _) => ShowSection(section);
            _tabs[section] = button;
            _sectionNavigation.Controls.Add(button);
        }
        page.Controls.Add(_sectionNavigation, 0, 0);

        _body.Paint += (_, e) =>
        {
            var area = new Rectangle(0, 0, Math.Max(1, _body.Width), Math.Max(1, _body.Height));
            using var fill = new LinearGradientBrush(area,
                Color.FromArgb(22, 38, 61), Color.FromArgb(8, 16, 30),
                LinearGradientMode.Vertical);
            e.Graphics.FillRectangle(fill, area);
        };
        page.Controls.Add(_body, 1, 0);

        var top = new TableLayoutPanel
        {
            Name = "WorkerIdentityBiographyRow",
            ColumnCount = 2, RowCount = 1,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };
        top.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 29.5f));
        top.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 70.5f));
        top.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        _identityBiography = top;
        BuildIdentity(top);
        BuildBiography(top);

        _mainArea.Name = "WorkerGeneralCanvas";
        _content.Name = "WorkerSectionContent";
        _mainArea.Controls.Add(_content);
        _body.Controls.Add(top);
        _body.Controls.Add(_mainArea);

        _body.Resize += (_, _) => LayoutWorkerCanvas();
        LayoutWorkerCanvas();
        BuildActions();
    }

    private void LayoutWorkerCanvas()
    {
        if (_identityBiography is null || _body.Width < 200) return;
        // The approved mockup reserves a 21px right-hand gutter.
        // Both the top biography and four data columns share this boundary.
        int width = Math.Max(100, _body.ClientSize.Width - 21);
        int height = _body.ClientSize.Height;
        _identityBiography.Bounds = new Rectangle(0, 10, width, 145);
        int generalY = 165;
        int generalHeight = Math.Max(340, Math.Min(520, height - generalY - 12));
        _mainArea.Bounds = new Rectangle(0, generalY, width, generalHeight);
        _content.Bounds = _mainArea.ClientRectangle;
    }

    // The top row is a label-and-field editor, not a banner card.
    private void BuildIdentity(TableLayoutPanel top)
    {
        var card = new Panel
        {
            Name = "WorkerIdentity",
            Dock = DockStyle.Fill, Margin = Padding.Empty,
            BackColor = Color.FromArgb(17, 32, 52)
        };
        top.Controls.Add(card, 0, 0);
        var worker = First("tblWorker");

        var name = new Label
        {
            Name = "WorkerNameTitle", Text = "Name",
            Location = new Point(7, 8), Size = new Size(89, 22),
            ForeColor = Color.White, BackColor = Color.Transparent,
            Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleLeft
        };
        var shortName = new Label
        {
            Text = "Short Name", Location = new Point(7, 37),
            Size = new Size(89, 22),
            ForeColor = Color.White, BackColor = Color.Transparent,
            Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleLeft
        };
        var pictureLabel = new Label
        {
            Text = "Picture", Location = new Point(7, 76),
            Size = new Size(84, 22),
            ForeColor = Color.White, BackColor = Color.Transparent,
            Font = new Font("Segoe UI", 9.5f, FontStyle.Bold)
        };
        card.Controls.Add(name);
        card.Controls.Add(shortName);
        card.Controls.Add(pictureLabel);
        var first = ValueBox(Display(worker, "Name"), "WorkerField_Name", false);
        first.Dock = DockStyle.None;
        first.Location = new Point(100, 8);
        first.Size = new Size(210, 22);
        var second = ValueBox(Display(worker, "ShortName"), "WorkerField_ShortName", false);
        second.Dock = DockStyle.None;
        second.Location = new Point(100, 37);
        second.Size = new Size(210, 22);
        card.Controls.Add(first);
        card.Controls.Add(second);

        var portrait = new PictureBox
        {
            Name = "WorkerPicture",
            Location = new Point(100, 68), Size = new Size(91, 77),
            BackColor = Color.FromArgb(21, 41, 61),
            BorderStyle = BorderStyle.FixedSingle,
            SizeMode = PictureBoxSizeMode.Zoom
        };
        card.Controls.Add(portrait);
        string picture = Display(worker, "Picture");
        string safeName = Path.GetFileName(picture);
        if (!string.IsNullOrWhiteSpace(safeName))
        {
            var paths = new[]
            {
                Path.Combine(_paths.Pictures, safeName),
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
                    portrait.Image = new Bitmap(image);
                    portrait.Disposed += (_, _) => portrait.Image?.Dispose();
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

        var typeLabel = new Label
        {
            Text = "Picture Type", Location = new Point(200, 86),
            Size = new Size(121, 18),
            Font = new Font("Segoe UI", 9f, FontStyle.Bold),
            ForeColor = Color.White
        };
        var typeBox = ValueBox(Display(worker, "BiographyType"),
            "WorkerField_BiographyType", true);
        typeBox.Dock = DockStyle.None;
        typeBox.Location = new Point(200, 110);
        typeBox.Size = new Size(110, 22);
        card.Controls.Add(typeLabel);
        card.Controls.Add(typeBox);
        card.Resize += (_, _) =>
        {
            int valueWidth = Math.Max(100, card.ClientSize.Width - 107);
            first.Width = valueWidth;
            second.Width = valueWidth;
            typeLabel.Left = Math.Max(193, card.ClientSize.Width - 126);
            typeBox.Left = typeLabel.Left;
            typeBox.Width = Math.Max(91, card.ClientSize.Width - typeBox.Left - 5);
        };
    }

    private void BuildBiography(TableLayoutPanel top)
    {
        var card = new Panel
        {
            Name = "WorkerBiographyPanel",
            Dock = DockStyle.Fill, Margin = Padding.Empty,
            BackColor = Color.FromArgb(17, 32, 52)
        };
        top.Controls.Add(card, 1, 0);
        var heading = new Label
        {
            Text = "Biography", Location = new Point(5, 9),
            Size = new Size(89, 22),
            Font = new Font("Segoe UI", 9.7f, FontStyle.Bold),
            ForeColor = Color.White, BackColor = Color.Transparent
        };
        var text = Display(First("tblWorkerBio"), "Biography");
        var bio = new RichTextBox
        {
            Name = "WorkerBiography",
            Location = new Point(90, 8),
            Size = new Size(605, 131),
            BorderStyle = BorderStyle.FixedSingle,
            Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
            ReadOnly = true,
            DetectUrls = false,
            TabStop = false,
            ScrollBars = RichTextBoxScrollBars.None,
            BackColor = FieldInk,
            ForeColor = Color.FromArgb(16, 28, 41),
            Text = string.IsNullOrWhiteSpace(text) ?
                "No biography has been entered for this worker." : text
        };
        card.Controls.Add(heading);
        card.Controls.Add(bio);
        card.Resize += (_, _) =>
        {
            bio.Width = Math.Max(100, card.ClientSize.Width - bio.Left - 6);
            bio.Height = Math.Max(70, card.ClientSize.Height - bio.Top - 6);
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
            BackColor = Dark
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
            value.CalendarIndicator = isDate;
            value.SpinnerIndicator = isWeight;
            if (isWeight)
            {
                value.Dock = DockStyle.Left;
                value.Width = 98;
            }
            grid.Controls.Add(value, 1, i);
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
            BackColor = Dark
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
                AutoCheck = false,
                TabStop = false,
                Checked = source is not null && source.Table.Columns.Contains(field) &&
                    IsTrue(source[field]),
                ForeColor = TextInk,
                Font = new Font("Segoe UI", 9.1f, FontStyle.Regular),
                Margin = Padding.Empty,
                BackColor = Color.Transparent
            };
            grid.Controls.Add(tick, 0, i);
        }
        card.Controls.Add(grid);
        grid.BringToFront();
        return grid;
    }

    private static TableLayoutPanel Stack(params (Panel Panel, int Weight)[] groups)
    {
        var result = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = groups.Length };
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
        grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 448));
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
        personal.Height = 496;
        grid.Controls.Add(personal, 0, 0);
        grid.SetRowSpan(personal, 2);

        var loyalty = Card("", "WorkerLoyalty");
        MakeRows(loyalty, [
            ("Loyalty", "LoyaltyWorkerUID"), ("Loyalty Type", "LoyaltyType"),
            ("Freelance", "Freelance"), ("User Character", "UserCharacter")
        ], worker);
        var roles = Card("Roles", "WorkerRoles");
        var roleChecks = MakeChecks(roles, [
            ("Wrestler", "RoleWrestler"), ("Occasional Wrestler", "RolePartTimeWrestler"),
            ("Manager", "RoleManager"), ("On-Screen Personality", "RoleOnScreenPersonality"),
            ("Play By Play", "RolePlayByPlay"), ("Colour Commentator", "RoleColour"),
            ("Referee", "RoleReferee"), ("Road Agent", "RoleProducer")
        ], worker, rowHeight: 20);
        roleChecks.Dock = DockStyle.None;
        roleChecks.Location = new Point(3, 55);
        var competitor = new Panel
        {
            Name = "WorkerCompetesVs", Dock = DockStyle.None,
            Location = new Point(3, 28),
            Height = 26, BackColor = Dark,
            Padding = new Padding(3, 3, 4, 2)
        };
        var competitorLabel = TextLabel("Competes Vs.");
        competitorLabel.Dock = DockStyle.Left;
        competitorLabel.Width = 105;
        var competitorValue = ValueBox(Display(worker, "CompetesVs"),
            "WorkerField_CompetesVs");
        competitor.Controls.Add(competitorValue);
        competitor.Controls.Add(competitorLabel);
        roles.Controls.Add(competitor);
        competitor.BringToFront();
        roles.Resize += (_, _) =>
        {
            competitor.Width = Math.Max(100, roles.ClientSize.Width - 6);
            roleChecks.Width = Math.Max(100, roles.ClientSize.Width - 6);
        };
        var physical = Card("Physical Health", "WorkerPhysical");
        MakeRows(physical, [
            ("Head Condition", "HeadCondition"), ("Body Condition", "BodyCondition"),
            ("Arms Condition", "ArmsCondition"), ("Legs Condition", "LegsCondition")
        ], First("tblWorkerPhysical"), labelShare: 60f);
        var middleColumn = Stack((loyalty, 27), (roles, 47), (physical, 26));
        middleColumn.Dock = DockStyle.Top;
        middleColumn.Height = 496;
        grid.Controls.Add(middleColumn, 1, 0);
        grid.SetRowSpan(middleColumn, 2);

        var settings = Card("Default & Age-Related Settings", "WorkerSettings");
        MakeRows(settings, [
            ("Move Set", "DefaultMoveSet"),
            ("Face Gimmick", "FaceGimmick"),
            ("Face Basis", "FaceGimmickBasis"),
            ("Heel Gimmick", "HeelGimmick"),
            ("Heel Basis", "HeelGimmickBasis"),
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
        // The approved action strip starts at y=675 and is 36px high at the
        // fixed logical 1366x768 size, with open space beneath it.
        _actionArea.Padding = new Padding(8, 22, 3, 14);
        grid.Controls.Add(_actionArea, 2, 1);
        grid.SetColumnSpan(_actionArea, 2);
        _content.Controls.Add(grid);
    }

    private void ShowSection(string section)
    {
        _active = section;
        _recordIndex = 0;
        foreach (var (name, button) in _tabs)
        {
            // The approved General composition uses Alter Ego as the
            // highlighted section in its reference image. Retain that
            // visual until the user explicitly selects another section.
            bool selected = section == "General"
                ? name == "Alter Ego"
                : name.Equals(section, StringComparison.OrdinalIgnoreCase);
            if (button is WorkerSectionButton tab)
            {
                tab.Active = selected;
                tab.Invalidate();
            }
        }
        _content.SuspendLayout();
        if (_actionArea.Parent is Control oldParent)
            oldParent.Controls.Remove(_actionArea);
        _content.Controls.Clear();
        if (section == "General") BuildGeneral();
        else BuildSecondary(section);
        _content.ResumeLayout();
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
    private sealed class WorkerActionButton : Button
    {
        internal WorkerActionButton()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer, true);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var bounds = ClientRectangle;
            if (bounds.Width <= 2 || bounds.Height <= 2) return;
            e.Graphics.Clear(BackColor);
            using var outline = new Pen(FlatAppearance.BorderColor, 1f);
            e.Graphics.DrawRectangle(outline, 0, 0,
                bounds.Width - 1, bounds.Height - 1);
            TextRenderer.DrawText(e.Graphics, Text, Font, bounds,
                Enabled ? Color.White : Color.FromArgb(191, 215, 234),
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter |
                TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
        }
    }

    /// <summary>
    /// Vector action icons reproduce the approved screenshot without relying
    /// on decorative Unicode characters that turn into missing-glyph squares
    /// on some Windows machines. This draws no new skin assets.
    /// </summary>
    private sealed class WorkerActionCaption : Control
    {
        private readonly string _kind;

        internal WorkerActionCaption(string kind, string text)
        {
            SetStyle(ControlStyles.SupportsTransparentBackColor |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.UserPaint, true);
            _kind = kind;
            Text = text;
            Font = new Font("Segoe UI", 9.1f, FontStyle.Bold);
            ForeColor = Color.White;
            BackColor = Color.Transparent;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            if (Width < 20 || Height < 20) return;
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            var measure = TextRenderer.MeasureText(g, Text, Font,
                new Size(int.MaxValue, Height),
                TextFormatFlags.SingleLine | TextFormatFlags.NoPadding);
            int glyphWidth = 19, gap = 6;
            int totalWidth = glyphWidth + gap + measure.Width;
            int x = Math.Max(5, (Width - totalWidth) / 2);
            int y = (Height - glyphWidth) / 2;
            using var pen = new Pen(Color.White, 1.6f)
            {
                LineJoin = LineJoin.Round
            };
            using var brush = new SolidBrush(Color.White);

            switch (_kind)
            {
                case "moveset":
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
                case "suggest":
                    g.DrawRectangle(pen, x + 1, y + 1, 16, 16);
                    foreach (var dot in new[] {
                        new Point(5, 5), new Point(12, 5), new Point(9, 9),
                        new Point(5, 13), new Point(12, 13)
                    })
                        g.FillEllipse(brush, x + dot.X, y + dot.Y, 2, 2);
                    break;
                case "biography":
                    g.DrawRectangle(pen, x + 2, y + 1, 14, 17);
                    g.DrawLine(pen, x + 5, y + 6, x + 13, y + 6);
                    g.DrawLine(pen, x + 5, y + 10, x + 13, y + 10);
                    g.DrawLine(pen, x + 5, y + 14, x + 11, y + 14);
                    break;
                case "save":
                    g.DrawRectangle(pen, x + 1, y + 1, 16, 16);
                    g.DrawRectangle(pen, x + 5, y + 1, 8, 6);
                    g.DrawRectangle(pen, x + 5, y + 11, 8, 6);
                    break;
            }

            TextRenderer.DrawText(g, Text, Font,
                new Rectangle(x + glyphWidth + gap, 0,
                    Math.Max(1, Width - (x + glyphWidth + gap) - 2), Height),
                Color.White, TextFormatFlags.Left |
                TextFormatFlags.VerticalCenter |
                TextFormatFlags.EndEllipsis |
                TextFormatFlags.SingleLine |
                TextFormatFlags.NoPadding);
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

    private void BuildActions()
    {
        _actionArea.Padding = Padding.Empty;
        _actionArea.Margin = Padding.Empty;
        _actionArea.BackColor = Color.Transparent;
        var buttons = new TableLayoutPanel
        {
            Name = "WorkerEditorActions",
            Dock = DockStyle.Fill,
            ColumnCount = 4, RowCount = 1,
            Padding = Padding.Empty, Margin = Padding.Empty,
            BackColor = Color.Transparent
        };
        buttons.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        foreach (int width in new[] { 28, 25, 27, 20 })
            buttons.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, width));

        var moveset = ActionButton("Create Moveset", "WorkerCreateMoveset");
        var suggest = ActionButton("Suggest Name", "WorkerSuggestName");
        var biography = ActionButton("Make Biography", "WorkerMakeBiography");
        var save = ActionButton("Save", "WorkerSave", primary: true);
        var actions = new[] { moveset, suggest, biography, save };
        for (int i = 0; i < actions.Length; i++)
        {
            var button = actions[i];
            button.Dock = DockStyle.Fill;
            button.Size = Size.Empty;
            button.Height = 38;
            button.Margin = new Padding(3, 2, 3, 2);
            button.Enabled = false;
            _tips.SetToolTip(button,
                "Worker editing functions are not yet connected to the database.");
            var holder = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.Transparent, Margin = Padding.Empty
            };
            button.Dock = DockStyle.Fill;
            holder.Controls.Add(button);
            var caption = new WorkerActionCaption(
                i switch
                {
                    0 => "moveset",
                    1 => "suggest",
                    2 => "biography",
                    _ => "save"
                }, button.Text)
            {
                Name = button.Name + "Caption",
                Dock = DockStyle.Fill
            };
            // The caption owns both icon and label drawing; the disabled
            // button underneath provides only its styled surface and border.
            button.Text = "";
            holder.Controls.Add(caption);
            caption.BringToFront();
            buttons.Controls.Add(holder, i, 0);
        }
        _actionArea.Controls.Add(buttons);
    }

}
