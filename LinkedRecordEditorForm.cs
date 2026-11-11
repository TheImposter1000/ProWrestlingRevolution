using System.Data;
using System.Drawing;
using System.Windows.Forms;

namespace ProWrestlingRevolution;

/// <summary>
/// Entity-focused company and match inspectors. Linked tables appear as
/// sections of one record, rather than as independent top-level categories.
/// Fields remain read-only until a validated editing workflow is implemented.
/// </summary>
internal sealed class LinkedRecordEditorForm : Form
{
    private static readonly Color Background = Color.FromArgb(9, 18, 31);
    private static readonly Color Surface = Color.FromArgb(17, 34, 53);
    private static readonly Color Edge = Color.FromArgb(65, 132, 183);
    private static readonly Color Accent = Color.FromArgb(39, 177, 232);
    private static readonly Color TextInk = Color.FromArgb(225, 239, 248);

    private readonly EditorMetadataCatalog _metadata;
    private readonly IReadOnlyList<PrwProfileSection> _sections;
    private readonly string _primaryTable;
    private readonly string _recordName;
    private readonly DataGridView _grid = new();
    private readonly Label _sectionHeading = new();
    private readonly Label _sectionCount = new();
    private readonly Dictionary<string, Button> _buttons =
        new(StringComparer.OrdinalIgnoreCase);

    internal LinkedRecordEditorForm(
        string primaryTable, string recordName, EditorMetadataCatalog metadata,
        IReadOnlyList<PrwProfileSection> sections)
    {
        if (primaryTable is not ("tblFed" or "tblMatch"))
            throw new ArgumentException("Unknown linked editor category.", nameof(primaryTable));

        _metadata = metadata;
        _sections = sections;
        _primaryTable = primaryTable;
        _recordName = recordName;

        Name = primaryTable == "tblFed" ? "CompanyEditorForm" : "MatchTypeEditorForm";
        Text = "Pro Wrestling Revolution | " + _metadata.CategoryLabel(primaryTable);
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(SkinArt.WindowWidth, SkinArt.WindowHeight);
        AutoScaleMode = AutoScaleMode.None;
        ShowInTaskbar = false;
        DoubleBuffered = true;
        KeyPreview = true;
        BackColor = Background;
        ForeColor = TextInk;
        Font = new Font("Segoe UI", 9.3f);
        KeyDown += (_, e) =>
        {
            if (e.KeyCode != Keys.Escape) return;
            Close();
            e.Handled = true;
        };

        BuildLayout();
        ShowSection(_sections.First(s => s.Table.Equals(
            _primaryTable, StringComparison.OrdinalIgnoreCase)));
    }

    internal void FillLauncherWindow(Form launcher)
    {
        StartPosition = FormStartPosition.Manual;
        MinimumSize = Size.Empty;
        MaximumSize = Size.Empty;
        Bounds = launcher.Bounds;
    }

    private void BuildLayout()
    {
        var header = new Panel
        {
            Dock = DockStyle.Top, Height = 59,
            BackColor = Color.FromArgb(18, 47, 74)
        };
        header.Paint += (_, e) =>
        {
            using var line = new Pen(Accent, 2f);
            e.Graphics.DrawLine(line, 0, header.Height - 2,
                header.Width, header.Height - 2);
        };
        var back = new Button
        {
            Name = "ReturnToDatabaseDirectory",
            Dock = DockStyle.Right, Width = 190,
            Text = "RETURN TO EDITOR",
            Font = new Font("Segoe UI", 9.3f, FontStyle.Bold),
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(27, 64, 101),
            ForeColor = Color.White,
            Cursor = Cursors.Hand
        };
        back.FlatAppearance.BorderColor = Edge;
        back.Click += (_, _) => Close();
        header.Controls.Add(back);
        var title = new Label
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(20, 0, 0, 0),
            Text = "DATABASE EDITOR  /  " +
                   _metadata.CategoryLabel(_primaryTable).ToUpperInvariant(),
            Font = new Font("Segoe UI", 14f, FontStyle.Bold),
            ForeColor = Color.White,
            TextAlign = ContentAlignment.MiddleLeft
        };
        header.Controls.Add(title);

        var navigation = new Panel
        {
            Name = "EntitySectionNavigation", Dock = DockStyle.Left,
            Width = 224, Padding = new Padding(10, 14, 9, 10),
            BackColor = Color.FromArgb(11, 26, 42)
        };
        var sectionButtons = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false
        };
        navigation.Controls.Add(sectionButtons);
        var heading = new Label
        {
            Text = "RECORD SECTIONS",
            Dock = DockStyle.Top, Height = 34,
            Font = new Font("Segoe UI", 10f, FontStyle.Bold),
            ForeColor = Accent, TextAlign = ContentAlignment.MiddleLeft
        };
        navigation.Controls.Add(heading);

        // Keep the parent entity first, with its related entities alphabetical.
        var ordered = _sections.Where(s => s.Table == _primaryTable)
            .Concat(_sections.Where(s => s.Table != _primaryTable)
                .OrderBy(s => _metadata.CategoryLabel(s.Table),
                    StringComparer.OrdinalIgnoreCase));
        foreach (var section in ordered)
        {
            var button = new Button
            {
                Name = "EntitySection_" + section.Table,
                Width = 195, Height = 43,
                Margin = new Padding(0, 2, 0, 5),
                Text = _metadata.CategoryLabel(section.Table) + "  (" +
                       section.Records.Rows.Count.ToString("N0") + ")",
                Font = new Font("Segoe UI", 8.9f, FontStyle.Bold),
                FlatStyle = FlatStyle.Flat,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(9, 0, 0, 0),
                BackColor = Surface,
                ForeColor = TextInk,
                Cursor = Cursors.Hand
            };
            button.FlatAppearance.BorderColor = Edge;
            button.Click += (_, _) => ShowSection(section);
            sectionButtons.Controls.Add(button);
            _buttons[section.Table] = button;
        }

        var page = new Panel
        {
            Dock = DockStyle.Fill, Padding = new Padding(15, 9, 17, 12),
            BackColor = Background
        };
        var identity = new Panel
        {
            Dock = DockStyle.Top, Height = 82,
            BackColor = Surface, Padding = new Padding(16, 9, 12, 8)
        };
        identity.Controls.Add(new Label
        {
            Dock = DockStyle.Top, Height = 29,
            Text = _recordName,
            Font = new Font("Segoe UI", 16f, FontStyle.Bold),
            ForeColor = Color.White,
            AutoEllipsis = true
        });
        identity.Controls.Add(new Label
        {
            Dock = DockStyle.Bottom, Height = 25,
            Text = "Linked Access records grouped by their declared relationships.",
            Font = new Font("Segoe UI", 9f),
            ForeColor = Color.FromArgb(170, 202, 222)
        });

        var sectionBar = new Panel
        {
            Dock = DockStyle.Top, Height = 53,
            Padding = new Padding(10, 10, 10, 5),
            BackColor = Color.FromArgb(13, 32, 50)
        };
        _sectionHeading.Dock = DockStyle.Left;
        _sectionHeading.Width = 410;
        _sectionHeading.Font = new Font("Segoe UI", 11f, FontStyle.Bold);
        _sectionHeading.ForeColor = Color.White;
        _sectionHeading.TextAlign = ContentAlignment.MiddleLeft;
        _sectionCount.Dock = DockStyle.Right;
        _sectionCount.Width = 230;
        _sectionCount.ForeColor = Accent;
        _sectionCount.TextAlign = ContentAlignment.MiddleRight;
        sectionBar.Controls.Add(_sectionCount);
        sectionBar.Controls.Add(_sectionHeading);

        _grid.Name = "LinkedRecordFields";
        _grid.Dock = DockStyle.Fill;
        _grid.ReadOnly = true;
        _grid.AllowUserToAddRows = false;
        _grid.AllowUserToDeleteRows = false;
        _grid.AllowUserToResizeRows = false;
        _grid.MultiSelect = false;
        _grid.RowHeadersVisible = false;
        _grid.BackgroundColor = Color.FromArgb(9, 22, 35);
        _grid.GridColor = Edge;
        _grid.BorderStyle = BorderStyle.None;
        _grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        _grid.EnableHeadersVisualStyles = false;
        _grid.ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle
        {
            BackColor = Color.FromArgb(27, 64, 101),
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 9.5f, FontStyle.Bold)
        };
        _grid.DefaultCellStyle = new DataGridViewCellStyle
        {
            BackColor = Surface,
            ForeColor = TextInk,
            SelectionBackColor = Color.FromArgb(34, 95, 131),
            SelectionForeColor = Color.White,
            WrapMode = DataGridViewTriState.True
        };
        _grid.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.DisplayedCells;
        _grid.Columns.Add("record", "Record");
        _grid.Columns.Add("field", "Field");
        _grid.Columns.Add("value", "Value");
        _grid.Columns[0].FillWeight = 23;
        _grid.Columns[1].FillWeight = 40;
        _grid.Columns[2].FillWeight = 75;

        var footer = new Label
        {
            Dock = DockStyle.Bottom, Height = 32,
            Text = "VIEW ONLY  |  Source values remain in the selected universe MDB.",
            TextAlign = ContentAlignment.MiddleLeft,
            ForeColor = Color.FromArgb(163, 194, 213),
            Font = new Font("Segoe UI", 8.8f)
        };
        page.Controls.Add(_grid);
        page.Controls.Add(footer);
        page.Controls.Add(sectionBar);
        page.Controls.Add(identity);

        Controls.Add(page);
        Controls.Add(navigation);
        Controls.Add(header);
    }

    private void ShowSection(PrwProfileSection section)
    {
        _sectionHeading.Text = _metadata.CategoryLabel(section.Table).ToUpperInvariant();
        _sectionCount.Text = section.Records.Rows.Count.ToString("N0") +
                             " linked record(s)";
        foreach (var (name, button) in _buttons)
        {
            bool current = string.Equals(name, section.Table,
                StringComparison.OrdinalIgnoreCase);
            button.BackColor = current
                ? Color.FromArgb(28, 79, 115) : Surface;
            button.FlatAppearance.BorderColor = current ? Accent : Edge;
        }

        _grid.SuspendLayout();
        try
        {
            _grid.Rows.Clear();
            const int maxDisplayedRows = 10000;
            var columns = section.Records.Columns.Cast<DataColumn>()
                .OrderBy(c => _metadata.FieldLabel(section.Table, c.ColumnName),
                    StringComparer.OrdinalIgnoreCase)
                .ToArray();
            foreach (DataRow row in section.Records.Rows)
            {
                int index = section.Records.Rows.IndexOf(row) + 1;
                string recordName = section.Records.Columns.Contains("Name") &&
                                    row["Name"] is not DBNull
                    ? Convert.ToString(row["Name"]) ?? ""
                    : "";
                string recordLabel = recordName.Length > 0
                    ? recordName : "Record " + index;
                foreach (DataColumn column in columns)
                {
                    _grid.Rows.Add(recordLabel,
                        _metadata.FieldLabel(section.Table, column.ColumnName),
                        row[column] is DBNull ? "" : Convert.ToString(row[column]) ?? "");
                    if (_grid.Rows.Count >= maxDisplayedRows)
                    {
                        _sectionCount.Text += " (first 10,000 field values shown)";
                        return;
                    }
                }
            }
        }
        finally
        {
            _grid.ResumeLayout();
        }
    }
}
