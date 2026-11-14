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
    private readonly PwrSharedHeader _pageHeader = new();
    internal bool SkillsImmersiveMode { get; private set; }
    private readonly Button _updateButton = new();
    private readonly Font _navFont = new("Segoe UI", 10.5f, FontStyle.Regular);
    private readonly Font _navFontActive = new("Segoe UI", 10.5f, FontStyle.Bold);

    /// <summary>
    /// There is one header for every interior page. Skills changes only the
    /// contextual title and whether the launcher icon rail is hidden; the
    /// logo, height, gradient, borders and utility buttons never change.
    /// This public entry point is also used by the cached Worker overlay.
    /// </summary>
    internal void SetWorkerSectionChrome(string? section)
    {
        bool isWorker = !string.IsNullOrEmpty(section);
        bool isSkills = string.Equals(section, "Skills",
            StringComparison.OrdinalIgnoreCase);
        SkillsImmersiveMode = isSkills;
        bool isContractCanvas = section is "Contracts" or "Contract Editor";
        _sidebar.Visible = _currentPage != "HOME" && !isSkills && !isContractCanvas;
        if (isWorker)
        {
            var title = section == "General" ? "Worker Editor" :
                section == "Contract Editor" ? "Contracts" : section!;
            var detail = section switch
            {
                "General" => "Edit Worker",
                "Skills" => "Worker Skill Editor",
                "Contracts" => "Worker Contracts",
                "Contract Editor" => "Contract Editor",
                "Popularity" => "Worker Popularity",
                "Attributes" => "Worker Attributes",
                "Business Skills" => "Worker Business Skills",
                "Job History" => "Worker Job History",
                "Business History" => "Worker Business History",
                "Legacy" => "Worker Legacy",
                "Alter Ego" => "Worker Alter Ego",
                _ => "Worker Editor"
            };
            _pageHeader.SetPage(title, detail);
        }
        else UpdatePageHeader();
    }

    private void UpdatePageHeader()
    {
        var (title, detail) = _currentPage switch
        {
            "DATABASE EDITOR" => ("Database Editor", "Database Management"),
            "DATABASE SELECTOR" => ("Database Selector", "Select Universe"),
            "NEW GAME" => ("New Game", "Create Career"),
            "LOAD GAME" => ("Load Game", "Continue Career"),
            "SKINS" => ("Skins", "Customize Appearance"),
            "ACHIEVEMENTS" => ("Achievements", "Career Milestones"),
            "CREDITS" => ("Credits", "About PWR"),
            _ => ("Pro Wrestling Revolution", "")
        };
        _pageHeader.SetPage(title, detail);
    }

    private void PositionPageHeader()
    {
        if (_pageHeader.IsDisposed) return;
        int edge = _topbar.Controls.OfType<TopUtilityButton>()
            .Select(button => button.Left)
            .DefaultIfEmpty(_topbar.ClientSize.Width - 200).Min();
        _pageHeader.Bounds = new Rectangle(0, 0,
            Math.Max(0, edge), _topbar.ClientSize.Height);
    }

    private void BuildChrome()
    {
        _page.BackColor = _skin.Background;
        _sidebar.BackColor = _skin.Sidebar;
        _sidebar.Width = SkinArt.SidebarWidth;
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

        // Consistent slim icon rail on every interior page. The same keys
        // and icon art are used on full-window utility screens.
        var navTooltips = new ToolTip { InitialDelay = 350, ReshowDelay = 80 };
        _sidebar.Disposed += (_, _) => navTooltips.Dispose();
        foreach (var (key, label, y) in SkinArt.MenuIcons)
        {
            var caption = key switch
            {
                "back" => "Home",
                "database" => "Database",
                "editor" => "Editor",
                _ => label
            };
            var item = new SidebarMenuRow(key, caption, _skin, _navFont, _navFontActive)
            {
                Name = "Menu_" + key,
                IconOnly = true,
                Location = new Point(0, y - 27),
                Width = _sidebar.Width
            };
            navTooltips.SetToolTip(item, label);
            item.MouseEnter += (_, _) => SetHoverIcon(key);
            item.MouseLeave += (_, _) => SetHoverIcon(null);
            item.Click += (_, _) => RunSafe(() => Navigate(key));
            _menuRows.Add(key, item);
            _sidebar.Controls.Add(item);
        }

        // The shared update button is also used by the title screen updater.
        // Do not add a second button with the same Name: the smoke test and
        // accessibility tree require exactly one update action in the sidebar.
        _updateButton.Text = "Check for Updates";
        _updateButton.Font = new Font("Roboto", 18, FontStyle.Bold);
        _updateButton.AccessibleName = "Check for Updates";
        navTooltips.SetToolTip(_updateButton, "Check for Updates");
        _updateButton.BringToFront();

        // One persistent reference-style header; unlike the former Skills
        // special case, this is reused by every interior page.
        _topbar.Controls.Add(_pageHeader);
        _topbar.MouseDown += DragWindow;
        _pageHeader.MouseDown += DragWindow;
        UpdatePageHeader();

        var sound = new TopUtilityButton("sound", _skin);
        var help = new TopUtilityButton("help", _skin);
        var minimize = new TopUtilityButton("minimize", _skin);
        var close = new TopUtilityButton("close", _skin);
        var utility = new[] { sound, help, minimize, close };

        // Position the utility cluster against the actual header bounds,
        // not the design-resolution width. This keeps the controls flush
        // with the right edge after DPI/layout changes on interior pages.
        void AlignHeaderUtilities()
        {
            int x = _topbar.ClientSize.Width;
            for (int i = utility.Length - 1; i >= 0; i--)
            {
                utility[i].Height = _topbar.ClientSize.Height;
                x -= utility[i].Width;
                utility[i].Location = new Point(x, 0);
            }
        }
        foreach (var button in utility)
            _topbar.Controls.Add(button);
        _topbar.SizeChanged += (_, _) =>
        {
            AlignHeaderUtilities();
            PositionPageHeader();
        };
        AlignHeaderUtilities();
        PositionPageHeader();
        _pageHeader.BringToFront();
        foreach (var utilityButton in utility) utilityButton.BringToFront();

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
        _menuHeader.Visible = false; // No label header in the compact rail.

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
        SetWorkerSectionChrome(null);
        _hoverIcon = null;
        RenderSidebar();

        // Only the title screen uses the approved full-bleed, bottom-menu layout.
        // Interior screens retain their existing functional navigation chrome.
        SuspendLayout();
        _sidebar.Visible = false;
        _topbar.Visible = false;
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
        RenderSidebar();
    }
}
