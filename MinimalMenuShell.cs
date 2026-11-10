using System.Drawing;
using System.Windows.Forms;

namespace ProWrestlingRevolution;

/// <summary>
/// Version 0.5 minimal desktop shell. Navigation and utility chrome are kept
/// independent of the user's title artwork, MDB database and simulation code.
/// </summary>
public sealed partial class LauncherForm
{
    private readonly Dictionary<string, SidebarMenuRow> _menuRows = new(StringComparer.OrdinalIgnoreCase);
    private readonly Label _menuHeader = new();
    private readonly Label _titleText = new();
    private readonly Label _editorPageTitle = new();
    private readonly Label _versionText = new();
    private readonly Button _updateButton = new();
    private readonly Font _navFont = new("Segoe UI", 10.5f, FontStyle.Regular);
    private readonly Font _navFontActive = new("Segoe UI", 10.5f, FontStyle.Bold);

    private void BuildChrome()
    {
        _page.BackColor = _skin.Background;
        _sidebar.BackColor = _skin.Sidebar;
        _topbar.BackColor = _skin.Surface;
        Controls.Add(_page);
        Controls.Add(_sidebar);
        Controls.Add(_topbar);

        // Keep the native interior-screen updater; the title screen has its
        // own painted button that invokes this same update action.
        _updateButton.Name = "CheckUpdatesButton";
        _updateButton.Text = "Check for Updates";
        _updateButton.Dock = DockStyle.Bottom;
        _updateButton.Height = 46;
        _updateButton.FlatStyle = FlatStyle.Flat;
        _updateButton.BackColor = _skin.Button;
        _updateButton.ForeColor = _skin.Foreground;
        _updateButton.FlatAppearance.BorderColor = _skin.Border;
        _updateButton.Font = new Font("Segoe UI", 10, FontStyle.Bold);
        _updateButton.Cursor = Cursors.Hand;
        _updateButton.Click += async (_, _) =>
            await GameUpdate.CheckAndInstallAsync(this, _updateButton);
        _sidebar.Controls.Add(_updateButton);

        // One unified row per menu option: vector icon, text and click target.
        // The previous 57px TEW-style image strip is deliberately not added.
        _menuHeader.Text = "M A I N   M E N U";
        _menuHeader.Location = new Point(28, 51);
        _menuHeader.Size = new Size(_sidebar.Width - 36, 28);
        _menuHeader.TextAlign = ContentAlignment.MiddleLeft;
        _menuHeader.BackColor = Color.Transparent;
        _menuHeader.ForeColor = _skin.Muted;
        _menuHeader.Font = new Font("Segoe UI", 9, FontStyle.Regular);
        _menuHeader.Paint += (_, e) =>
        {
            using var rule = new Pen(Color.FromArgb(100, _skin.AccentSecondary), 1);
            e.Graphics.DrawLine(rule, 111, 14, _menuHeader.Width - 4, 14);
        };
        _sidebar.Controls.Add(_menuHeader);

        int menuIndex = 0;
        foreach (var (key, label, _) in SkinArt.MenuIcons)
        {
            var text = key switch
            {
                "back" => "Home",
                "database" => "Database",
                "editor" => "Editor",
                _ => label
            };
            int center = key == "back" ? 63 : 124 + (menuIndex++ * 56);
            var item = new SidebarMenuRow(key, text, _skin, _navFont, _navFontActive)
            {
                Name = "Menu_" + key,
                Location = new Point(0, center - 27),
                Width = _sidebar.Width
            };
            item.MouseEnter += (_, _) => SetHoverIcon(key);
            item.MouseLeave += (_, _) => SetHoverIcon(null);
            item.Click += (_, _) => RunSafe(() => Navigate(key));
            _menuRows.Add(key, item);
            _sidebar.Controls.Add(item);
        }

        // Brushed-metal chrome replaces the separate raster button strip.
        // Every utility button is clickable, keyboard-accessible and has a tooltip name.
        _titleText.Text = "PRO WRESTLING REVOLUTION";
        _titleText.Location = new Point(56, 0);
        _titleText.Size = new Size(250, _topbar.Height - 1);
        _titleText.TextAlign = ContentAlignment.MiddleLeft;
        _titleText.BackColor = Color.Transparent;
        _titleText.ForeColor = _skin.Foreground;
        _titleText.Font = new Font("Segoe UI", 11.5f, FontStyle.Bold);

        _versionText.Text = "v" + GameUpdate.DisplayVersion;
        _versionText.Location = new Point(326, 0);
        _versionText.Size = new Size(75, _topbar.Height - 1);
        _versionText.TextAlign = ContentAlignment.MiddleLeft;
        _versionText.BackColor = Color.Transparent;
        _versionText.ForeColor = _skin.Muted;
        _versionText.Font = new Font("Segoe UI", 9.5f);

        _editorPageTitle.Text = "DATABASE EDITOR";
        _editorPageTitle.Location = new Point(452, 0);
        _editorPageTitle.Size = new Size(275, _topbar.Height - 1);
        _editorPageTitle.TextAlign = ContentAlignment.MiddleLeft;
        _editorPageTitle.BackColor = Color.Transparent;
        _editorPageTitle.ForeColor = _skin.Foreground;
        _editorPageTitle.Font = new Font("Segoe UI", 13, FontStyle.Bold | FontStyle.Italic);
        _editorPageTitle.Visible = false;

        _topbar.Controls.Add(_titleText);
        _topbar.Controls.Add(_versionText);
        _topbar.Controls.Add(_editorPageTitle);
        _topbar.MouseDown += DragWindow;
        _titleText.MouseDown += DragWindow;
        _versionText.MouseDown += DragWindow;

        var sound = new TopUtilityButton("sound", _skin);
        var help = new TopUtilityButton("help", _skin);
        var minimize = new TopUtilityButton("minimize", _skin);
        var close = new TopUtilityButton("close", _skin);
        var utility = new[] { sound, help, minimize, close };

        for (int i = 0; i < utility.Length; i++)
        {
            utility[i].Location = new Point(
                SkinArt.WindowWidth - (utility.Length - i) * utility[i].Width, 0);
            _topbar.Controls.Add(utility[i]);
        }

        sound.SetSoundEnabled(_soundEnabled);
        sound.Click += (_, _) =>
        {
            _soundEnabled = !_soundEnabled;
            sound.SetSoundEnabled(_soundEnabled);
        };
        help.Click += (_, _) => MessageBox.Show(this,
            "Use the navigation at left to choose an option.\n\n" +
            "New Game creates an independent MDB career.\n" +
            "Database Selector imports your Access MDB.\n" +
            "Skins lets you change the game's graphic assets.",
            "Main Menu Help", MessageBoxButtons.OK, MessageBoxIcon.Information);
        minimize.Click += (_, _) => WindowState = FormWindowState.Minimized;
        close.Click += (_, _) => Close();

        FormClosed += (_, _) =>
        {
            _navFont.Dispose();
            _navFontActive.Dispose();
        };
        RenderSidebar();
    }

    private void SetHoverIcon(string? key)
    {
        if (_hoverIcon == key) return;
        _hoverIcon = key;
        RenderSidebar();
    }

    private void UpdateNavigation()
    {
        bool isHome = _currentPage == "HOME";
        _menuHeader.Visible = isHome;
        _menuHeader.BackColor = Color.Transparent;
        _menuHeader.ForeColor = _skin.Muted;

        string? active = _currentPage switch
        {
            "HOME" => "new",
            "NEW GAME" => "new",
            "LOAD GAME" => "load",
            "DATABASE SELECTOR" => "database",
            "DATABASE EDITOR" => "editor",
            "SKINS" => "skins",
            "ACHIEVEMENTS" => "achievements",
            "CREDITS" => "credits",
            _ => null
        };

        foreach (var (key, item) in _menuRows)
        {
            item.Visible = key != "back" || !isHome;
            item.UpdateAppearance(_skin,
                _hoverIcon == key && item.Visible,
                active == key);
        }
    }

    private Image LoadArt(string filename, string fallback)
    {
        // Custom skins may contain just a background or any subset of images.
        // Always prefer their own artwork before using Obsidian as a fallback.
        var candidates = new[]
        {
            _skin.Asset(filename),
            _skin.Asset(fallback),
            Path.Combine(_paths.SkinsRoot, "Obsidian", filename),
            Path.Combine(_paths.SkinsRoot, "Obsidian", fallback)
        };
        var path = candidates.FirstOrDefault(File.Exists);
        if (path is null)
            throw new FileNotFoundException("Required skin artwork is missing: " + fallback);
        if (_assetCache.TryGetValue(path, out var cached)) return cached;
        using var stream = File.OpenRead(path);
        using var bitmap = Image.FromStream(stream);
        var copy = new Bitmap(bitmap);
        _assetCache[path] = copy;
        return copy;
    }

    private void RenderSidebar()
    {
        UpdateNavigation();
    }

    private void ClearAssetCache()
    {
        foreach (var item in _assetCache.Values) item.Dispose();
        _assetCache.Clear();
    }

    private void ClearPageContent()
    {
        // Disposing the previous title view also detaches its update events.
        foreach (Control child in _page.Controls.Cast<Control>().ToArray())
        {
            _page.Controls.Remove(child);
            child.Dispose();
        }
    }

    private void ShowHome()
    {
        _currentPage = "HOME";
        _hoverIcon = null;
        RenderSidebar();

        // Only the title screen uses the approved full-bleed, bottom-menu layout.
        // Interior screens retain their existing functional navigation chrome.
        SuspendLayout();
        _sidebar.Visible = false;
        _topbar.Visible = false;
        _editorPageTitle.Visible = false;
        ClearPageContent();

        var title = new TitleScreenView
        {
            Dock = DockStyle.Fill,
            Artwork = LoadArt(Path.Combine("Backgrounds", "Title_Screen.jpg"),
                Path.Combine("Backgrounds", "Title_Screen.jpg")),
            VersionText = "v" + GameUpdate.DisplayVersion,
            UpdateText = _updateButton.Text,
            UpdateEnabled = _updateButton.Enabled,
            SoundEnabled = _soundEnabled
        };

        // An exact 1672x941 approved composite takes precedence over procedural
        // chrome if present. This keeps every pixel of the user's chosen design.
        // The custom skin's original Backgrounds/Title_Screen.jpg is not modified.
        string exactImagePath = _skin.Asset(Path.Combine("Graphics", "Title_Menu_Exact.png"));
        if (File.Exists(exactImagePath))
        {
            using var stream = File.OpenRead(exactImagePath);
            using var image = Image.FromStream(stream);
            title.ExactLayout = new Bitmap(image);
            title.Disposed += (_, _) => title.ExactLayout?.Dispose();
        }

        title.CommandInvoked += command =>
        {
            switch (command)
            {
                case "sound":
                    _soundEnabled = !_soundEnabled;
                    title.SoundEnabled = _soundEnabled;
                    title.Invalidate();
                    foreach (var button in _topbar.Controls.OfType<TopUtilityButton>())
                        button.SetSoundEnabled(_soundEnabled);
                    break;
                case "help":
                    MessageBox.Show(this,
                        "Select a menu item to begin.\n\n" +
                        "New Game creates a new MDB career.\n" +
                        "Database allows you to import an MDB universe.\n" +
                        "Use the arrow keys and Enter, or Alt+letter shortcuts, to navigate.",
                        "Main Menu Help", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    break;
                case "minimize":
                    WindowState = FormWindowState.Minimized;
                    break;
                case "close":
                    Close();
                    break;
                case "updates":
                    _ = GameUpdate.CheckAndInstallAsync(this, _updateButton);
                    break;
                default:
                    RunSafe(() => Navigate(command));
                    break;
            }
        };

        EventHandler refreshUpdate = (_, _) =>
        {
            if (title.IsDisposed) return;
            title.UpdateText = _updateButton.Text;
            title.UpdateEnabled = _updateButton.Enabled;
            title.Invalidate();
        };
        _updateButton.TextChanged += refreshUpdate;
        _updateButton.EnabledChanged += refreshUpdate;
        title.Disposed += (_, _) =>
        {
            _updateButton.TextChanged -= refreshUpdate;
            _updateButton.EnabledChanged -= refreshUpdate;
        };

        // Drag only from the title plaque, not from the menu hit targets.
        title.MouseDown += (sender, args) =>
        {
            if (args.Button == MouseButtons.Left &&
                args.X < title.Width * (390f / 1672f) &&
                args.Y < title.Height * (158f / 941f))
                DragWindow(sender, args);
        };

        _page.Controls.Add(title);
        ResumeLayout(true);
    }

    private void ApplySkin()
    {
        ClearAssetCache();
        _sidebar.BackColor = _skin.Sidebar;
        _sidebar.UpdatePalette(_skin);
        _menuHeader.ForeColor = _skin.Muted;
        _updateButton.BackColor = _skin.Button;
        _updateButton.ForeColor = _skin.Foreground;
        _updateButton.FlatAppearance.BorderColor = _skin.Border;
        _topbar.BackColor = _skin.Surface;
        _topbar.UpdatePalette(_skin);
        foreach (var button in _topbar.Controls.OfType<TopUtilityButton>())
            button.UpdatePalette(_skin);
        _page.BackColor = _skin.Background;
        _titleText.ForeColor = _skin.Foreground;
        _editorPageTitle.ForeColor = _skin.Foreground;
        _versionText.ForeColor = _skin.Muted;
        RenderSidebar();
    }
}
