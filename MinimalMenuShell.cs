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
    private readonly Font _navFont = new("Segoe UI", 9.5f, FontStyle.Regular);
    private readonly Font _navFontActive = new("Segoe UI", 9.5f, FontStyle.Bold);
    private Image? _topArt;

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
        _menuHeader.Location = new Point(24, 75);
        _menuHeader.Size = new Size(198, 28);
        _menuHeader.TextAlign = ContentAlignment.MiddleLeft;
        _menuHeader.BackColor = _skin.Sidebar;
        _menuHeader.ForeColor = _skin.Muted;
        _menuHeader.Font = new Font("Segoe UI", 9, FontStyle.Regular);
        _sidebar.Controls.Add(_menuHeader);

        foreach (var (key, label, y) in SkinArt.MenuIcons)
        {
            var text = key switch
            {
                "back" => "Home",
                "database" => "Database Selector",
                "editor" => "Database Editor",
                _ => label
            };
            var item = new SidebarMenuRow(key, text, _skin, _navFont, _navFontActive)
            {
                Name = "Menu_" + key,
                Location = new Point(6, y - 27),
                Width = _sidebar.Width - 12
            };
            item.MouseEnter += (_, _) => SetHoverIcon(key);
            item.MouseLeave += (_, _) => SetHoverIcon(null);
            item.Click += (_, _) => RunSafe(() => Navigate(key));
            _menuRows.Add(key, item);
            _sidebar.Controls.Add(item);
        }

        _titleText.Text = "PRO WRESTLING REVOLUTION";
        _titleText.Location = new Point(20, 0);
        _titleText.Size = new Size(291, 38);
        _titleText.TextAlign = ContentAlignment.MiddleLeft;
        _titleText.BackColor = _skin.Surface;
        _titleText.ForeColor = _skin.Foreground;
        _titleText.Font = new Font("Segoe UI", 10.5f, FontStyle.Bold);

        _versionText.Text = "v0.5.0";
        _versionText.Location = new Point(313, 0);
        _versionText.Size = new Size(74, 38);
        _versionText.TextAlign = ContentAlignment.MiddleLeft;
        _versionText.BackColor = _skin.Surface;
        _versionText.ForeColor = _skin.Muted;
        _versionText.Font = new Font("Segoe UI", 9.5f);

        _topbar.Controls.Add(_titleText);
        _topbar.Controls.Add(_versionText);
        _topbar.Controls.Add(_topImage);
        _topImage.BringToFront();
        // Retain the user's existing TopBars button art without its old title
        // pill by displaying just the controls in the last 180 pixels.
        _topImage.Paint += (_, e) =>
        {
            if (_topArt is null) return;
            int sourceX = Math.Max(0, _topArt.Width - _topImage.Width);
            e.Graphics.DrawImage(_topArt,
                new Rectangle(0, 0, _topImage.Width, _topImage.Height),
                new Rectangle(sourceX, 0, Math.Min(_topImage.Width, _topArt.Width),
                    Math.Min(SkinArt.TopHeight, _topArt.Height)), GraphicsUnit.Pixel);
        };
        _topbar.MouseDown += DragWindow;
        _titleText.MouseDown += DragWindow;
        _versionText.MouseDown += DragWindow;
        _topImage.MouseClick += (_, e) =>
        {
            if (e.Button != MouseButtons.Left) return;
            int x = e.X + SkinArt.WindowWidth - _topImage.Width;
            if (x is >= 1201 and < 1238)
            {
                _soundEnabled = !_soundEnabled;
                RenderTop();
            }
            else if (x is >= 1238 and < 1278)
                MessageBox.Show(this,
                    "Use the navigation at left to choose an option.\n\n" +
                    "New Game creates an independent MDB career.\n" +
                    "Database Selector imports your Access MDB.\n" +
                    "Skins lets you change the game's graphic assets.",
                    "Main Menu Help", MessageBoxButtons.OK, MessageBoxIcon.Information);
            else if (x is >= 1278 and < 1318)
                WindowState = FormWindowState.Minimized;
            else if (x >= 1318)
                Close();
        };
        FormClosed += (_, _) =>
        {
            _navFont.Dispose();
            _navFontActive.Dispose();
        };
        RenderSidebar();
        RenderTop();
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
        _menuHeader.BackColor = _skin.Sidebar;
        _menuHeader.ForeColor = _skin.Muted;

        string? active = _currentPage switch
        {
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

    private void RenderTop()
    {
        _topArt = LoadArt(Path.Combine("TopBars",
                _soundEnabled ? "Title_Screen_On.jpg" : "Title_Screen_Off.jpg"),
            Path.Combine("TopBars", "Title_Screen_On.jpg"));
        _topImage.Invalidate();
    }

    private void ClearAssetCache()
    {
        _topImage.Image = null;
        _topArt = null;
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
        _menuHeader.BackColor = _skin.Sidebar;
        _topbar.BackColor = _skin.Surface;
        _page.BackColor = _skin.Background;
        _titleText.BackColor = _skin.Surface;
        _titleText.ForeColor = _skin.Foreground;
        _versionText.BackColor = _skin.Surface;
        _versionText.ForeColor = _skin.Muted;
        RenderSidebar();
        RenderTop();
    }
}
