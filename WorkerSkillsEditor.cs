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

    internal WorkerSkillsEditor(PrwWorkerProfile profile, bool canEdit, Action save)
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
            _original.Add(skill.Column, GetStoredValue(skill.Column));
        }

        Build();
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
        var layout = new TableLayoutPanel
        {
            Name = "SkillsWorkspaceLayout",
            Dock = DockStyle.Fill,
            ColumnCount = 1, RowCount = 3,
            Padding = new Padding(10, 6, 10, 6),
            Margin = Padding.Empty, BackColor = Color.Transparent
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 65));

        var title = new Label
        {
            Name = "SkillsSectionHeading", Dock = DockStyle.Fill,
            Text = "Skills    /    Worker Skill Editor",
            Font = new Font("Segoe UI", 13.5f, FontStyle.Bold),
            ForeColor = Color.White, TextAlign = ContentAlignment.MiddleLeft,
            BackColor = Color.Transparent
        };
        layout.Controls.Add(title, 0, 0);

        var panels = new TableLayoutPanel
        {
            Name = "SkillsFourPanels", Dock = DockStyle.Fill,
            ColumnCount = 2, RowCount = 2, Margin = Padding.Empty,
            Padding = Padding.Empty, BackColor = Color.Transparent
        };
        panels.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        panels.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        panels.RowStyles.Add(new RowStyle(SizeType.Percent, 49));
        panels.RowStyles.Add(new RowStyle(SizeType.Percent, 51));

        for (int groupIndex = 0; groupIndex < Groups.Count; groupIndex++)
            panels.Controls.Add(CreateGroup(Groups[groupIndex]), groupIndex % 2,
                groupIndex / 2);
        layout.Controls.Add(panels, 0, 1);
        layout.Controls.Add(CreateActions(), 0, 2);
        Controls.Add(layout);
    }

    private Control CreateGroup(SkillGroup group)
    {
        var panel = new SkillPanel
        {
            Name = "SkillPanel_" + group.Title.Replace(" ", "").Replace("/", ""),
            Dock = DockStyle.Fill,
            Margin = new Padding(4, 3, 5, 4),
            Padding = new Padding(1),
            BackColor = PanelInk
        };
        var inner = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1, RowCount = 2,
            Margin = Padding.Empty, Padding = Padding.Empty,
            BackColor = Color.Transparent
        };
        inner.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        inner.RowStyles.Add(new RowStyle(SizeType.Absolute, 45));
        inner.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var header = new SkillPanelHeader
        {
            Dock = DockStyle.Fill, Margin = Padding.Empty
        };
        var icon = new Label
        {
            Dock = DockStyle.Left, Width = 41,
            Text = group.Icon, ForeColor = Color.White,
            Font = new Font("Segoe UI Symbol", 21f, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleCenter,
            BackColor = Color.Transparent
        };
        var words = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2,
            BackColor = Color.Transparent, Padding = Padding.Empty
        };
        words.RowStyles.Add(new RowStyle(SizeType.Absolute, 25));
        words.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        words.Controls.Add(new Label
        {
            Dock = DockStyle.Fill, AutoEllipsis = true,
            Text = group.Title, Font = new Font("Segoe UI", 11.2f, FontStyle.Bold),
            ForeColor = Color.White, TextAlign = ContentAlignment.BottomLeft
        }, 0, 0);
        words.Controls.Add(new Label
        {
            Dock = DockStyle.Fill, AutoEllipsis = true,
            Text = group.Description, Font = new Font("Segoe UI", 8.2f, FontStyle.Italic),
            ForeColor = Color.FromArgb(199, 222, 239),
            TextAlign = ContentAlignment.TopLeft
        }, 0, 1);
        header.Controls.Add(words);
        header.Controls.Add(icon);
        inner.Controls.Add(header, 0, 0);

        var rows = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, ColumnCount = 1, RowCount = group.Skills.Count,
            Margin = Padding.Empty, Padding = new Padding(8, 3, 8, 3),
            BackColor = Color.Transparent, AutoScroll = true
        };
        rows.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        for (int skillIndex = 0; skillIndex < group.Skills.Count; skillIndex++)
        {
            var skill = group.Skills[skillIndex];
            rows.RowStyles.Add(new RowStyle(SizeType.Absolute, 23));
            var rating = new SkillRatingRow(skill.Label, skill.Column,
                InitialRating(skill.Column), _canEdit,
                value => UpdateSkill(skill.Column, value));
            rating.Dock = DockStyle.Fill;
            rows.Controls.Add(rating, 0, skillIndex);
            _rows.Add(skill.Column, rating);
        }
        inner.Controls.Add(rows, 0, 1);
        panel.Controls.Add(inner);
        return panel;
    }

    private Control CreateActions()
    {
        var action = new SkillPanel
        {
            Name = "WorkerSkillsActions", Dock = DockStyle.Fill,
            Padding = new Padding(11, 9, 9, 9),
            Margin = new Padding(4, 7, 5, 1)
        };
        var strip = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 1,
            Margin = Padding.Empty, BackColor = Color.Transparent
        };
        strip.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        strip.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        strip.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 126));
        strip.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 142));
        strip.Controls.Add(_status, 0, 0);

        Button MakeButton(string text, string name, bool primary)
        {
            var button = new Button
            {
                Name = name, Dock = DockStyle.Fill, Text = text,
                Font = new Font("Segoe UI", 10f, FontStyle.Bold),
                Margin = new Padding(4, 0, 4, 0),
                FlatStyle = FlatStyle.Flat,
                BackColor = primary ? Color.FromArgb(11, 112, 174) : Color.FromArgb(17, 42, 67),
                ForeColor = Color.White, Cursor = Cursors.Hand,
                Enabled = _canEdit
            };
            button.FlatAppearance.BorderColor = Edge;
            button.FlatAppearance.BorderSize = primary ? 2 : 1;
            button.FlatAppearance.MouseOverBackColor = Color.FromArgb(21, 123, 188);
            return button;
        }

        var reset = MakeButton("Reset", "SkillsReset", false);
        var save = MakeButton("Save", "SkillsSave", true);
        reset.Click += (_, _) => ResetToLoadedValues();
        save.Click += (_, _) =>
        {
            var invalidRow = _rows.Values
                .Select(row => row.ValidateNumber())
                .FirstOrDefault(result => result is not null);
            if (invalidRow is not null)
            {
                _status.Text = invalidRow;
                return;
            }
            if (_invalid.Count > 0)
            {
                _status.Text = "Correct the invalid numerical ratings before saving.";
                return;
            }
            _save();
        };
        strip.Controls.Add(reset, 1, 0);
        strip.Controls.Add(save, 2, 0);
        action.Controls.Add(strip);
        return action;
    }

    // In-memory test seam. A native MDB write must happen only on Save.
    internal void SetRatingForSmoke(string field, int? value)
    {
        UpdateSkill(field, value);
        _rows[field].SetRating(value);
    }

    internal void ResetForSmoke() => ResetToLoadedValues();

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
            _status.Text = "";
        }
        finally { _resetting = false; }
    }

    /// <summary>Paints the cyan bordered, dark blue sports-management cards.</summary>
    private sealed class SkillPanel : Panel
    {
        internal SkillPanel()
        {
            DoubleBuffered = true;
            BackColor = PanelInk;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var rect = ClientRectangle;
            if (rect.Width < 3 || rect.Height < 3) return;
            using var border = new Pen(Edge, 1.2f);
            e.Graphics.DrawRectangle(border, 1, 1, rect.Width - 3, rect.Height - 3);
        }
    }

    private sealed class SkillPanelHeader : Panel
    {
        internal SkillPanelHeader()
        {
            DoubleBuffered = true;
            BackColor = Color.FromArgb(18, 60, 103);
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            var rect = ClientRectangle;
            if (rect.Width < 2 || rect.Height < 2) return;
            using var brush = new LinearGradientBrush(rect,
                Color.FromArgb(33, 102, 164), Color.FromArgb(9, 34, 65),
                LinearGradientMode.Horizontal);
            e.Graphics.FillRectangle(brush, rect);
            using var line = new Pen(Edge);
            e.Graphics.DrawLine(line, 0, Height - 1, Width, Height - 1);
        }
    }

    private sealed class SkillRatingRow : UserControl
    {
        private readonly SkillRatingSlider _slider;
        private readonly TextBox _number;
        private readonly Action<int?> _changed;
        private bool _syncing;

        internal SkillRatingRow(string label, string field,
            int? rating, bool enabled, Action<int?> changed)
        {
            _changed = changed;
            Name = "Skill_" + field;
            BackColor = Color.Transparent;
            Enabled = enabled;
            Height = 23;
            var table = new TableLayoutPanel
            {
                Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 1,
                Padding = Padding.Empty, Margin = Padding.Empty,
                BackColor = Color.Transparent
            };
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 38));
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 62));
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 67));
            table.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            var title = new Label
            {
                Name = "SkillLabel_" + field, Text = label,
                Dock = DockStyle.Fill, AutoEllipsis = false,
                Font = new Font("Segoe UI", 8.6f, FontStyle.Bold),
                ForeColor = Color.White, Margin = Padding.Empty,
                TextAlign = ContentAlignment.MiddleLeft
            };
            _slider = new SkillRatingSlider
            {
                Name = "SkillSlider_" + field,
                Dock = DockStyle.Fill,
                Margin = new Padding(1, 0, 7, 0)
            };
            _slider.SetValue(rating);
            _slider.RatingChanged += (_, _) =>
            {
                if (_syncing) return;
                SetRating(_slider.Rating);
                _changed(_slider.Rating);
            };

            _number = new TextBox
            {
                Name = "SkillNumber_" + field,
                Dock = DockStyle.Fill,
                TextAlign = HorizontalAlignment.Center,
                BorderStyle = BorderStyle.FixedSingle,
                Margin = new Padding(1, 1, 1, 1),
                Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                ForeColor = Color.White,
                BackColor = Color.FromArgb(16, 40, 66)
            };
            _number.Text = rating?.ToString(CultureInfo.InvariantCulture) ?? "";
            _number.KeyPress += (_, e) =>
            {
                if (!char.IsControl(e.KeyChar) && !char.IsDigit(e.KeyChar))
                    e.Handled = true;
            };
            _number.TextChanged += (_, _) =>
            {
                if (_syncing) return;
                if (string.IsNullOrWhiteSpace(_number.Text))
                {
                    _slider.SetValue(null);
                    _changed(null);
                }
                else if (int.TryParse(_number.Text,
                    NumberStyles.None, CultureInfo.InvariantCulture, out int parsed) &&
                    parsed is >= 0 and <= 100)
                {
                    _slider.SetValue(parsed);
                    _changed(parsed);
                }
                else
                {
                    _number.BackColor = Color.FromArgb(95, 33, 44);
                }
            };

            table.Controls.Add(title, 0, 0);
            table.Controls.Add(_slider, 1, 0);
            table.Controls.Add(_number, 2, 0);
            Controls.Add(table);
        }

        internal string? ValidateNumber()
        {
            string value = _number.Text.Trim();
            if (value.Length == 0) return null;
            return int.TryParse(value, NumberStyles.None,
                       CultureInfo.InvariantCulture, out int number) &&
                   number is >= 0 and <= 100
                ? null : "Skill ratings must be whole numbers between 0 and 100.";
        }

        internal void SetRating(int? rating)
        {
            _syncing = true;
            try
            {
                _slider.SetValue(rating);
                _number.Text = rating?.ToString(CultureInfo.InvariantCulture) ?? "";
                _number.BackColor = Color.FromArgb(16, 40, 66);
            }
            finally { _syncing = false; }
        }
    }

    private sealed class SkillRatingSlider : Control
    {
        internal event EventHandler? RatingChanged;
        internal int? Rating { get; private set; }
        private bool _dragging;

        internal SkillRatingSlider()
        {
            DoubleBuffered = true;
            SetStyle(ControlStyles.UserPaint | ControlStyles.Selectable |
                     ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer, true);
            Cursor = Cursors.Hand;
            TabStop = true;
        }

        internal void SetValue(int? value)
        {
            Rating = value;
            Invalidate();
        }

        private void DragTo(int x)
        {
            int length = Math.Max(1, Width - 16);
            int rating = (int)Math.Round(100d *
                Math.Clamp(x - 8, 0, length) / length);
            if (Rating == rating) return;
            Rating = rating;
            Invalidate();
            RatingChanged?.Invoke(this, EventArgs.Empty);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button != MouseButtons.Left || !Enabled) return;
            _dragging = true;
            Capture = true;
            DragTo(e.X);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (_dragging) DragTo(e.X);
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
            if (e.KeyCode is not (Keys.Left or Keys.Right)) return;
            int next = Math.Clamp((Rating ?? 0) +
                (e.KeyCode == Keys.Right ? 1 : -1), 0, 100);
            if (Rating != next)
            {
                Rating = next;
                Invalidate();
                RatingChanged?.Invoke(this, EventArgs.Empty);
            }
            e.Handled = true;
            e.SuppressKeyPress = true;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            if (Width < 22 || Height < 12) return;
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            int x = 8, width = Width - 16, y = Height / 2;
            using var rail = new Pen(Color.FromArgb(95, 148, 186), 4f)
            { StartCap = LineCap.Round, EndCap = LineCap.Round };
            e.Graphics.DrawLine(rail, x, y, x + width, y);
            if (Rating.HasValue)
            {
                int end = x + (int)(width * Rating.Value / 100f);
                using var fill = new Pen(Edge, 4f)
                { StartCap = LineCap.Round, EndCap = LineCap.Round };
                e.Graphics.DrawLine(fill, x, y, end, y);
                using var glow = new SolidBrush(Color.FromArgb(224, 245, 255));
                using var outline = new Pen(Color.FromArgb(40, 153, 217), 1.6f);
                e.Graphics.FillEllipse(glow, end - 7, y - 7, 14, 14);
                e.Graphics.DrawEllipse(outline, end - 7, y - 7, 14, 14);
            }
            else
            {
                using var outline = new Pen(Color.FromArgb(110, 145, 174), 1.5f);
                e.Graphics.DrawEllipse(outline, x - 6, y - 6, 12, 12);
            }
        }
    }
}
