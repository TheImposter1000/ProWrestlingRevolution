using System.Data;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace ProWrestlingRevolution;

/// <summary>
/// Database directory: visual interface and category-wide tools.
/// All names and records are sourced from the selected MDB. The controls below
/// do not alter browsing, sorting, selection, mass editing, or import behavior.
/// </summary>
internal sealed class DatabaseEditorWorkspace : UserControl
{
    private static readonly Color Ink = Color.FromArgb(9, 15, 24);
    private static readonly Color PanelInk = Color.FromArgb(17, 29, 40);
    private static readonly Color ButtonInk = Color.FromArgb(29, 47, 61);
    private static readonly Color CardBorder = Color.FromArgb(59, 84, 99);
    private static readonly Color Cyan = Color.FromArgb(91, 212, 232);
    private static readonly Color Red = Color.FromArgb(242, 48, 75);
    private static readonly Color Soft = Color.FromArgb(170, 191, 207);

    private readonly AppPaths _paths;
    private readonly Action _goBack;
    private readonly ComboBox _section = new();
    private readonly ComboBox _massField = new();
    private readonly ComboBox _sort = new();
    private readonly TextBox _search = new();
    private readonly ListBox _records = new();
    private readonly RichTextBox _notes = new();
    private readonly Label _dbName = new();
    private readonly Label _count = new();
    private readonly Label _recordStatus = new();
    private readonly Label _hint = new();
    private readonly EditorActionButton _massButton = new();
    private readonly EditorActionButton _notesTab = new();
    private readonly EditorActionButton _summaryTab = new();

    private UniverseEntry? _universe;
    private DataTable? _loaded;
    private long _total;
    private bool _showSummary;
    private string _password = "";
    private List<Entry> _allEntries = [];

    private sealed record Entry(DataRow Row, string Name)
    {
        public override string ToString() => Name;
    }

    // Simple vector-based chrome; no image assets or database-dependent UI rendering.
    private static GraphicsPath Rounded(Rectangle bounds, int radius)
    {
        int size = Math.Max(2, Math.Min(radius * 2, Math.Min(bounds.Width, bounds.Height)));
        var path = new GraphicsPath();
        path.AddArc(bounds.Left, bounds.Top, size, size, 180, 90);
        path.AddArc(bounds.Right - size, bounds.Top, size, size, 270, 90);
        path.AddArc(bounds.Right - size, bounds.Bottom - size, size, size, 0, 90);
        path.AddArc(bounds.Left, bounds.Bottom - size, size, size, 90, 90);
        path.CloseFigure();
        return path;
    }

    private sealed class EditorCard : Panel
    {
        internal EditorCard()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.UserPaint, true);
            BackColor = PanelInk;
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            e.Graphics.Clear(Ink);
            var rect = new Rectangle(1, 1, Math.Max(2, Width - 3), Math.Max(2, Height - 3));
            using var path = Rounded(rect, 10);
            using var fill = new LinearGradientBrush(rect,
                Color.FromArgb(30, 46, 60), PanelInk, LinearGradientMode.Vertical);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            e.Graphics.FillPath(fill, path);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var rect = new Rectangle(1, 1, Math.Max(2, Width - 3), Math.Max(2, Height - 3));
            using var path = Rounded(rect, 10);
            using var outline = new Pen(CardBorder, 1f);
            using var shine = new Pen(Color.FromArgb(60, 146, 171, 188), 1);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            e.Graphics.DrawPath(outline, path);
            e.Graphics.DrawLine(shine, 13, 2, Math.Max(13, Width - 14), 2);
        }
    }

    private sealed class EditorActionButton : Button
    {
        private bool _hover;
        private bool _active;
        [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
        internal string Glyph { get; set; } = "";
        [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
        internal bool Important { get; set; }
        [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
        internal bool Active
        {
            get => _active;
            set { _active = value; Invalidate(); }
        }

        internal EditorActionButton()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer, true);
            FlatStyle = FlatStyle.Flat;
            FlatAppearance.BorderSize = 0;
            ForeColor = Color.White;
            BackColor = ButtonInk;
            Font = new Font("Segoe UI", 9.5f, FontStyle.Regular);
            Cursor = Cursors.Hand;
            UseVisualStyleBackColor = false;
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

        protected override void OnEnabledChanged(EventArgs e)
        {
            base.OnEnabledChanged(e);
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            if (Width < 4 || Height < 4) return;

            var rect = new Rectangle(1, 1, Width - 3, Height - 3);
            using var outline = Rounded(rect, 5);
            bool light = Enabled && (_hover || _active || Important);
            Color top = Enabled
                ? (light ? Color.FromArgb(36, 82, 103) : Color.FromArgb(41, 59, 71))
                : Color.FromArgb(30, 43, 53);
            Color bottom = Enabled
                ? (light ? Color.FromArgb(14, 43, 59) : Color.FromArgb(22, 37, 49))
                : Color.FromArgb(24, 35, 44);
            using (var fill = new LinearGradientBrush(rect, top, bottom, LinearGradientMode.Vertical))
                g.FillPath(fill, outline);
            using (var border = new Pen(_active || (Enabled && Important) ? Cyan : CardBorder,
                       _active ? 1.6f : 1f))
                g.DrawPath(border, outline);
            if (Enabled)
            {
                using var highlight = new Pen(Color.FromArgb(57, 185, 216, 231), 1f);
                g.DrawLine(highlight, 8, 2, Width - 9, 2);
            }

            Color foreground = Enabled ? Color.FromArgb(237, 243, 247) :
                Color.FromArgb(119, 138, 150);
            if (Glyph.Length > 0)
                DrawGlyph(g, Glyph, new Rectangle(15, (Height - 19) / 2, 19, 19),
                    _active ? Cyan : foreground);
            Rectangle textArea = Glyph.Length > 0
                ? new Rectangle(40, 1, Math.Max(5, Width - 49), Height - 2)
                : new Rectangle(8, 1, Math.Max(5, Width - 16), Height - 2);
            TextRenderer.DrawText(g, Text, Font, textArea, foreground,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter |
                TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
            if (Focused && ShowFocusCues)
                ControlPaint.DrawFocusRectangle(g, Rectangle.Inflate(rect, -5, -4));
        }

        private static void DrawGlyph(Graphics g, string glyph, Rectangle bounds, Color color)
        {
            GraphicsState saved = g.Save();
            try
            {
                g.TranslateTransform(bounds.X, bounds.Y);
                using var p = new Pen(color, 1.9f) { StartCap = LineCap.Round,
                    EndCap = LineCap.Round, LineJoin = LineJoin.Round };
                switch (glyph)
                {
                    case "plus":
                        g.DrawLine(p, 9, 2, 9, 17);
                        g.DrawLine(p, 2, 9, 17, 9);
                        break;
                    case "delete":
                        g.DrawLine(p, 4, 4, 15, 15);
                        g.DrawLine(p, 15, 4, 4, 15);
                        break;
                    case "copy":
                        g.DrawRectangle(p, 5, 5, 11, 12);
                        g.DrawLine(p, 3, 13, 2, 2);
                        g.DrawLine(p, 2, 2, 12, 2);
                        break;
                    case "import":
                        g.DrawLine(p, 9, 1, 9, 12);
                        g.DrawLines(p, [new Point(5, 8), new Point(9, 12), new Point(13, 8)]);
                        g.DrawLines(p, [new Point(2, 13), new Point(2, 17),
                            new Point(16, 17), new Point(16, 13)]);
                        break;
                    case "edit":
                        g.DrawLine(p, 4, 15, 14, 4);
                        g.DrawLine(p, 6, 17, 16, 6);
                        g.DrawLine(p, 4, 15, 3, 18);
                        break;
                    case "layers":
                        g.DrawLines(p, [new Point(2, 6), new Point(9, 2),
                            new Point(17, 6), new Point(9, 10), new Point(2, 6)]);
                        g.DrawLines(p, [new Point(2, 10), new Point(9, 14), new Point(17, 10)]);
                        g.DrawLines(p, [new Point(2, 14), new Point(9, 18), new Point(17, 14)]);
                        break;
                    case "all":
                        g.DrawRectangle(p, 2, 2, 15, 15);
                        g.DrawLines(p, [new Point(5, 9), new Point(8, 12), new Point(14, 5)]);
                        break;
                    case "clear":
                        g.DrawRectangle(p, 3, 3, 13, 13);
                        break;
                    case "invert":
                        g.DrawLines(p, [new Point(2, 6), new Point(16, 6),
                            new Point(12, 2)]);
                        g.DrawLines(p, [new Point(16, 13), new Point(2, 13),
                            new Point(6, 17)]);
                        break;
                    case "database":
                        g.DrawEllipse(p, 2, 2, 15, 6);
                        g.DrawArc(p, 2, 9, 15, 7, 0, 180);
                        g.DrawLine(p, 2, 5, 2, 13);
                        g.DrawLine(p, 17, 5, 17, 13);
                        break;
                    case "info":
                        g.DrawEllipse(p, 2, 2, 15, 15);
                        g.DrawLine(p, 9, 8, 9, 14);
                        g.FillEllipse(new SolidBrush(color), 8, 5, 2, 2);
                        break;
                    case "verify":
                        g.DrawEllipse(p, 2, 2, 15, 15);
                        g.DrawLines(p, [new Point(5, 9), new Point(8, 12), new Point(14, 6)]);
                        break;
                    case "access":
                        g.DrawLines(p, [new Point(12, 2), new Point(17, 2),
                            new Point(17, 7)]);
                        g.DrawLine(p, 17, 2, 8, 11);
                        g.DrawLines(p, [new Point(15, 11), new Point(15, 16),
                            new Point(2, 16), new Point(2, 4), new Point(8, 4)]);
                        break;
                    case "back":
                        g.DrawLines(p, [new Point(10, 3), new Point(3, 9),
                            new Point(10, 15)]);
                        g.DrawLine(p, 3, 9, 17, 9);
                        break;
                }
            }
            finally { g.Restore(saved); }
        }
    }

    internal DatabaseEditorWorkspace(AppPaths paths, SkinPalette skin, Action goBack)
    {
        _paths = paths;
        _goBack = goBack;
        Dock = DockStyle.Fill;
        BackColor = Ink;
        ForeColor = Color.White;
        Font = new Font("Segoe UI", 10);
        Name = "DatabaseEditorWorkspace";
        BuildLayout();
        LoadUniverse();
    }

    private static Label Label(string text, int height = 24, bool strong = false)
    {
        return new Label
        {
            Text = text,
            Height = height,
            Width = 250,
            Margin = new Padding(0, 2, 0, 3),
            TextAlign = ContentAlignment.MiddleLeft,
            Font = new Font("Segoe UI", strong ? 10 : 9, strong ? FontStyle.Bold : FontStyle.Regular),
            ForeColor = strong ? Color.FromArgb(235, 242, 248) : Soft,
            BackColor = Color.Transparent
        };
    }

    private static EditorActionButton Button(string text, bool important = false, string glyph = "")
    {
        return new EditorActionButton
        {
            Text = text,
            Glyph = glyph,
            Important = important,
            Height = 36,
            Width = 250,
            Margin = new Padding(0, 2, 0, 3),
            Font = new Font("Segoe UI", 9.2f, important ? FontStyle.Bold : FontStyle.Regular)
        };
    }

    private static ComboBox DropDown()
    {
        return new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            FlatStyle = FlatStyle.Flat,
            Height = 34,
            Width = 250,
            Margin = new Padding(0, 2, 0, 7),
            BackColor = Color.FromArgb(15, 29, 41),
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 9.5f)
        };
    }

    private static EditorCard Pane()
    {
        return new EditorCard
        {
            Dock = DockStyle.Fill,
            Margin = new Padding(0, 0, 9, 0),
            Padding = new Padding(11)
        };
    }

    private static void SetEnabled(Button b, bool enabled, string tooltip)
    {
        b.Enabled = enabled;
        new ToolTip().SetToolTip(b, tooltip);
    }

    private void BuildLayout()
    {
        var bottom = new Panel
        {
            Dock = DockStyle.Bottom, Height = 66,
            BackColor = Color.FromArgb(11, 21, 32),
            Padding = new Padding(12, 6, 12, 7)
        };
        bottom.Paint += (_, e) =>
        {
            using var red = new Pen(Color.FromArgb(160, Red));
            e.Graphics.DrawLine(red, 0, bottom.Height - 2, bottom.Width, bottom.Height - 2);
        };
        Controls.Add(bottom);

        var footer = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 6, RowCount = 1,
            Margin = Padding.Empty,
            BackColor = Color.Transparent
        };
        for (int i = 0; i < 6; i++)
            footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f / 6f));
        bottom.Controls.Add(footer);
        AddFooter(footer, 0, "Change Database", ChooseUniverse, "database");
        AddFooter(footer, 1, "Import Database", ImportUniverse, "import");
        AddFooter(footer, 2, "Database Details", ShowDetails, "info");
        AddFooter(footer, 3, "Data Check", CheckData, "verify");
        AddFooter(footer, 4, "Open MDB in Access", OpenInAccess, "access");
        AddFooter(footer, 5, "Back", _goBack, "back");

        var content = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(12, 12, 4, 9),
            BackColor = Ink,
            ColumnCount = 3, RowCount = 1,
            GrowStyle = TableLayoutPanelGrowStyle.FixedSize
        };
        content.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 288));
        content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 44));
        content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 56));
        content.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        Controls.Add(content);

        var left = Pane();
        left.Name = "EditorTools";
        content.Controls.Add(left, 0, 0);
        BuildLeft(left);

        var center = Pane();
        center.Name = "RecordDirectory";
        content.Controls.Add(center, 1, 0);
        BuildCenter(center);

        var right = Pane();
        right.Name = "NotesAndSummary";
        right.Margin = new Padding(0);
        content.Controls.Add(right, 2, 0);
        BuildRight(right);
    }

    private static void AddFooter(TableLayoutPanel footer, int index,
        string title, Action callback, string glyph)
    {
        var button = Button(title, false, glyph);
        button.Dock = DockStyle.Fill;
        button.Margin = new Padding(3, 3, 4, 3);
        button.Click += (_, _) => Safe(callback);
        footer.Controls.Add(button, index, 0);
    }

    private void BuildLeft(Panel host)
    {
        var scroll = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoScroll = true,
            BackColor = Color.Transparent
        };
        host.Controls.Add(scroll);
        void Add(Control c) => scroll.Controls.Add(c);

        Add(Label("ACTIVE DATABASE", 20, true));
        _dbName.Name = "ActiveDatabaseName";
        _dbName.Width = 250;
        _dbName.Height = 39;
        _dbName.Margin = new Padding(0, 0, 0, 4);
        _dbName.Text = "NO DATABASE SELECTED";
        _dbName.AutoEllipsis = true;
        _dbName.ForeColor = Red;
        _dbName.TextAlign = ContentAlignment.MiddleLeft;
        _dbName.Font = new Font("Segoe UI", 13.5f, FontStyle.Bold);
        Add(_dbName);
        var rule = new Panel
        {
            Height = 2, Width = 250,
            Margin = new Padding(0, 0, 0, 9),
            BackColor = Red
        };
        Add(rule);

        Add(Label("CURRENT SECTION", 26, true));
        _section.Name = "EditorCategorySelector";
        ConfigureCombo(_section);
        _section.SelectedIndexChanged += (_, _) => LoadRecords();
        Add(_section);

        var add = Button("Add New Item", false, "plus");
        SetEnabled(add, false, "Category-specific Add screens will be implemented separately.");
        Add(add);

        var delete = Button("Delete Item(s)", false, "delete");
        SetEnabled(delete, false, "Direct record deletion is not enabled; use the MDB editor in Access.");
        Add(delete);

        var copy = Button("Copy Item(s)", false, "copy");
        SetEnabled(copy, false, "Record duplication requires a category-specific editor.");
        Add(copy);

        var import = Button("Import Items", false, "import");
        SetEnabled(import, false, "Import individual records will be implemented for each category.");
        Add(import);

        var external = Button("External Editing", false, "edit");
        external.Click += (_, _) => Safe(OpenInAccess);
        Add(external);

        _massButton.Text = "Mass Edit Selected Items";
        _massButton.Name = "MassEditButton";
        _massButton.Glyph = "layers";
        _massButton.Important = true;
        StyleButton(_massButton);
        _massButton.Click += (_, _) => Safe(MassEdit);
        Add(_massButton);

        _massField.Name = "MassEditFieldSelector";
        ConfigureCombo(_massField);
        _massField.Items.Add("Choose a text field...");
        _massField.SelectedIndex = 0;
        Add(_massField);

        Add(Label("RECORD SELECTION", 27, true));
        var selectAll = Button("Select All", false, "all");
        selectAll.Click += (_, _) =>
        {
            _records.BeginUpdate();
            try
            {
                for (int i = 0; i < _records.Items.Count; i++)
                    _records.SetSelected(i, true);
            }
            finally { _records.EndUpdate(); }
        };
        Add(selectAll);

        var clear = Button("Clear Selection", false, "clear");
        clear.Click += (_, _) => _records.ClearSelected();
        Add(clear);

        var invert = Button("Invert Selection", false, "invert");
        invert.Click += (_, _) =>
        {
            _records.BeginUpdate();
            try
            {
                for (int i = 0; i < _records.Items.Count; i++)
                    _records.SetSelected(i, !_records.GetSelected(i));
            }
            finally { _records.EndUpdate(); }
        };
        Add(invert);

        _hint.Height = 44;
        _hint.Width = 250;
        _hint.Margin = new Padding(0, 3, 0, 0);
        _hint.ForeColor = Soft;
        _hint.Font = new Font("Segoe UI", 8);
        _hint.Text = "Double-click an item to inspect its fields.";
        Add(_hint);

        void ResizeChildren()
        {
            if (scroll.IsDisposed) return;
            int width = Math.Max(175, scroll.ClientSize.Width -
                (scroll.VerticalScroll.Visible ? 22 : 6));
            foreach (Control control in scroll.Controls) control.Width = width;
        }
        scroll.SizeChanged += (_, _) => ResizeChildren();
        scroll.ControlAdded += (_, _) => ResizeChildren();
        ResizeChildren();
    }

    private static void ConfigureCombo(ComboBox combo)
    {
        combo.DropDownStyle = ComboBoxStyle.DropDownList;
        combo.FlatStyle = FlatStyle.Flat;
        combo.Height = 34;
        combo.Width = 250;
        combo.Margin = new Padding(0, 2, 0, 7);
        combo.BackColor = Color.FromArgb(15, 29, 41);
        combo.ForeColor = Color.White;
        combo.Font = new Font("Segoe UI", 9.5f);
    }

    private static void StyleButton(EditorActionButton button)
    {
        button.Height = 36;
        button.Width = 250;
        button.Margin = new Padding(0, 2, 0, 3);
        button.Font = new Font("Segoe UI", 9.2f, FontStyle.Regular);
    }

    private void BuildCenter(Panel host)
    {
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1, RowCount = 4,
            Margin = Padding.Empty,
            BackColor = Color.Transparent
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 41));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 53));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 29));
        host.Controls.Add(layout);

        var header = new Panel { Dock = DockStyle.Fill, BackColor = Color.Transparent };
        var title = new Label
        {
            Text = "RECORDS",
            Dock = DockStyle.Left, Width = 140,
            Font = new Font("Segoe UI", 13, FontStyle.Bold),
            ForeColor = Color.White, TextAlign = ContentAlignment.MiddleLeft
        };
        header.Controls.Add(title);
        _count.Dock = DockStyle.Right;
        _count.Width = 150;
        _count.ForeColor = Cyan;
        _count.Font = new Font("Segoe UI", 9.5f);
        _count.TextAlign = ContentAlignment.MiddleRight;
        header.Controls.Add(_count);
        header.Paint += (_, e) =>
        {
            using var red = new SolidBrush(Red);
            e.Graphics.FillRectangle(red, 0, 11, 5, 19);
        };
        title.Padding = new Padding(16, 0, 0, 0);
        layout.Controls.Add(header, 0, 0);

        var searchRow = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2, RowCount = 1,
            BackColor = Color.Transparent,
            Margin = Padding.Empty
        };
        searchRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 67));
        searchRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33));
        layout.Controls.Add(searchRow, 0, 1);

        _search.Name = "RecordSearch";
        _search.Dock = DockStyle.Fill;
        _search.Margin = new Padding(0, 3, 8, 9);
        _search.PlaceholderText = "Search names...";
        _search.BackColor = Color.FromArgb(10, 25, 36);
        _search.ForeColor = Color.White;
        _search.BorderStyle = BorderStyle.FixedSingle;
        _search.Font = new Font("Segoe UI", 10);
        _search.TextChanged += (_, _) => FilterRecords();
        searchRow.Controls.Add(_search, 0, 0);

        ConfigureCombo(_sort);
        _sort.Name = "RecordSort";
        _sort.Dock = DockStyle.Fill;
        _sort.Margin = new Padding(0, 3, 0, 9);
        _sort.Items.AddRange(["Name (A to Z)", "Name (Z to A)"]);
        _sort.SelectedIndex = 0;
        _sort.SelectedIndexChanged += (_, _) => FilterRecords();
        searchRow.Controls.Add(_sort, 1, 0);

        _records.Name = "DatabaseRecordList";
        _records.Dock = DockStyle.Fill;
        _records.Margin = new Padding(0, 0, 0, 4);
        _records.BorderStyle = BorderStyle.FixedSingle;
        _records.SelectionMode = SelectionMode.MultiExtended;
        _records.IntegralHeight = false;
        _records.BackColor = Color.FromArgb(6, 16, 25);
        _records.ForeColor = Color.FromArgb(235, 241, 246);
        _records.Font = new Font("Segoe UI", 10.25f);
        _records.ItemHeight = 23;
        _records.SelectedIndexChanged += (_, _) => UpdateNotesAndSummary();
        _records.DoubleClick += (_, _) => OpenRecordDetails();
        layout.Controls.Add(_records, 0, 2);

        _recordStatus.Dock = DockStyle.Fill;
        _recordStatus.ForeColor = Soft;
        _recordStatus.Text = "Choose a database to begin.";
        _recordStatus.Font = new Font("Segoe UI", 8.5f);
        _recordStatus.TextAlign = ContentAlignment.MiddleLeft;
        layout.Controls.Add(_recordStatus, 0, 3);
    }

    private void BuildRight(Panel host)
    {
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2,
            Margin = Padding.Empty, BackColor = Color.Transparent
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 50));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        host.Controls.Add(layout);

        var tabs = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1,
            BackColor = Color.Transparent, Margin = Padding.Empty
        };
        tabs.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 40));
        tabs.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 60));
        _notesTab.Text = "NOTES";
        _summaryTab.Text = "DATABASE SUMMARY";
        _notesTab.Name = "NotesTab";
        _summaryTab.Name = "DatabaseSummaryTab";
        for (int i = 0; i < 2; i++)
        {
            var button = i == 0 ? _notesTab : _summaryTab;
            StyleButton(button);
            button.Dock = DockStyle.Fill;
            button.Margin = new Padding(0, 0, i == 0 ? 7 : 0, 7);
            tabs.Controls.Add(button, i, 0);
        }
        layout.Controls.Add(tabs, 0, 0);
        _notesTab.Click += (_, _) => { _showSummary = false; UpdateNotesAndSummary(); };
        _summaryTab.Click += (_, _) => { _showSummary = true; UpdateNotesAndSummary(); };

        var notesFrame = new Panel
        {
            Dock = DockStyle.Fill,
            BorderStyle = BorderStyle.FixedSingle,
            BackColor = Color.FromArgb(6, 16, 25),
            Padding = new Padding(10, 8, 7, 6),
            Margin = new Padding(0)
        };
        _notes.Name = "EditorNotes";
        _notes.Dock = DockStyle.Fill;
        _notes.ReadOnly = true;
        _notes.BorderStyle = BorderStyle.None;
        _notes.BackColor = Color.FromArgb(6, 16, 25);
        _notes.ForeColor = Color.FromArgb(224, 234, 241);
        _notes.Font = new Font("Segoe UI", 10);
        _notes.Text = "Choose a database, category and record to view its notes.";
        notesFrame.Controls.Add(_notes);
        layout.Controls.Add(notesFrame, 0, 1);
        UpdateNotesAndSummary();
    }

    private void LoadUniverse()
    {
        var universes = _paths.Universes();
        if (universes.Count == 0)
        {
            _universe = null;
            _dbName.Text = "NO DATABASE SELECTED";
            _count.Text = "";
            _notes.Text = "No MDB database is installed.\n\nUse Import Database below to add an existing Access MDB.";
            _section.Items.Clear();
            _records.Items.Clear();
            return;
        }

        string selected = _paths.ReadSetting("Database", "");
        _universe = universes.FirstOrDefault(x => x.Name.Equals(selected, StringComparison.OrdinalIgnoreCase))
            ?? universes[0];
        _dbName.Text = _universe.Name;
        _paths.WriteSetting("Database", _universe.Name);
        LoadSections();
    }

    private void LoadSections()
    {
        _section.Items.Clear();
        _records.Items.Clear();
        _loaded = null;
        _allEntries.Clear();
        _massField.Items.Clear();
        _massField.Items.Add("Choose a text field...");
        _massField.SelectedIndex = 0;
        _count.Text = "";
        if (_universe is null) return;

        try
        {
            foreach (string name in AccessDatabase.TableNames(_universe.MdbPath, _password))
                _section.Items.Add(name);
            if (_section.Items.Count > 0)
            {
                string initial = _section.Items.Cast<string>()
                    .FirstOrDefault(s => s.Equals("Workers", StringComparison.OrdinalIgnoreCase))
                    ?? (string)_section.Items[0]!;
                _section.SelectedItem = initial;
            }
            else _recordStatus.Text = "This MDB has no user tables.";
        }
        catch (Exception ex)
        {
            _recordStatus.Text = "Cannot read MDB. Access Database Engine may be missing.";
            _notes.Text = "The database could not be opened.\n\n" + ex.Message +
                "\n\nThe game requires a compatible 64-bit Microsoft Access Database Engine.";
        }
    }

    private void LoadRecords()
    {
        _records.Items.Clear();
        _loaded = null;
        _allEntries.Clear();
        _total = 0;
        _massField.Items.Clear();
        _massField.Items.Add("Choose a text field...");
        _massField.SelectedIndex = 0;
        if (_universe is null || _section.SelectedItem is not string table) return;

        try
        {
            var result = AccessDatabase.Browse(_universe.MdbPath, table, _password, 10000);
            _loaded = result.Rows;
            _total = result.Total;
            var nameColumn = ChooseNameColumn(result.Rows);
            foreach (DataRow row in result.Rows.Rows)
            {
                string label = nameColumn is null ? "Record " + (_allEntries.Count + 1) :
                    Convert.ToString(row[nameColumn]) ?? "";
                if (string.IsNullOrWhiteSpace(label)) label = "(Unnamed record)";
                _allEntries.Add(new Entry(row, label));
            }
            foreach (DataColumn col in result.Rows.Columns)
                if (col.DataType == typeof(string) &&
                    !col.ColumnName.EndsWith("ID", StringComparison.OrdinalIgnoreCase))
                    _massField.Items.Add(col.ColumnName);
            _count.Text = _total.ToString("N0") + " items";
            FilterRecords();
        }
        catch (Exception ex)
        {
            _recordStatus.Text = "Unable to list records.";
            _notes.Text = "This category could not be read.\n\n" + ex.Message;
        }
    }

    private static DataColumn? ChooseNameColumn(DataTable table)
    {
        string[] preferred =
        [
            "Name", "WorkerName", "CompanyName", "FullName", "Title", "EventName",
            "ShowName", "NameShort", "ShortName", "Worker", "FirstName", "Description"
        ];
        foreach (var label in preferred)
            foreach (DataColumn column in table.Columns)
                if (column.DataType == typeof(string) &&
                    column.ColumnName.Equals(label, StringComparison.OrdinalIgnoreCase))
                    return column;
        return table.Columns.Cast<DataColumn>().FirstOrDefault(c => c.DataType == typeof(string))
            ?? table.Columns.Cast<DataColumn>().FirstOrDefault();
    }

    private void FilterRecords()
    {
        string q = _search.Text.Trim();
        IEnumerable<Entry> entries = _allEntries.Where(x =>
            q.Length == 0 || x.Name.Contains(q, StringComparison.OrdinalIgnoreCase));
        entries = _sort.SelectedIndex == 1
            ? entries.OrderByDescending(x => x.Name, StringComparer.OrdinalIgnoreCase)
            : entries.OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase);
        _records.BeginUpdate();
        try
        {
            _records.Items.Clear();
            foreach (Entry entry in entries) _records.Items.Add(entry);
        }
        finally { _records.EndUpdate(); }
        _recordStatus.Text = $"Showing {_records.Items.Count:N0} of {_total:N0} records" +
            (_total > _allEntries.Count ? " (first 10,000 loaded)" : "");
        UpdateNotesAndSummary();
    }

    private void UpdateNotesAndSummary()
    {
        if (_notes.IsDisposed || _notesTab.IsDisposed) return;
        _notesTab.Active = !_showSummary;
        _summaryTab.Active = _showSummary;

        if (_showSummary)
        {
            var file = _universe is null ? null : new FileInfo(_universe.MdbPath);
            string summary = _universe is null ? "No database selected." :
                "Database: " + _universe.Name +
                "\nCategory: " + (_section.SelectedItem?.ToString() ?? "None") +
                "\nRecords: " + _total.ToString("N0") +
                "\nCategories: " + _section.Items.Count +
                "\nFile size: " + (file!.Length / 1024d / 1024d).ToString("N1") + " MB" +
                "\nModified: " + file.LastWriteTime.ToString("g") +
                "\n\nLocation:\n" + _universe.MdbPath;
            if (_records.SelectedItem is Entry chosen)
            {
                summary += "\n\nSelected: " + chosen.Name + "\n\nFIELDS\n";
                foreach (DataColumn c in chosen.Row.Table.Columns.Cast<DataColumn>().Take(18))
                {
                    string value = Convert.ToString(chosen.Row[c]) ?? "";
                    if (value.Length > 85) value = value[..85] + "...";
                    summary += c.ColumnName + ": " + value + "\n";
                }
            }
            _notes.Text = summary;
            return;
        }

        if (_records.SelectedItem is not Entry item)
        {
            _notes.Text = "Select a record to see any notes stored in its MDB fields.";
            return;
        }

        var noteCols = item.Row.Table.Columns.Cast<DataColumn>()
            .Where(x => x.DataType == typeof(string) &&
                (x.ColumnName.Contains("Note", StringComparison.OrdinalIgnoreCase) ||
                 x.ColumnName.Contains("Bio", StringComparison.OrdinalIgnoreCase) ||
                 x.ColumnName.Contains("Description", StringComparison.OrdinalIgnoreCase)))
            .ToArray();
        _notes.Text = noteCols.Length == 0
            ? "No notes field exists in this category.\n\nSelect Database Summary to inspect the record's fields."
            : string.Join("\n\n", noteCols.Select(col =>
                col.ColumnName + "\n" +
                (string.IsNullOrWhiteSpace(Convert.ToString(item.Row[col])) ?
                    "(empty)" : Convert.ToString(item.Row[col]))));
    }

    private void OpenRecordDetails()
    {
        if (_records.SelectedItem is not Entry entry) return;
        using var dialog = new Form
        {
            Text = entry.Name + " | Record Details",
            StartPosition = FormStartPosition.CenterParent,
            Size = new Size(680, 610),
            MinimumSize = new Size(480, 420),
            BackColor = Ink,
            ForeColor = Color.White
        };
        var details = new DataGridView
        {
            Dock = DockStyle.Fill,
            ReadOnly = true,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            RowHeadersVisible = false,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
            BackgroundColor = PanelInk,
            GridColor = Color.FromArgb(53, 102, 126),
            ForeColor = Color.White,
            ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle
            {
                BackColor = Color.FromArgb(20, 48, 66), ForeColor = Color.White
            },
            EnableHeadersVisualStyles = false
        };
        details.Columns.Add("field", "Field");
        details.Columns.Add("value", "Value");
        foreach (DataColumn col in entry.Row.Table.Columns)
            details.Rows.Add(col.ColumnName, Convert.ToString(entry.Row[col]) ?? "");
        dialog.Controls.Add(details);

        var bar = new Panel { Dock = DockStyle.Bottom, Height = 57 };
        var open = Button("Edit in Microsoft Access");
        open.Dock = DockStyle.Right;
        open.Width = 225;
        open.Click += (_, _) => Safe(OpenInAccess);
        bar.Controls.Add(open);
        dialog.Controls.Add(bar);
        dialog.ShowDialog(FindForm());
    }

    private void MassEdit()
    {
        if (_universe is null || _loaded is null || _section.SelectedItem is not string table)
        {
            Alert("Choose a database and category first.");
            return;
        }
        if (_massField.SelectedIndex < 1)
        {
            Alert("Choose a text field from the dropdown directly below Mass Edit.");
            return;
        }
        var chosen = _records.SelectedItems.Cast<Entry>().ToArray();
        if (chosen.Length == 0)
        {
            Alert("Select one or more records first. Use Select All for the currently listed records.");
            return;
        }

        string field = (string)_massField.SelectedItem!;
        string? value = PromptValue("Mass Edit: " + field,
            $"Set {field} for {chosen.Length:N0} selected record(s).\nThe operation creates a backup first.");
        if (value is null) return;
        if (MessageBox.Show(FindForm(),
                $"Change '{field}' in {chosen.Length:N0} record(s)?\n\n" +
                "A backup is created before changes. This cannot be undone from the editor.",
                "Confirm Mass Edit", MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning) != DialogResult.Yes) return;

        string backup = AccessDatabase.MassSetText(
            _universe.MdbPath, table, field,
            chosen.Select(x => x.Row).ToArray(), value, _password);
        LoadRecords();
        Alert("Changes saved.\n\nBackup:\n" + backup);
    }

    private string? PromptValue(string title, string instructions)
    {
        using var dialog = new Form
        {
            Text = title, Width = 510, Height = 230,
            StartPosition = FormStartPosition.CenterParent,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            MaximizeBox = false, MinimizeBox = false,
            BackColor = Ink, ForeColor = Color.White
        };
        var message = new Label
        {
            Text = instructions, Left = 20, Top = 16,
            Width = 455, Height = 65, ForeColor = Color.White
        };
        var entry = new TextBox
        {
            Left = 20, Top = 90, Width = 445,
            BackColor = Color.FromArgb(20, 39, 51),
            ForeColor = Color.White
        };
        var okay = Button("Apply");
        okay.Left = 235; okay.Top = 136; okay.Width = 105;
        okay.DialogResult = DialogResult.OK;
        var cancel = Button("Cancel");
        cancel.Left = 350; cancel.Top = 136; cancel.Width = 105;
        cancel.DialogResult = DialogResult.Cancel;
        dialog.Controls.AddRange([message, entry, okay, cancel]);
        dialog.AcceptButton = okay;
        dialog.CancelButton = cancel;
        return dialog.ShowDialog(FindForm()) == DialogResult.OK ? entry.Text : null;
    }

    private void ChooseUniverse()
    {
        var entries = _paths.Universes();
        if (entries.Count == 0) { Alert("Import a database first."); return; }
        using var picker = new Form
        {
            Text = "Select Database", Size = new Size(455, 240),
            StartPosition = FormStartPosition.CenterParent,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            BackColor = Ink, ForeColor = Color.White,
            MaximizeBox = false, MinimizeBox = false
        };
        var combo = DropDown();
        combo.Location = new Point(20, 28);
        combo.Width = 390;
        foreach (var entry in entries) combo.Items.Add(entry.Name);
        combo.SelectedItem = _universe?.Name ?? entries[0].Name;
        var select = Button("Select Database", true);
        select.Location = new Point(20, 97);
        select.Width = 190;
        select.DialogResult = DialogResult.OK;
        picker.Controls.Add(combo);
        picker.Controls.Add(select);
        picker.AcceptButton = select;
        if (picker.ShowDialog(FindForm()) != DialogResult.OK || combo.SelectedItem is not string selected)
            return;

        _paths.WriteSetting("Database", selected);
        _universe = entries.First(x => x.Name == selected);
        _dbName.Text = selected;
        _search.Text = "";
        LoadSections();
    }

    private void ImportUniverse()
    {
        using var file = new OpenFileDialog
        {
            Filter = "Access databases (*.mdb)|*.mdb",
            Title = "Import a database universe",
            CheckFileExists = true
        };
        if (file.ShowDialog(FindForm()) != DialogResult.OK) return;
        string? name = PromptValue("Import Database",
            "Enter a unique name for the imported database.\nUse a short name without slashes.");
        if (name is null) return;
        if (string.IsNullOrWhiteSpace(name)) name = Path.GetFileNameWithoutExtension(file.FileName);
        var imported = _paths.ImportUniverse(file.FileName, name);
        _paths.WriteSetting("Database", imported.Name);
        LoadUniverse();
    }

    private void ShowDetails()
    {
        if (_universe is null) { Alert("No database selected."); return; }
        FileInfo file = new(_universe.MdbPath);
        Alert("Database: " + _universe.Name +
            "\nMDB: " + _universe.MdbPath +
            "\nSize: " + (file.Length / (1024d * 1024d)).ToString("N1") + " MB" +
            "\nModified: " + file.LastWriteTime.ToString("g") +
            "\nCategories: " + _section.Items.Count);
    }

    private void CheckData()
    {
        if (_universe is null) { Alert("No database selected."); return; }
        var tables = AccessDatabase.TableNames(_universe.MdbPath, _password);
        Alert("MDB header verified.\n\n" + tables.Count +
            " user tables are accessible.\n\nThis is an accessibility check, not a full relational integrity audit.");
    }

    private void OpenInAccess()
    {
        if (_universe is null) { Alert("Choose a database first."); return; }
        AccessDatabase.OpenInMicrosoftAccess(_universe.MdbPath);
    }

    private void Alert(string message) =>
        MessageBox.Show(FindForm(), message, "Database Editor",
            MessageBoxButtons.OK, MessageBoxIcon.Information);

    private static void Safe(Action action)
    {
        try { action(); }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Database Editor",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }
}
