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

        // The updater lives below the navigation and is visible on every page.
        // A verified release is installed only after the user confirms.
        _updateButton.Name = "CheckUpdatesButton";
        _updateButton.Text = "↻  Check for Updates";
        _updateButton.Dock = DockStyle.Bottom;
        _updateButton.Height = 46;
        _updateButton.FlatStyle = FlatStyle.Flat;
        _updateButton.BackColor = _skin.Button;
        _updateButton.ForeColor = _skin.Foreground;
        _updateButton.Font = new Font("Segoe UI", 9.5f, FontStyle.Bold);
        _updateButton.Cursor = Cursors.Hand;
        _updateButton.TabStop = true;
        _updateButton.AccessibleRole = AccessibleRole.PushButton;
        _updateButton.AccessibleName = "Check for Updates";
        _updateButton.FlatAppearance.BorderSize = 1;
        _updateButton.FlatAppearance.BorderColor = _skin.Border;
        _updateButton.Click += async (_, _) =>
            await GameUpdate.CheckAndInstallAsync(this, _updateButton);
        _sidebar.Controls.Add(_updateButton);
        _updateButton.BringToFront();

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

        _topbar.Controls.Add(_titleText);
        _topbar.Controls.Add(_versionText);
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

    private void ShowHome()
    {
        _currentPage = "HOME";
        _hoverIcon = null;
        RenderSidebar();
        _page.Controls.Clear();
        _page.Controls.Add(new TitleArtworkView
        {
            Dock = DockStyle.Fill,
            BackColor = _skin.Background,
            Artwork = LoadArt(Path.Combine("Backgrounds", "Title_Screen.jpg"),
                Path.Combine("Backgrounds", "Title_Screen.jpg"))
        });
        // No LowerBars image, duplicate logo, added CTA or version footer.
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
        _versionText.ForeColor = _skin.Muted;
        RenderSidebar();
    }
}
