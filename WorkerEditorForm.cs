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
internal sealed class WorkerEditorForm : Form
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
    private readonly Panel _content = new() { Name = "WorkerSectionContent", Dock = DockStyle.Fill };
    private readonly Panel _sectionNavigation = new() { Name = "WorkerSectionNavigation", Dock = DockStyle.Left, Width = 158 };
    private readonly Panel _mainRail = new() { Name = "WorkerMainMenuRail", Dock = DockStyle.Left, Width = SkinArt.SidebarWidth };
    private readonly TableLayoutPanel _body = new() { Name = "WorkerEditorBody", Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3 };
    private readonly ToolTip _tips = new() { InitialDelay = 350, ReshowDelay = 80 };
    private readonly Font _railRegular = new("Segoe UI", 10.5f);
    private readonly Font _railBold = new("Segoe UI", 10.5f, FontStyle.Bold);
    private string _active = "General";
    private int _recordIndex;

    internal string? RequestedNavigation { get; private set; }

    internal WorkerEditorForm(AppPaths paths, SkinPalette palette, PrwWorkerProfile profile)
    {
        _paths = paths;
        _palette = palette;
        _profile = profile;

        Name = "WorkerEditorForm";
        Text = "Pro Wrestling Revolution";
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(SkinArt.WindowWidth, SkinArt.WindowHeight);
        AutoScaleMode = AutoScaleMode.None;
        Font = new Font("Segoe UI", 9f);
        BackColor = Ink;
        ForeColor = TextInk;
        DoubleBuffered = true;
        KeyPreview = true;
        KeyDown += (_, e) =>
        {
            if (e.KeyCode != Keys.Escape) return;
            DialogResult = DialogResult.Cancel;
            Close();
            e.Handled = true;
        };

        BuildFrame();
        ShowSection("General");
        Disposed += (_, _) =>
        {
            _tips.Dispose();
            _railRegular.Dispose();
            _railBold.Dispose();
        };
    }

    internal void FillLauncherWindow(Form launcher)
    {
        StartPosition = FormStartPosition.Manual;
        MinimumSize = Size.Empty;
        MaximumSize = Size.Empty;
        Bounds = launcher.Bounds;
    }

    private static Panel Card(string title, string name)
    {
        var card = new Panel
        {
            Name = name,
            Dock = DockStyle.Fill,
            Margin = new Padding(3),
            BackColor = Dark
        };
        card.Paint += (_, e) =>
        {
            using var pen = new Pen(Edge);
            e.Graphics.DrawRectangle(pen, 0, 0, card.Width - 1, card.Height - 1);
        };
        var heading = new Panel
        {
            Dock = DockStyle.Top, Height = 27,
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
        var label = new Label
        {
            Text = title.ToUpperInvariant(),
            Dock = DockStyle.Fill,
            Padding = new Padding(11, 0, 0, 0),
            TextAlign = ContentAlignment.MiddleLeft,
            Font = new Font("Segoe UI", 9.1f, FontStyle.Bold),
            ForeColor = Color.White,
            BackColor = Color.Transparent
        };
        heading.Controls.Add(label);
        card.Controls.Add(heading);
        return card;
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

    private static TextBox ValueBox(string value, string name) => new()
    {
        Name = name,
        Text = value,
        Dock = DockStyle.Fill,
        Margin = new Padding(2, 1, 5, 1),
        BorderStyle = BorderStyle.FixedSingle,
        ReadOnly = true,
        TabStop = false,
        ForeColor = Color.FromArgb(22, 37, 49),
        BackColor = FieldInk,
        Font = new Font("Segoe UI", 8.6f, FontStyle.Bold)
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
        var chrome = new Panel { Dock = DockStyle.Top, Height = 42, Name = "WorkerEditorHeader", BackColor = Surface };
        chrome.Paint += (_, e) =>
        {
            var bounds = new Rectangle(0, 0, Math.Max(1, chrome.Width), Math.Max(1, chrome.Height));
            using var gradient = new LinearGradientBrush(bounds,
                Color.FromArgb(13, 30, 48), Color.FromArgb(28, 61, 95), LinearGradientMode.Horizontal);
            e.Graphics.FillRectangle(gradient, bounds);
            using var line = new Pen(Accent);
            e.Graphics.DrawLine(line, 0, chrome.Height - 1, chrome.Width, chrome.Height - 1);
        };
        var logo = new Label
        {
            Text = "PWR  /  PRO WRESTLING REVOLUTION",
            Dock = DockStyle.Left, Width = 315,
            Padding = new Padding(16, 0, 0, 0),
            TextAlign = ContentAlignment.MiddleLeft,
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 11f, FontStyle.Bold),
            BackColor = Color.Transparent
        };
        var heading = new Label
        {
            Text = "WORKER EDITOR    /    EDIT WORKER",
            Dock = DockStyle.Left, Width = 430,
            TextAlign = ContentAlignment.MiddleLeft,
            Font = new Font("Segoe UI", 11.5f, FontStyle.Bold),
            ForeColor = Color.White, BackColor = Color.Transparent
        };
        var close = new Button
        {
            Name = "WorkerClose",
            Dock = DockStyle.Right, Width = 46,
            Text = "×", Font = new Font("Segoe UI", 18, FontStyle.Bold),
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(25, 42, 65), ForeColor = Color.White
        };
        close.FlatAppearance.BorderSize = 0;
        close.Click += (_, _) => Close();
        chrome.Controls.Add(heading);
        chrome.Controls.Add(logo);
        chrome.Controls.Add(close);
        Controls.Add(chrome);

        _mainRail.BackColor = Color.FromArgb(10, 22, 35);
        var railTooltips = _tips;
        foreach (var (key, label, y) in SkinArt.MenuIcons)
        {
            string caption = key switch
            {
                "back" => "Home",
                "database" => "Database",
                "editor" => "Editor",
                _ => label
            };
            var item = new SidebarMenuRow(key, caption, _palette, _railRegular, _railBold)
            {
                Name = "WorkerMenu_" + key,
                IconOnly = true,
                Width = SkinArt.SidebarWidth,
                Location = new Point(0, Math.Max(0, y - 67))
            };
            item.UpdateAppearance(_palette, false, key == "editor");
            item.Click += (_, _) =>
            {
                RequestedNavigation = key;
                Close();
            };
            railTooltips.SetToolTip(item, label);
            _mainRail.Controls.Add(item);
        }
        Controls.Add(_mainRail);

        _sectionNavigation.BackColor = Color.FromArgb(11, 23, 38);
        string[] sections =
        [
            "General", "Contracts", "Skills", "Popularity", "Attributes",
            "Business Skills", "Job History", "Business History", "Legacy", "Alter Ego"
        ];
        for (int i = 0; i < sections.Length; i++)
        {
            string section = sections[i];
            var button = new Button
            {
                Name = "WorkerSection_" + section.Replace(" ", ""),
                Text = section,
                Location = new Point(7, 14 + i * 47),
                Size = new Size(144, 40),
                FlatStyle = FlatStyle.Flat,
                BackColor = Surface,
                ForeColor = TextInk,
                Font = new Font("Segoe UI", 8.7f, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            button.FlatAppearance.BorderColor = Edge;
            button.FlatAppearance.MouseOverBackColor = Color.FromArgb(28, 75, 112);
            button.Click += (_, _) => ShowSection(section);
            _tabs[section] = button;
            _sectionNavigation.Controls.Add(button);
        }
        Controls.Add(_sectionNavigation);

        _body.BackColor = Ink;
        _body.Padding = new Padding(4, 8, 7, 5);
        _body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        _body.RowStyles.Add(new RowStyle(SizeType.Absolute, 162));
        _body.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        _body.RowStyles.Add(new RowStyle(SizeType.Absolute, 53));
        Controls.Add(_body);
        // Keep the top chrome, main-menu icons and section buttons visible
        // while the remaining space is reserved for the worker fields.
        Controls.SetChildIndex(chrome, 0);
        Controls.SetChildIndex(_mainRail, 2);
        Controls.SetChildIndex(_sectionNavigation, 1);
        Controls.SetChildIndex(_body, 3);

        var top = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            Margin = Padding.Empty
        };
        top.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 37));
        top.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 63));
        top.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        BuildIdentity(top);
        BuildBiography(top);
        _body.Controls.Add(top, 0, 0);
        _body.Controls.Add(_content, 0, 1);
        BuildActions();
    }

    private void BuildIdentity(TableLayoutPanel top)
    {
        var card = new Panel
        {
            Name = "WorkerIdentity",
            Dock = DockStyle.Fill,
            Margin = new Padding(3),
            BackColor = Surface
        };
        card.Paint += (_, e) =>
        {
            using var pen = new Pen(Edge);
            e.Graphics.DrawRectangle(pen, 0, 0, card.Width - 1, card.Height - 1);
        };
        top.Controls.Add(card, 0, 0);
        var worker = First("tblWorker");

        var name = new Label
        {
            Name = "WorkerNameTitle",
            Text = "WORKER DETAILS",
            Location = new Point(15, 7), Size = new Size(420, 32),
            ForeColor = Color.White, AutoEllipsis = true,
            Font = new Font("Segoe UI", 14.5f, FontStyle.Bold | FontStyle.Italic)
        };
        card.Controls.Add(name);

        var portrait = new PictureBox
        {
            Name = "WorkerPicture", Location = new Point(15, 49),
            Size = new Size(85, 101), BackColor = Color.FromArgb(21, 41, 61),
            BorderStyle = BorderStyle.FixedSingle, SizeMode = PictureBoxSizeMode.Zoom
        };
        card.Controls.Add(portrait);
        string picture = Display(worker, "Picture");
        var safeName = Path.GetFileName(picture);
        if (!string.IsNullOrWhiteSpace(safeName))
        {
            string[] choices =
            [
                Path.Combine(_paths.Pictures, safeName),
                Path.Combine(_paths.Pictures, "Workers", safeName),
                Path.Combine(_paths.Pictures, "People", safeName),
                Path.Combine(_paths.Pictures, "Worker", safeName)
            ];
            var file = choices.FirstOrDefault(File.Exists);
            if (file is not null)
            {
                try
                {
                    using var stream = File.OpenRead(file);
                    using var image = Image.FromStream(stream);
                    portrait.Image = new Bitmap(image);
                    portrait.Disposed += (_, _) => portrait.Image?.Dispose();
                }
                catch (Exception ex) when (ex is IOException or ArgumentException or OutOfMemoryException)
                {
                    // A missing or invalid picture never prevents opening a worker.
                }
            }
        }
        if (portrait.Image is null)
        {
            var noImage = new Label
            {
                Text = "NO IMAGE",
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter,
                ForeColor = Color.FromArgb(143, 181, 205),
                Font = new Font("Segoe UI", 8, FontStyle.Bold)
            };
            portrait.Controls.Add(noImage);
        }

        var fields = new TableLayoutPanel
        {
            Name = "WorkerIdentityFields", Location = new Point(106, 48),
            Size = new Size(320, 105),
            RowCount = 3, ColumnCount = 2
        };
        fields.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 94));
        fields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        for (int i = 0; i < 3; i++) fields.RowStyles.Add(new RowStyle(SizeType.Percent, 100f / 3));
        fields.Controls.Add(TextLabel("Name"), 0, 0);
        fields.Controls.Add(ValueBox(Display(worker, "Name"), "WorkerField_Name"), 1, 0);
        fields.Controls.Add(TextLabel("Short Name"), 0, 1);
        fields.Controls.Add(ValueBox(Display(worker, "ShortName"), "WorkerField_ShortName"), 1, 1);
        fields.Controls.Add(TextLabel("Bio Type"), 0, 2);
        fields.Controls.Add(ValueBox(Display(worker, "BiographyType"), "WorkerField_BiographyType"), 1, 2);
        card.Controls.Add(fields);
        card.Resize += (_, _) =>
        {
            name.Width = Math.Max(200, card.Width - 30);
            fields.Width = Math.Max(180, card.Width - 118);
        };
    }

    private void BuildBiography(TableLayoutPanel top)
    {
        var card = Card("Biography", "WorkerBiographyPanel");
        top.Controls.Add(card, 1, 0);
        var bio = new RichTextBox
        {
            Name = "WorkerBiography",
            Dock = DockStyle.Fill,
            BorderStyle = BorderStyle.None,
            Margin = new Padding(11, 9, 10, 8),
            Font = new Font("Segoe UI", 8.65f, FontStyle.Regular),
            ReadOnly = true,
            DetectUrls = false,
            TabStop = false,
            ScrollBars = RichTextBoxScrollBars.None,
            BackColor = FieldInk,
            ForeColor = Color.FromArgb(26, 39, 53),
            Text = Display(First("tblWorkerBio"), "Biography")
        };
        card.Controls.Add(bio);
        card.Controls.Find(card.Name + "Heading", false).First().BringToFront();
    }

    private static (string Label, string Field)[] Pairs(params string[] fields) =>
        fields.Select(x => (x.Replace("ID", " ID").Replace("Pounds", " Weight").Replace("Inches", "").Replace("WorkerUID", " Worker"), x)).ToArray();

    private static TableLayoutPanel MakeRows(Panel card,
        IReadOnlyList<(string Label, string Field)> fields, DataRow? source)
    {
        var grid = new TableLayoutPanel
        {
            Name = card.Name + "Fields",
            Dock = DockStyle.Fill, ColumnCount = 2, RowCount = fields.Count,
            Padding = new Padding(2, 4, 2, 5), Margin = Padding.Empty,
            BackColor = Dark
        };
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 46));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 54));
        for (int i = 0; i < fields.Count; i++)
        {
            grid.RowStyles.Add(new RowStyle(SizeType.Percent, 100f / Math.Max(1, fields.Count)));
            grid.Controls.Add(TextLabel(fields[i].Label), 0, i);
            grid.Controls.Add(ValueBox(Display(source, fields[i].Field),
                "WorkerField_" + fields[i].Field), 1, i);
        }
        card.Controls.Add(grid);
        card.Controls.Find(card.Name + "Heading", false).First().BringToFront();
        return grid;
    }

    private static TableLayoutPanel MakeChecks(Panel card,
        IReadOnlyList<(string Label, string Field)> fields, DataRow? source)
    {
        var grid = new TableLayoutPanel
        {
            Name = card.Name + "Fields",
            Dock = DockStyle.Fill, ColumnCount = 1, RowCount = fields.Count,
            Padding = new Padding(9, 4, 2, 5), Margin = Padding.Empty
        };
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        for (int i = 0; i < fields.Count; i++)
        {
            var (label, field) = fields[i];
            grid.RowStyles.Add(new RowStyle(SizeType.Percent, 100f / Math.Max(1, fields.Count)));
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
                Font = new Font("Segoe UI", 8.8f, FontStyle.Regular)
            };
            grid.Controls.Add(tick, 0, i);
        }
        card.Controls.Add(grid);
        card.Controls.Find(card.Name + "Heading", false).First().BringToFront();
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
        var grid = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, Name = "WorkerGeneralGrid",
            ColumnCount = 4, RowCount = 1, Margin = Padding.Empty
        };
        foreach (float percent in new[] { 31f, 26f, 25f, 18f })
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, percent));
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
        ], worker);
        grid.Controls.Add(personal, 0, 0);

        var loyalty = Card("Loyalty", "WorkerLoyalty");
        MakeRows(loyalty, [
            ("Loyalty", "LoyaltyWorkerUID"), ("Loyalty Type", "LoyaltyType"),
            ("Freelance", "Freelance"), ("User Character", "UserCharacter")
        ], worker);
        var roles = Card("Roles", "WorkerRoles");
        MakeChecks(roles, [
            ("Wrestler", "RoleWrestler"), ("Occasional Wrestler", "RolePartTimeWrestler"),
            ("Manager", "RoleManager"), ("On-Screen Personality", "RoleOnScreenPersonality"),
            ("Play By Play", "RolePlayByPlay"), ("Colour Commentator", "RoleColour"),
            ("Referee", "RoleReferee"), ("Road Agent", "RoleProducer")
        ], worker);
        var physical = Card("Physical Health", "WorkerPhysical");
        MakeRows(physical, [
            ("Head Condition", "HeadCondition"), ("Body Condition", "BodyCondition"),
            ("Arms Condition", "ArmsCondition"), ("Legs Condition", "LegsCondition")
        ], First("tblWorkerPhysical"));
        grid.Controls.Add(Stack((loyalty, 27), (roles, 46), (physical, 27)), 1, 0);

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
        ], worker);
        grid.Controls.Add(settings, 2, 0);

        var regions = Card("Active Areas", "WorkerActiveAreas");
        MakeChecks(regions, [
            ("USA", "ActiveUSA"), ("Canada", "ActiveCanada"),
            ("Mexico", "ActiveMexico"), ("British Isles", "ActiveBritishIsles"),
            ("Japan", "ActiveJapan"), ("Europe", "ActiveEurope"),
            ("Oceania", "ActiveOceania"), ("India", "ActiveIndia")
        ], worker);
        var languages = Card("Languages", "WorkerLanguages");
        MakeRows(languages, [
            ("English", "LanguageEnglish"), ("Japanese", "LanguageJapanese"),
            ("French", "LanguageFrench"), ("Spanish", "LanguageSpanish"),
            ("Mediterranean", "LanguageMediterranean"),
            ("Germanic", "LanguageGermanic"), ("Slavic", "LanguageSlavic"),
            ("Hindi", "LanguageHindi")
        ], worker);
        grid.Controls.Add(Stack((regions, 52), (languages, 48)), 3, 0);
        _content.Controls.Add(grid);
    }

    private void ShowSection(string section)
    {
        _active = section;
        _recordIndex = 0;
        foreach (var (name, button) in _tabs)
        {
            bool selected = name.Equals(section, StringComparison.OrdinalIgnoreCase);
            button.BackColor = selected ? Color.FromArgb(13, 107, 169) : Surface;
            button.FlatAppearance.BorderColor = selected ? Accent : Edge;
            button.ForeColor = Color.White;
        }
        _content.SuspendLayout();
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

    private static Button ActionButton(string label, string name, bool primary = false, int width = 148)
    {
        var button = new Button
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
        var footer = new Panel
        {
            Name = "WorkerEditorActions",
            Dock = DockStyle.Fill,
            Margin = new Padding(3, 2, 3, 2),
            BackColor = Surface
        };
        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Right, Width = 674,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Padding = new Padding(0, 6, 0, 0),
            AutoScroll = false
        };
        var moveset = ActionButton("Create Moveset", "WorkerCreateMoveset", width: 146);
        var suggest = ActionButton("Suggest Name", "WorkerSuggestName", width: 137);
        var biography = ActionButton("Make Biography", "WorkerMakeBiography", width: 152);
        var save = ActionButton("Save", "WorkerSave", true, width: 154);
        foreach (var button in new[] { moveset, suggest, biography, save })
        {
            // Workflow/back-end integration belongs to the separate database
            // implementation. Never claim that a read-only preview was saved.
            button.Enabled = false;
            _tips.SetToolTip(button, "Editing actions will be enabled when worker record saving is connected.");
            buttons.Controls.Add(button);
        }
        footer.Controls.Add(buttons);
        var status = new Label
        {
            Text = "VIEWING WORKER RECORD   •   Editing not yet connected",
            Dock = DockStyle.Fill, Padding = new Padding(12, 0, 0, 0),
            TextAlign = ContentAlignment.MiddleLeft,
            ForeColor = Color.FromArgb(168, 197, 218),
            Font = new Font("Segoe UI", 8.5f)
        };
        footer.Controls.Add(status);
        status.SendToBack();
        _body.Controls.Add(footer, 0, 2);
    }
}
