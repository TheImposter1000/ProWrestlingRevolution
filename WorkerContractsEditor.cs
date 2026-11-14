using System.Data;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Windows.Forms;

namespace ProWrestlingRevolution;

/// <summary>
/// Dedicated, two-stage Contracts workspace, following the supplied PWR
/// list and four-card editor artwork. All editable values belong to native
/// tblContract / tblContractBio, with no speculative MDB columns.
/// </summary>
internal sealed class WorkerContractsEditor : UserControl
{
    private static readonly Color Ink = Color.FromArgb(5, 20, 37);
    private static readonly Color Navy = Color.FromArgb(7, 31, 55);
    private static readonly Color Medium = Color.FromArgb(15, 54, 86);
    private static readonly Color Cyan = Color.FromArgb(0, 216, 255);
    private static readonly Color White = Color.FromArgb(243, 249, 255);
    private static readonly Color Muted = Color.FromArgb(181, 208, 231);
    private static readonly Color Border = Color.FromArgb(49, 169, 219);

    private readonly AppPaths _paths;
    private readonly PrwWorkerProfile _profile;
    private readonly DataTable _contracts;
    private readonly DataTable? _bios;
    private readonly string? _databaseFile;
    private readonly string? _password;
    private readonly Action<string> _onContext;
    private readonly Action _onSaved;
    private readonly ToolTip _tips = new() { InitialDelay = 300 };
    private readonly Dictionary<string, IReadOnlyList<string>> _choices =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _invalidFields =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly List<PrwContractEditorService.Company> _companies = [];
    private readonly List<PrwContractEditorService.WorkerChoice> _workers = [];
    private readonly List<PrwContractEditorService.Template> _templates = [];
    private readonly Dictionary<string, Bitmap> _logoCache =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly Panel _view = new() { Dock = DockStyle.Fill, BackColor = Color.Transparent };
    // Each view is disposed when navigating; controls must be recreated.
    private ContractGrid _grid = new();
    private TextBox _search = new();
    private ComboBox _filter = new();
    private Label _listStatus = new();
    private Label _editorStatus = new();
    private DataRow? _editing;
    private bool _busy;
    private int _tempUid = -1;
    private Dictionary<string, object>? _newDraftInitial;

    internal bool IsEditing => _editing is not null;
    internal string? EditingName => _editing is null ? null : Value(_editing, "Name");

    internal WorkerContractsEditor(AppPaths paths, PrwWorkerProfile profile,
        string? databaseFile, string? password,
        Action<string> onContext, Action onSaved)
    {
        _paths = paths;
        _profile = profile;
        _databaseFile = databaseFile;
        _password = password;
        _onContext = onContext;
        _onSaved = onSaved;
        _contracts = profile.Sections.Single(s => s.Table == "tblContract").Records;
        _bios = profile.Sections.FirstOrDefault(s => s.Table == "tblContractBio")?.Records;
        Name = "WorkerContractsEditor";
        Dock = DockStyle.Fill;
        BackColor = Color.Transparent;
        PrwContractEditorService.AssertFields(_contracts);
        Controls.Add(_view);
        Disposed += (_, _) =>
        {
            _tips.Dispose();
            foreach (var logo in _logoCache.Values) logo.Dispose();
            _logoCache.Clear();
        };
        BuildList();
        if (_databaseFile is not null && File.Exists(_databaseFile))
        {
            HandleCreated += (_, _) => _ = FetchLookupsAsync();
            if (IsHandleCreated) _ = FetchLookupsAsync();
        }
    }

    internal bool ValidateBeforeLeaving()
    {
        if (_invalidFields.Count == 0) return true;
        _editorStatus.Text = "Correct the marked fields before leaving: " +
            string.Join(", ", _invalidFields);
        return false;
    }

    internal bool ReturnToList()
    {
        if (!IsEditing) return false;
        if (!ValidateBeforeLeaving()) return true;
        _editing = null;
        _invalidFields.Clear();
        BuildList();
        _onContext("Contracts");
        return true;
    }

    private async Task FetchLookupsAsync()
    {
        if (_databaseFile is null || _busy || IsDisposed) return;
        _busy = true;
        try
        {
            string file = _databaseFile;
            var lookup = await Task.Run(() =>
            {
                var companies = PrwContractEditorService.LoadCompanies(file, _password);
                var people = PrwContractEditorService.LoadWorkers(file, _password);
                var templates = PrwContractEditorService.LoadTemplates(
                    file, _password, _profile.Identity.Uid);
                return (companies, people, templates);
            });
            if (IsDisposed || Disposing) return;
            _companies.AddRange(lookup.companies);
            _workers.AddRange(lookup.people);
            _templates.AddRange(lookup.templates);
            // Populate each dropdown with distinct values from the active
            // MDB, not an unverified list of Total Extreme Wrestling codes.
            foreach (string field in PrwSchema.Tables.Single(schema =>
                         schema.Name == "tblContract").Columns)
            {
                var values = _contracts.Rows.Cast<DataRow>()
                    .Where(r => r.RowState != DataRowState.Deleted)
                    .Select(r => Value(r, field))
                    .Concat(_templates.Select(t => Value(t.Values, field)))
                    .Where(value => !string.IsNullOrWhiteSpace(value))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
                    .Take(150).ToArray();
                _choices[field] = values;
            }
            if (_editing is null) BuildList();
            else PopulateLiveChoices();
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException
                                   or System.Data.OleDb.OleDbException)
        {
            if (IsDisposed) return;
            _listStatus.Text = "Employer lookup unavailable: " + ex.Message;
        }
        finally { _busy = false; }
    }

    private void PopulateLiveChoices()
    {
        // A slow MDB lookup must never destroy a textbox while the user is
        // typing. Add the newly discovered options to the active controls.
        var employers = _view.Controls.Find("ContractEmployer", true)
            .OfType<ComboBox>().FirstOrDefault();
        if (employers is not null)
        {
            foreach (var company in _companies)
                if (!employers.Items.Contains(company)) employers.Items.Add(company);
            if (_editing is not null)
            {
                var actual = _companies.FirstOrDefault(c =>
                    Convert.ToString(c.Uid, CultureInfo.InvariantCulture) ==
                        Value(_editing, "FedUID"));
                if (actual is not null) employers.SelectedItem = actual;
            }
        }
        var manager = _view.Controls.Find("ContractField_ManagerWorkerUID", true)
            .OfType<ComboBox>().FirstOrDefault();
        if (manager is not null)
        {
            foreach (var worker in _workers)
                if (!manager.Items.Contains(worker)) manager.Items.Add(worker);
            if (_editing is not null)
            {
                var actual = _workers.FirstOrDefault(w =>
                    Convert.ToString(w.Uid, CultureInfo.InvariantCulture) ==
                        Value(_editing, "ManagerWorkerUID"));
                if (actual is not null) manager.SelectedItem = actual;
            }
        }
        var templates = _view.Controls.Find("ContractTemplateSelector", true)
            .OfType<ComboBox>().FirstOrDefault();
        if (templates is not null)
        {
            foreach (var template in _templates)
                if (!templates.Items.Contains(template))
                    templates.Items.Add(template);
            if (templates.SelectedIndex < 0 && templates.Items.Count > 0)
                templates.SelectedIndex = 0;
            var apply = _view.Controls.Find("ContractCopyTerms", true)
                .OfType<Button>().FirstOrDefault();
            if (apply is not null) apply.Enabled = templates.Items.Count != 0;
        }
        foreach (var control in _view.Controls.Find("ContractFourCards", true)
            .SelectMany(card => Descendants(card)).OfType<ComboBox>())
        {
            if (!control.Name.StartsWith("ContractField_", StringComparison.Ordinal) ||
                control.DropDownStyle != ComboBoxStyle.DropDown) continue;
            string field = control.Name["ContractField_".Length..];
            if (!_choices.TryGetValue(field, out var values)) continue;
            string typed = control.Text;
            foreach (var choice in values)
                if (!control.Items.Contains(choice)) control.Items.Add(choice);
            control.Text = typed;
        }
    }

    private static IEnumerable<Control> Descendants(Control parent) =>
        parent.Controls.Cast<Control>().SelectMany(child =>
            new[] { child }.Concat(Descendants(child)));

    private static string Value(DataRow? row, string field)
    {
        if (row is null || row.RowState == DataRowState.Deleted ||
            !row.Table.Columns.Contains(field) || row[field] is DBNull)
            return "";
        object raw = row[field];
        return raw is DateTime dt
            ? dt.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
            : raw is bool flag ? flag ? "Yes" : "No"
            : Convert.ToString(raw, CultureInfo.InvariantCulture) ?? "";
    }

    private static bool Yes(DataRow row, string field)
    {
        string value = Value(row, field);
        return value.Equals("Yes", StringComparison.OrdinalIgnoreCase) ||
            value.Equals("True", StringComparison.OrdinalIgnoreCase) || value == "1" ||
            value.Equals("-1", StringComparison.OrdinalIgnoreCase);
    }

    private static DateTime? Date(DataRow row, string field)
    {
        if (row.Table.Columns.Contains(field) && row[field] is DateTime d)
            return d;
        return DateTime.TryParse(Value(row, field), CultureInfo.InvariantCulture,
            DateTimeStyles.None, out var parsed) ? parsed : null;
    }

    private static string ContractStatus(DataRow row)
    {
        DateTime today = DateTime.Today;
        DateTime? begins = Date(row, "StartDate");
        DateTime? ends = Date(row, "ExpirationDate");
        return ends.HasValue && ends.Value.Date < today ? "Expired"
            : begins.HasValue && begins.Value.Date > today ? "Upcoming"
            : "Active";
    }

    private static string PrimaryRole(DataRow row)
    {
        foreach (var (field, label) in new[]
        {
            ("RoleWrestler", "Wrestler"),
            ("RolePartTimeWrestler", "Occasional Wrestler"),
            ("RoleManager", "Manager"),
            ("RoleOnScreenPersonality", "On-Screen Personality"),
            ("RolePlayByPlay", "Play By Play Commentator"),
            ("RoleColour", "Colour Commentator"),
            ("RoleReferee", "Referee"),
            ("RoleProducer", "Road Agent")
        })
            if (Yes(row, field)) return label;
        return "Unassigned";
    }

    private string Employer(DataRow row)
    {
        var company = _companies.FirstOrDefault(c =>
            Convert.ToString(c.Uid, CultureInfo.InvariantCulture) ==
            Value(row, "FedUID"));
        return company is not null
            ? (string.IsNullOrWhiteSpace(company.Initials)
                ? company.Name : company.Initials + "   " + company.Name)
            : string.IsNullOrWhiteSpace(Value(row, "EmployerInitials"))
                ? "Employer #" + Value(row, "FedUID")
                : Value(row, "EmployerInitials");
    }

    private Image? CompanyLogo(DataRow row)
    {
        var company = _companies.FirstOrDefault(c =>
            Convert.ToString(c.Uid, CultureInfo.InvariantCulture) ==
            Value(row, "FedUID"));
        string safe = Path.GetFileName(company?.Logo ?? "");
        if (string.IsNullOrWhiteSpace(safe)) return null;
        if (_logoCache.TryGetValue(safe, out var cached)) return cached;
        foreach (string path in new[]
        {
            Path.Combine(_paths.Pictures, "Default", "Logos", safe),
            Path.Combine(_paths.Pictures, "Default", "Companies", safe),
            Path.Combine(_paths.Pictures, "Logos", safe),
            Path.Combine(_paths.Pictures, "Companies", safe),
            Path.Combine(_paths.Pictures, safe)
        })
        {
            if (!File.Exists(path)) continue;
            try
            {
                using var loaded = Image.FromFile(path);
                var logo = new Bitmap(loaded);
                _logoCache[safe] = logo;
                return logo;
            }
            catch (Exception ex) when (ex is IOException or ArgumentException
                                       or OutOfMemoryException) { }
        }
        return null;
    }

    private void PaintEmployerCell(object? sender,
        DataGridViewCellPaintingEventArgs e)
    {
        if (e.RowIndex < 0 || e.ColumnIndex != 0 ||
            e.RowIndex >= _grid.Rows.Count) return;
        if (_grid.Rows[e.RowIndex].Tag is not DataRow contract) return;
        var graphics = e.Graphics;
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        bool selected = (e.State & DataGridViewElementStates.Selected) != 0;
        using var background = new SolidBrush(selected
            ? Color.FromArgb(4, 129, 180)
            : e.RowIndex % 2 == 0 ? Ink : Color.FromArgb(7, 31, 55));
        graphics.FillRectangle(background, e.CellBounds);
        int boxX = e.CellBounds.X + 13;
        int boxY = e.CellBounds.Y + 6;
        var image = CompanyLogo(contract);
        if (image is not null)
            graphics.DrawImage(image, new Rectangle(boxX, boxY, 77, 38));
        else
        {
            using var tile = new LinearGradientBrush(
                new Rectangle(boxX, boxY, 77, 38),
                Color.FromArgb(30, 111, 168), Ink,
                LinearGradientMode.Vertical);
            graphics.FillRectangle(tile, new Rectangle(boxX, boxY, 77, 38));
            using var border = new Pen(Color.FromArgb(99, Cyan));
            graphics.DrawRectangle(border, boxX, boxY, 77, 38);
            string initials = Value(contract, "EmployerInitials");
            if (initials.Length == 0) initials = "PWR";
            using var logoFont = new Font("Segoe UI", 12, FontStyle.Bold | FontStyle.Italic);
            TextRenderer.DrawText(graphics, initials, logoFont,
                new Rectangle(boxX + 2, boxY + 1, 73, 35),
                White, TextFormatFlags.HorizontalCenter |
                TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        }
        using var font = new Font("Segoe UI", 10.5f, FontStyle.Bold);
        string employer = Employer(contract);
        TextRenderer.DrawText(graphics, employer, font,
            new Rectangle(boxX + 89, e.CellBounds.Y,
                Math.Max(20, e.CellBounds.Width - 108), e.CellBounds.Height),
            White, TextFormatFlags.VerticalCenter |
            TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding);
        using var underline = new Pen(Color.FromArgb(29, 82, 116));
        graphics.DrawLine(underline, e.CellBounds.Left,
            e.CellBounds.Bottom - 1, e.CellBounds.Right, e.CellBounds.Bottom - 1);
        e.Handled = true;
    }

    private static void HeaderIcon(Graphics g, int x, int y, string kind)
    {
        using var pen = new Pen(Color.FromArgb(225, 239, 252), 1.9f)
        {
            StartCap = LineCap.Round, EndCap = LineCap.Round,
            LineJoin = LineJoin.Round
        };
        if (kind == "Roles" || kind == "Roster Details")
        {
            g.DrawEllipse(pen, x + 9, y + 2, 10, 10);
            g.DrawEllipse(pen, x + 2, y + 14, 24, 17);
            g.DrawEllipse(pen, x + 1, y + 7, 7, 7);
            g.DrawEllipse(pen, x + 22, y + 7, 7, 7);
        }
        else if (kind.StartsWith("Terms", StringComparison.Ordinal))
        {
            g.DrawEllipse(pen, x + 3, y + 2, 24, 9);
            g.DrawArc(pen, x + 3, y + 7, 24, 9, 0, 180);
            g.DrawArc(pen, x + 3, y + 13, 24, 9, 0, 180);
            g.DrawArc(pen, x + 3, y + 19, 24, 9, 0, 180);
        }
        else
        {
            g.DrawRectangle(pen, x + 6, y + 1, 20, 30);
            g.DrawLine(pen, x + 10, y + 10, x + 22, y + 10);
            g.DrawLine(pen, x + 10, y + 16, x + 22, y + 16);
            g.DrawLine(pen, x + 10, y + 22, x + 20, y + 22);
        }
    }

    private void ClearView()
    {
        _view.SuspendLayout();
        foreach (Control previous in _view.Controls.Cast<Control>().ToArray())
        {
            _view.Controls.Remove(previous);
            previous.Dispose();
        }
        _view.ResumeLayout(false);
    }

    private static Label Label(string text, float size = 10.3f,
        bool bold = false, Color? color = null)
    {
        return new Label
        {
            Text = text, AutoEllipsis = true, BackColor = Color.Transparent,
            ForeColor = color ?? White,
            Font = new Font("Segoe UI", size,
                bold ? FontStyle.Bold : FontStyle.Regular),
            TextAlign = ContentAlignment.MiddleLeft
        };
    }

    private static Panel Frame(string name, bool bright = false)
    {
        var panel = new Panel
        {
            Name = name, Dock = DockStyle.Fill,
            BackColor = Ink, Margin = new Padding(4),
            Padding = new Padding(2)
        };
        panel.Paint += (_, e) =>
        {
            if (panel.Width < 4 || panel.Height < 4) return;
            var graphics = e.Graphics;
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using var pen = new Pen(bright ? Cyan : Border, bright ? 1.7f : 1.1f);
            using var path = Rounded(new Rectangle(1, 1,
                panel.Width - 3, panel.Height - 3), 10);
            graphics.DrawPath(pen, path);
        };
        return panel;
    }

    private static GraphicsPath Rounded(Rectangle rect, int radius)
    {
        int d = Math.Max(2, radius * 2);
        var path = new GraphicsPath();
        path.AddArc(rect.Left, rect.Top, d, d, 180, 90);
        path.AddArc(rect.Right - d, rect.Top, d, d, 270, 90);
        path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
        path.AddArc(rect.Left, rect.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }

    private sealed class ContractGrid : DataGridView
    {
        internal Image? Artwork { get; set; }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            base.OnPaintBackground(e);
            if (Artwork is null || Width < 10 || Height < 10) return;
            // The prepared bitmap is exactly the grid size. Avoid GDI+
            // interpolation on every repaint, which delayed Worker pages.
            if (Artwork.Size == ClientSize)
                e.Graphics.DrawImageUnscaled(Artwork, 0, 0);
            else
                e.Graphics.DrawImage(Artwork, ClientRectangle);
            using var tint = new SolidBrush(Color.FromArgb(164, 3, 17, 34));
            e.Graphics.FillRectangle(tint, ClientRectangle);
        }
    }

    private async Task RestoreListArtworkAsync(ContractGrid grid)
    {
        try
        {
            string skin = _paths.ReadSetting("Skin", "Obsidian");
            string current = Path.Combine(_paths.SkinsRoot, skin,
                "Backgrounds", "Worker_Editor.jpg");
            string fallback = Path.Combine(_paths.SkinsRoot, "Obsidian",
                "Backgrounds", "Worker_Editor.jpg");
            string imageFile = File.Exists(current) ? current : fallback;
            if (!File.Exists(imageFile) || grid.IsDisposed) return;
            Size target = grid.ClientSize;
            if (target.Width < 10 || target.Height < 10) return;
            var prepared = await WorkerBackgroundCache.GetPreparedAsync(
                imageFile, target);
            if (!grid.IsDisposed && grid.ClientSize == target && prepared is not null)
            {
                grid.Artwork = prepared;
                grid.Invalidate();
            }
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException) { }
    }

    private sealed class ContractButton : Button
    {
        private readonly bool _primary;
        private bool _hovered;
        internal ContractButton(string text, string name, bool primary = false)
        {
            _primary = primary;
            Text = text;
            Name = name;
            Cursor = Cursors.Hand;
            TabStop = true;
            FlatStyle = FlatStyle.Flat;
            BackColor = Navy;
            FlatAppearance.BorderSize = 0;
            ForeColor = White;
            Font = new Font("Segoe UI", 11f, FontStyle.Bold);
            SetStyle(ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint, true);
        }
        protected override void OnMouseEnter(EventArgs e) { base.OnMouseEnter(e); _hovered = true; Invalidate(); }
        protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); _hovered = false; Invalidate(); }
        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            var rect = new Rectangle(1, 1, Width - 3, Height - 3);
            if (rect.Width < 5 || rect.Height < 5) return;
            using var path = Rounded(rect, 7);
            using var fill = new LinearGradientBrush(rect,
                _primary ? Color.FromArgb(10, 186, 241) :
                    _hovered ? Color.FromArgb(39, 103, 155) : Medium,
                _primary ? Color.FromArgb(2, 101, 165) : Ink,
                LinearGradientMode.Vertical);
            g.FillPath(fill, path);
            using var border = new Pen(_primary ? Cyan : Border,
                _primary ? 2f : 1.4f);
            g.DrawPath(border, path);
            TextRenderer.DrawText(g, Text, Font, ClientRectangle,
                Enabled ? White : Muted, TextFormatFlags.HorizontalCenter |
                TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding |
                TextFormatFlags.EndEllipsis);
        }
    }

    private static ContractButton Button(string label, string name, bool primary = false)
        => new(label, name, primary) { Height = 45 };

    private void BuildList()
    {
        _editing = null;
        _invalidFields.Clear();
        ClearView();
        _grid = new ContractGrid();
        _search = new TextBox();
        _filter = new ComboBox();
        _listStatus = new Label();
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3,
            Padding = new Padding(18, 12, 18, 12),
            BackColor = Color.Transparent
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 126));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 78));
        _view.Controls.Add(layout);

        var header = Frame("ContractListHeader", bright: true);
        header.Margin = new Padding(2, 0, 2, 8);
        header.Paint += (_, e) =>
        {
            using var brush = new LinearGradientBrush(
                new Rectangle(0, 2, Math.Max(1, header.Width), 67),
                Color.FromArgb(38, 124, 186),
                Color.FromArgb(6, 32, 61), LinearGradientMode.Horizontal);
            e.Graphics.FillRectangle(brush, 4, 4,
                Math.Max(1, header.Width - 9), 60);
            HeaderIcon(e.Graphics, 25, 12, "Worker Contracts");
            using var separator = new Pen(Cyan, 2.1f);
            e.Graphics.DrawLine(separator, 92, 14, 92, 55);
            using var rule = new Pen(Cyan, 1f);
            e.Graphics.DrawLine(rule, 4, 66, header.Width - 4, 66);
        };
        var heading = Label("Worker Contracts", 21, true);
        heading.Bounds = new Rectangle(110, 6, 520, 54);
        var subtitle = Label("Select a contract to edit or create a new one.",
            12, false, White);
        subtitle.Font = new Font("Segoe UI", 12f, FontStyle.Italic);
        subtitle.Bounds = new Rectangle(27, 73, 520, 36);
        header.Controls.AddRange([heading, subtitle]);
        _search.Name = "ContractSearch";
        _search.Text = "";
        _search.PlaceholderText = "Search contracts...";
        _search.Font = new Font("Segoe UI", 11);
        _search.BackColor = Navy;
        _search.ForeColor = White;
        _search.BorderStyle = BorderStyle.FixedSingle;
        _search.TextChanged += (_, _) => RefreshRows();
        header.Controls.Add(_search);
        _filter.Name = "ContractFilter";
        _filter.DropDownStyle = ComboBoxStyle.DropDownList;
        _filter.FlatStyle = FlatStyle.Flat;
        _filter.BackColor = Medium;
        _filter.ForeColor = White;
        _filter.Font = new Font("Segoe UI", 10.5f, FontStyle.Bold);
        _filter.Items.AddRange(["All Contracts", "Active", "Expired", "Upcoming"]);
        _filter.SelectedIndex = 0;
        _filter.SelectedIndexChanged += (_, _) => RefreshRows();
        header.Controls.Add(_filter);
        header.Resize += (_, _) =>
        {
            int right = header.ClientSize.Width - 23;
            _filter.Bounds = new Rectangle(Math.Max(580, right - 207), 75, 204, 32);
            _search.Bounds = new Rectangle(
                Math.Max(350, _filter.Left - 305), 75, 292, 32);
        };
        layout.Controls.Add(header, 0, 0);

        var table = Frame("ContractListTable", bright: true);
        table.Margin = new Padding(2, 0, 2, 8);
        _grid.Name = "WorkerContractGrid";
        _grid.Dock = DockStyle.Fill;
        _grid.Margin = Padding.Empty;
        _grid.BorderStyle = BorderStyle.None;
        _grid.BackgroundColor = Color.FromArgb(5, 25, 47);
        _grid.GridColor = Color.FromArgb(35, 89, 126);
        _grid.EnableHeadersVisualStyles = false;
        _grid.ColumnHeadersHeight = 43;
        _grid.ColumnHeadersHeightSizeMode =
            DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
        _grid.RowTemplate.Height = 51;
        _grid.RowHeadersVisible = false;
        _grid.AllowUserToAddRows = false;
        _grid.AllowUserToDeleteRows = false;
        _grid.AllowUserToResizeRows = false;
        _grid.AllowUserToResizeColumns = false;
        _grid.AutoGenerateColumns = false;
        _grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        _grid.MultiSelect = false;
        _grid.ReadOnly = true;
        _grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        _grid.ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle
        {
            BackColor = Color.FromArgb(21, 70, 112),
            ForeColor = White,
            Font = new Font("Segoe UI", 11.2f, FontStyle.Bold),
            SelectionBackColor = Medium,
            Alignment = DataGridViewContentAlignment.MiddleLeft,
            Padding = new Padding(12, 0, 4, 0)
        };
        _grid.DefaultCellStyle = new DataGridViewCellStyle
        {
            BackColor = Ink,
            ForeColor = White,
            SelectionBackColor = Color.FromArgb(7, 113, 163),
            SelectionForeColor = White,
            Font = new Font("Segoe UI", 11f),
            Padding = new Padding(12, 0, 6, 0),
            Alignment = DataGridViewContentAlignment.MiddleLeft
        };
        _grid.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(7, 31, 55);
        foreach (var (title, name, weight) in new[]
        {
            ("Employer", "Employer", 22f),
            ("Contract Type", "ContractType", 17f),
            ("Status", "Status", 12f),
            ("Begins", "Begins", 14f),
            ("Expires", "Expires", 14f),
            ("Primary Role", "PrimaryRole", 21f)
        })
        {
            _grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = name, HeaderText = title,
                FillWeight = weight, SortMode = DataGridViewColumnSortMode.Automatic
            });
        }
        _grid.CellPainting += PaintEmployerCell;
        var artworkGrid = _grid;
        artworkGrid.HandleCreated += (_, _) =>
            _ = RestoreListArtworkAsync(artworkGrid);
        artworkGrid.SizeChanged += (_, _) =>
        {
            if (artworkGrid.IsHandleCreated &&
                artworkGrid.Artwork?.Size != artworkGrid.ClientSize)
                _ = RestoreListArtworkAsync(artworkGrid);
        };
        _grid.CellDoubleClick += (_, e) =>
        {
            if (e.RowIndex >= 0) OpenSelected();
        };
        _grid.KeyDown += (_, e) =>
        {
            if (e.KeyCode != Keys.Enter) return;
            e.Handled = true;
            e.SuppressKeyPress = true;
            OpenSelected();
        };
        table.Controls.Add(_grid);
        layout.Controls.Add(table, 0, 1);

        var footer = Frame("ContractListFooter", bright: true);
        footer.Margin = new Padding(2, 0, 2, 0);
        _listStatus.Name = "ContractListStatus";
        _listStatus.ForeColor = White;
        _listStatus.BackColor = Color.Transparent;
        _listStatus.Font = new Font("Segoe UI", 11.3f, FontStyle.Bold);
        _listStatus.TextAlign = ContentAlignment.MiddleLeft;
        footer.Controls.Add(_listStatus);
        var remove = Button("Remove", "ContractRemove");
        var open = Button("Open", "ContractOpen");
        var add = Button("+   Add Contract", "ContractAdd", true);
        footer.Controls.AddRange([remove, open, add]);
        add.Enabled = _databaseFile is not null;
        remove.Click += (_, _) => RemoveSelected();
        open.Click += (_, _) => OpenSelected();
        add.Click += (_, _) => AddContract();
        footer.Resize += (_, _) =>
        {
            int right = footer.ClientSize.Width - 17;
            add.Bounds = new Rectangle(right - 206, 12, 202, 45);
            open.Bounds = new Rectangle(add.Left - 177, 12, 167, 45);
            remove.Bounds = new Rectangle(open.Left - 177, 12, 167, 45);
            _listStatus.Bounds = new Rectangle(19, 13,
                Math.Max(200, remove.Left - 31), 42);
        };
        layout.Controls.Add(footer, 0, 2);
        RefreshRows();
    }

    private DataRow? Selected()
    {
        if (_grid.SelectedRows.Count == 0) return null;
        return _grid.SelectedRows[0].Tag as DataRow;
    }

    private void RefreshRows()
    {
        if (_grid.IsDisposed || _grid.Columns.Count == 0) return;
        DataRow? previous = Selected();
        var visible = _contracts.Rows.Cast<DataRow>()
            .Where(row => row.RowState != DataRowState.Deleted)
            .Where(row =>
            {
                string query = _search.Text.Trim();
                return query.Length == 0 ||
                    (Employer(row) + " " + Value(row, "ContractType") + " " +
                     Value(row, "Name") + " " + PrimaryRole(row))
                    .Contains(query, StringComparison.OrdinalIgnoreCase);
            })
            .Where(row => _filter.SelectedIndex <= 0 ||
                ContractStatus(row).Equals(Convert.ToString(_filter.SelectedItem),
                    StringComparison.OrdinalIgnoreCase)).ToArray();
        _grid.SuspendLayout();
        _grid.Rows.Clear();
        foreach (var row in visible)
        {
            string status = ContractStatus(row);
            int index = _grid.Rows.Add(Employer(row),
                Value(row, "ContractType"),
                (status == "Active" ? "●  " : "○  ") + status,
                Value(row, "StartDate"), Value(row, "ExpirationDate"),
                PrimaryRole(row));
            _grid.Rows[index].Tag = row;
            _grid.Rows[index].Cells[2].Style.ForeColor = status == "Active"
                ? Color.FromArgb(51, 234, 134)
                : status == "Expired" ? Color.FromArgb(209, 211, 218) : Muted;
            if (ReferenceEquals(previous, row))
                _grid.Rows[index].Selected = true;
        }
        if (previous is null && _grid.Rows.Count > 0)
            _grid.Rows[0].Selected = true;
        _grid.ResumeLayout(true);
        int total = _contracts.Rows.Cast<DataRow>().Count(row =>
            row.RowState != DataRowState.Deleted);
        int active = _contracts.Rows.Cast<DataRow>().Count(row =>
            row.RowState != DataRowState.Deleted && ContractStatus(row) == "Active");
        int expired = _contracts.Rows.Cast<DataRow>().Count(row =>
            row.RowState != DataRowState.Deleted && ContractStatus(row) == "Expired");
        _listStatus.Text = total + " contracts  (" + active +
            " active, " + expired + " expired)";
    }

    private void OpenSelected()
    {
        DataRow? selected = Selected();
        if (selected is null) return;
        BuildEditor(selected);
        _onContext("Contracts.Edit");
    }

    private void AddContract()
    {
        if (_databaseFile is null) return;
        var row = _contracts.NewRow();
        foreach (DataColumn column in _contracts.Columns)
        {
            if (column.ColumnName == "UID")
            {
                row[column] = Convert.ChangeType(_tempUid--, column.DataType,
                    CultureInfo.InvariantCulture);
            }
            else if (column.ColumnName == "WorkerUID")
            {
                row[column] = Convert.ChangeType(_profile.Identity.Uid,
                    column.DataType, CultureInfo.InvariantCulture);
            }
            else if (column.ColumnName is "Name" or "ShortName")
            {
                row[column] = column.ColumnName == "Name"
                    ? _profile.Identity.Name : _profile.Identity.ShortName;
            }
            else if (!column.AllowDBNull && row[column] is DBNull)
            {
                row[column] = column.DataType == typeof(string) ? ""
                    : column.DataType == typeof(bool) ? false
                    : column.DataType == typeof(DateTime) ? DateTime.Today
                    : column.DataType.IsValueType
                        ? Activator.CreateInstance(column.DataType)!
                        : DBNull.Value;
            }
        }
        _contracts.Rows.Add(row);
        _newDraftInitial = _contracts.Columns.Cast<DataColumn>()
            .ToDictionary(column => column.ColumnName,
                column => row[column], StringComparer.OrdinalIgnoreCase);
        BuildEditor(row);
        _onContext("Contracts.Edit");
    }

    private void RemoveSelected()
    {
        DataRow? row = Selected();
        if (row is null || _databaseFile is null) return;
        if (MessageBox.Show(FindForm(),
                "Remove this contract from the selected worker? This also removes its contract biography.",
                "Remove Contract", MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2)
            != DialogResult.Yes) return;
        try
        {
            string id = Value(row, "UID");
            foreach (DataRow bio in _bios?.Rows.Cast<DataRow>().ToArray() ?? [])
                if (bio.RowState != DataRowState.Deleted &&
                    Value(bio, "ContractUID") == id)
                    bio.Delete();
            row.Delete();
            PrwContractEditorService.Save(_databaseFile, _profile, _password);
            _onSaved();
            BuildList();
        }
        catch (Exception ex)
        {
            _listStatus.Text = "Remove failed: " + ex.Message;
        }
    }

    private void BuildEditor(DataRow row)
    {
        _editing = row;
        _invalidFields.Clear();
        ClearView();
        _editorStatus = new Label();
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3,
            Padding = new Padding(13, 10, 13, 12),
            BackColor = Color.Transparent
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 148));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 100));
        _view.Controls.Add(layout);

        var identity = Frame("ContractIdentity", bright: true);
        identity.Margin = new Padding(2, 0, 2, 9);
        BuildIdentity(identity, row);
        layout.Controls.Add(identity, 0, 0);

        var cards = new TableLayoutPanel
        {
            Name = "ContractFourCards", Dock = DockStyle.Fill,
            ColumnCount = 4, RowCount = 1,
            Margin = Padding.Empty, BackColor = Color.Transparent
        };
        for (int i = 0; i < 4; i++)
            cards.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
        cards.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        cards.Controls.Add(Card("Basic Details", "ContractBasicDetails", row, new[]
        {
            ("Contract Type", "ContractType", "choice"),
            ("Exclusive", "Exclusive", "choice"),
            ("Touring", "Touring", "choice"),
            ("Pay", "PayBasis", "choice"),
            ("On Loan", "OnLoan", "choice"),
            ("In Developmental", "InDevelopmental", "choice"),
            ("Iron Clad", "IronClad", "choice"),
            ("Wage", "Wage", "number"),
            ("Downside", "Downside", "number"),
            ("Contract Begin", "StartDate", "date"),
            ("Expires In", "TermCode", "choice"),
            ("Contract Expires", "ExpirationDate", "date"),
            ("Appearances Left", "AppearanceLimit", "number")
        }), 0, 0);
        cards.Controls.Add(Card("Roster Details", "ContractRosterDetails", row, new[]
        {
            ("Alignment", "Alignment", "choice"),
            ("Last Turn", "", "unsupported"),
            ("Competes In", "CompetesIn", "choice"),
            ("Brand", "Brands", "choice"),
            ("Division", "Divisions", "choice"),
            ("Manager", "ManagerWorkerUID", "manager"),
            ("Mask", "MaskRespect", "choice"),
            ("Move Set", "MoveSet", "choice"),
            ("Momentum", "Momentum", "choice"),
            ("Gimmick", "GimmickName", "text"),
            ("Gimmick Basis", "GimmickBasis", "choice"),
            ("Gimmick Rating", "GimmickRating", "number"),
            ("Gimmick Status", "CompanyPush", "choice")
        }), 1, 0);
        cards.Controls.Add(Card("Terms, Perks & Bonuses", "ContractTerms", row, new[]
        {
            ("Creative Control", "", "unsupported"),
            ("Hiring Veto", "", "unsupported"),
            ("Merchandise Cut", "", "unsupported"),
            ("Wage Matching", "", "unsupported"),
            ("Roster Usage", "RosterUsage", "choice"),
            ("Intended Role", "IntendedRole", "choice"),
            ("Shows Control", "", "unsupported"),
            ("Bonus Type", "", "unsupported"),
            ("Bonus Amount", "", "unsupported"),
            ("Travel Cover", "", "unsupported")
        }), 2, 0);
        cards.Controls.Add(BuildRoles(row), 3, 0);
        layout.Controls.Add(cards, 0, 1);

        var footer = Frame("ContractEditorFooter", bright: true);
        footer.Margin = new Padding(2, 10, 2, 0);
        BuildFooter(footer, row);
        layout.Controls.Add(footer, 0, 2);
    }

    private void BuildIdentity(Panel panel, DataRow row)
    {
        var image = new PictureBox
        {
            Name = "ContractWorkerPicture",
            Bounds = new Rectangle(13, 9, 99, 113),
            SizeMode = PictureBoxSizeMode.Zoom,
            BorderStyle = BorderStyle.FixedSingle,
            BackColor = Navy
        };
        panel.Controls.Add(image);
        string picture = Value(row, "Picture");
        if (string.IsNullOrWhiteSpace(picture))
            picture = Value(_profile.Sections.FirstOrDefault(s =>
                s.Table == "tblWorker")?.Records.Rows.Cast<DataRow>().FirstOrDefault(),
                "Picture");
        string safe = Path.GetFileName(picture);
        if (safe.Length != 0)
        {
            var files = new[]
            {
                Path.Combine(_paths.Pictures, "Default", "Workers", safe),
                Path.Combine(_paths.Pictures, "Workers", safe),
                Path.Combine(_paths.Pictures, safe)
            };
            string? file = files.FirstOrDefault(File.Exists);
            if (file is not null)
                try
                {
                    using var loaded = Image.FromFile(file);
                    image.Image = new Bitmap(loaded);
                    image.Disposed += (_, _) => image.Image?.Dispose();
                }
                catch (Exception ex) when (ex is IOException or ArgumentException
                                           or OutOfMemoryException) { }
        }
        if (image.Image is null)
        {
            var noImage = Label("NO IMAGE", 8, true, Muted);
            noImage.Dock = DockStyle.Fill;
            noImage.TextAlign = ContentAlignment.MiddleCenter;
            image.Controls.Add(noImage);
        }

        var name = Label("Employee", 10, true);
        var employer = Label("Employer", 10, true);
        var longName = Label("Name", 10, true);
        var shortName = Label("Short Name", 10, true);
        var biography = Label("Biography", 10, true);
        panel.Controls.AddRange([name, employer, longName, shortName, biography]);

        var employee = new ComboBox
        {
            Name = "ContractEmployee", DropDownStyle = ComboBoxStyle.DropDownList,
            BackColor = Medium, ForeColor = White, FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 10, FontStyle.Bold),
            Enabled = false
        };
        employee.Items.Add(_profile.Identity.Name);
        employee.SelectedIndex = 0;
        panel.Controls.Add(employee);
        var employers = new ComboBox
        {
            Name = "ContractEmployer", DropDownStyle = ComboBoxStyle.DropDownList,
            BackColor = Medium, ForeColor = White, FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 10, FontStyle.Bold),
            Enabled = _databaseFile is not null
        };
        foreach (var company in _companies) employers.Items.Add(company);
        var found = _companies.FindIndex(c =>
            Convert.ToString(c.Uid, CultureInfo.InvariantCulture) ==
                Value(row, "FedUID"));
        if (found >= 0) employers.SelectedIndex = found;
        else if (row["FedUID"] is not DBNull)
        {
            employers.Items.Insert(0, "Employer #" + Value(row, "FedUID") +
                " (" + Value(row, "EmployerInitials") + ")");
            employers.SelectedIndex = 0;
        }
        employers.SelectedIndexChanged += (_, _) =>
        {
            if (employers.SelectedItem is not PrwContractEditorService.Company company)
                return;
            Set(row, "FedUID", company.Uid);
            Set(row, "EmployerInitials", company.Initials);
        };
        panel.Controls.Add(employers);

        var names = Input(row, "Name", "text");
        var shortNames = Input(row, "ShortName", "text");
        panel.Controls.Add(names);
        panel.Controls.Add(shortNames);
        var bio = new RichTextBox
        {
            Name = "ContractBiography", BorderStyle = BorderStyle.FixedSingle,
            BackColor = Color.FromArgb(8, 28, 48), ForeColor = White,
            Font = new Font("Segoe UI", 9.2f),
            MaxLength = _bios?.Columns.Contains("Biography") == true &&
                _bios.Columns["Biography"]!.MaxLength > 0
                ? _bios.Columns["Biography"]!.MaxLength : 32767,
            Text = Biography(row), ScrollBars = RichTextBoxScrollBars.Vertical,
            Enabled = _databaseFile is not null
        };
        bio.TextChanged += (_, _) => SetBiography(row, bio.Text);
        panel.Controls.Add(bio);
        panel.Resize += (_, _) =>
        {
            int width = panel.ClientSize.Width;
            int column1 = Math.Clamp((int)(width * .23), 230, 323);
            int column2 = Math.Clamp((int)(width * .23), 260, 310);
            name.Bounds = new Rectangle(129, 15, 80, 27);
            employee.Bounds = new Rectangle(205, 15, column1 - 80, 27);
            longName.Bounds = new Rectangle(129, 53, 70, 27);
            names.Bounds = new Rectangle(205, 53, column1 - 80, 27);
            shortName.Bounds = new Rectangle(129, 91, 83, 27);
            shortNames.Bounds = new Rectangle(205, 91, column1 - 80, 27);
            int ex = 130 + column1 + 8;
            employer.Bounds = new Rectangle(ex, 15, 78, 27);
            employers.Bounds = new Rectangle(ex + 82, 15, column2 - 80, 27);
            int bx = ex + column2 + 8;
            biography.Bounds = new Rectangle(bx, 14, 82, 27);
            bio.Bounds = new Rectangle(bx + 90, 12,
                Math.Max(100, width - bx - 104), 109);
        };
    }

    private Panel Card(string heading, string name, DataRow row,
        (string Label, string Field, string Kind)[] fields)
    {
        var frame = Frame(name, bright: true);
        frame.Margin = new Padding(5, 0, 5, 0);
        var header = new Panel
        {
            Dock = DockStyle.Top, Height = 53, BackColor = Medium
        };
        header.Paint += (_, e) =>
        {
            using var brush = new LinearGradientBrush(
                new Rectangle(0, 0, Math.Max(1, header.Width), header.Height),
                Color.FromArgb(29, 110, 174),
                Color.FromArgb(8, 38, 69), LinearGradientMode.Horizontal);
            e.Graphics.FillRectangle(brush, header.ClientRectangle);
            using var line = new Pen(Cyan, 1);
            e.Graphics.DrawLine(line, 0, header.Height - 1,
                header.Width, header.Height - 1);
            HeaderIcon(e.Graphics, 12, 10, heading);
            using var separator = new Pen(Cyan, 1.8f);
            e.Graphics.DrawLine(separator, 46, 10, 46, 42);
        };
        var title = Label(heading, 13.5f, true);
        title.Dock = DockStyle.Fill;
        title.Padding = new Padding(54, 0, 0, 0);
        header.Controls.Add(title);
        var body = new Panel
        {
            Name = name + "Fields", Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(6, 23, 43),
            AutoScroll = true,
            Padding = new Padding(11, 10, 8, 9)
        };
        var slots = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2,
            BackColor = Color.Transparent,
            Padding = Padding.Empty, Margin = Padding.Empty
        };
        slots.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        slots.RowStyles.Add(new RowStyle(SizeType.Absolute, 53));
        slots.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        slots.Controls.Add(header, 0, 0);
        slots.Controls.Add(body, 0, 1);
        frame.Controls.Add(slots);
        void Arrange()
        {
            int inner = Math.Max(100, body.ClientSize.Width - 27);
            int gap = 7;
            int textWidth = Math.Clamp((int)(inner * .53), 126, 172);
            int y = 10;
            foreach (var field in fields)
            {
                var caption = Label(field.Label, 10, true);
                caption.Bounds = new Rectangle(11, y, textWidth - gap, 25);
                body.Controls.Add(caption);
                Control input = field.Kind == "unsupported"
                    ? Unavailable(field.Label)
                    : Input(row, field.Field, field.Kind);
                input.Bounds = new Rectangle(textWidth + 11, y,
                    Math.Max(100, inner - textWidth), 25);
                body.Controls.Add(input);
                y += 27;
            }
            body.AutoScrollMinSize = new Size(0, y + 3);
        }
        // Build once, then let panel scrolling preserve field position.
        body.HandleCreated += (_, _) => { if (body.Controls.Count == 0) Arrange(); };
        if (body.IsHandleCreated) Arrange();
        return frame;
    }

    private Control Unavailable(string label)
    {
        var box = new TextBox
        {
            Name = "ContractUnavailable_" + label.Replace(" ", ""),
            ReadOnly = true, BorderStyle = BorderStyle.FixedSingle,
            BackColor = Color.FromArgb(11, 32, 51), ForeColor = Muted,
            Font = new Font("Segoe UI", 9),
            Text = "Not in MDB", TabStop = false
        };
        _tips.SetToolTip(box, label + " has no field in the current native PWR " +
            "contract table. It cannot be saved until the new database defines it.");
        return box;
    }

    private Control Input(DataRow row, string field, string kind)
    {
        if (!row.Table.Columns.Contains(field))
            return Unavailable(field);
        var column = row.Table.Columns[field]!;
        if (column.DataType == typeof(DateTime) || kind == "date" &&
            column.DataType == typeof(DateTime))
        {
            var date = new DateTimePicker
            {
                Name = "ContractField_" + field, Format = DateTimePickerFormat.Custom,
                CustomFormat = "yyyy-MM-dd", ShowCheckBox = column.AllowDBNull,
                Value = Date(row, field) ?? DateTime.Today,
                Checked = Date(row, field).HasValue,
                Font = new Font("Segoe UI", 9.3f),
                CalendarForeColor = White,
                Enabled = _databaseFile is not null
            };
            date.ValueChanged += (_, _) =>
                Set(row, field, date.Checked ? date.Value.Date : DBNull.Value);
            return date;
        }

        if (kind == "manager")
        {
            var select = new ComboBox
            {
                Name = "ContractField_" + field,
                DropDownStyle = ComboBoxStyle.DropDownList,
                BackColor = Medium, ForeColor = White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9.3f),
                Enabled = _databaseFile is not null
            };
            select.Items.Add("(None)");
            foreach (var worker in _workers) select.Items.Add(worker);
            int index = _workers.FindIndex(worker =>
                Convert.ToString(worker.Uid, CultureInfo.InvariantCulture) ==
                Value(row, field));
            select.SelectedIndex = index < 0 ? 0 : index + 1;
            select.SelectedIndexChanged += (_, _) =>
            {
                object value = select.SelectedItem is PrwContractEditorService.WorkerChoice person
                    ? person.Uid
                    : column.AllowDBNull ? DBNull.Value
                    : Convert.ChangeType(0, column.DataType, CultureInfo.InvariantCulture);
                Set(row, field, value);
            };
            return select;
        }

        if (kind == "choice")
        {
            var combo = new ComboBox
            {
                Name = "ContractField_" + field,
                DropDownStyle = column.DataType == typeof(bool)
                    ? ComboBoxStyle.DropDownList : ComboBoxStyle.DropDown,
                BackColor = Medium, ForeColor = White, FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9.4f),
                Enabled = _databaseFile is not null
            };
            if (column.DataType == typeof(bool))
                combo.Items.AddRange(["No", "Yes"]);
            else
            {
                combo.Items.Add("");
                if (_choices.TryGetValue(field, out var values))
                    foreach (string choice in values) combo.Items.Add(choice);
            }
            combo.Text = Value(row, field);
            combo.TextChanged += (_, _) =>
                ApplyText(row, field, combo.Text);
            return combo;
        }

        var text = new TextBox
        {
            Name = "ContractField_" + field,
            Text = Value(row, field),
            BorderStyle = BorderStyle.FixedSingle,
            BackColor = Color.FromArgb(9, 33, 58),
            ForeColor = White,
            Font = new Font("Segoe UI", 9.7f),
            Enabled = _databaseFile is not null
        };
        text.TextChanged += (_, _) => ApplyText(row, field, text.Text);
        return text;
    }

    private void ApplyText(DataRow row, string field, string text)
    {
        if (!row.Table.Columns.Contains(field)) return;
        DataColumn column = row.Table.Columns[field]!;
        try
        {
            object candidate;
            if (string.IsNullOrWhiteSpace(text))
            {
                if (!column.AllowDBNull && column.DataType != typeof(string))
                    throw new FormatException("This field cannot be empty.");
                candidate = column.AllowDBNull ? DBNull.Value : "";
            }
            else if (column.DataType == typeof(bool))
            {
                candidate = text.Equals("Yes", StringComparison.OrdinalIgnoreCase) ||
                    text.Equals("True", StringComparison.OrdinalIgnoreCase) ||
                    text == "1";
            }
            else if (column.DataType == typeof(DateTime))
            {
                candidate = DateTime.Parse(text, CultureInfo.InvariantCulture);
            }
            else if (column.DataType == typeof(string) &&
                     (field == "StartDate" || field == "ExpirationDate"))
            {
                if (!DateTime.TryParse(text, CultureInfo.InvariantCulture,
                    DateTimeStyles.None, out var parsed))
                    throw new FormatException("Enter a valid yyyy-MM-dd date.");
                candidate = parsed.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            }
            else if (column.DataType == typeof(string)) candidate = text;
            else candidate = Convert.ChangeType(text, column.DataType,
                CultureInfo.InvariantCulture);
            if (candidate is string str &&
                column.MaxLength > 0 && str.Length > column.MaxLength)
                throw new ArgumentException("Text exceeds the database field length.");
            Set(row, field, candidate);
            _invalidFields.Remove(field);
            _editorStatus.Text = "";
        }
        catch (Exception ex) when (ex is ArgumentException or FormatException
                                   or InvalidCastException or OverflowException
                                   or ConstraintException)
        {
            _invalidFields.Add(field);
            _editorStatus.Text = "Invalid " + field + ": " + ex.Message;
        }
    }

    private static void Set(DataRow row, string field, object? candidate)
    {
        if (!row.Table.Columns.Contains(field) || row.RowState == DataRowState.Deleted)
            return;
        if (candidate is null) candidate = DBNull.Value;
        if (candidate is not DBNull && !row.Table.Columns[field]!.DataType
                .IsInstanceOfType(candidate))
            candidate = Convert.ChangeType(candidate,
                row.Table.Columns[field]!.DataType,
                CultureInfo.InvariantCulture);
        if (!Equals(row[field], candidate)) row[field] = candidate;
    }

    private Panel BuildRoles(DataRow row)
    {
        var card = Frame("ContractRoles", bright: true);
        card.Margin = new Padding(5, 0, 5, 0);
        var header = new Panel
        {
            Dock = DockStyle.Top, Height = 53, BackColor = Medium
        };
        header.Paint += (_, e) =>
        {
            using var brush = new LinearGradientBrush(
                new Rectangle(0, 0, Math.Max(1, header.Width), header.Height),
                Color.FromArgb(29, 110, 174),
                Color.FromArgb(8, 38, 69), LinearGradientMode.Horizontal);
            e.Graphics.FillRectangle(brush, header.ClientRectangle);
            using var pen = new Pen(Cyan);
            e.Graphics.DrawLine(pen, 0, header.Height - 1,
                header.Width, header.Height - 1);
            HeaderIcon(e.Graphics, 12, 10, "Roles");
            using var separator = new Pen(Cyan, 1.8f);
            e.Graphics.DrawLine(separator, 46, 10, 46, 42);
        };
        var title = Label("Roles", 13.5f, true);
        title.Padding = new Padding(54, 0, 0, 0);
        title.Dock = DockStyle.Fill;
        header.Controls.Add(title);
        var body = new FlowLayoutPanel
        {
            Name = "ContractRolesList", Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown, WrapContents = false,
            AutoScroll = true,
            BackColor = Color.FromArgb(6, 23, 43),
            Padding = new Padding(12, 15, 4, 5)
        };
        var slots = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2,
            BackColor = Color.Transparent,
            Padding = Padding.Empty, Margin = Padding.Empty
        };
        slots.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        slots.RowStyles.Add(new RowStyle(SizeType.Absolute, 53));
        slots.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        slots.Controls.Add(header, 0, 0);
        slots.Controls.Add(body, 0, 1);
        card.Controls.Add(slots);
        foreach (var (name, label) in new[]
        {
            ("RoleWrestler", "Wrestler"),
            ("RolePartTimeWrestler", "Occasional Wrestler"),
            ("RoleManager", "Manager"),
            ("RoleOnScreenPersonality", "On-Screen Personality"),
            ("RolePlayByPlay", "Play By Play Commentator"),
            ("RoleColour", "Colour Commentator"),
            ("RoleReferee", "Referee"),
            ("RoleProducer", "Road Agent")
        })
        {
            var checkbox = new CheckBox
            {
                Name = "ContractField_" + name, Text = label,
                Width = 250, Height = 34,
                Checked = Yes(row, name),
                Font = new Font("Segoe UI", 10.2f, FontStyle.Bold),
                ForeColor = White, BackColor = Color.Transparent,
                Enabled = _databaseFile is not null,
                Margin = new Padding(4, 1, 0, 1)
            };
            checkbox.CheckedChanged += (_, _) =>
                Set(row, name, row.Table.Columns[name]?.DataType == typeof(string)
                    ? checkbox.Checked ? "Yes" : "No"
                    : checkbox.Checked);
            body.Controls.Add(checkbox);
        }
        return card;
    }

    private string Biography(DataRow contract)
    {
        var id = Value(contract, "UID");
        var row = _bios?.Rows.Cast<DataRow>().FirstOrDefault(r =>
            r.RowState != DataRowState.Deleted && Value(r, "ContractUID") == id);
        return Value(row, "Biography");
    }

    private void SetBiography(DataRow contract, string text)
    {
        if (_bios is null) return;
        var id = Value(contract, "UID");
        var row = _bios.Rows.Cast<DataRow>().FirstOrDefault(r =>
            r.RowState != DataRowState.Deleted && Value(r, "ContractUID") == id);
        if (row is null)
        {
            if (string.IsNullOrWhiteSpace(text)) return;
            row = _bios.NewRow();
            row["ContractUID"] = Convert.ChangeType(contract["UID"],
                _bios.Columns["ContractUID"]!.DataType, CultureInfo.InvariantCulture);
            row["Biography"] = text;
            _bios.Rows.Add(row);
        }
        else Set(row, "Biography", text);
    }

    private void BuildFooter(Panel footer, DataRow row)
    {
        var left = new Panel
        {
            Name = "ContractCopyAndImport", BackColor = Color.Transparent
        };
        footer.Controls.Add(left);
        var ego = new ComboBox
        {
            Name = "ContractAlterEgoSelector",
            DropDownStyle = ComboBoxStyle.DropDownList,
            Font = new Font("Segoe UI", 9f),
            BackColor = Medium, ForeColor = White, FlatStyle = FlatStyle.Flat
        };
        var alternatives = _profile.Sections.FirstOrDefault(s =>
            s.Table == "tblAlternative")?.Records.Rows.Cast<DataRow>()
            .Where(r => r.RowState != DataRowState.Deleted).ToArray() ?? [];
        foreach (var alt in alternatives)
            ego.Items.Add(new Option(alt,
                Value(alt, "RecordName") + "  |  " + Value(alt, "Name")));
        if (ego.Items.Count != 0) ego.SelectedIndex = 0;
        var fromEgo = Button("Apply", "ContractImportAlterEgo");
        fromEgo.Enabled = ego.Items.Count != 0 && _databaseFile is not null;
        fromEgo.Click += (_, _) =>
        {
            if (ego.SelectedItem is not Option option) return;
            foreach (var (target, source) in new[]
            {
                ("Name", "Name"), ("ShortName", "ShortName"),
                ("Picture", "Picture"), ("MoveSet", "MoveSet"),
                ("GimmickName", "Gimmick"), ("GimmickBasis", "GimmickBasis")
            })
                if (row.Table.Columns.Contains(target) &&
                    option.Record.Table.Columns.Contains(source) &&
                    option.Record[source] is not DBNull)
                    Set(row, target, option.Record[source]);
            string bio = Value(option.Record, "Biography");
            if (bio.Length != 0) SetBiography(row, bio);
            BuildEditor(row);
        };
        var templates = new ComboBox
        {
            Name = "ContractTemplateSelector",
            DropDownStyle = ComboBoxStyle.DropDownList,
            Font = new Font("Segoe UI", 9f),
            BackColor = Medium, ForeColor = White, FlatStyle = FlatStyle.Flat
        };
        foreach (var item in _templates) templates.Items.Add(item);
        if (templates.Items.Count != 0) templates.SelectedIndex = 0;
        var copy = Button("Apply", "ContractCopyTerms");
        copy.Enabled = templates.Items.Count != 0 && _databaseFile is not null;
        copy.Click += (_, _) =>
        {
            if (templates.SelectedItem is not PrwContractEditorService.Template template)
                return;
            foreach (DataColumn column in row.Table.Columns)
            {
                if (column.ColumnName is "UID" or "WorkerUID" or "FedUID" or
                    "EmployerInitials" or "Name" or "ShortName" or "Picture" or
                    "StartDate" or "ExpirationDate") continue;
                if (!template.Values.Table.Columns.Contains(column.ColumnName))
                    continue;
                object value = template.Values[column.ColumnName];
                if (value is not DBNull)
                    Set(row, column.ColumnName, value);
            }
            BuildEditor(row);
        };

        var first = Label("Import Details From Alter Ego", 9.4f, true);
        var second = Label("Copy Terms From Another Contract", 9.4f, true);
        left.Controls.AddRange([first, second, ego, fromEgo, templates, copy]);
        var back = Button("Back", "ContractBack");
        var reset = Button("Reset", "ContractReset");
        var save = Button("Save", "ContractSave", true);
        footer.Controls.AddRange([back, reset, save]);
        back.Click += (_, _) => ReturnToList();
        reset.Click += (_, _) => Reset(row);
        save.Click += (_, _) => Save();
        save.Enabled = _databaseFile is not null;
        _editorStatus.Name = "ContractEditorStatus";
        _editorStatus.BackColor = Color.Transparent;
        _editorStatus.ForeColor = Color.FromArgb(255, 181, 168);
        _editorStatus.Font = new Font("Segoe UI", 8.3f, FontStyle.Bold);
        _editorStatus.Text = "";
        footer.Controls.Add(_editorStatus);
        footer.Resize += (_, _) =>
        {
            int w = footer.ClientSize.Width;
            left.Bounds = new Rectangle(12, 6, Math.Max(440, w - 445), 84);
            first.Bounds = new Rectangle(10, 5, 230, 31);
            ego.Bounds = new Rectangle(236, 6, 232, 28);
            fromEgo.Bounds = new Rectangle(480, 4, 80, 30);
            second.Bounds = new Rectangle(10, 41, 230, 30);
            templates.Bounds = new Rectangle(236, 42, 232, 28);
            copy.Bounds = new Rectangle(480, 40, 80, 30);
            int right = w - 13;
            save.Bounds = new Rectangle(right - 145, 17, 142, 49);
            reset.Bounds = new Rectangle(save.Left - 145, 17, 136, 49);
            back.Bounds = new Rectangle(reset.Left - 115, 17, 106, 49);
            _editorStatus.Bounds = new Rectangle(14, 73,
                Math.Max(200, w - 30), 20);
        };
    }

    private sealed record Option(DataRow Record, string Caption)
    {
        public override string ToString() => Caption;
    }

    private void Reset(DataRow row)
    {
        if (row.RowState == DataRowState.Modified) row.RejectChanges();
        else if (row.RowState == DataRowState.Added && _newDraftInitial is not null)
        {
            foreach (var (field, value) in _newDraftInitial)
                if (row.Table.Columns.Contains(field))
                    row[field] = value;
        }
        var bio = _bios?.Rows.Cast<DataRow>().FirstOrDefault(r =>
            r.RowState != DataRowState.Deleted &&
            Value(r, "ContractUID") == Value(row, "UID"));
        if (bio?.RowState == DataRowState.Added) bio.Delete();
        else if (bio?.RowState == DataRowState.Modified) bio.RejectChanges();
        BuildEditor(row);
    }

    private void Save()
    {
        if (_databaseFile is null || _editing is null) return;
        if (!ValidateBeforeLeaving()) return;
        try
        {
            PrwContractEditorService.Save(_databaseFile, _profile, _password);
            _onSaved();
            _editing = null;
            _newDraftInitial = null;
            BuildList();
            _onContext("Contracts");
        }
        catch (Exception ex)
        {
            _editorStatus.Text = "Save failed: " + ex.Message;
            _tips.SetToolTip(_editorStatus, ex.Message);
        }
    }
}
