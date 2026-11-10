using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace ProWrestlingRevolution;

/// <summary>
/// Reusable, purpose-built modal window for small management tasks.
/// The shell supplies the same title, list, information pane and action footer
/// without turning a database selector or save picker into a full game page.
/// </summary>
internal abstract class UtilityPopupForm : Form
{
    [DllImport("user32.dll")] private static extern bool ReleaseCapture();
    [DllImport("user32.dll")] private static extern IntPtr SendMessage(
        IntPtr hwnd, int message, int wparam, int lparam);

    protected readonly AppPaths Paths;
    protected readonly SkinPalette Palette;
    protected readonly ListBox Items = new();
    protected readonly RichTextBox Information = new();
    protected readonly Label Context = new();
    protected readonly FlowLayoutPanel Actions = new();

    private readonly string _title;
    private readonly string _helpText;
    private readonly string _listHeading;
    private readonly string _infoHeading;

    protected UtilityPopupForm(AppPaths paths, SkinPalette palette,
        string title, string listHeading, string infoHeading, string helpText,
        int width, int height)
    {
        Paths = paths;
        Palette = palette;
        _title = title;
        _listHeading = listHeading;
        _infoHeading = infoHeading;
        _helpText = helpText;

        Name = "PwrUtilityPopup";
        Text = "Pro Wrestling Revolution | " + title;
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        ShowIcon = false;
        StartPosition = FormStartPosition.CenterParent;
        Size = new Size(width, height);
        MinimumSize = Size;
        MaximumSize = Size;
        AutoScaleMode = AutoScaleMode.None;
        BackColor = Color.FromArgb(7, 19, 31);
        ForeColor = Color.White;
        Font = new Font("Segoe UI", 10);
        DoubleBuffered = true;
        KeyPreview = true;
        BuildChrome();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        using var border = new Pen(Color.FromArgb(86, 173, 199), 1);
        e.Graphics.DrawRectangle(border, 0, 0, Width - 1, Height - 1);
    }

    private void BuildChrome()
    {
        // Explicit rows avoid docking-order overlap on Windows display scaling.
        var frame = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 4,
            Margin = new Padding(0),
            Padding = new Padding(1),
            BackColor = Color.FromArgb(7, 19, 31)
        };
        frame.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        frame.RowStyles.Add(new RowStyle(SizeType.Absolute, 47));
        frame.RowStyles.Add(new RowStyle(SizeType.Absolute, 45));
        frame.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        frame.RowStyles.Add(new RowStyle(SizeType.Absolute, 66));
        Controls.Add(frame);

        var header = new Panel
        {
            Name = "PopupHeader",
            Dock = DockStyle.Fill,
            Height = 47,
            Margin = new Padding(0),
            BackColor = Color.FromArgb(9, 25, 42)
        };
        header.Paint += (_, e) =>
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using var line = new Pen(Color.FromArgb(109, 206, 237), 1);
            g.DrawLine(line, 0, header.Height - 1, header.Width, header.Height - 1);
            using var red = new SolidBrush(Color.FromArgb(246, 39, 73));
            g.FillPolygon(red,
            [
                new Point(21, 12), new Point(33, 12),
                new Point(22, 34), new Point(10, 34)
            ]);
        };
        frame.Controls.Add(header, 0, 0);

        var label = new Label
        {
            Text = "PRO WRESTLING REVOLUTION   /   " + _title.ToUpperInvariant(),
            Location = new Point(43, 0),
            Size = new Size(width: Width - 165, height: 46),
            TextAlign = ContentAlignment.MiddleLeft,
            Font = new Font("Segoe UI", 11, FontStyle.Bold),
            ForeColor = Color.White,
            BackColor = Color.Transparent
        };
        header.Controls.Add(label);

        var close = HeaderButton("×", "Close this window", Color.FromArgb(255, 65, 89));
        close.Dock = DockStyle.Right;
        close.Click += (_, _) => { DialogResult = DialogResult.Cancel; Close(); };
        header.Controls.Add(close);

        var help = HeaderButton("?", "Help", Color.White);
        help.Dock = DockStyle.Right;
        help.Click += (_, _) => MessageBox.Show(this, _helpText,
            _title + " Help", MessageBoxButtons.OK, MessageBoxIcon.Information);
        header.Controls.Add(help);

        header.MouseDown += Drag;
        label.MouseDown += Drag;

        var contextPanel = new Panel
        {
            Dock = DockStyle.Fill,
            Height = 45,
            Margin = new Padding(0),
            Padding = new Padding(23, 7, 18, 0),
            BackColor = Color.FromArgb(8, 20, 34)
        };
        Context.Dock = DockStyle.Fill;
        Context.Name = "PopupContext";
        Context.ForeColor = Color.FromArgb(178, 216, 231);
        Context.Font = new Font("Segoe UI", 10, FontStyle.Bold);
        Context.TextAlign = ContentAlignment.MiddleLeft;
        contextPanel.Controls.Add(Context);
        frame.Controls.Add(contextPanel, 0, 1);

        var footer = new Panel
        {
            Dock = DockStyle.Fill,
            Height = 66,
            Margin = new Padding(0),
            BackColor = Color.FromArgb(9, 23, 38),
            Padding = new Padding(15, 11, 12, 9)
        };
        Actions.Dock = DockStyle.Fill;
        Actions.Name = "PopupActions";
        Actions.WrapContents = false;
        Actions.AutoScroll = true;
        Actions.BackColor = Color.Transparent;
        footer.Controls.Add(Actions);
        frame.Controls.Add(footer, 0, 3);

        var workspace = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Name = "PopupWorkspace",
            Margin = new Padding(0),
            Padding = new Padding(20, 13, 20, 15),
            ColumnCount = 2,
            RowCount = 1,
            BackColor = Color.FromArgb(7, 19, 31)
        };
        workspace.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 43));
        workspace.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 57));
        workspace.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        frame.Controls.Add(workspace, 0, 2);

        var left = Pane("PopupListPane", _listHeading);
        var right = Pane("PopupInformationPane", _infoHeading);
        left.Margin = new Padding(0, 0, 12, 0);
        right.Margin = new Padding(0);

        Items.Name = "PopupItemList";
        Items.Dock = DockStyle.Fill;
        Items.BorderStyle = BorderStyle.None;
        Items.IntegralHeight = false;
        Items.ItemHeight = 27;
        Items.Font = new Font("Segoe UI", 11);
        Items.BackColor = Color.FromArgb(4, 13, 24);
        Items.ForeColor = Color.White;
        Items.SelectionMode = SelectionMode.One;
        left.Controls.Add(Items, 0, 1);

        Information.Name = "PopupInformation";
        Information.Dock = DockStyle.Fill;
        Information.ReadOnly = true;
        Information.BorderStyle = BorderStyle.None;
        Information.ScrollBars = RichTextBoxScrollBars.Vertical;
        Information.BackColor = Color.FromArgb(12, 27, 42);
        Information.ForeColor = Color.FromArgb(228, 238, 245);
        Information.Font = new Font("Segoe UI", 10.5f);
        Information.DetectUrls = false;
        Information.TabStop = false;
        right.Controls.Add(Information, 0, 1);

        workspace.Controls.Add(left, 0, 0);
        workspace.Controls.Add(right, 1, 0);

    }

    private static TableLayoutPanel Pane(string name, string heading)
    {
        var outer = new TableLayoutPanel
        {
            Name = name,
            Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(12, 27, 42),
            Margin = new Padding(0),
            Padding = new Padding(2),
            ColumnCount = 1,
            RowCount = 2
        };
        outer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        outer.RowStyles.Add(new RowStyle(SizeType.Absolute, 35));
        outer.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        outer.Paint += (_, e) =>
        {
            using var border = new Pen(Color.FromArgb(71, 127, 154));
            e.Graphics.DrawRectangle(border, 0, 0, outer.Width - 1, outer.Height - 1);
        };
        var caption = new Label
        {
            Text = heading.ToUpperInvariant(),
            Dock = DockStyle.Fill,
            Margin = new Padding(0),
            Padding = new Padding(12, 0, 0, 0),
            ForeColor = Color.White,
            BackColor = Color.FromArgb(22, 44, 62),
            Font = new Font("Segoe UI", 10, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleLeft
        };
        outer.Controls.Add(caption, 0, 0);
        return outer;
    }

    private static Button HeaderButton(string text, string tooltip, Color foreground)
    {
        var result = new Button
        {
            Width = 43,
            Text = text,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 17, FontStyle.Bold),
            ForeColor = foreground,
            BackColor = Color.FromArgb(9, 25, 42),
            Cursor = Cursors.Hand,
            AccessibleName = tooltip
        };
        result.FlatAppearance.BorderSize = 0;
        new ToolTip().SetToolTip(result, tooltip);
        return result;
    }

    private void Drag(object? sender, MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left) return;
        ReleaseCapture();
        SendMessage(Handle, 0xA1, 0x2, 0);
    }

    protected Button AddAction(string label, Action onClick,
        bool primary = false, bool destructive = false, int width = 133)
    {
        var button = new Button
        {
            Name = "Action" + string.Concat(label.Where(char.IsLetterOrDigit)),
            Text = label,
            Size = new Size(width, 39),
            FlatStyle = FlatStyle.Flat,
            Margin = new Padding(0, 1, 8, 0),
            BackColor = primary ? Color.FromArgb(25, 76, 101) : Color.FromArgb(22, 40, 56),
            ForeColor = destructive ? Color.FromArgb(255, 95, 120) : Color.White,
            Font = new Font("Segoe UI", 9, FontStyle.Bold),
            Cursor = Cursors.Hand,
            UseVisualStyleBackColor = false
        };
        button.FlatAppearance.BorderColor = primary ?
            Color.FromArgb(101, 218, 246) : Color.FromArgb(60, 111, 138);
        button.FlatAppearance.MouseOverBackColor = Color.FromArgb(33, 83, 107);
        button.Click += (_, _) => RunSafely(onClick);
        Actions.Controls.Add(button);
        return button;
    }

    protected void RunSafely(Action command)
    {
        try { command(); }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, _title,
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    protected void Explain(string text) =>
        MessageBox.Show(this, text, _title,
            MessageBoxButtons.OK, MessageBoxIcon.Information);

    protected bool Confirm(string message) =>
        MessageBox.Show(this, message, _title, MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning) == DialogResult.Yes;

    protected string? AskForName(string caption, string initial)
    {
        using var input = new Form
        {
            Text = caption, StartPosition = FormStartPosition.CenterParent,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            ClientSize = new Size(430, 145),
            BackColor = Color.FromArgb(12, 27, 42),
            ForeColor = Color.White,
            MaximizeBox = false,
            MinimizeBox = false
        };
        var label = new Label
        {
            Text = caption,
            Location = new Point(16, 13), Width = 395, Height = 23,
            ForeColor = Color.White
        };
        var value = new TextBox
        {
            Text = initial,
            Location = new Point(17, 43), Width = 393,
            BackColor = Color.FromArgb(25, 46, 60),
            ForeColor = Color.White
        };
        var okay = new Button
        {
            Text = "OK", DialogResult = DialogResult.OK,
            Location = new Point(222, 100), Size = new Size(90, 32)
        };
        var cancel = new Button
        {
            Text = "Cancel", DialogResult = DialogResult.Cancel,
            Location = new Point(319, 100), Size = new Size(90, 32)
        };
        input.Controls.AddRange([label, value, okay, cancel]);
        input.AcceptButton = okay;
        input.CancelButton = cancel;
        return input.ShowDialog(this) == DialogResult.OK ? value.Text : null;
    }
}

internal sealed class DatabaseSelectorDialog : UtilityPopupForm
{
    private readonly Button _use;

    internal DatabaseSelectorDialog(AppPaths paths, SkinPalette palette)
        : base(paths, palette, "Database Selector", "Available Databases",
            "Database Information",
            "Choose an installed universe on the left, review its file details, " +
            "then select Use This Database. Import MDB copies a new universe into " +
            "the game without modifying the original file.",
            910, 525)
    {
        Name = "DatabaseSelectorDialog";
        _use = AddAction("Use This Database", SelectDatabase, true, width: 191);
        AddAction("Import MDB", ImportDatabase, width: 125);
        AddAction("Open Folder", () => OpenFolder(Paths.Databases), width: 133);
        AddAction("Cancel", () => { DialogResult = DialogResult.Cancel; Close(); }, width: 110);
        Items.SelectedIndexChanged += (_, _) => UpdateDetails();
        Items.DoubleClick += (_, _) => RunSafely(SelectDatabase);
        RefreshDatabases();
    }

    internal string? SelectedDatabaseName { get; private set; }

    private void RefreshDatabases(string? preferred = null)
    {
        string active = Paths.ReadSetting("Database", "");
        Context.Text = "CURRENT DATABASE  " + (string.IsNullOrWhiteSpace(active) ? "None selected" : active);
        Items.BeginUpdate();
        try
        {
            Items.Items.Clear();
            foreach (var world in Paths.Universes()) Items.Items.Add(world);
            Items.DisplayMember = nameof(UniverseEntry.Name);
        }
        finally { Items.EndUpdate(); }

        var selected = Items.Items.Cast<UniverseEntry>().FirstOrDefault(x =>
            x.Name.Equals(preferred ?? active, StringComparison.OrdinalIgnoreCase));
        if (selected is not null) Items.SelectedItem = selected;
        else if (Items.Items.Count > 0) Items.SelectedIndex = 0;
        UpdateDetails();
    }

    private void UpdateDetails()
    {
        var chosen = Items.SelectedItem as UniverseEntry;
        _use.Enabled = chosen is not null;
        if (chosen is null)
        {
            Information.Text = "No databases have been installed.\n\nUse Import MDB to add a universe.";
            return;
        }
        var file = new FileInfo(chosen.MdbPath);
        int saves = Paths.Careers().Count(x => x.UniverseName.Equals(chosen.Name,
            StringComparison.OrdinalIgnoreCase));
        Information.Text =
            chosen.Name + "\n\n" +
            "Source file: " + file.Name + "\n" +
            "File size: " + (file.Length / 1048576d).ToString("N2") + " MB\n" +
            "Last modified: " + file.LastWriteTime.ToString("g") + "\n" +
            "Saved careers: " + saves.ToString("N0") + "\n\n" +
            "Location:\n" + chosen.DirectoryPath + "\n\n" +
            (Paths.ReadSetting("Database", "").Equals(chosen.Name, StringComparison.OrdinalIgnoreCase)
                ? "This is the active database."
                : "Select Use This Database to make this universe active.") +
            "\n\nDescription, author and version will appear here when the game " +
            "supports database metadata. They are not inferred from the MDB filename.";
    }

    private void SelectDatabase()
    {
        if (Items.SelectedItem is not UniverseEntry item) return;
        Paths.WriteSetting("Database", item.Name);
        SelectedDatabaseName = item.Name;
        DialogResult = DialogResult.OK;
        Close();
    }

    private void ImportDatabase()
    {
        using var picker = new OpenFileDialog
        {
            Title = "Import an Access MDB universe",
            Filter = "Access Database (*.mdb)|*.mdb",
            CheckFileExists = true
        };
        if (picker.ShowDialog(this) != DialogResult.OK) return;
        string suggested = Path.GetFileNameWithoutExtension(picker.FileName);
        string? name = AskForName("Name for imported database", suggested);
        if (name is null) return;
        UniverseEntry result = Paths.ImportUniverse(picker.FileName, name);
        RefreshDatabases(result.Name);
        Explain("Imported database " + result.Name +
            ".\n\nSelect Use This Database to activate it.");
    }

    private static void OpenFolder(string path)
    {
        Directory.CreateDirectory(path);
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(path)
            { UseShellExecute = true });
    }
}

internal sealed class LoadGameDialog : UtilityPopupForm
{
    private readonly Button _load;
    private readonly Button _rename;
    private readonly Button _copy;
    private readonly Button _recovery;
    private readonly Button _delete;
    private readonly Button _check;
    private readonly Button _folder;

    internal LoadGameDialog(AppPaths paths, SkinPalette palette)
        : base(paths, palette, "Load Game", "Saved Careers",
            "Save Game Information",
            "Select a saved career to inspect it. Rename and Copy work on its " +
            "save folder. Recovery restores the initial backup after saving a " +
            "separate safety copy. Delete archives the whole save folder outside " +
            "SaveGames, so it is not permanently erased. Loading the simulation " +
            "is not available until the gameplay engine is connected.",
            1110, 615)
    {
        Name = "LoadGameDialog";
        _load = AddAction("Load", () => Explain(
            "The save file exists, but resuming gameplay is not implemented yet."), true, width: 105);
        _rename = AddAction("Rename", Rename, width: 112);
        _copy = AddAction("Copy", Copy, width: 104);
        _recovery = AddAction("Recovery", Recover, width: 117);
        _delete = AddAction("Delete", Archive, destructive: true, width: 112);
        _check = AddAction("Check Save", Check, width: 123);
        _folder = AddAction("Open Folder", OpenSaveFolder, width: 132);
        AddAction("Close", () => Close(), width: 105);
        Items.DisplayMember = nameof(SaveItem.DisplayName);
        Items.SelectedIndexChanged += (_, _) => UpdateDetails();
        RefreshSaves();
    }

    private sealed record SaveItem(CareerEntry Career)
    {
        public string DisplayName => Career.Name + "   (" + Career.UniverseName + ")";
    }

    private CareerEntry? Selected => (Items.SelectedItem as SaveItem)?.Career;

    private void RefreshSaves(string? preferredPath = null)
    {
        string? current = preferredPath ?? Selected?.MdbPath;
        Items.BeginUpdate();
        try
        {
            Items.Items.Clear();
            foreach (var save in Paths.Careers()) Items.Items.Add(new SaveItem(save));
        }
        finally { Items.EndUpdate(); }

        var item = Items.Items.Cast<SaveItem>().FirstOrDefault(x =>
            string.Equals(x.Career.MdbPath, current, StringComparison.OrdinalIgnoreCase));
        if (item is not null) Items.SelectedItem = item;
        else if (Items.Items.Count > 0) Items.SelectedIndex = 0;
        UpdateDetails();
    }

    private void UpdateDetails()
    {
        CareerEntry? save = Selected;
        Context.Text = "SAVED CAREERS  " + Items.Items.Count.ToString("N0") +
            "     /     Select a career to view its information";
        _load.Enabled = false; // No gameplay engine: never claim loading works.
        foreach (var b in new[] { _rename, _copy, _delete, _check, _folder })
            b.Enabled = save is not null;
        _recovery.Enabled = save is not null &&
            File.Exists(Path.Combine(Path.GetDirectoryName(save.MdbPath)!,
                "Backups", "initial.mdb"));

        if (save is null)
        {
            Information.Text = "No saved careers were found.\n\n" +
                "Create a career from New Game to add one here.";
            return;
        }
        var file = new FileInfo(save.MdbPath);
        var dir = Path.GetDirectoryName(save.MdbPath)!;
        var initial = Path.Combine(dir, "Backups", "initial.mdb");
        bool validHeader = AccessDatabase.LooksLikeMdb(save.MdbPath);
        Information.Text =
            save.Name + "\n\n" +
            "Universe: " + save.UniverseName + "\n" +
            "Save file: " + file.Name + "\n" +
            "File size: " + (file.Length / 1048576d).ToString("N2") + " MB\n" +
            "Last modified: " + file.LastWriteTime.ToString("g") + "\n" +
            "MDB header: " + (validHeader ? "Recognized" : "Not recognized") + "\n" +
            "Initial recovery file: " + (File.Exists(initial) ? "Available" : "Unavailable") +
            "\n\nLocation:\n" + dir + "\n\n" +
            "Gameplay loading will become available once the simulation engine " +
            "is connected. This window manages existing save files now.";
    }

    private void Rename()
    {
        if (Selected is not { } current) return;
        string? name = AskForName("Rename saved career", current.Name);
        if (name is null) return;
        CareerEntry changed = Paths.RenameCareer(current, name);
        RefreshSaves(changed.MdbPath);
    }

    private void Copy()
    {
        if (Selected is not { } current) return;
        string? name = AskForName("Name for copied career", current.Name + " Copy");
        if (name is null) return;
        CareerEntry copy = Paths.CopyCareer(current, name);
        RefreshSaves(copy.MdbPath);
    }

    private void Recover()
    {
        if (Selected is not { } current) return;
        if (!Confirm("Restore '" + current.Name + "' from its initial backup?\n\n" +
            "The current PWRSave.mdb will be preserved in Backups before restoration. " +
            "This resets the active career database to its initial snapshot."))
            return;
        string safety = Paths.RestoreCareerInitialBackup(current);
        RefreshSaves(current.MdbPath);
        Explain("Initial backup restored.\n\nPrevious save preserved at:\n" + safety);
    }

    private void Archive()
    {
        if (Selected is not { } current) return;
        if (!Confirm("Remove '" + current.Name + "' from the saved careers list?\n\n" +
            "Its entire folder will be moved into System/DeletedSaves for recovery. " +
            "No MDB data will be permanently destroyed."))
            return;
        string archived = Paths.ArchiveCareer(current);
        RefreshSaves();
        Explain("Save removed from active careers.\n\nArchive location:\n" + archived);
    }

    private void Check()
    {
        if (Selected is not { } current) return;
        if (!AccessDatabase.LooksLikeMdb(current.MdbPath))
        {
            Explain("The save file does not have a recognized MDB header.\n" +
                "This check does not alter or repair files.");
            return;
        }
        try
        {
            int tables = AccessDatabase.TableNames(current.MdbPath, null).Count;
            Explain("The MDB header is recognized, and " + tables +
                " user tables were accessible.\n\nThis is a basic readability check, " +
                "not a full database repair or relational integrity validation.");
        }
        catch (Exception ex)
        {
            Explain("The MDB header was recognized, but the file could not be read " +
                "through the installed Access provider.\n\n" + ex.Message +
                "\n\nThe check did not change the save.");
        }
    }

    private void OpenSaveFolder()
    {
        if (Selected is not { } current) return;
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(
            Path.GetDirectoryName(current.MdbPath)!) { UseShellExecute = true });
    }
}
