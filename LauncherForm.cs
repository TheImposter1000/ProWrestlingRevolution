using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace ProWrestlingRevolution;

public sealed partial class LauncherForm : Form
{
    [DllImport("user32.dll")] private static extern bool ReleaseCapture();
    [DllImport("user32.dll")] private static extern IntPtr SendMessage(IntPtr handle, int message, int wparam, int lparam);

    private readonly AppPaths _paths;
    private SkinPalette _skin;
    private readonly Panel _page = new() { Dock = DockStyle.Fill };
    private readonly MetalSidebar _sidebar;
    private readonly MetalTopBar _topbar;
    private readonly Dictionary<string, Image> _assetCache = new(StringComparer.OrdinalIgnoreCase);
    private string? _hoverIcon;
    private bool _soundEnabled = true;
    private readonly List<Button> _navigation = [];
    private readonly Label _status = new();
    private string _currentPage = "HOME";

    public LauncherForm(AppPaths paths)
    {
        _paths = paths;
        _skin = paths.LoadSkin(paths.ReadSetting("Skin", "Obsidian"));
        _sidebar = new MetalSidebar(_skin);
        _topbar = new MetalTopBar(_skin);
        Text = "Pro Wrestling Revolution";
        AutoScaleMode = AutoScaleMode.None;
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.CenterScreen;
        Font = new Font("Segoe UI", 10);
        ClientSize = new Size(SkinArt.WindowWidth, SkinArt.WindowHeight);
        MinimumSize = new Size(SkinArt.WindowWidth, SkinArt.WindowHeight);
        MaximumSize = new Size(SkinArt.WindowWidth, SkinArt.WindowHeight);
        KeyPreview = true;
        DoubleBuffered = true;
        BuildChrome();
        ShowHome();
        FormClosed += (_, _) => ClearAssetCache();
        KeyDown += HandleShortcut;
    }

    private void HandleShortcut(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Escape && _currentPage != "HOME")
        {
            Navigate("back");
            e.Handled = true;
            return;
        }
        if (!e.Alt) return;
        string? key = e.KeyCode switch
        {
            Keys.N => "new",
            Keys.L => "load",
            Keys.D => "database",
            Keys.E => "editor",
            Keys.S => "skins",
            Keys.A => "achievements",
            Keys.C => "credits",
            Keys.H => "back",
            _ => null
        };
        if (key is not null)
        {
            RunSafe(() => Navigate(key));
            e.Handled = true;
        }
    }

    private void Navigate(string key)
    {

        switch (key)
        {
            case "back":
                if (_currentPage == "DATABASE EDITOR" &&
                    _page.Controls.OfType<DatabaseEditorWorkspace>()
                        .FirstOrDefault()?.TryCloseWorkerProfile() == true)
                    break;
                if (_currentPage != "HOME") ShowHome();
                break;
            case "updates": _ = GameUpdate.CheckAndInstallAsync(this, _updateButton); break;
            case "new": ShowNewGame(); break;
            case "load": ShowLoadGame(); break;
            case "database": ShowDatabaseSelector(); break;
            case "editor": ShowDatabaseEditor(); break;
            case "skins": ShowSkins(); break;
            case "achievements": ShowAchievements(); break;
            case "credits": ShowCredits(); break;
        }
    }

    private void DragWindow(object? sender, MouseEventArgs args)
    {
        if (args.Button != MouseButtons.Left) return;
        ReleaseCapture();
        SendMessage(Handle, 0xA1, 0x2, 0);
    }

    private Button TopButton(string text, Action click)
    {
        var b = new Button
        {
            Text = text, Dock = DockStyle.Right, Width = 50,
            FlatStyle = FlatStyle.Flat, BackColor = _skin.Surface,
            ForeColor = _skin.Foreground, Font = new Font("Segoe UI", 17, FontStyle.Regular),
            Cursor = Cursors.Hand, TabStop = false
        };
        b.FlatAppearance.BorderSize = 0;
        b.Click += (_, _) => click();
        return b;
    }

    private Button NavigationButton(string text, Action action)
    {
        var button = new Button
        {
            Text = text, Size = new Size(238, 46), Margin = new Padding(0, 0, 0, 8),
            TextAlign = ContentAlignment.MiddleLeft, Padding = new Padding(16, 0, 0, 0),
            FlatStyle = FlatStyle.Flat, BackColor = _skin.Button, ForeColor = _skin.Foreground,
            Font = new Font("Segoe UI", 10, FontStyle.Bold), Cursor = Cursors.Hand,
            UseVisualStyleBackColor = false
        };
        button.FlatAppearance.BorderColor = _skin.Border;
        button.FlatAppearance.MouseOverBackColor = _skin.Surface;
        button.Click += (_, _) => RunSafe(action);
        _navigation.Add(button);
        return button;
    }

    private Button ActionButton(string text, Action action, bool primary = false)
    {
        var b = new Button
        {
            Text = text, AutoSize = true, MinimumSize = new Size(162, 44),
            Margin = new Padding(0, 0, 11, 10), Padding = new Padding(12, 4, 12, 4),
            FlatStyle = FlatStyle.Flat, BackColor = primary ? _skin.Accent : _skin.Button,
            ForeColor = primary ? _skin.Background : _skin.Foreground,
            Font = new Font("Segoe UI", 10, FontStyle.Bold), Cursor = Cursors.Hand
        };
        b.FlatAppearance.BorderColor = primary ? _skin.Accent : _skin.Border;
        b.Click += (_, _) => RunSafe(action);
        return b;
    }

    private Label TextLabel(string text, float size = 11, bool bold = false, Color? color = null)
    {
        return new Label
        {
            Text = text, AutoSize = false,
            ForeColor = color ?? _skin.Foreground, Font = new Font("Segoe UI", size,
            bold ? FontStyle.Bold : FontStyle.Regular), TextAlign = ContentAlignment.MiddleLeft
        };
    }

    private void RunSafe(Action action)
    {
        try { action(); }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Pro Wrestling Revolution",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private Panel NewPage(string pageName, string description, out Panel content)
    {
        SetWorkerSectionChrome(null);
        _currentPage = pageName;
        _hoverIcon = null;
        // Interior pages need the regular navigation; the title hides it.
        _editorPageTitle.Visible = false;
        _sidebar.Visible = true;
        _topbar.Visible = true;
        RenderSidebar();
        ClearPageContent();
        _page.BackColor = _skin.Background;
        var root = new Panel { Dock = DockStyle.Fill, BackColor = _skin.Background };
        var header = new Panel { Dock = DockStyle.Top, Height = 125, BackColor = _skin.Background };
        var title = TextLabel(pageName, 26, true);
        title.Location = new Point(37, 22);
        title.Size = new Size(760, 46);
        var desc = TextLabel(description, 10.5f, color: _skin.Muted);
        desc.Location = new Point(41, 74);
        desc.Size = new Size(990, 35);
        header.Controls.Add(title);
        header.Controls.Add(desc);
        content = new Panel { Dock = DockStyle.Fill, Padding = new Padding(34, 8, 34, 30) };
        root.Controls.Add(content);
        root.Controls.Add(header);
        _page.Controls.Add(root);
        return root;
    }

    private ListBox StyledList()
    {
        return new ListBox
        {
            Dock = DockStyle.Fill, IntegralHeight = false, BorderStyle = BorderStyle.FixedSingle,
            BackColor = _skin.Surface, ForeColor = _skin.Foreground,
            Font = new Font("Segoe UI", 12), ItemHeight = 33
        };
    }

    private TextBox StyledInput(string value = "", bool secret = false)
    {
        return new TextBox
        {
            Text = value, Width = 270, Height = 34, UseSystemPasswordChar = secret,
            BackColor = _skin.Surface, ForeColor = _skin.Foreground,
            BorderStyle = BorderStyle.FixedSingle, Font = new Font("Segoe UI", 12)
        };
    }

    private void ShowDatabaseSelector()
    {
        using var dialog = new DatabaseSelectorDialog(_paths, _skin);
        dialog.FillLauncherWindow(this);
        dialog.AttachMainNavigation("database");
        var result = dialog.ShowDialog(this);
        if (dialog.RequestedMainNavigation is string requested)
        {
            Navigate(requested);
            return;
        }
        if (result == DialogResult.OK && _currentPage == "NEW GAME")
            ShowNewGame();
    }

    private void ShowNewGame()
    {
        NewPage("NEW GAME", "Create a fresh, independent career from a selected database.", out var content);
        var worlds = StyledList();
        var right = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown,
            WrapContents = false, AutoScroll = true, Padding = new Padding(26, 15, 0, 0)
        };
        var caption = TextLabel("CAREER NAME", 12, true, _skin.Accent);
        caption.Size = new Size(320, 38);
        var name = StyledInput("My Wrestling Career");
        right.Controls.Add(caption);
        right.Controls.Add(name);
        right.Controls.Add(ActionButton("CREATE CAREER", () =>
        {
            if (worlds.SelectedItem is not UniverseEntry universe)
                throw new InvalidOperationException("Import and select a database before creating a career.");
            var save = _paths.CreateCareer(universe, name.Text);
            MessageBox.Show(this,
                "Career created in:\n" + Path.GetDirectoryName(save.MdbPath) +
                "\n\nThe Phase One build sets up the MDB career. Gameplay integration follows in a later phase.",
                "Career Database Ready");
            ShowLoadGame();
        }, true));
        right.Controls.Add(ActionButton("DATABASE SELECTOR", ShowDatabaseSelector));
        var message = TextLabel("A new game is a COPY of its universe MDB.\nThe starting universe remains unchanged.\nBackups/initial.mdb is created automatically.", 10, color: _skin.Muted);
        message.Size = new Size(340, 135);
        right.Controls.Add(message);
        foreach (var world in _paths.Universes()) worlds.Items.Add(world);
        var active = _paths.ReadSetting("Database", "");
        for (int i = 0; i < worlds.Items.Count; i++)
            if (((UniverseEntry)worlds.Items[i]!).Name == active) worlds.SelectedIndex = i;
        if (worlds.SelectedIndex < 0 && worlds.Items.Count > 0) worlds.SelectedIndex = 0;
        content.Controls.Add(MakeSplit(worlds, right));
    }

    private void ShowLoadGame()
    {
        // Independent full-window Load Game screen: cover the title artwork
        // completely, while Close/Escape restores the existing launcher state.
        // Gameplay loading remains unavailable until the engine is connected.
        using var dialog = new LoadGameDialog(_paths, _skin);
        dialog.FillLauncherWindow(this);
        dialog.AttachMainNavigation("load");
        dialog.ShowDialog(this);
        if (dialog.RequestedMainNavigation is string requested)
            Navigate(requested);
    }

    private void ShowDatabaseEditor()
    {
        // This is the simple directory screen, not a record editing form.
        // Main menu stays visible while the editor owns the content area.
        _currentPage = "DATABASE EDITOR";
        _hoverIcon = null;
        _sidebar.Visible = true;
        RenderSidebar();
        _topbar.Visible = true;
        _editorPageTitle.Visible = false;
        ClearPageContent();
        _page.BackColor = Color.FromArgb(7, 18, 30);
        _page.Controls.Add(new DatabaseEditorWorkspace(_paths, _skin, ShowHome, Navigate));
    }

    private void ShowSkins()
    {
        NewPage("SKINS", "Switch the game interface without changing databases or saved careers.", out var content);
        var skins = StyledList();
        var right = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown,
            WrapContents = false, AutoScroll = true, Padding = new Padding(26, 15, 0, 0)
        };
        var information = TextLabel("", 10.5f, color: _skin.Muted);
        information.Size = new Size(390, 145);
        right.Controls.Add(ActionButton("APPLY SKIN", () =>
        {
            if (skins.SelectedItem is not string skinName) return;
            _paths.WriteSetting("Skin", skinName);
            _skin = _paths.LoadSkin(skinName);
            ApplySkin();
            ShowSkins();
        }, true));
        right.Controls.Add(ActionButton("OPEN SKINS FOLDER", () => OpenFolder(_paths.SkinsRoot)));
        right.Controls.Add(information);
        skins.SelectedIndexChanged += (_, _) =>
        {
            if (skins.SelectedItem is string selected)
                information.Text = "Selected: " + selected +
                    "\nActive: " + _skin.Id +
                    "\n\nCustom skins are folders inside Skins. Add images in " +
                    "Backgrounds, SideBars, TopBars or other category folders. " +
                    "Missing images use Obsidian artwork automatically.";
        };
        foreach (var skinName in _paths.Skins()) skins.Items.Add(skinName);
        var selectedIndex = skins.Items.IndexOf(_skin.Id);
        if (selectedIndex >= 0) skins.SelectedIndex = selectedIndex;
        content.Controls.Add(MakeSplit(skins, right));
    }

    private void ShowAchievements()
    {
        NewPage("ACHIEVEMENTS", "Your long-term wrestling management milestones will appear here.", out var content);
        var label = TextLabel("Achievement tracking will be connected after career gameplay is implemented.", 14, color: _skin.Muted);
        label.Dock = DockStyle.Top;
        label.Height = 90;
        content.Controls.Add(label);
    }

    private void ShowCredits()
    {
        NewPage("CREDITS", "About Pro Wrestling Revolution.", out var content);
        var label = TextLabel("PRO WRESTLING REVOLUTION\n\nAn original single-player wrestling management project.\n" +
            "Developed as an independent Windows desktop application.\n\n" +
            "Not affiliated with Grey Dog Software or Total Extreme Wrestling.\n" +
            "No third-party copyrighted game graphics or databases are bundled.",
            13, color: _skin.Foreground);
        label.Dock = DockStyle.Top;
        label.Height = 280;
        content.Controls.Add(label);
    }

    private static SplitContainer MakeSplit(Control left, Control right, int width = 380)
    {
        var split = new SplitContainer { Dock = DockStyle.Fill, SplitterWidth = 8, BackColor = Color.FromArgb(49, 63, 82) };
        split.Panel1.Padding = new Padding(0, 0, 12, 0);
        split.Panel2.Padding = new Padding(12, 0, 0, 0);
        split.Panel1.Controls.Add(left);
        split.Panel2.Controls.Add(right);
        split.HandleCreated += (_, _) =>
        {
            if (split.Width > width + 180) split.SplitterDistance = width;
        };
        return split;
    }

    private static void OpenFolder(string path)
    {
        Directory.CreateDirectory(path);
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(path) { UseShellExecute = true });
    }

    private string? Prompt(string heading, string description, string initial)
    {
        using var dialog = new Form
        {
            Text = heading, Width = 450, Height = 220, StartPosition = FormStartPosition.CenterParent,
            FormBorderStyle = FormBorderStyle.FixedDialog, MaximizeBox = false, MinimizeBox = false,
            BackColor = _skin.Background, ForeColor = _skin.Foreground
        };
        var prompt = TextLabel(description, 11);
        prompt.Location = new Point(24, 20); prompt.Size = new Size(380, 33);
        var input = StyledInput(initial);
        input.Location = new Point(24, 68); input.Width = 386;
        var okay = new Button
        {
            Text = "OK", DialogResult = DialogResult.OK, Location = new Point(225, 120),
            Width = 88, Height = 36
        };
        var cancel = new Button
        {
            Text = "Cancel", DialogResult = DialogResult.Cancel, Location = new Point(320, 120),
            Width = 88, Height = 36
        };
        dialog.Controls.AddRange([prompt, input, okay, cancel]);
        dialog.AcceptButton = okay;
        dialog.CancelButton = cancel;
        return dialog.ShowDialog(this) == DialogResult.OK ? input.Text : null;
    }

    private sealed record CareerItem(CareerEntry Data)
    {
        public override string ToString() => Data.UniverseName + "  /  " + Data.Name;
    }

    private sealed class HomeArtwork : Panel
    {
        private readonly SkinPalette _palette;
        private readonly Image? _background;
        private readonly Image? _logo;

        public HomeArtwork(SkinPalette palette)
        {
            _palette = palette;
            DoubleBuffered = true;
            _background = LoadCopy(palette.BackgroundImage);
            _logo = LoadCopy(palette.LogoImage);
            Disposed += (_, _) => { _background?.Dispose(); _logo?.Dispose(); };
        }

        private static Image? LoadCopy(string? path)
        {
            if (path is null) return null;
            try
            {
                using var stream = File.OpenRead(path);
                using var original = Image.FromStream(stream);
                return new Bitmap(original);
            }
            catch { return null; }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using (var bg = new LinearGradientBrush(ClientRectangle, _palette.Background, _palette.Surface, 35f))
                g.FillRectangle(bg, ClientRectangle);
            if (_background is not null)
            {
                g.DrawImage(_background, ClientRectangle);
                using var veil = new SolidBrush(Color.FromArgb(125, _palette.Background));
                g.FillRectangle(veil, ClientRectangle);
            }
            int cx = Width / 2;
            int cy = Height / 2;
            using (var pen = new Pen(Color.FromArgb(70, _palette.Accent), 2f))
            {
                for (int i = 0; i < 4; i++)
                {
                    var r = (int)(Math.Min(Width, Height) * (0.30 + i * 0.11));
                    g.DrawEllipse(pen, cx - r, cy - r, r * 2, r * 2);
                }
                g.DrawLine(pen, 0, Height - 120, Width, Height - 210);
                g.DrawLine(pen, 0, 130, Width, 40);
            }
            using (var triangle = new SolidBrush(Color.FromArgb(30, _palette.Accent)))
            {
                var points = new[]
                {
                    new Point(cx - 345, cy - 155), new Point(cx + 360, cy - 155),
                    new Point(cx + 250, cy + 140), new Point(cx, cy + 220),
                    new Point(cx - 250, cy + 140)
                };
                g.FillPolygon(triangle, points);
            }

            if (_logo is not null)
            {
                int maxW = Math.Min(700, Width - 80);
                int maxH = 310;
                float scale = Math.Min((float)maxW / _logo.Width, (float)maxH / _logo.Height);
                int w = (int)(_logo.Width * scale), h = (int)(_logo.Height * scale);
                g.DrawImage(_logo, cx - w / 2, cy - h / 2, w, h);
            }
            else
            {
                using var small = new Font("Segoe UI", Math.Clamp(Width / 55f, 14, 24), FontStyle.Bold);
                using var large = new Font("Segoe UI", Math.Clamp(Width / 15.5f, 42, 92), FontStyle.Bold);
                using var titleBrush = new SolidBrush(_palette.Foreground);
                using var accentBrush = new SolidBrush(_palette.Accent);
                Center(g, "PRO WRESTLING", small, accentBrush, cx, cy - 78);
                Center(g, "REVOLUTION", large, titleBrush, cx, cy + 7);
            }
            using var footer = new Font("Segoe UI", 11, FontStyle.Bold);
            using var muted = new SolidBrush(_palette.Muted);
            Center(g, "BUILD YOUR PROMOTION. SHAPE THE INDUSTRY.", footer, muted, cx, cy + 140);
            using var smallFooter = new Font("Segoe UI", 9);
            g.DrawString("SINGLE PLAYER  •  NATIVE WINDOWS  •  ACCESS MDB UNIVERSES",
                smallFooter, muted, new PointF(30, Height - 49));
        }

        private static void Center(Graphics g, string text, Font font, Brush brush, int cx, int baseline)
        {
            var dimensions = g.MeasureString(text, font);
            g.DrawString(text, font, brush, cx - dimensions.Width / 2f, baseline - dimensions.Height / 2f);
        }
    }
}