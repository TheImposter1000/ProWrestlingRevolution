using System.Data;
using System.Drawing;
using System.Windows.Forms;

namespace ProWrestlingRevolution;

/// <summary>
/// Simple database directory. This screen locates records and offers category-wide
/// operations; it is deliberately NOT the per-worker/per-company editing form.
/// All displayed records and section names come from the selected MDB.
/// </summary>
internal sealed class DatabaseEditorWorkspace : UserControl
{
    private static readonly Color Ink = Color.FromArgb(7, 18, 30);
    private static readonly Color PanelInk = Color.FromArgb(12, 28, 43);
    private static readonly Color ButtonInk = Color.FromArgb(19, 39, 54);
    private static readonly Color Cyan = Color.FromArgb(100, 208, 238);
    private static readonly Color Red = Color.FromArgb(248, 45, 78);
    private static readonly Color Soft = Color.FromArgb(175, 197, 214);

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
    private readonly Button _massButton = new();
    private readonly Button _notesTab = new();
    private readonly Button _summaryTab = new();

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
            Dock = DockStyle.Top,
            Height = height,
            TextAlign = ContentAlignment.MiddleLeft,
            Font = new Font("Segoe UI", strong ? 11 : 9, strong ? FontStyle.Bold : FontStyle.Regular),
            ForeColor = strong ? Color.White : Soft,
            BackColor = Color.Transparent
        };
    }

    private static Button Button(string text, bool important = false)
    {
        var button = new Button
        {
            Text = text,
            Height = 34,
            Width = 238,
            Margin = new Padding(0, 2, 0, 2),
            TextAlign = ContentAlignment.MiddleCenter,
            FlatStyle = FlatStyle.Flat,
            BackColor = ButtonInk,
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 9, important ? FontStyle.Bold : FontStyle.Regular),
            Cursor = Cursors.Hand,
            TabStop = true,
            UseVisualStyleBackColor = false
        };
        button.FlatAppearance.BorderColor = important ? Cyan : Color.FromArgb(63, 105, 126);
        button.FlatAppearance.MouseOverBackColor = Color.FromArgb(29, 66, 89);
        return button;
    }

    private static ComboBox DropDown()
    {
        return new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            FlatStyle = FlatStyle.Flat,
            Height = 33,
            Width = 238,
            Margin = new Padding(0, 2, 0, 7),
            BackColor = Color.FromArgb(15, 34, 50),
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 10)
        };
    }

    private static Panel Pane()
    {
        return new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = PanelInk,
            Margin = new Padding(0, 0, 9, 0),
            Padding = new Padding(9)
        };
    }

    private static void SetEnabled(Button b, bool enabled, string tooltip)
    {
        b.Enabled = enabled;
        if (!enabled) b.ForeColor = Color.FromArgb(133, 146, 158);
        new ToolTip().SetToolTip(b, tooltip);
    }

    private void BuildLayout()
    {
        var bottom = new Panel
        {
            Dock = DockStyle.Bottom, Height = 65,
            BackColor = Color.FromArgb(9, 25, 39),
            Padding = new Padding(15, 8, 15, 6)
        };
        Controls.Add(bottom);
        var footer = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            WrapContents = false,
            BackColor = Color.Transparent
        };
        bottom.Controls.Add(footer);

        AddFooter(footer, "Change Database", ChooseUniverse);
        AddFooter(footer, "Import Database", ImportUniverse);
        AddFooter(footer, "Database Details", ShowDetails);
        AddFooter(footer, "Data Check", CheckData);
        AddFooter(footer, "Open MDB in Access", OpenInAccess);
        AddFooter(footer, "Back", _goBack);

        var content = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(14, 15, 5, 11),
            BackColor = Ink,
            ColumnCount = 3, RowCount = 1,
            GrowStyle = TableLayoutPanelGrowStyle.FixedSize
        };
        content.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 285));
        content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 43));
        content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 57));
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

    private static void AddFooter(FlowLayoutPanel footer, string title, Action callback)
    {
        Button b = Button(title);
        b.AutoSize = true;
        b.MinimumSize = new Size(title.Length > 16 ? 165 : 131, 39);
        b.Height = 39;
        b.Margin = new Padding(0, 1, 9, 0);
        b.Click += (_, _) => Safe(callback);
        footer.Controls.Add(b);
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
        Add(Label("CURRENT SECTION", 27, true));

        _section.Name = "EditorCategorySelector";
        ConfigureCombo(_section);
        _section.SelectedIndexChanged += (_, _) => LoadRecords();
        Add(_section);

        var add = Button("＋   Add New Item");
        SetEnabled(add, false, "Category-specific Add screens will be implemented separately.");
        Add(add);

        var delete = Button("×   Delete Item(s)");
        SetEnabled(delete, false, "Direct record deletion is not enabled; use the MDB editor in Access.");
        Add(delete);

        var copy = Button("▢   Copy Item(s)");
        SetEnabled(copy, false, "Record duplication requires a category-specific editor.");
        Add(copy);

        var import = Button("⇩   Import Items");
        SetEnabled(import, false, "Import individual records will be implemented for each category.");
        Add(import);

        var external = Button("↗   External Editing");
        external.Click += (_, _) => Safe(OpenInAccess);
        Add(external);

        _massButton.Text = "Mass Edit Selected Items";
        _massButton.Name = "MassEditButton";
        StyleButton(_massButton);
        _massButton.Click += (_, _) => Safe(MassEdit);
        Add(_massButton);

        // Intentional layout: mass edit field drop-down immediately BELOW its button.
        _massField.Name = "MassEditFieldSelector";
        ConfigureCombo(_massField);
        _massField.Items.Add("Choose a text field...");
        _massField.SelectedIndex = 0;
        Add(_massField);

        Add(Label("SEARCH RECORDS", 28, true));
        _search.Name = "RecordSearch";
        _search.Width = 238;
        _search.Height = 30;
        _search.Margin = new Padding(0, 2, 0, 6);
        _search.PlaceholderText = "Search names...";
        _search.BackColor = Color.FromArgb(15, 34, 50);
        _search.ForeColor = Color.White;
        _search.BorderStyle = BorderStyle.FixedSingle;
        _search.TextChanged += (_, _) => FilterRecords();
        Add(_search);

        ConfigureCombo(_sort);
        _sort.Items.AddRange(["Name (A to Z)", "Name (Z to A)"]);
        _sort.SelectedIndex = 0;
        _sort.SelectedIndexChanged += (_, _) => FilterRecords();
        Add(_sort);

        var selectAll = Button("Select All");
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

        var clear = Button("Clear Selection");
        clear.Click += (_, _) => _records.ClearSelected();
        Add(clear);

        var invert = Button("Invert Selection");
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

        _hint.Height = 48;
        _hint.Width = 238;
        _hint.ForeColor = Soft;
        _hint.Font = new Font("Segoe UI", 8);
        _hint.Text = "Double-click an item to inspect its fields. Individual editing screens will be added by category.";
        Add(_hint);

        void ResizeChildren()
        {
            if (scroll.IsDisposed) return;
            int width = Math.Max(180, scroll.ClientSize.Width - (scroll.VerticalScroll.Visible ? 22 : 5));
            foreach (Control control in scroll.Controls) control.Width = width;
        }
        scroll.SizeChanged += (_, _) => ResizeChildren();
        scroll.ControlAdded += (_, _) => ResizeChildren();
        ResizeChildren();
    }

    private static void ConfigureCombo(ComboBox combo)
    {
        var sample = DropDown();
        combo.DropDownStyle = sample.DropDownStyle;
        combo.FlatStyle = sample.FlatStyle;
        combo.Height = sample.Height;
        combo.Width = sample.Width;
        combo.Margin = sample.Margin;
        combo.BackColor = sample.BackColor;
        combo.ForeColor = sample.ForeColor;
        combo.Font = sample.Font;
        sample.Dispose();
    }

    private static void StyleButton(Button button)
    {
        using var b = Button(button.Text);
        button.Height = b.Height;
        button.Width = b.Width;
        button.Margin = b.Margin;
        button.FlatStyle = b.FlatStyle;
        button.BackColor = b.BackColor;
        button.ForeColor = b.ForeColor;
        button.Font = b.Font;
        button.Cursor = Cursors.Hand;
        button.FlatAppearance.BorderColor = Cyan;
        button.UseVisualStyleBackColor = false;
    }

    private void BuildCenter(Panel host)
    {
        var heading = new Panel { Dock = DockStyle.Top, Height = 58, BackColor = Color.FromArgb(15, 36, 51) };
        host.Controls.Add(heading);

        // The database NAME is the central heading, where "Records" used to be.
        _dbName.Name = "ActiveDatabaseName";
        _dbName.Dock = DockStyle.Fill;
        _dbName.Text = "NO DATABASE SELECTED";
        _dbName.ForeColor = Color.White;
        _dbName.TextAlign = ContentAlignment.MiddleLeft;
        _dbName.Padding = new Padding(13, 0, 0, 0);
        _dbName.Font = new Font("Segoe UI", 15, FontStyle.Bold);
        heading.Controls.Add(_dbName);
        _count.Dock = DockStyle.Right;
        _count.Width = 122;
        _count.ForeColor = Cyan;
        _count.TextAlign = ContentAlignment.MiddleRight;
        _count.Padding = new Padding(0, 0, 8, 0);
        _count.Font = new Font("Segoe UI", 9);
        heading.Controls.Add(_count);
        _count.BringToFront();

        _recordStatus.Dock = DockStyle.Bottom;
        _recordStatus.Height = 28;
        _recordStatus.ForeColor = Soft;
        _recordStatus.Text = "Choose a database to begin.";
        _recordStatus.Font = new Font("Segoe UI", 8);
        _recordStatus.Padding = new Padding(4, 5, 0, 0);
        host.Controls.Add(_recordStatus);

        _records.Name = "DatabaseRecordList";
        _records.Dock = DockStyle.Fill;
        _records.BorderStyle = BorderStyle.None;
        _records.SelectionMode = SelectionMode.MultiExtended;
        _records.IntegralHeight = false;
        _records.BackColor = Color.FromArgb(5, 16, 26);
        _records.ForeColor = Color.White;
        _records.Font = new Font("Segoe UI", 10.5f, FontStyle.Regular);
        _records.ItemHeight = 23;
        _records.SelectedIndexChanged += (_, _) => UpdateNotesAndSummary();
        _records.DoubleClick += (_, _) => OpenRecordDetails();
        host.Controls.Add(_records);
        _records.BringToFront();
        heading.BringToFront();
    }

    private void BuildRight(Panel host)
    {
        var tabs = new TableLayoutPanel
        {
            Dock = DockStyle.Top, Height = 49, ColumnCount = 2,
            BackColor = Color.Transparent
        };
        tabs.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 35));
        tabs.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 65));

        _notesTab.Text = "NOTES";
        _summaryTab.Text = "DATABASE SUMMARY";
        _notesTab.Name = "NotesTab";
        _summaryTab.Name = "DatabaseSummaryTab";
        for (int i = 0; i < 2; i++)
        {
            var button = i == 0 ? _notesTab : _summaryTab;
            StyleButton(button);
            button.Dock = DockStyle.Fill;
            button.Margin = new Padding(0, 0, 7, 8);
            tabs.Controls.Add(button, i, 0);
        }

        _notesTab.Click += (_, _) => { _showSummary = false; UpdateNotesAndSummary(); };
        _summaryTab.Click += (_, _) => { _showSummary = true; UpdateNotesAndSummary(); };

        _notes.Name = "EditorNotes";
        _notes.Dock = DockStyle.Fill;
        _notes.ReadOnly = true;
        _notes.BorderStyle = BorderStyle.FixedSingle;
        _notes.BackColor = Color.FromArgb(5, 16, 26);
        _notes.ForeColor = Color.FromArgb(224, 234, 241);
        _notes.Font = new Font("Segoe UI", 10);
        _notes.Text = "Choose a database, category and record to view its notes.";
        host.Controls.Add(_notes);
        host.Controls.Add(tabs);
        tabs.BringToFront();
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
        _notesTab.BackColor = !_showSummary ? Color.FromArgb(26, 73, 97) : ButtonInk;
        _summaryTab.BackColor = _showSummary ? Color.FromArgb(26, 73, 97) : ButtonInk;
        _notesTab.FlatAppearance.BorderColor = !_showSummary ? Cyan : Soft;
        _summaryTab.FlatAppearance.BorderColor = _showSummary ? Cyan : Soft;

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
            details.Rows.Add(col.ColumnName, Convert.ToString(entry.Row[col]));
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
