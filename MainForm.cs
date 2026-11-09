using System.Drawing;
using System.Windows.Forms;

namespace ProWrestlingRevolution;

public sealed class MainForm : Form
{
    private readonly GameStore _store;
    private readonly string _scenario;
    private GameState _game;
    private readonly Color _bg = Color.FromArgb(12, 22, 36);
    private readonly Color _panel = Color.FromArgb(21, 37, 57);
    private readonly Color _surface = Color.FromArgb(28, 49, 71);
    private readonly Color _accent = Color.FromArgb(220, 178, 98);
    private readonly Color _text = Color.FromArgb(233, 239, 247);

    private readonly TabControl _tabs = new();
    private readonly Label _header = new();
    private readonly Label _subheader = new();
    private readonly Label _metrics = new();
    private readonly Label _bookingStatus = new();
    private readonly ListBox _news = new();
    private readonly DataGridView _roster = new(), _card = new(), _stories = new(), _titles = new(),
        _agents = new(), _history = new(), _world = new();
    private readonly ListBox _results = new();
    private readonly ComboBox _a = new(), _b = new(), _winner = new(), _title = new(),
        _storyA = new(), _storyB = new();
    private readonly TextBox _note = new(), _showName = new(), _storyName = new();
    private readonly NumericUpDown _minutes = new();
    private readonly Label _status = new();

    public MainForm(string scenarioFile, string saveFile)
    {
        _scenario = scenarioFile;
        _store = new GameStore(saveFile);
        _game = _store.LoadOrCreate(_scenario);
        Text = "Pro Wrestling Revolution | Wrestling Management Simulator";
        MinimumSize = new Size(1120, 720);
        Size = new Size(1480, 880);
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = _bg;
        ForeColor = _text;
        Font = new Font("Segoe UI", 10);
        FormClosing += (_, _) => { try { _store.Save(_game); } catch { } _store.Dispose(); };
        BuildLayout();
        RefreshAll();
    }

    private Label Label(string value, int size = 11, bool bold = false, Color? color = null)
        => new() { Text = value, AutoSize = true, ForeColor = color ?? _text,
            Font = new Font("Segoe UI", size, bold ? FontStyle.Bold : FontStyle.Regular),
            Margin = new Padding(6), Padding = new Padding(2) };

    private Button Button(string caption, Action action, bool primary = false)
    {
        var button = new Button { Text = caption, AutoSize = true, MinimumSize = new Size(115, 37),
            Padding = new Padding(10, 5, 10, 5), Margin = new Padding(5),
            FlatStyle = FlatStyle.Flat, BackColor = primary ? Color.FromArgb(55, 106, 81) : _surface,
            ForeColor = Color.White, Cursor = Cursors.Hand };
        button.FlatAppearance.BorderColor = primary ? Color.FromArgb(90, 158, 112) : Color.FromArgb(64, 95, 126);
        button.Click += (_, _) => Execute(action);
        return button;
    }

    private static DataGridView ConfigureGrid(DataGridView grid)
    {
        grid.Dock = DockStyle.Fill;
        grid.ReadOnly = true;
        grid.AllowUserToAddRows = false;
        grid.AllowUserToDeleteRows = false;
        grid.AllowUserToResizeRows = false;
        grid.MultiSelect = false;
        grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        grid.RowHeadersVisible = false;
        grid.BorderStyle = BorderStyle.None;
        grid.BackgroundColor = Color.FromArgb(18, 35, 54);
        grid.GridColor = Color.FromArgb(42, 66, 91);
        grid.EnableHeadersVisualStyles = false;
        grid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(45, 75, 108);
        grid.ColumnHeadersDefaultCellStyle.ForeColor = Color.White;
        grid.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 10, FontStyle.Bold);
        grid.DefaultCellStyle.BackColor = Color.FromArgb(22, 42, 62);
        grid.DefaultCellStyle.ForeColor = Color.White;
        grid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(53, 101, 146);
        grid.DefaultCellStyle.SelectionForeColor = Color.White;
        grid.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(30, 51, 73);
        grid.RowTemplate.Height = 29;
        grid.ColumnHeadersHeight = 34;
        return grid;
    }

    private static void SetupCombo(ComboBox combo, int width = 265)
    {
        combo.DropDownStyle = ComboBoxStyle.DropDownList;
        combo.Width = width;
        combo.Height = 32;
        combo.Margin = new Padding(5);
        combo.BackColor = Color.WhiteSmoke;
        combo.ForeColor = Color.Black;
    }

    private TabPage NewTab(string title)
    {
        var tab = new TabPage(title) { BackColor = _panel, ForeColor = _text, Padding = new Padding(14) };
        _tabs.TabPages.Add(tab);
        return tab;
    }

    private void BuildLayout()
    {
        var top = new Panel { Dock = DockStyle.Top, Height = 84, BackColor = Color.FromArgb(18, 38, 59) };
        _header.Text = "PRO WRESTLING REVOLUTION";
        _header.ForeColor = Color.White;
        _header.Font = new Font("Segoe UI", 22, FontStyle.Bold);
        _header.AutoSize = true;
        _header.Location = new Point(23, 12);
        _subheader.ForeColor = _accent;
        _subheader.Font = new Font("Segoe UI", 10, FontStyle.Bold);
        _subheader.AutoSize = true;
        _subheader.Location = new Point(27, 52);
        top.Controls.Add(_header);
        top.Controls.Add(_subheader);
        Controls.Add(top);

        var bottom = new Panel { Dock = DockStyle.Bottom, Height = 59, BackColor = Color.FromArgb(13, 30, 48) };
        var controls = new FlowLayoutPanel { Dock = DockStyle.Left, Width = 590, FlowDirection = FlowDirection.LeftToRight };
        controls.Controls.Add(Button("Save Career", () => Save()));
        controls.Controls.Add(Button("Advance Week", () => { GameEngine.AdvanceWeek(_game); Save(); }));
        controls.Controls.Add(Button("New Career", NewCareer));
        bottom.Controls.Add(controls);
        _status.Dock = DockStyle.Right;
        _status.Width = 620;
        _status.TextAlign = ContentAlignment.MiddleRight;
        _status.ForeColor = _accent;
        _status.Padding = new Padding(0, 0, 22, 0);
        bottom.Controls.Add(_status);
        Controls.Add(bottom);

        _tabs.Dock = DockStyle.Fill;
        _tabs.Appearance = TabAppearance.Normal;
        _tabs.Font = new Font("Segoe UI", 11, FontStyle.Bold);
        _tabs.ItemSize = new Size(135, 34);
        _tabs.SizeMode = TabSizeMode.Fixed;
        _tabs.DrawMode = TabDrawMode.OwnerDrawFixed;
        _tabs.DrawItem += (_, args) =>
        {
            var selected = args.Index == _tabs.SelectedIndex;
            var rect = args.Bounds;
            using var b = new SolidBrush(selected ? Color.FromArgb(51, 89, 126) : Color.FromArgb(24, 49, 75));
            args.Graphics.FillRectangle(b, rect);
            TextRenderer.DrawText(args.Graphics, _tabs.TabPages[args.Index].Text,
                new Font("Segoe UI", 10, FontStyle.Bold), rect,
                selected ? Color.White : Color.FromArgb(180, 199, 215),
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        };
        Controls.Add(_tabs);

        var office = NewTab("Office");
        var officeLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3 };
        officeLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 52));
        officeLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 128));
        officeLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        officeLayout.Controls.Add(Label("PROMOTION OFFICE", 20, true), 0, 0);
        _metrics.Font = new Font("Segoe UI", 16, FontStyle.Bold);
        _metrics.ForeColor = _accent;
        _metrics.Dock = DockStyle.Fill;
        _metrics.Padding = new Padding(18);
        _metrics.BackColor = _surface;
        officeLayout.Controls.Add(_metrics, 0, 1);
        var newsPanel = new Panel { Dock = DockStyle.Fill };
        var newsHeading = Label("LATEST WRESTLING WORLD NEWS", 14, true);
        newsHeading.Dock = DockStyle.Top;
        _news.Dock = DockStyle.Fill;
        _news.BackColor = _bg;
        _news.ForeColor = _text;
        _news.BorderStyle = BorderStyle.None;
        _news.ItemHeight = 25;
        newsPanel.Controls.Add(_news);
        newsPanel.Controls.Add(newsHeading);
        officeLayout.Controls.Add(newsPanel, 0, 2);
        office.Controls.Add(officeLayout);

        var roster = NewTab("Roster");
        roster.Controls.Add(ConfigureGrid(_roster));

        var booking = NewTab("Book Show");
        var bookingLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 2 };
        bookingLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 68));
        bookingLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 32));
        bookingLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        bookingLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 69));
        var bookingLeft = new Panel { Dock = DockStyle.Fill };
        _bookingStatus.Dock = DockStyle.Top;
        _bookingStatus.Height = 38;
        _bookingStatus.ForeColor = _accent;
        _bookingStatus.Font = new Font("Segoe UI", 11, FontStyle.Bold);
        bookingLeft.Controls.Add(ConfigureGrid(_card));
        bookingLeft.Controls.Add(_bookingStatus);
        bookingLayout.Controls.Add(bookingLeft, 0, 0);
        var bookingEditor = EditorPanel();
        bookingEditor.Controls.Add(Label("BOOK A SEGMENT", 16, true));
        bookingEditor.Controls.Add(Label("Show name"));
        _showName.Width = 270;
        bookingEditor.Controls.Add(_showName);
        bookingEditor.Controls.Add(Button("Update Show Name", () => {
            if (_game.ShowRunThisWeek) throw new InvalidOperationException("Advance week before editing the event.");
            if (string.IsNullOrWhiteSpace(_showName.Text)) throw new InvalidOperationException("Enter an event name.");
            _game.NextShowName = _showName.Text.Trim();
            Save();
        }));
        bookingEditor.Controls.Add(Label("Wrestler A"));
        bookingEditor.Controls.Add(_a);
        bookingEditor.Controls.Add(Label("Wrestler B"));
        bookingEditor.Controls.Add(_b);
        bookingEditor.Controls.Add(Label("Winner"));
        bookingEditor.Controls.Add(_winner);
        bookingEditor.Controls.Add(Label("Championship"));
        bookingEditor.Controls.Add(_title);
        bookingEditor.Controls.Add(Label("Minutes"));
        _minutes.Minimum = 1; _minutes.Maximum = 90; _minutes.Value = 12; _minutes.Width = 120;
        bookingEditor.Controls.Add(_minutes);
        bookingEditor.Controls.Add(Label("Match stipulation / promo description"));
        _note.Width = 270;
        _note.Text = "Regular match";
        bookingEditor.Controls.Add(_note);
        bookingEditor.Controls.Add(Button("Add Match", AddMatch));
        bookingEditor.Controls.Add(Button("Add Angle / Promo", AddAngle));
        bookingLayout.Controls.Add(bookingEditor, 1, 0);
        var bookingActions = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight };
        bookingActions.Controls.Add(Button("Move Up", () => { GameEngine.Move(_game, SelectedId(_card), -1); Save(); }));
        bookingActions.Controls.Add(Button("Move Down", () => { GameEngine.Move(_game, SelectedId(_card), 1); Save(); }));
        bookingActions.Controls.Add(Button("Remove", () => { GameEngine.Remove(_game, SelectedId(_card)); Save(); }));
        bookingActions.Controls.Add(Button("RUN SHOW", RunShow, true));
        bookingLayout.Controls.Add(bookingActions, 0, 1);
        bookingLayout.SetColumnSpan(bookingActions, 2);
        booking.Controls.Add(bookingLayout);

        var stories = NewTab("Storylines");
        var storyLayout = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2 };
        storyLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        storyLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 155));
        storyLayout.Controls.Add(ConfigureGrid(_stories), 0, 0);
        var storyEditor = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = true, Padding = new Padding(10) };
        storyEditor.Controls.Add(Label("New rivalry name"));
        _storyName.Width = 220; _storyName.Text = "The Next Chapter";
        storyEditor.Controls.Add(_storyName);
        storyEditor.Controls.Add(Label("Worker A"));
        storyEditor.Controls.Add(_storyA);
        storyEditor.Controls.Add(Label("Worker B"));
        storyEditor.Controls.Add(_storyB);
        storyEditor.Controls.Add(Button("Start Rivalry", () => {
            GameEngine.CreateStory(_game, _storyName.Text, PickWorker(_storyA), PickWorker(_storyB)); Save();
        }));
        storyLayout.Controls.Add(storyEditor, 0, 1);
        stories.Controls.Add(storyLayout);

        var titles = NewTab("Titles");
        titles.Controls.Add(ConfigureGrid(_titles));
        var freeAgency = NewTab("Free Agents");
        var agentsLayout = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2 };
        agentsLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        agentsLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 62));
        agentsLayout.Controls.Add(ConfigureGrid(_agents), 0, 0);
        agentsLayout.Controls.Add(Button("Sign Selected Wrestler", () => {
            GameEngine.Sign(_game, SelectedId(_agents)); Save();
        }, true), 0, 1);
        freeAgency.Controls.Add(agentsLayout);

        var history = NewTab("Show History");
        var historySplit = new SplitContainer { Dock = DockStyle.Fill, Orientation = Orientation.Horizontal, SplitterDistance = 270 };
        historySplit.Panel1.Controls.Add(ConfigureGrid(_history));
        _results.Dock = DockStyle.Fill;
        _results.BackColor = _bg;
        _results.ForeColor = _text;
        _results.ItemHeight = 22;
        historySplit.Panel2.Controls.Add(_results);
        _history.SelectionChanged += (_, _) => ShowSelectedResults();
        history.Controls.Add(historySplit);

        var world = NewTab("World");
        world.Controls.Add(ConfigureGrid(_world));
        foreach (var cb in new[] { _a, _b, _winner, _title, _storyA, _storyB }) SetupCombo(cb);
    }

    private FlowLayoutPanel EditorPanel() => new()
    {
        Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown,
        WrapContents = false, AutoScroll = true, Padding = new Padding(10),
        BackColor = _surface
    };

    private int PickWorker(ComboBox combo) => (combo.SelectedItem as Worker)?.Id
        ?? throw new InvalidOperationException("Select a wrestler.");
    private static int SelectedId(DataGridView grid)
    {
        if (grid.SelectedRows.Count < 1) throw new InvalidOperationException("Select a row.");
        return Convert.ToInt32(grid.SelectedRows[0].Cells["Id"].Value);
    }

    private void AddMatch()
    {
        int a = PickWorker(_a), b = PickWorker(_b);
        int winnerId = (_winner.SelectedItem as Choice)?.Id ?? 0;
        int titleId = (_title.SelectedItem as Choice)?.Id ?? 0;
        GameEngine.AddMatch(_game, a, b, winnerId == 0 ? null : winnerId,
            titleId == 0 ? null : titleId, (int)_minutes.Value, _note.Text);
        Save();
    }

    private void AddAngle()
    {
        GameEngine.AddAngle(_game, PickWorker(_a), PickWorker(_b), Math.Min(30, (int)_minutes.Value), _note.Text);
        Save();
    }

    private void RunShow()
    {
        var report = GameEngine.RunShow(_game);
        Save();
        string output = Path.Combine(AppContext.BaseDirectory, "Reports",
            "Week-" + report.Week.ToString("000") + ".txt");
        Directory.CreateDirectory(Path.GetDirectoryName(output)!);
        File.WriteAllText(output, report.Name + Environment.NewLine +
            "Rating: " + report.Rating + Environment.NewLine +
            "Attendance: " + report.Attendance + Environment.NewLine +
            "Profit: " + report.Profit.ToString("C0") + Environment.NewLine + Environment.NewLine +
            string.Join(Environment.NewLine, report.Results));
        MessageBox.Show(report.Name + Environment.NewLine + Environment.NewLine +
            "Overall: " + report.Rating + "/100" + Environment.NewLine +
            "Attendance: " + report.Attendance.ToString("N0") + Environment.NewLine +
            "Profit: " + report.Profit.ToString("C0"), "Official Show Report",
            MessageBoxButtons.OK, MessageBoxIcon.Information);
        _tabs.SelectedIndex = 6;
    }

    private void NewCareer()
    {
        if (MessageBox.Show("Start a new career? Your current save will be overwritten.",
            "Start New Career", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
        _game = GameState.LoadScenario(_scenario);
        Save();
        _tabs.SelectedIndex = 0;
    }

    private void Save()
    {
        _store.Save(_game);
        RefreshAll();
    }

    private void Execute(Action action)
    {
        try { action(); }
        catch (Exception e) { MessageBox.Show(e.Message, "Pro Wrestling Revolution",
            MessageBoxButtons.OK, MessageBoxIcon.Warning); }
    }

    private void RefreshAll()
    {
        _subheader.Text = _game.Player.Name.ToUpperInvariant() + "   |   " +
            _game.Date.ToString("MMMM dd, yyyy") + "   |   WEEK " + _game.Week;
        _metrics.Text = "CASH   " + _game.Player.Cash.ToString("C0") +
            "          POPULARITY   " + _game.Player.Popularity + "/100" +
            Environment.NewLine + "ROSTER   " + _game.Roster().Count() +
            "          PRESTIGE   " + _game.Player.Prestige + "/100" +
            "          LAST RATING   " + (_game.History.Count == 0 ? "N/A" : _game.History[0].Rating.ToString());
        _status.Text = _game.ShowRunThisWeek ? "EVENT COMPLETE   |   ADVANCE WEEK" : "CAREER SAVED   |   BOOKING OPEN";
        _news.DataSource = _game.News.Take(40).Select(x => "[W" + x.Week + "] " + x.Text).ToList();
        _roster.DataSource = _game.Roster().OrderByDescending(w => w.Popularity)
            .Select(w => new { w.Id, w.Name, w.Age, w.Alignment, w.Style, w.Ring, w.Psychology,
                w.Microphone, w.Popularity, w.Momentum, w.Morale, w.Availability,
                Contract = w.ContractWeeks + " weeks", Pay = w.WeeklyPay }).ToList();
        _card.DataSource = _game.Card.Select(s => new { s.Id, s.Type,
            Participants = _game.Worker(s.A).Name + " vs " + _game.Worker(s.B).Name,
            s.Minutes, Winner = s.Winner is int id ? _game.Worker(id).Name : "Auto",
            Championship = s.ChampionshipId is int title
                ? _game.Championships.Single(c => c.Id == title).Name : "None", s.Notes }).ToList();
        _bookingStatus.Text = _game.NextShowName + "  |  " + _game.Card.Count + " segments  |  " +
            _game.Card.Sum(s => s.Minutes) + " minutes (30 minimum)";
        _showName.Text = _game.NextShowName;
        _stories.DataSource = _game.Storylines.Where(s => s.Active).Select(s => new { s.Id,
            s.Name, WrestlerA = _game.Worker(s.A).Name, WrestlerB = _game.Worker(s.B).Name,
            s.Heat, s.Active }).ToList();
        _titles.DataSource = _game.Championships.Select(c => new { c.Id, c.Name,
            Champion = _game.Worker(c.HolderId).Name, c.Prestige, c.Defenses }).ToList();
        _agents.DataSource = _game.FreeAgents().OrderByDescending(w => w.Popularity)
            .Select(w => new { w.Id, w.Name, w.Age, w.Alignment, w.Style, w.Ring, w.Microphone,
                w.Popularity, WeeklyPay = w.WeeklyPay, SigningBonus = w.WeeklyPay * 4 }).ToList();
        _history.DataSource = _game.History.Select(x => new { Id = x.Week, x.Date,
            Show = x.Name, x.Rating, x.Attendance, x.Revenue, x.Expenses, x.Profit }).ToList();
        _world.DataSource = _game.Promotions.OrderByDescending(x => x.Popularity)
            .Select(p => new { p.Id, p.Name, p.Initials, p.Popularity, p.Prestige, p.Cash,
                Workers = _game.Workers.Count(w => w.EmployerId == p.Id) }).ToList();
        var roster = _game.Roster().Where(w => w.InjuryWeeks == 0).OrderBy(w => w.Name).ToList();
        foreach (var combo in new[] { _a, _b, _storyA, _storyB })
        {
            combo.DataSource = roster.ToList();
            combo.DisplayMember = "Name";
        }
        if (roster.Count >= 2)
        {
            _b.SelectedIndex = 1;
            _storyB.SelectedIndex = 1;
        }
        _winner.DataSource = new[] { new Choice(0, "Auto-decide winner") }
            .Concat(roster.Select(x => new Choice(x.Id, x.Name))).ToList();
        _title.DataSource = new[] { new Choice(0, "Non-title match") }
            .Concat(_game.Championships.Select(x => new Choice(x.Id, x.Name))).ToList();
        ShowSelectedResults();
    }

    private void ShowSelectedResults()
    {
        var week = _history.SelectedRows.Count > 0
            ? Convert.ToInt32(_history.SelectedRows[0].Cells["Id"].Value) : -1;
        var report = _game.History.FirstOrDefault(x => x.Week == week);
        _results.DataSource = report == null ? new List<string>() :
            new[] { report.Name + "  |  RATING " + report.Rating,
                "ATTENDANCE " + report.Attendance.ToString("N0") +
                "  |  NET " + report.Profit.ToString("C0"), "" }
                .Concat(report.Results).ToList();
    }

    private sealed record Choice(int Id, string Name)
    {
        public override string ToString() => Name;
    }
}
