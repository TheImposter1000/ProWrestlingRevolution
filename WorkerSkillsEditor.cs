using System.Data;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Windows.Forms;

namespace ProWrestlingRevolution;

/// <summary>
/// Dedicated worker Skills workspace. This is intentionally a view over
/// tblWorkerSkills, not a second database model. The existing Worker Editor
/// owns navigation, save transactions, and unsaved-change handling.
/// </summary>
internal sealed class WorkerSkillsEditor : UserControl
{
    internal sealed record Skill(string Label, string Column);
    internal sealed record SkillGroup(string Title, string Description, string Icon,
        IReadOnlyList<Skill> Skills);

    internal static readonly IReadOnlyList<SkillGroup> Groups =
    [
        new("In-Ring", "Core in-ring abilities and wrestling style.", "▤",
        [
            new("Brawling", "Brawling"), new("Puroresu", "Puroresu"),
            new("Hardcore", "Hardcore"), new("Technical", "Technical"),
            new("Aerial", "Aerial"), new("Flashiness", "Flashiness"),
            new("Psychology", "Psychology"), new("Experience", "Experience")
        ]),
        new("Entertainment & Presence", "Character, popularity, and audience appeal.", "♬",
        [
            new("Charisma", "Charisma"), new("Microphone", "Microphone"),
            new("Acting", "Acting"), new("Star Quality", "StarQuality"),
            new("Sex Appeal", "SexAppeal"), new("Menace", "Menace"),
            new("Respect", "Respect"), new("Reputation", "Reputation")
        ]),
        new("Fundamentals & Physical", "Wrestling fundamentals and physical abilities.", "✦",
        [
            new("Basics", "Basics"), new("Selling", "Selling"),
            new("Consistency", "Consistency"), new("Safety", "Safety"),
            new("Stamina", "Stamina"), new("Athleticism", "Athleticism"),
            new("Power", "Power"), new("Toughness", "Toughness"),
            new("Resilience", "Resilience")
        ]),
        new("Commentary / Officiating / Potential",
            "Broadcasting, officiating, and development potential.", "▤",
        [
            new("Play By Play", "PlayByPlay"), new("Colour", "Colour"),
            new("Refereeing", "Refereeing"),
            new("Potential Primary", "PotentialPrimary"),
            new("Potential Mental", "PotentialMental"),
            new("Potential Performance", "PotentialPerformance"),
            new("Potential Fundamentals", "PotentialFundamentals"),
            new("Potential Physical", "PotentialPhysical"),
            new("Potential Play By Play", "PotentialPlayByPlay"),
            new("Potential Colour", "PotentialColour"),
            new("Potential Refereeing", "PotentialRefereeing")
        ])
    ];

    private static readonly Color Canvas = Color.FromArgb(7, 19, 33);
    private static readonly Color PanelInk = Color.FromArgb(8, 24, 43);
    private static readonly Color Edge = Color.FromArgb(0, 207, 255);
    private static readonly Color Fore = Color.FromArgb(235, 245, 252);

    private readonly PrwWorkerProfile _profile;
    private readonly Action _save;
    private readonly Dictionary<string, object> _original = new(StringComparer.Ordinal);
    private readonly Dictionary<string, SkillRatingRow> _rows = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _invalid = new(StringComparer.Ordinal);
    private readonly Label _status = new()
    {
        Name = "WorkerSkillsSaveStatus", Dock = DockStyle.Fill,
        ForeColor = Color.FromArgb(255, 159, 160),
        TextAlign = ContentAlignment.MiddleLeft, AutoEllipsis = true
    };
    private DataRow? _currentRow;
    private readonly DataTable? _table;
    private readonly bool _canEdit;
    private bool _createdRow;
    private bool _resetting;
    private SkillChromePreset? _preset;

    internal WorkerSkillsEditor(PrwWorkerProfile profile, bool canEdit, Action save,
        IReadOnlyDictionary<string, object>? loadedValues = null)
    {
        _profile = profile;
        _save = save;
        _canEdit = canEdit;
        _table = profile.Sections.FirstOrDefault(x => x.Table == "tblWorkerSkills")?.Records;
        _currentRow = _table?.Rows.Cast<DataRow>().FirstOrDefault();

        Name = "WorkerSkillsEditor";
        Dock = DockStyle.Fill;
        BackColor = Canvas;
        ForeColor = Fore;
        Font = new Font("Segoe UI", 9f);
        DoubleBuffered = true;
        AutoScroll = true;

        foreach (var skill in Groups.SelectMany(group => group.Skills))
        {
            if (_original.ContainsKey(skill.Column))
                throw new InvalidDataException("Duplicate worker skill: " + skill.Column);
            _original.Add(skill.Column,
                loadedValues?.TryGetValue(skill.Column, out object? original) == true
                    ? original ?? DBNull.Value : GetOriginalValue(skill.Column));
        }

        Build();
    }

    // The reference screen has an atmospheric navy arena glow in the
    // negative space between the cards, rather than a flat black canvas.
    protected override void OnPaintBackground(PaintEventArgs e)
    {
        var rect = ClientRectangle;
        if (rect.Width < 2 || rect.Height < 2) return;
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        using var baseLayer = new LinearGradientBrush(rect,
            Color.FromArgb(6, 27, 51), Color.FromArgb(3, 17, 35),
            LinearGradientMode.Vertical);
        g.FillRectangle(baseLayer, rect);
        using var path = new GraphicsPath();
        path.AddEllipse(new Rectangle(
            rect.Width / 3, -rect.Height / 2,
            Math.Max(5, rect.Width), Math.Max(5, rect.Height)));
        using var glow = new PathGradientBrush(path)
        {
            CenterColor = Color.FromArgb(47, 9, 85, 146),
            SurroundColors = [Color.FromArgb(0, 9, 85, 146)]
        };
        g.FillPath(glow, path);
    }

    private object GetStoredValue(string column)
    {
        if (_currentRow is null || !_currentRow.Table.Columns.Contains(column) ||
            _currentRow[column] is DBNull)
            return DBNull.Value;
        return _currentRow[column];
    }

    internal static void AssertSchemaMapping()
    {
        var actual = Groups.SelectMany(g => g.Skills).Select(s => s.Column).ToArray();
        var expected = PrwSchema.Tables.Single(x => x.Name == "tblWorkerSkills")
            .Columns.Where(x => x != "WorkerUID").ToArray();
        if (actual.Length != 36 || actual.Distinct(StringComparer.Ordinal).Count() != 36 ||
            !actual.ToHashSet(StringComparer.Ordinal).SetEquals(expected))
            throw new InvalidDataException(
                "Skills UI must preserve all 36 native MDB columns without renaming.");
    }

    private object GetOriginalValue(string field)
    {
        if (_currentRow is null || !_currentRow.Table.Columns.Contains(field))
            return DBNull.Value;
        if (_currentRow.HasVersion(DataRowVersion.Original))
            return _currentRow[field, DataRowVersion.Original];
        return GetStoredValue(field);
    }

    internal void ShowSaveError(string message) =>
        _status.Text = "Save failed: " + message;

    internal bool HasUncommittedInvalidInput() =>
        _rows.Values.Any(row => row.ValidateNumber() is not null);

    internal bool ValidateBeforeLeaving()
    {
        var invalid = _rows.Values.Select(row => row.ValidateNumber())
            .FirstOrDefault(result => result is not null);
        if (invalid is null) return true;
        _status.Text = invalid;
        return false;
    }

    private int? InitialRating(string field)
    {
        object stored = GetStoredValue(field);
        if (stored is DBNull) return null;
        try
        {
            int integer = Convert.ToInt32(stored, CultureInfo.InvariantCulture);
            return integer is >= 0 and <= 100 ? integer : null;
        }
        catch (Exception ex) when (ex is FormatException or InvalidCastException
                                   or OverflowException)
        {
            return null;
        }
    }

    private void Build()
    {
        // The game shell already owns the PWR title bar. The approved screen
        // begins immediately with four large, illustrated skill cards.
        var layout = new TableLayoutPanel
        {
            Name = "SkillsWorkspaceLayout",
            Dock = DockStyle.Fill,
            ColumnCount = 1, RowCount = 2,
            // Reference screenshot measures 1672x941 with a 56px shell
            // header. Its body cards start at (29,82), and the action
            // strip begins at y=813. Insets reproduce those proportions.
            Padding = new Padding(19, 16, 39, 47),
            Margin = Padding.Empty,
            BackColor = Color.Transparent
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 81));

        var panels = new TableLayoutPanel
        {
            Name = "SkillsFourPanels", Dock = DockStyle.Fill,
            ColumnCount = 2, RowCount = 2,
            Margin = Padding.Empty, Padding = Padding.Empty,
            BackColor = Color.Transparent
        };
        panels.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        panels.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        panels.RowStyles.Add(new RowStyle(SizeType.Percent, 46.55f));
        panels.RowStyles.Add(new RowStyle(SizeType.Percent, 53.45f));

        for (int i = 0; i < Groups.Count; i++)
            panels.Controls.Add(CreateGroup(Groups[i], i), i % 2, i / 2);

        layout.Controls.Add(panels, 0, 0);
        layout.Controls.Add(CreateActions(), 0, 1);
        Controls.Add(layout);
    }

    private Control CreateGroup(SkillGroup group, int groupIndex)
    {
        var panel = new SkillGlassPanel
        {
            Name = "SkillPanel_" + group.Title.Replace(" ", "")
                .Replace("/", "").Replace(",", "").Replace("&", ""),
            Dock = DockStyle.Fill,
            Margin = new Padding(10, groupIndex < 2 ? 10 : 6,
                10, groupIndex < 2 ? 10 : 28),
            Padding = new Padding(3),
        };

        var inner = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1, RowCount = 2,
            Margin = Padding.Empty, Padding = Padding.Empty,
            BackColor = Color.Transparent
        };
        inner.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        inner.RowStyles.Add(new RowStyle(SizeType.Absolute, 59));
        inner.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        string icon = groupIndex switch
        {
            0 => "ring",
            1 => "microphone",
            2 => "physical",
            _ => "clipboard"
        };
        var header = new SkillGlassHeader(group.Title, group.Description, icon)
        {
            Name = "SkillsHeader_" + groupIndex,
            Dock = DockStyle.Fill,
            Margin = Padding.Empty
        };
        inner.Controls.Add(header, 0, 0);

        var rows = new FlowLayoutPanel
        {
            Name = "SkillRows_" + groupIndex,
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            Margin = Padding.Empty,
            Padding = new Padding(31, 12, 32, 5),
            BackColor = SkillChrome.Ink,
            AutoScroll = true
        };
        foreach (var skill in group.Skills)
        {
            float labelPercent = groupIndex switch
            {
                0 => 33.8f, 1 => 34.7f, 2 => 34.1f, _ => 37.6f
            };
            int railGap = groupIndex switch
            {
                0 => 29, 1 => 18, 2 => 27, _ => 8
            };
            var rating = new SkillRatingRow(skill.Label, skill.Column,
                InitialRating(skill.Column), _canEdit,
                value => UpdateSkill(skill.Column, value),
                labelPercent, railGap)
            {
                Height = group.Skills.Count > 9 ? 24 : 28,
                Margin = Padding.Empty
            };
            rows.Controls.Add(rating);
            _rows.Add(skill.Column, rating);
        }

        void PlaceRows()
        {
            int height = Math.Clamp(
                (rows.ClientSize.Height - rows.Padding.Vertical) /
                    group.Skills.Count, 22, 33);
            int width = Math.Max(150, rows.ClientSize.Width -
                rows.Padding.Horizontal -
                (rows.VerticalScroll.Visible
                    ? SystemInformation.VerticalScrollBarWidth : 0) - 2);
            foreach (Control rating in rows.Controls)
            {
                if (rating.Width != width) rating.Width = width;
                if (rating.Height != height) rating.Height = height;
            }
        }
        rows.Resize += (_, _) => PlaceRows();
        inner.Controls.Add(rows, 0, 1);
        panel.Controls.Add(inner);
        panel.Resize += (_, _) =>
        {
            int desired = panel.ClientSize.Width >= 730 ? 61 : 57;
            if (inner.RowStyles[0].Height != desired)
                inner.RowStyles[0].Height = desired;
            PlaceRows();
        };
        PlaceRows();
        return panel;
    }

    private Control CreateActions()
    {
        var footer = new SkillGlassPanel
        {
            Name = "WorkerSkillsActions", Footer = true,
            Dock = DockStyle.Fill,
            Margin = new Padding(10, 0, 10, 0),
            Padding = Padding.Empty
        };
        var presetLabel = new Label
        {
            Name = "SkillsPresetLabel",
            Text = "Preset:",
            Font = new Font("Segoe UI", 13.5f, FontStyle.Bold),
            ForeColor = SkillChrome.White,
            TextAlign = ContentAlignment.MiddleLeft,
            BackColor = Color.Transparent,
            UseMnemonic = false
        };
        _preset = new SkillChromePreset(
            "None", "Balanced (50)", "In-Ring Specialist",
            "Entertainment Specialist", "Physical Specialist",
            "Commentary Specialist")
        { Enabled = _canEdit };
        _preset.Selected += ApplyPreset;

        // A Dock=Fill status label would sit over the Preset controls.
        _status.Dock = DockStyle.None;
        _status.AutoEllipsis = true;
        _status.Font = new Font("Segoe UI", 9.0f, FontStyle.Bold);
        _status.ForeColor = Color.FromArgb(255, 179, 181);
        _status.TextAlign = ContentAlignment.MiddleLeft;
        _status.BackColor = Color.Transparent;

        SkillChromeButton Button(string label, string name, string icon,
            bool primary) => new(icon, primary)
        {
            Name = name,
            Text = label,
            Enabled = _canEdit,
            AccessibleName = label
        };
        var balance = Button("Auto Balance", "SkillsAutoBalance", "balance", false);
        var reset = Button("Reset", "SkillsReset", "reset", false);
        var save = Button("Save", "SkillsSave", "save", true);
        balance.Click += (_, _) => AutoBalance();
        reset.Click += (_, _) => ResetToLoadedValues();
        save.Click += (_, _) =>
        {
            if (!ValidateBeforeLeaving())
                return;
            if (_invalid.Count > 0)
            {
                _status.Text = "Correct the invalid ratings before saving.";
                return;
            }
            _save();
        };

        var tips = new ToolTip
        {
            AutoPopDelay = 8000, InitialDelay = 400, ReshowDelay = 100
        };
        footer.Disposed += (_, _) => tips.Dispose();
        tips.SetToolTip(balance,
            "Normalize the average of populated ratings to 50, keeping " +
            "relative differences and leaving unset skills untouched. " +
            "Review before Save.");
        tips.SetToolTip(reset,
            "Restore all 36 skills to their values when the worker was opened.");
        tips.SetToolTip(_preset,
            "Apply a named 36-skill template. Changes are not saved automatically.");
        tips.SetToolTip(save, "Commit staged worker skills to the active database.");

        footer.Controls.Add(presetLabel);
        footer.Controls.Add(_preset);
        footer.Controls.Add(_status);
        footer.Controls.Add(balance);
        footer.Controls.Add(reset);
        footer.Controls.Add(save);
        void Arrange()
        {
            int w = footer.ClientSize.Width;
            int h = footer.ClientSize.Height;
            if (w < 450 || h < 20) return;
            int buttonH = Math.Min(53, h - 12);
            int top = (h - buttonH) / 2;
            int gap = w >= 1400 ? 18 : Math.Clamp(w / 120, 8, 13);
            int saveWidth = Math.Clamp((int)(w * .128), 120, 205);
            int resetWidth = Math.Clamp((int)(w * .120), 105, 192);
            int balanceWidth = Math.Clamp((int)(w * .138), 135, 219);
            int right = w - 14;

            save.Bounds = new Rectangle(right - saveWidth, top,
                saveWidth, buttonH);
            right = save.Left - gap;
            reset.Bounds = new Rectangle(right - resetWidth, top,
                resetWidth, buttonH);
            right = reset.Left - gap;
            balance.Bounds = new Rectangle(right - balanceWidth, top,
                balanceWidth, buttonH);

            int labelWidth = Math.Clamp((int)(w * .06), 64, 95);
            presetLabel.Bounds = new Rectangle(33, top,
                Math.Max(54, labelWidth - 8), buttonH);
            int presetX = presetLabel.Right + 5;
            int presetWidth = Math.Clamp((int)(w * .156), 145, 250);
            _preset.Bounds = new Rectangle(presetX, top + 5,
                presetWidth, buttonH - 10);
            _status.Bounds = new Rectangle(_preset.Right + 12, top,
                Math.Max(0, balance.Left - _preset.Right - 20), buttonH);
        }
        footer.SizeChanged += (_, _) => Arrange();
        Arrange();
        return footer;
    }

    // In-memory test seam. A native MDB write must happen only on Save.
    internal void SetRatingForSmoke(string field, int? value)
    {
        UpdateSkill(field, value);
        _rows[field].SetRating(value);
    }

    internal void ResetForSmoke() => ResetToLoadedValues();

    private void ApplyPreset(string name)
    {
        if (name == "None" || !_canEdit) return;
        if (!ValidateBeforeLeaving())
        {
            _preset?.SelectValue("None");
            return;
        }

        string message = "Apply the \"" + name + "\" template to all 36 " +
            "skills? This replaces current and unset ratings with defined " +
            "values. Changes will remain unsaved until you press Save.";
        if (MessageBox.Show(FindForm(), message,
            "Apply Skills Preset", MessageBoxButtons.YesNo,
            MessageBoxIcon.Question, MessageBoxDefaultButton.Button2)
            != DialogResult.Yes)
        {
            _preset?.SelectValue("None");
            return;
        }

        for (int i = 0; i < Groups.Count; i++)
        {
            int groupValue = name == "Balanced (50)"
                ? 50 : name switch
                {
                    "In-Ring Specialist" when i == 0 => 75,
                    "Entertainment Specialist" when i == 1 => 75,
                    "Physical Specialist" when i == 2 => 75,
                    "Commentary Specialist" when i == 3 => 75,
                    _ => 45
                };
            foreach (var skill in Groups[i].Skills)
                SetRatingForSmoke(skill.Column, groupValue);
        }
        _status.Text = "Preset applied. Review ratings before saving.";
    }

    private void AutoBalance()
    {
        if (!_canEdit || !ValidateBeforeLeaving()) return;
        var populated = _rows
            .Select(pair => (pair.Key, Value: pair.Value.CurrentRating))
            .Where(pair => pair.Value.HasValue)
            .ToArray();
        if (populated.Length == 0)
        {
            _status.Text = "Enter at least one rating to balance.";
            return;
        }

        double average = populated.Average(item => item.Value!.Value);
        int adjustment = (int)Math.Round(50 - average,
            MidpointRounding.AwayFromZero);
        if (adjustment == 0)
        {
            _status.Text = "Populated skill ratings already average 50.";
            return;
        }
        if (MessageBox.Show(FindForm(),
            "Shift populated skill ratings by " +
            adjustment.ToString("+0;-0", CultureInfo.InvariantCulture) +
            " points toward an average of 50? Differences remain " +
            "proportional where possible, ratings stay within 0-100, " +
            "and unset skills are unchanged. This is not saved until Save.",
            "Auto Balance Skills", MessageBoxButtons.YesNo,
            MessageBoxIcon.Question, MessageBoxDefaultButton.Button2)
            != DialogResult.Yes)
            return;

        foreach (var (field, value) in populated)
            SetRatingForSmoke(field, Math.Clamp(value!.Value + adjustment, 0, 100));
        _preset?.SelectValue("None");
        _status.Text = "Balance applied. Review ratings before saving.";
    }

    private void UpdateSkill(string field, int? rating)
    {
        if (_resetting) return;
        if (!_canEdit || _table is null) return;
        _invalid.Remove(field);
        _status.Text = "";
        var column = _table.Columns[field];
        if (column is null)
        {
            _invalid[field] = "Skill column is missing from the native MDB.";
            _status.Text = _invalid[field];
            return;
        }
        if (rating is < 0 or > 100)
        {
            _invalid[field] = "Skill rating must be between 0 and 100.";
            _status.Text = _invalid[field];
            return;
        }
        if (!rating.HasValue && !column.AllowDBNull)
        {
            _invalid[field] = "The MDB does not allow this field to be blank.";
            _status.Text = _invalid[field];
            return;
        }
        try
        {
            object value = rating.HasValue
                ? column.DataType == typeof(string)
                    ? rating.Value.ToString(CultureInfo.InvariantCulture)
                    : Convert.ChangeType(rating.Value, column.DataType,
                        CultureInfo.InvariantCulture)
                : DBNull.Value;

            if (_currentRow is null)
            {
                _currentRow = _table.NewRow();
                _currentRow["WorkerUID"] = _profile.Identity.Uid;
                _table.Rows.Add(_currentRow);
                _createdRow = true;
            }
            if (!Equals(_currentRow[field], value))
                _currentRow[field] = value;
        }
        catch (Exception ex) when (ex is ArgumentException or ConstraintException
                                   or InvalidCastException or OverflowException or FormatException)
        {
            _invalid[field] = "Invalid " + field + ": " + ex.Message;
            _status.Text = _invalid[field];
        }
    }

    private void ResetToLoadedValues()
    {
        _resetting = true;
        try
        {
            if (_createdRow && _currentRow is not null)
            {
                _table?.Rows.Remove(_currentRow);
                _currentRow = null;
                _createdRow = false;
            }
            else if (_currentRow is not null)
            {
                foreach (var (field, value) in _original)
                {
                    if (_currentRow.Table.Columns.Contains(field) &&
                        !Equals(_currentRow[field], value))
                        _currentRow[field] = value;
                }
            }

            foreach (var (field, view) in _rows)
            {
                object value = _original[field];
                int? rating = value is DBNull ? null
                    : Convert.ToInt32(value, CultureInfo.InvariantCulture);
                view.SetRating(rating);
            }
            _invalid.Clear();
            _preset?.SelectValue("None");
            _status.Text = "";
        }
        finally { _resetting = false; }
    }

    private sealed class SkillRatingRow : UserControl
    {
        private readonly SkillGlowSlider _slider;
        private readonly SkillNumberSpinner _spinner;
        private readonly Action<int?> _changed;
        private bool _syncing;

        internal int? CurrentRating => _slider.Rating;

        internal SkillRatingRow(string label, string field,
            int? rating, bool enabled, Action<int?> changed,
            float labelPercent, int railGap)
        {
            _changed = changed;
            Name = "Skill_" + field;
            BackColor = SkillChrome.Ink;
            Enabled = enabled;
            Height = 27;

            var columns = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 3, RowCount = 1,
                Margin = Padding.Empty, Padding = Padding.Empty,
                BackColor = SkillChrome.Ink
            };
            // The screenshot's rhythm is label / wide illuminated rail /
            // metal number spinner, with a real 15px gap before the spinner.
            columns.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, labelPercent));
            columns.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f - labelPercent));
            columns.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 96));
            columns.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            var title = new Label
            {
                Name = "SkillLabel_" + field,
                Text = label, UseMnemonic = false,
                Dock = DockStyle.Fill, AutoEllipsis = true,
                Font = new Font("Segoe UI", 12.7f, FontStyle.Bold),
                ForeColor = SkillChrome.White,
                BackColor = Color.Transparent,
                Margin = Padding.Empty,
                TextAlign = ContentAlignment.MiddleLeft
            };
            _slider = new SkillGlowSlider
            {
                Name = "SkillSlider_" + field,
                Dock = DockStyle.Fill,
                AccessibleName = label + " slider",
                Margin = new Padding(0, 0, railGap, 0)
            };
            _slider.SetValue(rating);
            _spinner = new SkillNumberSpinner(field, rating, enabled)
            {
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 1, 4, 1)
            };
            _slider.RatingChanged += (_, _) =>
            {
                if (_syncing) return;
                SetRating(_slider.Rating);
                _changed(_slider.Rating);
            };
            _spinner.RatingChanged += number =>
            {
                if (_syncing) return;
                _slider.SetValue(number);
                _changed(number);
            };
            columns.Controls.Add(title, 0, 0);
            columns.Controls.Add(_slider, 1, 0);
            columns.Controls.Add(_spinner, 2, 0);
            Controls.Add(columns);
        }

        internal string? ValidateNumber() => _spinner.ValidateNumber();

        internal void SetRating(int? rating)
        {
            _syncing = true;
            try
            {
                _slider.SetValue(rating);
                _spinner.SetRating(rating);
            }
            finally { _syncing = false; }
        }
    }
}
