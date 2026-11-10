using System.Drawing;
using System.Windows.Forms;

namespace ProWrestlingRevolution;

/// <summary>
/// Version 0.5 minimal desktop shell. Navigation and utility chrome are kept
/// independent of the user's title artwork, MDB database and simulation code.
/// </summary>
public sealed partial class LauncherForm
{
    private readonly Dictionary<string, Label> _menuLabels = new(StringComparer.OrdinalIgnoreCase);
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

        // A custom skin's graphical icon strip is still used; the nearby text
        // is a cleaner clickable navigation layer, not a replacement image.
        _sidebar.Controls.Add(_sidebarImage);
        foreach (var (key, label, y) in SkinArt.MenuIcons)
        {
            var nav = new Label
            {
                Name = "Nav_" + key,
                Text = label.ToUpperInvariant(),
                Location = new Point(62, y - 23),
                Size = new Size(149, 46),
                TextAlign = ContentAlignment.MiddleLeft,
                BackColor = _skin.Sidebar,
                ForeColor = _skin.Foreground,
                Font = _navFont,
                Cursor = Cursors.Hand
            };
            nav.MouseEnter += (_, _) => SetHoverIcon(key);
            nav.MouseLeave += (_, _) => SetHoverIcon(null);
            nav.Click += (_, _) => RunSafe(() => Navigate(key));
            _menuLabels[key] = nav;
            _sidebar.Controls.Add(nav);
        }
        _sidebarImage.MouseMove += (_, e) =>
        {
            SetHoverIcon(IconAt(e.Y));
            _sidebarImage.Cursor = IconAt(e.Y) is null ? Cursors.Default : Cursors.Hand;
        };
        _sidebarImage.MouseLeave += (_, _) => SetHoverIcon(null);
        _sidebarImage.MouseClick += (_, e) =>
        {
            if (e.Button == MouseButtons.Left && IconAt(e.Y) is string key)
                RunSafe(() => Navigate(key));
        };

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
        foreach (var (key, nav) in _menuLabels)
        {
            bool home = _currentPage == "HOME";
            if (key == "back")
            {
                nav.Text = home ? "MAIN MENU" : "HOME";
                nav.Enabled = !home;
                nav.Cursor = home ? Cursors.Default : Cursors.Hand;
            }
            else
                nav.Text = key switch
                {
                    "database" => "DATABASE SELECTOR",
                    "editor" => "DATABASE EDITOR",
                    _ => SkinArt.MenuIcons.First(x => x.Key == key).Label.ToUpperInvariant()
                };
            bool hover = _hoverIcon == key && !(key == "back" && home);
            nav.ForeColor = hover ? _skin.AccentSecondary : key == "back" && home
                ? _skin.Muted : _skin.Foreground;
            nav.BackColor = hover ? _skin.Button : _skin.Sidebar;
            nav.Font = hover ? _navFontActive : _navFont;
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
        bool home = _currentPage == "HOME";
        string baseFile = home ? "Title_Screen_Alt.jpg" : "Title_Screen.jpg";
        string hover = _hoverIcon is null ? baseFile
            : (home ? "Title_Screen_Alt_Hover_" : "Title_Screen_Hover_") + _hoverIcon + ".jpg";
        _sidebarImage.Image = LoadArt(Path.Combine("SideBars", hover),
            Path.Combine("SideBars", baseFile));
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
        _sidebarImage.Image = null;
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
