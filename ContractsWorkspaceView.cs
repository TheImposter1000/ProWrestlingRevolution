using System.Data;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Windows.Forms;

namespace ProWrestlingRevolution;

/// <summary>
/// Two contract screens on the fixed 1366x712 interior canvas. Coordinates,
/// grouping, order and proportions follow the supplied selection/editor images.
/// The screenshot is a layout specification, never a baked, noninteractive UI.
/// </summary>
internal sealed class ContractsWorkspaceView : UserControl
{
    private const int DesignWidth = 1366;
    private const int DesignHeight = 712;
    private static readonly Color Navy = Color.FromArgb(1, 18, 35);
    private static readonly Color Panel = Color.FromArgb(2, 29, 52);
    private static readonly Color Cyan = Color.FromArgb(0, 221, 255);
    private static readonly Color White = Color.FromArgb(246, 249, 255);
    private static readonly Color Dim = Color.FromArgb(190, 213, 230);
    private static readonly Color Rule = Color.FromArgb(66, 139, 192);
    private static readonly Color Glow = Color.FromArgb(17, 134, 204);

    private readonly AppPaths _paths;
    private readonly SkinPalette _palette;
    private readonly PrwWorkerProfile _profile;
    private readonly string? _file;
    private readonly string? _password;
    private readonly Action<bool> _editorMode;
    private readonly Action _saved;
    private readonly IReadOnlyList<PrwContractStore.Employer> _employers;
    private readonly Image? _portrait;
    // Shared, skin-aware Worker Editor artwork. The bitmap is owned by
    // WorkerBackgroundCache and must not be disposed by this view.
    private Bitmap? _workerArtwork;
    private readonly Dictionary<string, Image?> _logos = new(StringComparer.OrdinalIgnoreCase);
    private readonly DataTable _records;
    private readonly object _workerUid;
    private readonly string _workerName;
    private readonly ToolTip _help = new();
    private readonly List<(Control Control, Rectangle Bounds)> _positioned = [];
    private readonly Dictionary<string, Control> _fields =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, RoleToggle> _roles =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly List<FieldSpec> _labels = [];
    private TextBox? _search;
    private DarkDrop? _filter;
    private DarkDrop? _employerPicker;
    private ContractText? _biography;
    private DataRow? _selected;
    private bool _editing;
    private bool _creating;
    private int _scroll;
    private int _sortColumn;
    private bool _descending;
    private string _searchText = "";
    private string _filterText = "All Contracts";
    private readonly List<PwrContractButton> _buttons = [];
    private readonly List<Control> _selectorControls = [];
    private readonly List<Control> _editorControls = [];

    private sealed record FieldSpec(string Label, string Column, int LabelX, int Y, int InputX,
        int Width, bool TextEntry, string DefaultValue, string[] Choices);
    private sealed record Listing(DataRow Record, PrwContractStore.Employer? Employer,
        string EmployerName, string ContractType, string Status, string Begins,
        string Expires, string Role);

    internal ContractsWorkspaceView(AppPaths paths, SkinPalette palette,
        PrwWorkerProfile profile, string? file, string? password,
        Action<bool> editorMode, Action saved)
    {
        _paths = paths;
        _palette = palette;
        _profile = profile;
        _file = file;
        _password = password;
        _editorMode = editorMode;
        _saved = saved;
        _records = profile.Sections.First(s => s.Table == "tblContract").Records;
        _workerUid = profile.Identity.Uid;
        _workerName = profile.Identity.Name;
        _employers = file is null ? [] : PrwContractStore.Employers(file, password);
        var identity = profile.Sections.First(s => s.Table == "tblWorker")
            .Records.Rows.Cast<DataRow>().FirstOrDefault();
        _portrait = Picture(Read(identity, "Picture", ""), _workerName, "Workers");
        foreach (var employer in _employers)
            _logos.TryAdd(employer.Initials,
                Picture(employer.Logo, employer.Initials, "Logos"));

        Name = "WorkerContractsView";
        Dock = DockStyle.Fill;
        Margin = Padding.Empty;
        BackColor = Navy;
        AutoScaleMode = AutoScaleMode.None;
        DoubleBuffered = true;
        ResizeRedraw = true;
        SetStyle(ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint, true);
        BuildSelector();
        _selected = _records.Rows.Cast<DataRow>().FirstOrDefault();
        // Match the other Worker sections; load a cached copy of the current
        // skin's Worker_Editor.jpg instead of painting an unrelated arena.
        HandleCreated += async (_, _) => await RestoreWorkerArtworkAsync();
        _ = RestoreWorkerArtworkAsync();
        SetVisibleControls();
        Resize += (_, _) => PositionAll();
        Disposed += (_, _) =>
        {
            _help.Dispose();
            _portrait?.Dispose();
            foreach (var img in _logos.Values) img?.Dispose();
        };
    }

    internal bool ReturnToList()
    {
        if (!_editing) return false;
        _editing = false;
        _creating = false;
        _editorMode(false);
        SetVisibleControls();
        Invalidate();
        return true;
    }

    private Image? Picture(string stored, string defaultName, string group)
    {
        string name = Path.GetFileName(stored);
        var possible = new List<string>();
        if (name.Length > 0) possible.Add(name);
        if (defaultName.Length > 0)
            foreach (var ext in new[] { ".png", ".jpg", ".jpeg", ".gif", ".bmp" })
                possible.Add(defaultName + ext);
        foreach (string image in possible.Distinct(StringComparer.OrdinalIgnoreCase))
            foreach (string folder in new[]
            {
                _paths.Pictures, Path.Combine(_paths.Pictures, "Default", group),
                Path.Combine(_paths.Pictures, group),
                Path.Combine(_paths.Pictures, "Default", group == "Logos" ? "Companies" : group)
            })
            {
                string path = Path.Combine(folder, image);
                if (!File.Exists(path)) continue;
                try
                {
                    using var data = File.OpenRead(path);
                    using var original = Image.FromStream(data);
                    return new Bitmap(original);
                }
                catch (Exception ex) when (ex is IOException or ArgumentException
                                           or OutOfMemoryException)
                {
                    // Optional art never prevents editing a record.
                }
            }
        return null;
    }

    private async Task RestoreWorkerArtworkAsync()
    {
        try
        {
            string selected = _palette.Asset(
                Path.Combine("Backgrounds", "Worker_Editor.jpg"));
            string fallback = Path.Combine(_paths.SkinsRoot,
                "Obsidian", "Backgrounds", "Worker_Editor.jpg");
            string source = File.Exists(selected) ? selected : fallback;
            var picture = await WorkerBackgroundCache.GetPreparedAsync(source,
                new Size(DesignWidth, DesignHeight));
            if (IsDisposed || Disposing || picture is null) return;
            _workerArtwork = picture;
            Invalidate();
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException
                                      or InvalidOperationException)
        {
            System.Diagnostics.Debug.WriteLine("Contracts background: " + error.Message);
        }
    }

    // The preview must contain the same skin art as the actual editor, even
    // when the Windows CI worker has not finished its normal UI event loop.
    internal Task PrepareArtworkForPreviewAsync() => RestoreWorkerArtworkAsync();

    private float SX => Width / (float)DesignWidth;
    private float SY => Height / (float)DesignHeight;
    private Rectangle BoundsFor(Rectangle area) =>
        new((int)Math.Round(area.X * SX), (int)Math.Round(area.Y * SY),
            Math.Max(1, (int)Math.Round(area.Width * SX)),
            Math.Max(1, (int)Math.Round(area.Height * SY)));

    private void Place(Control control, int x, int y, int w, int h, bool editor)
    {
        _positioned.Add((control, new Rectangle(x, y, w, h)));
        (editor ? _editorControls : _selectorControls).Add(control);
        Controls.Add(control);
        control.Bounds = BoundsFor(new Rectangle(x, y, w, h));
    }

    private void PositionAll()
    {
        foreach (var (control, bounds) in _positioned)
            if (!control.IsDisposed) control.Bounds = BoundsFor(bounds);
    }

    private static GraphicsPath Round(RectangleF r, float radius)
    {
        var path = new GraphicsPath();
        float d = radius * 2;
        path.AddArc(r.X, r.Y, d, d, 180, 90);
        path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }

    private static void FillGradient(Graphics g, Rectangle r,
        Color top, Color bottom, int radius = 8)
    {
        using var path = Round(r, radius);
        using var brush = new LinearGradientBrush(r, top, bottom, 90f);
        g.FillPath(brush, path);
    }

    private static void Stroke(Graphics g, Rectangle r, Color color,
        int radius = 8, float width = 1.5f)
    {
        using var path = Round(r, radius);
        using var pen = new Pen(color, width);
        g.DrawPath(pen, path);
    }

    private static void Caption(Graphics g, string text, int x, int y, int w, int h,
        float fontSize, bool bold = false, Color? color = null, bool italic = false,
        ContentAlignment alignment = ContentAlignment.MiddleLeft)
    {
        using var font = new Font("Segoe UI", fontSize,
            (bold ? FontStyle.Bold : FontStyle.Regular) |
            (italic ? FontStyle.Italic : FontStyle.Regular));
        var format = TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix |
            TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine;
        format |= alignment == ContentAlignment.MiddleCenter
            ? TextFormatFlags.HorizontalCenter :
            alignment == ContentAlignment.MiddleRight ? TextFormatFlags.Right : TextFormatFlags.Left;
        TextRenderer.DrawText(g, text, font, new Rectangle(x, y, w, h),
            color ?? White, format);
    }

    private void BuildSelector()
    {
        var search = new ContractText(false, "Search contracts...")
        {
            Name = "ContractSearch"
        };
        _search = search.Input;
        _search.TextChanged += (_, _) =>
        {
            _searchText = _search.Text;
            _scroll = 0;
            Invalidate();
        };
        Place(search, 792, 121, 291, 36, false);

        var filter = new DarkDrop(_palette) { Name = "ContractStatusFilter" };
        filter.SetChoices(["All Contracts", "Active", "Expired"], "All Contracts");
        filter.ValueChanged += value =>
        {
            _filterText = value;
            _scroll = 0;
            Invalidate();
        };
        _filter = filter;
        Place(filter, 1104, 121, 209, 36, false);

        AddButton("Remove", 696, 620, 186, 46, false, false,
            () => Safe(RemoveSelection));
        AddButton("Open", 901, 620, 180, 46, false, false,
            () => Safe(OpenSelection));
        AddButton("Add Contract", 1098, 620, 215, 46, true, false,
            () => Safe(() => OpenEditor(null)));
    }

    private void AddButton(string caption, int x, int y, int width, int height,
        bool primary, bool editor, Action action)
    {
        var button = new PwrContractButton(caption, primary)
        {
            Name = "ContractAction_" + caption.Replace(" ", ""),
        };
        button.Click += (_, _) => action();
        _buttons.Add(button);
        Place(button, x, y, width, height, editor);
    }

    private IEnumerable<Listing> Listings()
    {
        IEnumerable<Listing> records = _records.Rows.Cast<DataRow>()
            .Where(row => row.RowState != DataRowState.Deleted)
            .Select(row =>
            {
                var company = _employers.FirstOrDefault(f =>
                    Convert.ToString(f.Uid, CultureInfo.InvariantCulture) ==
                    Read(row, "FedUID"));
                string initials = company?.Initials ??
                    Read(row, "EmployerInitials", "Unknown");
                DateTime? finish = Date(row, "ExpirationDate");
                string status = finish.HasValue && finish.Value.Date < DateTime.Today
                    ? "Expired" : "Active";
                string primary = PrimaryRole(row);
                return new Listing(row, company, initials,
                    Read(row, "ContractType", "Handshake"), status,
                    Date(row, "StartDate")?.ToString("yyyy-MM-dd") ??
                        Read(row, "StartDate", ""),
                    finish?.ToString("yyyy-MM-dd") ??
                        Read(row, "ExpirationDate", ""),
                    primary);
            });
        if (_filterText != "All Contracts")
            records = records.Where(x => x.Status == _filterText);
        if (_searchText.Trim().Length != 0)
            records = records.Where(x =>
                (x.EmployerName + " " + x.ContractType + " " +
                 x.Status + " " + x.Role)
                .Contains(_searchText.Trim(), StringComparison.OrdinalIgnoreCase));

        Func<Listing, string> key = _sortColumn switch
        {
            0 => r => r.EmployerName,
            1 => r => r.ContractType,
            2 => r => r.Status,
            3 => r => r.Begins,
            4 => r => r.Expires,
            _ => r => r.Role
        };
        return _descending ? records.OrderByDescending(key,
            StringComparer.CurrentCultureIgnoreCase) :
            records.OrderBy(key, StringComparer.CurrentCultureIgnoreCase);
    }

    private static string Read(DataRow? record, string field, string fallback = "None")
    {
        if (record is null || !record.Table.Columns.Contains(field) ||
            record[field] is null or DBNull) return fallback;
        object value = record[field];
        if (value is bool boolean) return boolean ? "Yes" : "No";
        if (value is DateTime date) return date.ToString("yyyy-MM-dd");
        string text = Convert.ToString(value, CultureInfo.InvariantCulture) ?? "";
        return text.Length == 0 ? fallback : text;
    }

    private static DateTime? Date(DataRow? record, string field)
    {
        if (record is null || !record.Table.Columns.Contains(field) ||
            record[field] is DBNull) return null;
        if (record[field] is DateTime date) return date;
        return DateTime.TryParse(Convert.ToString(record[field]), out date) ? date : null;
    }

    private static bool EnabledRole(DataRow? row, string key)
    {
        if (row is null || !row.Table.Columns.Contains(key) || row[key] is DBNull)
            return false;
        var value = row[key];
        return value is bool b ? b :
            Convert.ToString(value, CultureInfo.InvariantCulture) is "1" or "True" or "Yes";
    }

    private static string PrimaryRole(DataRow row)
    {
        foreach (var (column, title) in new[]
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
            if (EnabledRole(row, column)) return title;
        return "None";
    }

    private void OpenSelection()
    {
        if (_selected is null)
            throw new InvalidOperationException("Select a contract in the table first.");
        OpenEditor(_selected);
    }

    private void RemoveSelection()
    {
        if (_selected is null)
            throw new InvalidOperationException("Select a contract to remove.");
        if (_file is null)
            throw new InvalidOperationException("Open a writable native PWR universe first.");
        if (MessageBox.Show(FindForm(),
            "Permanently remove this worker contract and its biography?",
            "Remove Contract", MessageBoxButtons.YesNo, MessageBoxIcon.Warning,
            MessageBoxDefaultButton.Button2) != DialogResult.Yes) return;
        PrwContractStore.Remove(_file, _password, _workerUid, _selected["UID"]);
        _selected = null;
        _saved();
    }

    private void OpenEditor(DataRow? record)
    {
        _selected = record;
        _creating = record is null;
        _editing = true;
        foreach (var control in _editorControls.ToArray())
        {
            Controls.Remove(control);
            control.Dispose();
        }
        _editorControls.Clear();
        _buttons.RemoveAll(button => button.IsDisposed);
        _positioned.RemoveAll(x => x.Control.IsDisposed);
        _fields.Clear();
        _roles.Clear();
        _labels.Clear();
        _employerPicker = null;
        _biography = null;
        BuildEditor();
        _editorMode(true);
        SetVisibleControls();
        Invalidate();
    }

    private void SetVisibleControls()
    {
        foreach (var control in _selectorControls) control.Visible = !_editing;
        foreach (var control in _editorControls) control.Visible = _editing;
        foreach (var button in _buttons)
            if (button.Visible && button.Text == "Remove")
                button.Enabled = _selected is not null && _file is not null;
    }

    private void Field(string label, string column, int labelX, int y, int inputX,
        int width, string defaultValue, bool text = false, params string[] choices)
    {
        _labels.Add(new FieldSpec(label, column, labelX, y, inputX, width,
            text, defaultValue, choices));
        string current = Read(_selected, column, defaultValue);
        var choicesForField = new List<string>();
        DataRow? template = _selected ??
            _records.Rows.Cast<DataRow>().FirstOrDefault();

        if (!text && template is not null && _file is not null &&
            template.Table.Columns.Contains(column))
        {
            foreach (var choice in PrwWorkerFieldOptions.Get(_paths, _file,
                         _password, template, column))
                choicesForField.Add(choice.Label);
        }
        if (!text && (template?.Table.Columns[column]?.DataType is null ||
            template.Table.Columns[column]!.DataType == typeof(string)))
            choicesForField.AddRange(choices);

        if (_creating && !text &&
            template?.Table.Columns[column]?.DataType is Type inputType &&
            inputType != typeof(string))
            current = choicesForField.FirstOrDefault() ?? current;
        if (!text && current.Length != 0)
            choicesForField.Insert(0, current);

        // All contract values are the same editable glass field. A field with
        // known choices also has a subtle arrow to open them; typing is always
        // possible for schema-backed fields, even when a list is available.
        var field = new DarkDrop(_palette)
        {
            Name = "ContractField_" + column
        };
        field.SetChoices(text ? [] : choicesForField, current);
        _fields[column] = field;
        Place(field, inputX, y, width, 25, true);

        // Do not present values absent from tblContract as saved options.
        bool supported = _selected?.Table.Columns.Contains(column) ??
            PrwSchema.Tables.Single(t => t.Name == "tblContract")
                .Columns.Contains(column, StringComparer.OrdinalIgnoreCase);
        if (!supported)
        {
            field.Enabled = false;
            _help.SetToolTip(field,
                "Read-only: this field is not in the current PWR contract schema.");
        }
    }

    private void BuildEditor()
    {
        Field("Employee", "_Employee", 178, 22, 267, 183, _workerName,
            false, _workerName);
        _fields["_Employee"].Enabled = false;

        var employer = new DarkDrop(_palette) { Name = "ContractEmployer" };
        var selectedEmployer = _selected is null ? null : _employers.FirstOrDefault(x =>
            Convert.ToString(x.Uid, CultureInfo.InvariantCulture) == Read(_selected, "FedUID"));
        employer.SetChoices(_employers.Select(x => x.ToString()).ToArray(),
            selectedEmployer?.ToString() ?? _employers.FirstOrDefault()?.ToString() ?? "None");
        _employerPicker = employer;
        Place(employer, 553, 22, 155, 27, true);
        if (!_creating) _help.SetToolTip(employer,
            "Changing employer updates the contract's company link.");

        Field("Name", "Name", 179, 61, 267, 183, Read(_selected, "Name", _workerName),
            true);
        Field("Short Name", "ShortName", 179, 97, 267, 183,
            Read(_selected, "ShortName", _profile.Identity.ShortName), true);

        _biography = new ContractText(true, "");
        _biography.Name = "ContractBiography";
        _biography.Input.Text = ContractBiography();
        Place(_biography, 817, 22, 502, 102, true);

        int y = 216;
        Field("Contract Type", "ContractType", 49, y, 175, 167, "Handshake",
            false, "Handshake", "Written");
        Field("Exclusive", "Exclusive", 49, y += 29, 175, 167, "No",
            false, "No", "Yes");
        Field("Touring", "Touring", 49, y += 29, 175, 167, "No",
            false, "No", "Yes");
        Field("Pay", "PayBasis", 49, y += 29, 175, 167, "Per Show",
            false, "Per Show", "Monthly");
        Field("On Loan", "OnLoan", 49, y += 29, 175, 167, "No",
            false, "No", "Yes");
        Field("In Developmental", "InDevelopmental", 49, y += 29, 175, 167,
            "No", false, "No", "Yes");
        Field("Iron Clad", "IronClad", 49, y += 29, 175, 167, "No",
            false, "No", "Yes");
        Field("Wage", "Wage", 49, y += 29, 175, 167, "0", true);
        Field("Downside", "Downside", 49, y += 29, 175, 167, "0", true);
        Field("Contract Begin", "StartDate", 49, y += 29, 175, 167,
            DateTime.Today.ToString("yyyy-MM-dd"), true);
        Field("Expires In", "TermCode", 49, y += 29, 175, 167, "Random",
            false, "Random", "Ongoing", "One Year", "Two Years");
        Field("Appearances Left", "AppearanceLimit", 49, y += 29, 175, 167,
            "Unlimited Appearances", false, "Unlimited Appearances");

        y = 211;
        Field("Alignment", "Alignment", 389, y, 504, 185, "Babyface",
            false, "Babyface", "Heel");
        Field("Last Turn", "LastTurn", 389, y += 28, 504, 185, "Random",
            false, "Random");
        Field("Competes In", "CompetesIn", 389, y += 28, 504, 185, "Men's Division",
            false, "Men's Division", "Women's Division");
        Field("Brand", "Brands", 389, y += 28, 504, 185, "None", false, "None");
        Field("Division", "Divisions", 389, y += 28, 504, 185, "None", false, "None");
        Field("Manager", "ManagerWorkerUID", 389, y += 28, 504, 185, "None", false, "None");
        Field("Mask", "MaskRespect", 389, y += 28, 504, 185, "No Mask",
            false, "No Mask");
        Field("Move Set", "MoveSet", 389, y += 28, 504, 185, _workerName,
            false, _workerName);
        Field("Momentum", "Momentum", 389, y += 28, 504, 185, "Neutral",
            false, "Neutral");
        Field("Gimmick", "GimmickName", 389, y += 28, 504, 185, "None",
            false, "None");
        Field("Gimmick Basis", "GimmickBasis", 389, y += 28, 504, 185, "None",
            false, "None");
        Field("Gimmick Rating", "GimmickRating", 389, y += 28, 504, 185, "Random",
            false, "Random");
        Field("Gimmick Status", "GimmickStatus", 389, y += 28, 504, 185, "Random",
            false, "Random");

        y = 216;
        Field("Creative Control", "CreativeControl", 738, y, 872, 164, "No",
            false, "No", "Yes");
        Field("Hiring Veto", "HiringVeto", 738, y += 29, 872, 164, "No",
            false, "No", "Yes");
        Field("Merchandise Cut", "MerchandiseCut", 738, y += 29, 872, 164,
            "Automatic", false, "Automatic");
        Field("Wage Matching", "WageMatching", 738, y += 29, 872, 164, "No",
            false, "No", "Yes");
        Field("Roster Usage", "RosterUsage", 738, y += 29, 872, 164, "Any",
            false, "Any");
        Field("Intended Role", "IntendedRole", 738, y += 29, 872, 164, "Normal",
            false, "Normal", "Icon", "Special Attraction", "Gatekeeper",
            "Passing The Torch", "Future Main Eventer", "Backbone",
            "Blue Chipper", "Nostalgia Act", "Developing");
        Field("Shows Control", "ShowsControl", 738, y += 29, 872, 164,
            "All Shows", false, "All Shows");
        Field("Bonus Type", "BonusType", 738, y += 29, 872, 164, "Never",
            false, "Never");
        Field("Bonus Amount", "BonusAmount", 738, y += 29, 872, 164, "None",
            false, "None");
        Field("Travel Cover", "TravelCover", 738, y += 29, 872, 164, "No",
            false, "No", "Yes");

        string[] roleKeys =
        [
            "RoleWrestler", "RolePartTimeWrestler", "RoleManager",
            "RoleOnScreenPersonality", "RolePlayByPlay", "RoleColour",
            "RoleReferee", "RoleProducer"
        ];
        string[] roleLabels =
        [
            "Wrestler", "Occasional Wrestler", "Manager", "On-Screen Personality",
            "Play By Play Commentator", "Colour Commentator", "Referee", "Road Agent"
        ];
        var workerIdentity = _profile.Sections.FirstOrDefault(s => s.Table == "tblWorker")
            ?.Records.Rows.Cast<DataRow>().FirstOrDefault();
        for (int i = 0; i < roleKeys.Length; i++)
        {
            var toggle = new RoleToggle(roleLabels[i])
            {
                Name = "ContractRole_" + roleKeys[i],
                Checked = EnabledRole(_selected ?? workerIdentity, roleKeys[i])
            };
            _roles.Add(roleKeys[i], toggle);
            Place(toggle, 1088, 222 + i * 34, 240, 29, true);
        }

        var alter = new DarkDrop(_palette) { Name = "ContractAlterEgoSelector" };
        alter.SetChoices(_profile.Sections.FirstOrDefault(s => s.Table == "tblAlternative")
            ?.Records.Rows.Cast<DataRow>().Select(r => Read(r, "RecordName", ""))
            .Where(x => x.Length > 0).ToArray() ?? [], "None");
        Place(alter, 357, 609, 300, 28, true);
        AddButton("Apply", 678, 609, 100, 28, false, true, () => Safe(() =>
        {
            var alternative = _profile.Sections.FirstOrDefault(s => s.Table == "tblAlternative")
                ?.Records.Rows.Cast<DataRow>().FirstOrDefault(r =>
                    Read(r, "RecordName", "") == alter.Value);
            if (alternative is null) return;
            foreach (var (target, source) in new[]
            {
                ("Name", "Name"), ("ShortName", "ShortName"),
                ("MoveSet", "MoveSet"), ("GimmickName", "Gimmick"),
                ("GimmickBasis", "GimmickBasis")
            })
                SetField(target, Read(alternative, source, ""));
        }));

        var copy = new DarkDrop(_palette) { Name = "ContractCopySource" };
        var others = _records.Rows.Cast<DataRow>()
            .Where(x => !ReferenceEquals(x, _selected))
            .Select(x => Read(x, "Name", "(unnamed)")).ToArray();
        copy.SetChoices(others, others.FirstOrDefault() ?? "None");
        Place(copy, 357, 651, 240, 27, true);
        var company = new DarkDrop(_palette) { Name = "ContractCopyCompany" };
        company.SetChoices(_employers.Select(x => x.ToString()).ToArray(),
            _employers.FirstOrDefault()?.ToString() ?? "None");
        Place(company, 615, 651, 196, 27, true);
        AddButton("Apply", 831, 651, 97, 27, false, true, () => Safe(() =>
        {
            var source = _records.Rows.Cast<DataRow>().FirstOrDefault(r =>
                Read(r, "Name", "(unnamed)") == copy.Value &&
                (_employers.FirstOrDefault(e =>
                    Convert.ToString(e.Uid, CultureInfo.InvariantCulture) ==
                    Read(r, "FedUID"))?.ToString() ?? "None") == company.Value);
            if (source is null) return;
            foreach (var key in new[]
            {
                "ContractType", "Exclusive", "Touring", "PayBasis", "Wage",
                "Downside", "IronClad", "TermCode", "AppearanceLimit", "RosterUsage",
                "IntendedRole"
            })
                SetField(key, Read(source, key, ""));
        }));
        AddButton("Reset", 1027, 622, 147, 42, false, true,
            () => Safe(() => OpenEditor(_selected)));
        AddButton("Save", 1189, 622, 141, 42, true, true,
            () => Safe(SaveEditor));
    }

    private string ContractBiography()
    {
        if (_selected is null) return "";
        var bios = _profile.Sections.FirstOrDefault(s => s.Table == "tblContractBio")
            ?.Records;
        return bios?.Rows.Cast<DataRow>()
            .Where(row => Read(row, "ContractUID", "") ==
                          Read(_selected, "UID", ""))
            .Select(row => Read(row, "Biography", ""))
            .FirstOrDefault() ?? "";
    }

    private void SetField(string key, string value)
    {
        if (_fields.TryGetValue(key, out var control) && control is DarkDrop field)
            field.SetValue(value);
    }

    private string GetField(string key) =>
        _fields.TryGetValue(key, out var control) && control is DarkDrop field
            ? field.Value : "";

    private void SaveEditor()
    {
        if (_file is null)
            throw new InvalidOperationException("Save requires a writable native PWR universe.");
        if (_employerPicker is null || _biography is null)
            throw new InvalidOperationException("The contract editor did not initialize.");
        var employer = _employers.FirstOrDefault(x => x.ToString() == _employerPicker.Value)
            ?? throw new InvalidOperationException("Choose a valid employer.");

        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (key, control) in _fields)
        {
            if (key.StartsWith("_", StringComparison.Ordinal)) continue;
            if (!control.Enabled) continue;
            string text = GetField(key);
            if (key is "StartDate" && !DateTime.TryParseExact(text,
                    "yyyy-MM-dd", CultureInfo.InvariantCulture,
                    DateTimeStyles.None, out _))
                throw new InvalidDataException("Contract Begin must use YYYY-MM-DD.");
            var column = (_selected ?? _records.Rows.Cast<DataRow>().FirstOrDefault())
                ?.Table.Columns[key];
            if (column is not null && column.DataType != typeof(string))
            {
                try { _ = PrwContractStore.ConvertInput(text, column); }
                catch (Exception ex) when (ex is FormatException or InvalidCastException
                                           or OverflowException)
                {
                    if (_creating) continue; // Database-defined default for an unmapped enum.
                    throw new InvalidDataException(
                        key + " needs a value supported by the universe.", ex);
                }
            }
            values[key] = text;
        }
        values["FedUID"] = Convert.ToString(employer.Uid, CultureInfo.InvariantCulture) ?? "";
        values["EmployerInitials"] = employer.Initials;
        var roles = _roles.ToDictionary(x => x.Key, x => x.Value.Checked,
            StringComparer.OrdinalIgnoreCase);
        if (_creating)
            PrwContractStore.Create(_file, _password, _workerUid,
                employer, values, roles, _biography.Input.Text);
        else
            PrwContractStore.Update(_file, _password, _workerUid,
                _selected ?? throw new InvalidOperationException("No contract selected."),
                values, roles, _biography.Input.Text);
        _editorMode(false);
        _saved();
    }

    private void Safe(Action callback)
    {
        try { callback(); }
        catch (Exception ex)
        {
            MessageBox.Show(FindForm(), ex.Message, "Contracts",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (_editing || e.Button != MouseButtons.Left) return;
        int x = (int)(e.X / SX), y = (int)(e.Y / SY);
        if (y >= 175 && y < 220 && x >= 50 && x < 1317)
        {
            int[] ends = [314, 542, 710, 878, 1044, 1317];
            _sortColumn = Array.FindIndex(ends, end => x < end);
            _descending = !_descending;
            Invalidate();
        }
        else if (y >= 220 && y < 592 && x >= 50 && x < 1317)
        {
            var visible = Listings().Skip(_scroll).Take(7).ToArray();
            int index = (y - 220) / 52;
            if (index >= 0 && index < visible.Length)
            {
                _selected = visible[index].Record;
                Invalidate();
            }
        }
    }

    protected override void OnMouseDoubleClick(MouseEventArgs e)
    {
        base.OnMouseDoubleClick(e);
        if (!_editing && e.Button == MouseButtons.Left && e.Y / SY >= 220 &&
            e.Y / SY < 592 && _selected is not null)
            Safe(OpenSelection);
    }

    protected override void OnMouseWheel(MouseEventArgs e)
    {
        base.OnMouseWheel(e);
        if (_editing) return;
        int maximum = Math.Max(0, Listings().Count() - 7);
        _scroll = Math.Clamp(_scroll + (e.Delta < 0 ? 1 : -1), 0, maximum);
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        var state = g.Save();
        g.ScaleTransform(SX, SY);
        PaintArena(g);
        if (_editing) PaintEditor(g);
        else PaintSelector(g);
        g.Restore(state);
    }

    private void PaintArena(Graphics g)
    {
        var rect = new Rectangle(0, 0, DesignWidth, DesignHeight);
        if (_workerArtwork is Image artwork)
            g.DrawImageUnscaled(artwork, 0, 0);
        else
        {
            using var background = new LinearGradientBrush(rect,
                Color.FromArgb(9, 28, 49), Color.FromArgb(4, 15, 31), 90f);
            g.FillRectangle(background, rect);
        }
        using var glass = new SolidBrush(Color.FromArgb(44, 5, 18, 34));
        g.FillRectangle(glass, rect);
    }

    private static void PanelFrame(Graphics g, Rectangle rect)
    {
        // Match the Worker Editor's glass instead of obscuring its skin
        // artwork with the old opaque, nearly black card rectangles.
        FillGradient(g, rect, Color.FromArgb(164, 11, 40, 66),
            Color.FromArgb(174, 4, 19, 39), 10);
        using var rounded = Round(rect, 10);
        using (var gleam = new Pen(Color.FromArgb(35, Cyan), 4f))
            g.DrawPath(gleam, rounded);
        Stroke(g, rect, Color.FromArgb(190, 41, 199, 230), 10, 1.25f);
    }

    private static void SectionBand(Graphics g, Rectangle rect, string name)
    {
        // Experienced players need section names, not pictograms or subtitles.
        FillGradient(g, rect, Color.FromArgb(186, 19, 76, 118),
            Color.FromArgb(166, 7, 37, 67), 8);
        using var line = new Pen(Color.FromArgb(162, 37, 185, 221), 1.1f);
        g.DrawLine(line, rect.Left, rect.Bottom, rect.Right, rect.Bottom);
        Caption(g, name, rect.Left + 18, rect.Top + 2,
            rect.Width - 32, rect.Height - 3, 16.1f, true);
    }

    private void PaintSelector(Graphics g)
    {
        var outer = new Rectangle(36, 35, 1294, 649);
        PanelFrame(g, outer);
        FillGradient(g, new Rectangle(37, 36, 1292, 73),
            Color.FromArgb(3, 73, 125), Color.FromArgb(1, 32, 65), 10);
        using (var blade = new SolidBrush(Color.FromArgb(91, 14, 146, 219)))
            g.FillPolygon(blade, [new Point(615, 36), new Point(700, 36),
                new Point(639, 108), new Point(565, 108)]);
        using (var cyanRule = new Pen(Color.FromArgb(190, Cyan), 1.5f))
            g.DrawLine(cyanRule, 36, 108, 1330, 108);
        Caption(g, "Worker Contracts", 81, 44, 600, 54, 27, true);

        var table = new Rectangle(50, 175, 1267, 417);
        FillGradient(g, table, Color.FromArgb(116, 8, 37, 64),
            Color.FromArgb(143, 3, 16, 35), 11);
        Stroke(g, table, Cyan, 11);
        FillGradient(g, new Rectangle(51, 176, 1265, 44),
            Color.FromArgb(22, 79, 125), Color.FromArgb(6, 32, 61), 10);
        using (var rule = new Pen(Color.FromArgb(90, 148, 193), 1f))
        {
            g.DrawLine(rule, 50, 219, 1317, 219);
            foreach (var x in new[] { 314, 542, 710, 878, 1044 })
                g.DrawLine(rule, x, 176, x, 219);
        }
        string[] headers = ["Employer", "Contract Type", "Status", "Begins",
            "Expires", "Primary Role"];
        int[] starts = [77, 340, 565, 735, 903, 1074];
        int[] widths = [224, 190, 129, 132, 135, 222];
        for (int i = 0; i < headers.Length; i++)
        {
            Caption(g, headers[i], starts[i], 180, widths[i], 35, 15, true);
            Caption(g, "⌃", starts[i] + widths[i] - 17, 177, 18, 20, 13, true, Dim);
            Caption(g, "⌄", starts[i] + widths[i] - 17, 191, 18, 20, 13, true, Dim);
        }

        var shown = Listings().Skip(_scroll).Take(7).ToArray();
        for (int i = 0; i < shown.Length; i++)
        {
            var row = shown[i];
            int y = 220 + i * 52;
            bool selected = ReferenceEquals(_selected, row.Record);
            if (selected)
            {
                var bar = new Rectangle(51, y + 1, 1265, 50);
                FillGradient(g, bar, Color.FromArgb(5, 132, 182),
                    Color.FromArgb(0, 76, 112), 5);
                using var glow = new Pen(Color.FromArgb(96, Cyan), 5);
                using var shape = Round(bar, 5);
                g.DrawPath(glow, shape);
                Stroke(g, bar, Cyan, 5, 1.7f);
            }
            else
            {
                using var divider = new Pen(Color.FromArgb(80, 112, 147), 1);
                g.DrawLine(divider, 51, y + 51, 1315, y + 51);
            }
            string initials = row.EmployerName;
            if (_logos.TryGetValue(initials, out var logo) && logo is not null)
                g.DrawImage(logo, new Rectangle(85, y + 7, 85, 37));
            else
                Caption(g, initials, 85, y + 5, 94, 40, 21, true,
                    row.Status == "Expired" ? White : Cyan, italic: true);
            Caption(g, initials, 177, y + 4, 121, 41, 15, true);
            Caption(g, row.ContractType, 340, y + 4, 190, 41, 15);
            using (var dot = new SolidBrush(row.Status == "Active"
                ? Color.FromArgb(33, 240, 54) : Color.FromArgb(236, 241, 245)))
                g.FillEllipse(dot, 566, y + 18, 16, 16);
            Caption(g, row.Status, 595, y + 4, 112, 41, 15);
            Caption(g, row.Begins, 734, y + 4, 142, 41, 15);
            Caption(g, row.Expires, 903, y + 4, 135, 41, 15);
            Caption(g, row.Role, 1073, y + 4, 242, 41, 14.5f);
        }
        var footer = new Rectangle(50, 609, 1267, 65);
        FillGradient(g, footer, Color.FromArgb(156, 9, 59, 98),
            Color.FromArgb(177, 3, 27, 53), 10);
        Stroke(g, footer, Color.FromArgb(0, 171, 227), 10);
        using (var accent = new Pen(Cyan, 4))
            g.DrawLine(accent, 68, 626, 68, 655);
        int total = _records.Rows.Count;
        int active = _records.Rows.Cast<DataRow>().Count(r =>
        {
            var last = Date(r, "ExpirationDate");
            return !last.HasValue || last.Value.Date >= DateTime.Today;
        });
        Caption(g, total + (total == 1 ? " contract" : " contracts"),
            85, 626, 153, 34, 17, true);
        Caption(g, "(" + active + " active, " + (total - active) + " expired)",
            228, 626, 280, 34, 15, false, White, italic: true);
    }

    private void PaintEditor(Graphics g)
    {
        PanelFrame(g, new Rectangle(25, 8, 1316, 133));
        Caption(g, "Employee", 179, 23, 87, 25, 10.4f, true);
        Caption(g, "Employer", 471, 23, 81, 25, 10.4f, true);
        Caption(g, "Name", 179, 62, 87, 25, 10.4f, true);
        Caption(g, "Short Name", 179, 97, 87, 25, 9.1f, true);
        Caption(g, "Biography", 732, 23, 84, 25, 10.4f, true);
        FillGradient(g, new Rectangle(46, 17, 113, 119),
            Color.FromArgb(22, 43, 61), Color.Black, 4);
        Stroke(g, new Rectangle(46, 17, 113, 119), Cyan, 4);
        Caption(g, _workerName.ToUpperInvariant(),
            48, 116, 110, 18, 9f, true, White,
            alignment: ContentAlignment.MiddleCenter);
        if (_portrait is not null)
            g.DrawImage(_portrait, new Rectangle(48, 19, 109, 96));
        else
            DrawHeadPortrait(g, new Rectangle(62, 28, 79, 83));

        int[] xs = [25, 366, 706, 1055];
        int[] widths = [330, 329, 335, 286];
        for (int i = 0; i < 4; i++)
        {
            var card = new Rectangle(xs[i], 155, widths[i], 426);
            PanelFrame(g, card);
            SectionBand(g, new Rectangle(xs[i] + 1, 156, widths[i] - 2, 43),
                new[] { "Basic Details", "Roster Details",
                    "Terms, Perks & Bonuses", "Roles" }[i]);
        }

        foreach (var label in _labels)
        {
            if (label.Column.StartsWith("_", StringComparison.Ordinal) ||
                label.Column is "Name" or "ShortName") continue;
            Caption(g, label.Label, label.LabelX, label.Y, label.InputX - label.LabelX - 5,
                25, 9.7f, true, White);
        }

        PanelFrame(g, new Rectangle(25, 596, 1316, 98));
        using (var pen = new Pen(Color.FromArgb(64, 114, 170), 1))
            g.DrawLine(pen, 35, 643, 934, 643);
        Caption(g, "Import Details From Alter Ego", 52, 606, 299, 29, 10.9f, true);
        Caption(g, "Copy Terms From Another Contract", 52, 650, 299, 29, 10.6f, true);
    }

    private static void DrawHeadPortrait(Graphics g, Rectangle rect)
    {
        using var shadow = new SolidBrush(Color.FromArgb(55, 65, 75));
        using var light = new SolidBrush(Color.FromArgb(139, 145, 152));
        using var dark = new SolidBrush(Color.FromArgb(20, 23, 28));
        g.FillEllipse(shadow, rect.Left + 20, rect.Top + 1, 40, 53);
        g.FillEllipse(light, rect.Left + 23, rect.Top + 5, 34, 48);
        g.FillEllipse(dark, rect.Left + 9, rect.Bottom - 27, 66, 53);
        using var pen = new Pen(Color.FromArgb(70, 80, 87), 2);
        g.DrawArc(pen, rect.Left + 24, rect.Top + 7, 30, 44, 0, 180);
    }

    private sealed class ContractText : UserControl
    {
        internal TextBox Input { get; }
        private readonly bool _searchIcon;
        private readonly string _placeholder;

        internal ContractText(bool multiline, string placeholder)
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                ControlStyles.OptimizedDoubleBuffer, true);
            BackColor = Color.FromArgb(0, 21, 37);
            _searchIcon = !multiline && placeholder.Length > 0;
            _placeholder = placeholder;
            Cursor = _searchIcon ? Cursors.IBeam : Cursors.Default;
            Input = new TextBox
            {
                BorderStyle = BorderStyle.None,
                Multiline = multiline,
                ScrollBars = multiline ? ScrollBars.Vertical : ScrollBars.None,
                Font = new Font("Segoe UI", multiline ? 10 : 11, FontStyle.Regular),
                ForeColor = White, BackColor = Color.FromArgb(4, 27, 48),
                PlaceholderText = placeholder,
                Visible = !_searchIcon
            };
            Controls.Add(Input);
            if (_searchIcon)
            {
                Click += (_, _) =>
                {
                    Input.Visible = true;
                    Input.Focus();
                    Invalidate();
                };
                Input.LostFocus += (_, _) =>
                {
                    if (Input.TextLength == 0)
                    {
                        Input.Visible = false;
                        Invalidate();
                    }
                };
            }
            Resize += (_, _) =>
            {
                int start = _searchIcon ? 45 : 10;
                Input.Bounds = new Rectangle(start, multiline ? 7 : 4,
                    Math.Max(1, Width - start - 10),
                    Math.Max(1, Height - (multiline ? 11 : 7)));
            };
            Input.TextChanged += (_, _) => Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var r = new Rectangle(1, 1, Math.Max(2, Width - 3),
                Math.Max(2, Height - 3));
            FillGradient(e.Graphics, r, Color.FromArgb(18, 52, 83),
                Color.FromArgb(1, 21, 40), 5);
            Stroke(e.Graphics, r, Color.FromArgb(139, 185, 225), 5, 1.2f);
            if (_searchIcon)
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using var searchPen = new Pen(White, 2.2f)
                {
                    StartCap = LineCap.Round, EndCap = LineCap.Round
                };
                e.Graphics.DrawEllipse(searchPen, 16, Height / 2 - 10, 17, 17);
                e.Graphics.DrawLine(searchPen, 29, Height / 2 + 4,
                    36, Height / 2 + 11);
                if (!Input.Visible && Input.TextLength == 0)
                    Caption(e.Graphics, _placeholder, 45, 3, Math.Max(1, Width - 52),
                        Math.Max(1, Height - 7), 10.4f, false, Dim, italic: true);
            }
        }
    }

    /// <summary>
    /// One unified entry treatment for all contract fields. Text editing,
    /// pick lists and typed numeric/date fields share identical blue-glass
    /// surfaces. The right arrow opens known MDB choices; it is never a
    /// separate opaque native button or a required input method.
    /// </summary>
    private sealed class DarkDrop : UserControl
    {
        private readonly SkinPalette _palette;
        private readonly TextBox _input;
        private ContextMenuStrip? _menu;
        private string[] _choices = [];

        internal string Value => _input.Text;
        internal TextBox Input => _input;
        internal event Action<string>? ValueChanged;

        internal DarkDrop(SkinPalette palette)
        {
            _palette = palette;
            SetStyle(ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.ResizeRedraw, true);
            BackColor = Color.FromArgb(18, 49, 78);
            ForeColor = White;
            Font = new Font("Segoe UI", 9.6f, FontStyle.Bold);
            TabStop = false;
            Cursor = Cursors.IBeam;
            _input = new TextBox
            {
                Name = "EditableValue",
                BorderStyle = BorderStyle.None,
                Multiline = false,
                ForeColor = White,
                BackColor = Color.FromArgb(19, 51, 80),
                Font = Font,
                TabStop = true,
                ShortcutsEnabled = true
            };
            Controls.Add(_input);
            _input.TextChanged += (_, _) => ValueChanged?.Invoke(_input.Text);
            Resize += (_, _) => PositionInput();
            EnabledChanged += (_, _) =>
            {
                _input.ReadOnly = !Enabled;
                _input.ForeColor = Enabled ? White : Dim;
                Invalidate();
            };
            Disposed += (_, _) =>
            {
                _menu?.Dispose();
                _input.Font = Control.DefaultFont;
            };
            PositionInput();
        }

        private void PositionInput()
        {
            int rightPadding = _choices.Length > 0 ? 27 : 7;
            _input.Bounds = new Rectangle(8, Math.Max(2, (Height - 19) / 2),
                Math.Max(1, Width - 8 - rightPadding), 19);
        }

        internal void SetChoices(IEnumerable<string> choices, string value)
        {
            _choices = choices.Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            SetValue(value);
            PositionInput();
            Invalidate();
        }

        internal void SetValue(string value)
        {
            _input.Text = value;
            Invalidate();
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (!Enabled || e.Button != MouseButtons.Left) return;
            if (_choices.Length > 0 && e.X >= Width - 31)
                ShowChoices();
            else
                _input.Focus();
        }

        private void ShowChoices()
        {
            if (!Enabled || _choices.Length == 0) return;
            _menu ??= new ContextMenuStrip
            {
                BackColor = Color.FromArgb(17, 45, 74),
                ForeColor = White,
                ShowImageMargin = false,
                Renderer = new ToolStripProfessionalRenderer(
                    new WorkerMenuColorTable(_palette))
            };
            _menu.Items.Clear();
            foreach (string choice in _choices)
            {
                string selection = choice;
                var item = new ToolStripMenuItem(selection);
                item.Click += (_, _) =>
                {
                    SetValue(selection);
                    _input.Focus();
                    _input.SelectionStart = _input.TextLength;
                };
                _menu.Items.Add(item);
            }
            _menu.Show(this, new Point(0, Height));
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (Enabled && _choices.Length > 0 &&
                keyData is (Keys.Alt | Keys.Down) or Keys.F4)
            {
                ShowChoices();
                return true;
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var rect = new Rectangle(1, 1, Math.Max(2, Width - 3),
                Math.Max(2, Height - 3));
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            FillGradient(e.Graphics, rect, Color.FromArgb(34, 81, 115),
                Color.FromArgb(16, 48, 80), 5);
            Stroke(e.Graphics, rect,
                Enabled ? Color.FromArgb(151, 194, 222) :
                    Color.FromArgb(83, 125, 151), 5, 1.1f);
            if (_choices.Length == 0) return;
            int x = Width - 15, y = Height / 2;
            using var chevron = new SolidBrush(
                Enabled ? Color.FromArgb(232, 241, 250) : Dim);
            e.Graphics.FillPolygon(chevron,
                [new Point(x - 5, y - 2), new Point(x + 5, y - 2),
                 new Point(x, y + 3)]);
        }
    }

    private sealed class RoleToggle : Control
    {
        [System.ComponentModel.DesignerSerializationVisibility(
            System.ComponentModel.DesignerSerializationVisibility.Hidden)]
        internal bool Checked { get; set; }
        internal RoleToggle(string title)
        {
            Text = title;
            TabStop = true;
            Cursor = Cursors.Hand;
            SetStyle(ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
        }
        protected override void OnClick(EventArgs e)
        {
            Checked = !Checked;
            Invalidate();
            base.OnClick(e);
        }
        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Space)
            {
                OnClick(EventArgs.Empty);
                e.Handled = true;
            }
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            FillGradient(g, new Rectangle(2, 3, 26, 24), Color.FromArgb(12, 43, 65),
                Color.FromArgb(0, 19, 36), 4);
            Stroke(g, new Rectangle(2, 3, 26, 24), Checked ? Cyan : Dim,
                4, Checked ? 2.5f : 1.7f);
            if (Checked)
            {
                using var pen = new Pen(White, 3f) { StartCap = LineCap.Round,
                    EndCap = LineCap.Round };
                g.DrawLines(pen, [new Point(7, 15), new Point(12, 20),
                    new Point(23, 8)]);
            }
            Caption(g, Text, 46, 0, Math.Max(1, Width - 48), Height, 10.1f, true);
        }
    }

    /// <summary>
    /// Deliberately restrained native-feeling action surface. No icons,
    /// extraneous focus rectangles or opaque black button backing.
    /// </summary>
    private sealed class PwrContractButton : Control
    {
        private readonly bool _primary;
        private bool _hover;

        internal PwrContractButton(string title, bool primary)
        {
            _primary = primary;
            Text = title;
            AccessibleRole = AccessibleRole.PushButton;
            AccessibleName = title;
            TabStop = true;
            Cursor = Cursors.Hand;
            ForeColor = White;
            BackColor = Color.Transparent;
            Font = new Font("Segoe UI", 10.6f, FontStyle.Bold);
            SetStyle(ControlStyles.Selectable | ControlStyles.UserPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.SupportsTransparentBackColor, true);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (!Enabled || e.KeyCode is not (Keys.Enter or Keys.Space)) return;
            OnClick(EventArgs.Empty);
            e.Handled = true;
            e.SuppressKeyPress = true;
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
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            var rect = new Rectangle(2, 2, Math.Max(2, Width - 5),
                Math.Max(2, Height - 5));
            Color upper = _primary
                ? Color.FromArgb(30, 121, 179) :
                  _hover ? Color.FromArgb(35, 90, 133) :
                           Color.FromArgb(26, 66, 101);
            Color lower = _primary
                ? Color.FromArgb(8, 64, 112) :
                  _hover ? Color.FromArgb(11, 48, 82) :
                           Color.FromArgb(10, 33, 57);
            FillGradient(g, rect, upper, lower, 5);
            Stroke(g, rect,
                _primary ? Color.FromArgb(122, 223, 242) :
                    Color.FromArgb(139, 184, 218), 5, 1.25f);
            Caption(g, Text, 7, 2, Math.Max(1, Width - 14),
                Math.Max(1, Height - 4), 10.6f, true,
                Enabled ? White : Dim,
                alignment: ContentAlignment.MiddleCenter);
        }
    }
}
