using System.Drawing;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

namespace ProWrestlingRevolution;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        try
        {
            if (args.Length > 0 &&
                args[0].Equals("--apply-update", StringComparison.OrdinalIgnoreCase))
                return GameUpdate.ApplyFromHelper(args);

            if (args.Contains("--generate-assets", StringComparer.OrdinalIgnoreCase))
            {
                var workingDirectory = new AppPaths(FindGameRoot());
                workingDirectory.EnsureLayout();
                return 0;
            }
            if (args.Contains("--smoke", StringComparer.OrdinalIgnoreCase))
            {
                SmokeTest();
                return 0;
            }

            ApplicationConfiguration.Initialize();
            var paths = new AppPaths(FindGameRoot());
            paths.EnsureLayout();
            var launcher = new LauncherForm(paths);
            launcher.EnableAutomaticReleaseUpdates();
            Application.Run(launcher);
            return 0;
        }
        catch (Exception e)
        {
            Console.Error.WriteLine(e);
            if (!args.Contains("--smoke", StringComparer.OrdinalIgnoreCase))
            {
                string folderHelp = e is IOException or UnauthorizedAccessException
                    or System.Security.SecurityException
                    ? "\n\nIf the folder is protected, extract the game to a writable location."
                    : "";
                MessageBox.Show(e.Message + folderHelp,
                    "Pro Wrestling Revolution", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            return 1;
        }
    }

    // Published games are portable: assets and data live beside the executable.
    // Visual Studio Debug builds may instead use the player's game folder above
    // a source checkout named Repos, so custom skins and settings do not reset.
    private static string FindGameRoot()
    {
        string executableRoot = Path.GetFullPath(AppContext.BaseDirectory);
#if DEBUG
        return ResolveGameRoot(executableRoot, useDevelopmentFolders: true);
#else
        return executableRoot;
#endif
    }

    private static string ResolveGameRoot(string executableFolder, bool useDevelopmentFolders)
    {
        string executableRoot = Path.GetFullPath(executableFolder);
        if (!useDevelopmentFolders)
            return executableRoot;

        for (DirectoryInfo? folder = new(executableRoot); folder is not null; folder = folder.Parent)
        {
            if (!folder.Name.Equals("Repos", StringComparison.OrdinalIgnoreCase))
                continue;

            // Only match a real Visual Studio build output under this checkout.
            // A normal installed executable must never be redirected to a parent.
            string relative = Path.GetRelativePath(folder.FullName, executableRoot);
            string[] components = relative.Split(
                Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (components.Length < 3 ||
                !components[0].Equals("bin", StringComparison.OrdinalIgnoreCase) ||
                !components[1].Equals("Debug", StringComparison.OrdinalIgnoreCase) ||
                !File.Exists(Path.Combine(folder.FullName, "ProWrestlingRevolution.csproj")))
                return executableRoot;

            string? gameFolder = folder.Parent?.FullName;
            if (gameFolder is not null &&
                new[] { "Databases", "Pictures", "Skins", "System" }
                    .All(name => Directory.Exists(Path.Combine(gameFolder, name))))
                return Path.GetFullPath(gameFolder);

            return executableRoot;
        }
        return executableRoot;
    }

    private static void SmokeTest()
    {
        PrwSchema.AssertDefinitionIntegrity();
        EditorCategoryDirectory.AssertMenuContract();
        var root = Path.Combine(Path.GetTempPath(), "PWR-phase1-" + Guid.NewGuid().ToString("N"));
        try
        {
            var paths = new AppPaths(root);
            Directory.CreateDirectory(paths.SystemRoot);
            var originalIni = Path.Combine(paths.SystemRoot, "settings.ini");
            File.WriteAllText(originalIni, "Skin=Real\nDatabase=MDB\n", Encoding.UTF8);
            paths.EnsureLayout();
            string workerBackground = Path.Combine(paths.SkinsRoot,
                "Obsidian", "Backgrounds", "Worker_Editor.jpg");
            if (!File.Exists(workerBackground))
                throw new Exception("The PWR Worker Editor background was not installed.");
            var preparedBackground = WorkerBackgroundCache
                .GetAsync(workerBackground).GetAwaiter().GetResult();
            if (preparedBackground is null ||
                preparedBackground.Width != SkinArt.WindowWidth ||
                preparedBackground.Height != SkinArt.WindowHeight ||
                !ReferenceEquals(preparedBackground,
                    WorkerBackgroundCache.GetAsync(workerBackground)
                        .GetAwaiter().GetResult()))
                throw new Exception("Worker background could not be decoded and cached.");
            // A release build embeds and installs the editor label database.
            // Local developer builds without the Java generator keep fallback labels.
            if (typeof(EditorMetadataTemplate).Assembly
                    .GetManifestResourceStream(EditorMetadataTemplate.Filename) is not null &&
                (!File.Exists(paths.EditorMetadataDatabase) ||
                 !AccessDatabase.LooksLikeMdb(paths.EditorMetadataDatabase)))
                throw new Exception("Bundled editor metadata MDB was not installed locally.");
            if (paths.ReadSetting("Skin", "") != "Real" ||
                paths.ReadSetting("Database", "") != "MDB")
                throw new Exception("Original skin and database preferences were not migrated.");
            if (File.Exists(paths.SettingsDatabase))
            {
                if (!AccessDatabase.LooksLikeMdb(paths.SettingsDatabase) ||
                    !File.Exists(originalIni + ".migrated"))
                    throw new Exception("Native settings MDB or legacy recovery file is missing.");
            }
            else if (!File.Exists(originalIni))
                throw new Exception("Legacy settings were deleted without MDB support.");
            // Simulate the exact player layout without touching user files.
            // VS Debug must read the game root outside Repos; a portable build
            // must always retain its own executable directory.
            var developmentGame = Path.Combine(root, "DevelopmentLayout");
            var sourceCheckout = Path.Combine(developmentGame, "Repos");
            var debugOutput = Path.Combine(sourceCheckout, "bin", "Debug", "net10.0-windows");
            Directory.CreateDirectory(debugOutput);
            File.WriteAllText(Path.Combine(sourceCheckout,
                "ProWrestlingRevolution.csproj"), "<Project />");
            foreach (var name in new[] { "Databases", "Pictures", "Skins", "System" })
                Directory.CreateDirectory(Path.Combine(developmentGame, name));

            static bool SameDirectory(string a, string b) =>
                Path.TrimEndingDirectorySeparator(Path.GetFullPath(a)).Equals(
                    Path.TrimEndingDirectorySeparator(Path.GetFullPath(b)),
                    StringComparison.OrdinalIgnoreCase);

            if (!SameDirectory(ResolveGameRoot(debugOutput, true), developmentGame))
                throw new Exception("Visual Studio Debug build lost the player's game folder.");
            if (!SameDirectory(ResolveGameRoot(debugOutput, false), debugOutput))
                throw new Exception("Portable builds must use their executable directory.");
            Directory.Delete(Path.Combine(developmentGame, "Skins"));
            if (!SameDirectory(ResolveGameRoot(debugOutput, true), debugOutput))
                throw new Exception("Missing game folders must not redirect runtime files.");
            var standaloneOutput = Path.Combine(root, "PortableGame");
            Directory.CreateDirectory(standaloneOutput);
            if (!SameDirectory(ResolveGameRoot(standaloneOutput, true), standaloneOutput))
                throw new Exception("Standalone games must not use a repository folder.");
            if (!File.Exists(paths.DefaultDatabase) ||
                !AccessDatabase.LooksLikeMdb(paths.DefaultDatabase) ||
                new FileInfo(paths.DefaultDatabase).Length != PrwDefaultTemplate.ExpectedLength)
                throw new Exception("The bundled base MDB was not installed locally.");
            byte[] pristineDefault = File.ReadAllBytes(paths.DefaultDatabase);
            // Simulate a previous install, then verify migration retains a
            // byte-for-byte backup and installs the replacement template.
            byte[] oldDefault = new byte[2048];
            Encoding.ASCII.GetBytes("Standard Jet DB").CopyTo(oldDefault, 4);
            File.WriteAllBytes(paths.DefaultDatabase, oldDefault);
            File.Delete(Path.Combine(paths.SystemRoot,
                "PRWDefault.template-sha256.txt"));
            PrwDefaultTemplate.EnsureInstalled(paths.SystemRoot);
            if (!new FileInfo(paths.DefaultDatabase).Length.Equals(
                    (long)PrwDefaultTemplate.ExpectedLength) ||
                !Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(
                    File.ReadAllBytes(paths.DefaultDatabase)))
                    .Equals(PrwDefaultTemplate.ExpectedSha256,
                        StringComparison.OrdinalIgnoreCase))
                throw new Exception("Replacement base database integrity check failed.");
            string preservedDefault = Directory.EnumerateFiles(
                Path.Combine(paths.SystemRoot, "TemplateBackups"), "*.mdb").Single();
            if (!File.ReadAllBytes(preservedDefault).SequenceEqual(oldDefault))
                throw new Exception("The previous base MDB was not preserved.");

            // Subsequent local template edits must survive later launches.
            File.WriteAllBytes(paths.DefaultDatabase, oldDefault);
            PrwDefaultTemplate.EnsureInstalled(paths.SystemRoot);
            if (!File.ReadAllBytes(paths.DefaultDatabase).SequenceEqual(oldDefault))
                throw new Exception("Customized current templates must be preserved.");
            File.WriteAllBytes(paths.DefaultDatabase, pristineDefault);
            var first = paths.CreateUniverse("From Base");
            var second = paths.CreateUniverse("Another Base");
            if (first.MdbPath == second.MdbPath ||
                !File.ReadAllBytes(first.MdbPath).SequenceEqual(
                    File.ReadAllBytes(paths.DefaultDatabase)))
                throw new Exception("Universe creation must copy the exact base database.");
            if (!File.Exists(second.MdbPath) ||
                !Directory.Exists(Path.Combine(first.DirectoryPath, "SaveGames")))
                throw new Exception("New universe folder structure was not created.");
            // Exercise the complete new-worker Save path when this runner
            // has the optional 64-bit ACE OLE DB provider installed.
            bool hasAce = true;
            try
            {
                using var providerCheck = AccessDatabase.Open(first.MdbPath, null);
            }
            catch (InvalidOperationException error)
                when (error.Message.StartsWith("Could not open the MDB",
                    StringComparison.OrdinalIgnoreCase))
            {
                hasAce = false;
                Console.WriteLine("ACE provider unavailable: live MDB integration test skipped.");
            }
            if (hasAce)
            {
                var draft = PrwWorkerRepository.CreateDraft(first.MdbPath);
                var newWorker = draft.Sections.Single(section =>
                    section.Table == "tblWorker").Records.Rows[0];
                newWorker["Name"] = "Regression Worker";
                newWorker["ShortName"] = "Regression";
                newWorker["Born"] = new DateTime(1997, 5, 12);
                newWorker["CompetesVs"] = "Both Divisions";
                newWorker["LanguageEnglish"] = 3;
                var draftSkills = draft.Sections.Single(section =>
                    section.Table == "tblWorkerSkills").Records.Rows[0];
                draftSkills["Brawling"] = 61;
                draftSkills["Charisma"] = 76;
                if (!PrwWorkerEditorService.Save(first.MdbPath, draft, null))
                    throw new Exception("New worker was not inserted.");

                var created = PrwWorkerRepository.List(first.MdbPath)
                    .SingleOrDefault(worker => worker.Name == "Regression Worker")
                    ?? throw new Exception("The new worker was not listed in the active MDB.");
                var loaded = PrwWorkerRepository.Load(first.MdbPath, created.Uid);
                if (!loaded.Sections.Any(section =>
                    section.Table == "tblWorkerBio" && section.Records.Rows.Count == 1))
                    throw new Exception("New worker linked details were not created.");

                loaded.Sections.Single(section => section.Table == "tblWorker")
                    .Records.Rows[0]["Name"] = "Regression Updated";
                loaded.Sections.Single(section => section.Table == "tblWorkerSkills")
                    .Records.Rows[0]["Brawling"] = 82;
                if (!PrwWorkerEditorService.Save(first.MdbPath, loaded, null))
                    throw new Exception("Existing worker edit was not persisted.");

                var reloaded = PrwWorkerRepository.Load(first.MdbPath, created.Uid);
                var savedWorker = reloaded.Sections.Single(section =>
                    section.Table == "tblWorker").Records.Rows[0];
                var savedSkills = reloaded.Sections.Single(section =>
                    section.Table == "tblWorkerSkills").Records.Rows[0];
                if (reloaded.Identity.Name != "Regression Updated" ||
                    !Equals(savedSkills["Brawling"], 82) ||
                    !Equals(savedSkills["Charisma"], 76) ||
                    Convert.ToString(savedWorker["CompetesVs"]) !=
                        "Both Divisions" ||
                    Convert.ToInt32(savedWorker["LanguageEnglish"]) != 3 ||
                    Convert.ToDateTime(savedWorker["Born"]).Date !=
                        new DateTime(1997, 5, 12))
                    throw new Exception(
                        "Worker Save did not persist skills, dates, languages or division in the MDB.");

                string backups = Path.Combine(first.DirectoryPath, "Backups");
                if (Directory.Exists(backups) &&
                    Directory.EnumerateFiles(backups, "*.mdb").Any())
                    throw new Exception("Worker editor created an unwanted backup MDB.");
                Console.WriteLine("Live worker MDB create/edit/save tests passed.");
            }
            bool duplicateRejected = false;
            try { paths.CreateUniverse("From Base"); }
            catch (IOException) { duplicateRejected = true; }
            if (!duplicateRejected) throw new Exception("Creating a duplicate universe was allowed.");
            Directory.Delete(first.DirectoryPath, recursive: true);
            Directory.Delete(second.DirectoryPath, recursive: true);
            if (paths.Skins().Count < 2) throw new Exception("Built-in skins missing.");
            var skin = paths.LoadSkin("Obsidian");
            if (skin.Accent.IsEmpty) throw new Exception("Skin palette could not load.");
            paths.WriteSetting("Skin", "Steel");
            if (paths.ReadSetting("Skin", "") != "Steel") throw new Exception("Skin setting failed.");
            // Exercise rendering with an empty custom folder: Obsidian must supply its artwork.
            paths.WriteSetting("Skin", "WorkInProgress");
            // Construct the actual launcher (without showing a window) and verify its
            // fixed logical canvas. This catches regressions in the native menu layout.
            ApplicationConfiguration.Initialize();

            // Construct a company profile without ACE so CI also verifies the
            // linked editor's section navigation and detail grid.
            var sampleCompany = new System.Data.DataTable("tblFed");
            sampleCompany.Columns.Add("UID", typeof(int));
            sampleCompany.Columns.Add("Name", typeof(string));
            sampleCompany.Rows.Add(1, "Example Company");
            var sampleContract = new System.Data.DataTable("tblContract");
            sampleContract.Columns.Add("UID", typeof(int));
            sampleContract.Columns.Add("FedUID", typeof(int));
            sampleContract.Rows.Add(11, 1);
            using (var companyView = new LinkedRecordEditorForm(
                "tblFed", "Example Company",
                EditorMetadataCatalog.Load(paths.EditorMetadataDatabase),
                [
                    new PrwProfileSection("tblFed", sampleCompany),
                    new PrwProfileSection("tblContract", sampleContract)
                ]))
            {
                var rows = companyView.Controls.Find("LinkedRecordFields", true)
                    .SingleOrDefault() as DataGridView;
                if (companyView.Name != "CompanyEditorForm" ||
                    companyView.ClientSize.Width != SkinArt.WindowWidth ||
                    companyView.Controls.Find("EntitySection_tblContract", true).Length != 1 ||
                    rows is null || rows.Columns.Count != 3 || rows.Rows.Count != 2)
                    throw new Exception(
                        "Company profile must show the parent record and linked contract section.");
            }
            using (var launcher = new LauncherForm(paths))
            {
                if (launcher.ClientSize.Width != SkinArt.WindowWidth ||
                    launcher.ClientSize.Height != SkinArt.WindowHeight)
                    throw new Exception(
                        "Launcher client dimensions must be 1366 x 768, received " +
                        launcher.ClientSize.Width + " x " + launcher.ClientSize.Height + ".");
                if (launcher.FormBorderStyle != FormBorderStyle.None)
                    throw new Exception("Launcher must not use a resizable standard border.");
                var titleCanvas = launcher.Controls.OfType<Panel>()
                    .SingleOrDefault(panel => panel.Dock == DockStyle.Fill);
                var titleScreen = titleCanvas?.Controls.OfType<TitleScreenView>().SingleOrDefault();
                if (titleScreen is null || titleScreen.Dock != DockStyle.Fill ||
                    titleScreen.Artwork is null)
                    throw new Exception("Full-bleed, functional bottom-menu title screen is missing.");
                var sidebar = launcher.Controls.OfType<Panel>()
                    .SingleOrDefault(control => control.Dock == DockStyle.Left);
                if (sidebar is null || sidebar.Width != SkinArt.SidebarWidth)
                    throw new Exception("Interior navigation must use the slim icon rail.");
                var updateButton = sidebar.Controls.OfType<Button>()
                    .SingleOrDefault(button => button.Name == "CheckUpdatesButton");
                if (updateButton is null || updateButton.Dock != DockStyle.Bottom ||
                    updateButton.Height != 46)
                    throw new Exception("Check for Updates must be docked at sidebar bottom.");
                if (sidebar.Controls.OfType<PictureBox>().Any())
                    throw new Exception("Old graphical icon strip was still added to sidebar.");
                var rows = sidebar.Controls.OfType<SidebarMenuRow>().ToList();
                if (rows.Count != SkinArt.MenuIcons.Length)
                    throw new Exception("Sidebar must contain eight unified navigation rows.");
                if (rows.Any(row => row.AccessibleRole != AccessibleRole.PushButton ||
                    !row.IconOnly || row.Width != SkinArt.SidebarWidth))
                    throw new Exception("Navigation buttons must be accessible, compact and icon-only.");
                var topbar = launcher.Controls.OfType<MetalTopBar>().SingleOrDefault();
                if (topbar is null || topbar.Height != 56)
                    throw new Exception("All interior pages must use the same 56px PWR header.");

                var sharedHeader = topbar.Controls.OfType<PwrSharedHeader>().SingleOrDefault()
                    ?? throw new Exception("Shared PWR title plaque is missing.");
                if (topbar.Controls.OfType<PwrSharedHeader>().Count() != 1 ||
                    topbar.Controls.Find("WorkerSkillsTopChrome", false).Length != 0 ||
                    topbar.Controls.OfType<Label>().Any())
                    throw new Exception(
                        "Legacy separate logo, version or Skills sharedHeader is still attached.");

                var controls = topbar.Controls.OfType<TopUtilityButton>().ToList();
                if (controls.Count != 4 || controls.Any(button =>
                    button.AccessibleRole != AccessibleRole.PushButton ||
                    button.Width != 50 || button.Height != 56))
                    throw new Exception("Every interior page must use uniform active utility buttons.");
                if (topbar.Controls.OfType<PictureBox>().Any())
                    throw new Exception("Old raster top utility strip should not appear.");

                foreach (var (key, _, y) in SkinArt.MenuIcons)
                {
                    var icon = rows.Single(row => row.Name == "Menu_" + key);
                    if (icon.Left != 0 || icon.Top != y - 27 ||
                        icon.Width != SkinArt.SidebarWidth || icon.Height != 54)
                        throw new Exception("Incorrect compact icon position for " + key);
                }

                var orderedChrome = controls.OrderBy(button => button.Left).ToArray();
                if (orderedChrome[0].Left != topbar.ClientSize.Width - 200 ||
                    orderedChrome[^1].Right != topbar.ClientSize.Width ||
                    orderedChrome.Any(button => button.Top != 0 ||
                        button.Bottom != topbar.ClientSize.Height) ||
                    orderedChrome.Zip(orderedChrome.Skip(1),
                        (left, right) => left.Right == right.Left).Any(touch => !touch))
                    throw new Exception("PWR utility controls must occupy one aligned sharedHeader.");
                if (sharedHeader.Left != 0 || sharedHeader.Top != 0 ||
                    sharedHeader.Height != 56 || sharedHeader.Right != orderedChrome[0].Left)
                    throw new Exception("Shared PWR header must meet its utility cluster exactly.");

                // Capture a real visible launcher. Compare each section's
                // composed sharedHeader, not an isolated, transparent title control.
                launcher.Show();
                topbar.Visible = true;
                Application.DoEvents();
                string? workerPreview = Environment.GetEnvironmentVariable("PWR_WORKER_PREVIEW");
                byte[]? referenceBackdrop = null;

                foreach (var (section, expectedTitle, expectedDetail, previewName) in new[]
                {
                    ("General", "Worker Editor", "Edit Worker", "pwr-header-worker.png"),
                    ("Skills", "Skills", "Worker Skill Editor", "pwr-header-skills.png"),
                    ("Contracts", "Contracts", "Worker Contracts", "pwr-header-contracts.png"),
                    ("Popularity", "Popularity", "Worker Popularity", "pwr-header-popularity.png"),
                    ("Business Skills", "Business Skills", "Worker Business Skills",
                        "pwr-header-business-skills.png")
                })
                {
                    launcher.SetWorkerSectionChrome(section);
                    topbar.PerformLayout();
                    topbar.Invalidate(true);
                    topbar.Update();
                    Application.DoEvents();

                    if (sharedHeader.PageTitle != expectedTitle ||
                        sharedHeader.PageDetail != expectedDetail ||
                        topbar.Height != 56 ||
                        sharedHeader.Right != orderedChrome[0].Left ||
                        controls.Any(button => button.Height != 56) ||
                        launcher.SkillsImmersiveMode != (section == "Skills"))
                        throw new Exception(
                            "Page header changes style or context unexpectedly: " + section);

                    using var composed = new System.Drawing.Bitmap(
                        topbar.Width, topbar.Height);
                    topbar.DrawToBitmap(composed,
                        new System.Drawing.Rectangle(0, 0, composed.Width, composed.Height));

                    // The top-left and right edges are outside the title
                    // glyphs. Their colours should be invariant across tabs.
                    byte[] backdrop = new byte[12];
                    int bi = 0;
                    foreach (int x in new[] { 8, 780, 1110, 1190 })
                    {
                        var pixel = composed.GetPixel(x, 2);
                        backdrop[bi++] = pixel.R;
                        backdrop[bi++] = pixel.G;
                        backdrop[bi++] = pixel.B;
                    }
                    if (referenceBackdrop is not null &&
                        !referenceBackdrop.SequenceEqual(backdrop))
                        throw new Exception("The header background changes between worker tabs.");
                    referenceBackdrop ??= backdrop;

                    int boundary = orderedChrome[0].Left;
                    var leftPixel = composed.GetPixel(boundary - 4, 3);
                    var rightPixel = composed.GetPixel(boundary + 4, 3);
                    int seamDifference =
                        Math.Abs(leftPixel.R - rightPixel.R) +
                        Math.Abs(leftPixel.G - rightPixel.G) +
                        Math.Abs(leftPixel.B - rightPixel.B);
                    if (seamDifference > 72)
                        throw new Exception("PWR header background has a utility seam.");

                    int textPixels = 0;
                    for (int y = 9; y < Math.Min(46, composed.Height); y++)
                        for (int x = 368; x < Math.Min(635, composed.Width); x++)
                        {
                            var pixel = composed.GetPixel(x, y);
                            if (pixel.R > 155 && pixel.G > 155 && pixel.B > 155)
                                textPixels++;
                        }
                    if (textPixels < 60)
                        throw new Exception(
                            "The composed PWR header title is not visible: " + section);

                    if (!string.IsNullOrWhiteSpace(workerPreview))
                    {
                        var screenshot = Path.Combine(
                            Path.GetDirectoryName(workerPreview)!, previewName);
                        composed.Save(screenshot,
                            System.Drawing.Imaging.ImageFormat.Png);
                        if (section == "Skills")
                        {
                            composed.Save(Path.Combine(
                                Path.GetDirectoryName(workerPreview)!,
                                "worker-skills-header-preview.png"),
                                System.Drawing.Imaging.ImageFormat.Png);
                            composed.Save(Path.Combine(
                                Path.GetDirectoryName(workerPreview)!,
                                "worker-skills-composed-header-preview.png"),
                                System.Drawing.Imaging.ImageFormat.Png);
                        }
                        Console.WriteLine("PWR shared header preview: " + screenshot);
                    }
                }

                launcher.SetWorkerSectionChrome(null);
                if (topbar.Height != 56 || launcher.SkillsImmersiveMode ||
                    controls.Any(button => button.Height != 56))
                    throw new Exception("Leaving Skills must retain the same PWR header.");
                // The editor is a full-width directory. The name of the MDB is
                // the central heading and the mass-edit dropdown stays beneath its button.
                typeof(LauncherForm).GetMethod("ShowDatabaseEditor",
                    System.Reflection.BindingFlags.Instance |
                    System.Reflection.BindingFlags.NonPublic)!.Invoke(launcher, null);
                if (sharedHeader.PageTitle != "Database Editor" ||
                    sharedHeader.PageDetail != "Database Management" ||
                    topbar.Height != 56 ||
                    topbar.Controls.OfType<PwrSharedHeader>().Count() != 1)
                    throw new Exception(
                        "Database Editor did not reuse the exact PWR header component.");
                if (!string.IsNullOrWhiteSpace(workerPreview))
                {
                    using var composedDatabase = new System.Drawing.Bitmap(
                        topbar.Width, topbar.Height);
                    topbar.DrawToBitmap(composedDatabase,
                        new System.Drawing.Rectangle(0, 0,
                            composedDatabase.Width, composedDatabase.Height));
                    composedDatabase.Save(Path.Combine(
                        Path.GetDirectoryName(workerPreview)!,
                        "pwr-header-database.png"),
                        System.Drawing.Imaging.ImageFormat.Png);
                }
                var workspace = titleCanvas!.Controls
                    .OfType<DatabaseEditorWorkspace>().SingleOrDefault();
                // The smoke test intentionally never shows the launcher.
                // Control.Visible is false while its parent Form is hidden,
                // even if the sidebar is explicitly enabled on the editor.
                if (workspace is null || sidebar.Dock != DockStyle.Left ||
                    sidebar.Controls.OfType<SidebarMenuRow>().Count() != SkinArt.MenuIcons.Length)
                    throw new Exception("Database editor must retain the main menu sidebar.");
                static IEnumerable<Control> Descendants(Control parent) =>
                    parent.Controls.Cast<Control>().SelectMany(child =>
                        new[] { child }.Concat(Descendants(child)));
                var descendants = Descendants(workspace).ToArray();
                var databaseName = descendants.SingleOrDefault(c => c.Name == "ActiveDatabaseName");
                var massButton = descendants.SingleOrDefault(c => c.Name == "MassEditButton");
                var massField = descendants.SingleOrDefault(c => c.Name == "MassEditFieldSelector");
                var categories = descendants.SingleOrDefault(c => c.Name == "EditorCategorySelector");
                var notes = descendants.SingleOrDefault(c => c.Name == "EditorNotes");
                foreach (string name in new[]
                    { "AddRecordButton", "DeleteRecordsButton",
                      "CopyRecordsButton", "ImportRecordsButton" })
                {
                    if (descendants.SingleOrDefault(c => c.Name == name) is not Button action ||
                        !action.Enabled)
                        throw new Exception(name + " must be a functional editor action.");
                }
                if (!PrwRecordOperations.Supports("tblWorker") ||
                    !PrwRecordOperations.Supports("tblFed") ||
                    !PrwRecordOperations.Supports("tblMatch") ||
                    PrwRecordOperations.Supports("tblContract"))
                    throw new Exception("Native record operation categories are incorrect.");
                if (databaseName is null || categories is null || notes is null)
                    throw new Exception("Editor directory sections are missing.");
                if (massButton is null || massField is null ||
                    massField.Top <= massButton.Top)
                    throw new Exception("Mass edit dropdown must be under the mass edit button.");

                // The launcher screens share the same physical header,
                // not copies of the Worker/Skills implementation.
                foreach (var (method, expectedPage, expectedDetail) in new[]
                {
                    ("ShowSkins", "Skins", "Customize Appearance"),
                    ("ShowAchievements", "Achievements", "Career Milestones"),
                    ("ShowCredits", "Credits", "About PWR"),
                    ("ShowNewGame", "New Game", "Create Career")
                })
                {
                    typeof(LauncherForm).GetMethod(method,
                        System.Reflection.BindingFlags.Instance |
                        System.Reflection.BindingFlags.NonPublic)!
                        .Invoke(launcher, null);
                    Application.DoEvents();
                    if (sharedHeader.PageTitle != expectedPage ||
                        sharedHeader.PageDetail != expectedDetail ||
                        topbar.Height != 56 ||
                        topbar.Controls.OfType<PwrSharedHeader>().Count() != 1)
                        throw new Exception(
                            "Launcher screen did not use the shared PWR header: " + method);
                }

            }

            WorkerSkillsEditor.AssertSchemaMapping();
            // v0.6.4 intentionally restores the complete v0.6.2 Worker
            // General editor. The separate Skills screen is retained, but
            // field types, labels and company lookups follow v0.6.2.
            // Verify the Worker Editor's fixed layout using in-memory worker data.
            // UI checks do not need a database provider.
            var workerTable = new System.Data.DataTable("tblWorker");
            workerTable.Columns.Add("UID", typeof(int));
            workerTable.Columns.Add("Name", typeof(string));
            workerTable.Columns.Add("ShortName", typeof(string));
            workerTable.Columns.Add("Freelance", typeof(bool));
            workerTable.Rows.Add(1, "Sample Worker", "Sample", false);
            var boolChoices = PrwWorkerFieldOptions.Get(paths,
                Path.Combine(root, "not-a-real-mdb.mdb"), null,
                workerTable.Rows[0], "Freelance");
            if (boolChoices.Count != 2 ||
                !boolChoices.Any(choice => Equals(choice.Value, false)) ||
                !boolChoices.Any(choice => Equals(choice.Value, true)))
                throw new Exception("Native Boolean worker dropdowns must offer Yes and No.");
            var skillsTable = new System.Data.DataTable("tblWorkerSkills");
            skillsTable.Columns.Add("WorkerUID", typeof(int));
            foreach (var field in PrwSchema.Tables.Single(x => x.Name == "tblWorkerSkills")
                         .Columns.Where(x => x != "WorkerUID"))
                skillsTable.Columns.Add(field, typeof(int));
            var sampleSkills = skillsTable.NewRow();
            sampleSkills["WorkerUID"] = 1;
            // Visual smoke values mirror the approved reference screenshot.
            // They are in-memory examples, never changes to a universe.
            sampleSkills["Brawling"] = 60;
            sampleSkills["Puroresu"] = 40;
            sampleSkills["Technical"] = 55;
            sampleSkills["Psychology"] = 68;
            sampleSkills["Charisma"] = 72;
            sampleSkills["Microphone"] = 70;
            sampleSkills["Basics"] = 65;
            sampleSkills["Selling"] = 67;
            sampleSkills["Safety"] = 75;
            sampleSkills["Stamina"] = 77;
            sampleSkills["PotentialPrimary"] = 70;
            sampleSkills["PotentialMental"] = 72;
            sampleSkills["PotentialPerformance"] = 80;
            sampleSkills["PotentialFundamentals"] = 76;
            sampleSkills["PotentialPhysical"] = 75;
            skillsTable.Rows.Add(sampleSkills);
            skillsTable.AcceptChanges();

            // A 40-column native contract makes tab timing representative
            // of the expensive case in the user's 19-second recording.
            var contractTable = new System.Data.DataTable("tblContract");
            foreach (string field in PrwSchema.Tables.Single(
                         schema => schema.Name == "tblContract").Columns)
                contractTable.Columns.Add(field,
                    field is "UID" or "WorkerUID" ? typeof(int) : typeof(string));
            var contract = contractTable.NewRow();
            contract["UID"] = 500;
            contract["WorkerUID"] = 1;
            contract["Name"] = "Test Contract";
            contractTable.Rows.Add(contract);
            contractTable.AcceptChanges();

            var popularityTable = new System.Data.DataTable("tblWorkerOver");
            popularityTable.Columns.Add("WorkerUID", typeof(int));
            popularityTable.Columns.Add("RegionCode", typeof(string));
            popularityTable.Columns.Add("Popularity", typeof(int));
            popularityTable.Rows.Add(1, "USA", 65);
            popularityTable.AcceptChanges();

            var attributesTable = new System.Data.DataTable("tblWorkerAttributes");
            foreach (string field in PrwSchema.Tables.Single(
                         schema => schema.Name == "tblWorkerAttributes").Columns)
                attributesTable.Columns.Add(field,
                    field is "UID" or "WorkerUID" ? typeof(int) : typeof(string));
            var attribute = attributesTable.NewRow();
            attribute["UID"] = 900;
            attribute["WorkerUID"] = 1;
            attribute["AttributeName"] = "Reliable";
            attributesTable.Rows.Add(attribute);
            attributesTable.AcceptChanges();

            var biographyTable = new System.Data.DataTable("tblWorkerBio");
            biographyTable.Columns.Add("Biography", typeof(string));
            biographyTable.Rows.Add("An example worker biography.");
            var profile = new PrwWorkerProfile(
                new PrwWorkerSummary(1, "Sample Worker", "Sample"),
                [
                    new PrwProfileSection("tblWorker", workerTable),
                    new PrwProfileSection("tblWorkerBio", biographyTable),
                    new PrwProfileSection("tblWorkerSkills", skillsTable),
                    new PrwProfileSection("tblContract", contractTable),
                    new PrwProfileSection("tblWorkerOver", popularityTable),
                    new PrwProfileSection("tblWorkerAttributes", attributesTable)
                ]);

            // Capture both contract canvases as real controls for pixel-level
            // comparison against the supplied 1672x941 reference images.
            using (var contractView = new ContractsWorkspaceView(
                paths, skin, profile, null, null, _ => { }, () => { }))
            using (var contractHost = new Panel
            {
                Size = new System.Drawing.Size(1366, 712),
                BackColor = System.Drawing.Color.Black
            })
            {
                contractHost.Controls.Add(contractView);
                contractView.Bounds = contractHost.ClientRectangle;
                contractHost.CreateControl();
                contractView.CreateControl();
                contractView.Show();
                // Worker art uses the normal async cache. Pump the UI
                // message loop while preparing it, rather than blocking a
                // captured WinForms synchronization context.
                var artworkTask = contractView.PrepareArtworkForPreviewAsync();
                while (!artworkTask.IsCompleted)
                {
                    Application.DoEvents();
                    Thread.Sleep(10);
                }
                artworkTask.GetAwaiter().GetResult();
                Application.DoEvents();
                if (contractView.Controls.Find("ContractSearch", true).Length != 1 ||
                    contractView.Controls.Find("ContractStatusFilter", true).Length != 1 ||
                    contractView.Controls.Find("ContractAction_AddContract", true).Length != 1)
                    throw new Exception("Contract selection screen is missing exact controls.");
                using (var snapshot = new System.Drawing.Bitmap(
                    contractView.Width, contractView.Height))
                {
                    contractView.DrawToBitmap(snapshot,
                        new System.Drawing.Rectangle(0, 0, snapshot.Width, snapshot.Height));
                    snapshot.Save(Path.Combine(Environment.CurrentDirectory, "worker-contract-list-preview.png"));
                }

                var editorMethod = typeof(ContractsWorkspaceView).GetMethod(
                    "OpenEditor", System.Reflection.BindingFlags.Instance |
                                  System.Reflection.BindingFlags.NonPublic)
                    ?? throw new Exception("Contract editor open action is missing.");
                editorMethod.Invoke(contractView, [contract]);
                contractView.PerformLayout();
                Application.DoEvents();
                foreach (string name in new[] { "ContractBiography", "ContractEmployer",
                             "ContractField_ContractType", "ContractRole_RoleWrestler",
                             "ContractAction_Save", "ContractAlterEgoSelector",
                             "ContractCopySource" })
                    if (contractView.Controls.Find(name, true).Length != 1)
                        throw new Exception("Contract editor layout is missing " + name);
                // Contracts must use the actual General Worker components,
                // not an independently styled portrait or navy biography box.
                var contractPortrait = contractView.Controls
                    .Find("ContractWorkerPicture", true)
                    .OfType<PictureBox>().Single();
                using (var expectedPortrait =
                    WorkerEditorTheme.CreatePortrait("ReferenceWorkerPortrait"))
                {
                    if (contractPortrait.Size != expectedPortrait.Size ||
                        contractPortrait.BorderStyle != expectedPortrait.BorderStyle ||
                        contractPortrait.SizeMode != expectedPortrait.SizeMode ||
                        contractPortrait.BackColor != expectedPortrait.BackColor)
                        throw new Exception("Contract portrait diverged from General.");
                }
                var contractBio = contractView.Controls.Find("ContractBiography", true)
                    .OfType<RichTextBox>().Single();
                if (contractBio.BackColor != WorkerEditorTheme.FieldInk ||
                    contractBio.ForeColor != Color.FromArgb(16, 28, 41) ||
                    contractBio.BorderStyle != BorderStyle.None)
                    throw new Exception("Contract biography differs from General.");
                if (contractView.Controls.Find("ContractRole_RoleWrestler", true)
                    .Single() is not CheckBox)
                    throw new Exception("Contracts must reuse General's checkbox style.");
                // Every editable entry, including numeric/date, must expose the
                // exact same styled input surface as list-backed contract fields.
                var fieldsToCheck = new[] { "Name", "ShortName", "Wage",
                    "Downside", "StartDate", "ContractType", "Alignment" };
                Type? sharedType = null;
                foreach (string field in fieldsToCheck)
                {
                    var control = contractView.Controls.Find("ContractField_" + field, true)
                        .SingleOrDefault() ?? throw new Exception(
                            "Missing contract input: " + field);
                    sharedType ??= control.GetType();
                    if (control.GetType() != sharedType ||
                        control.Controls.Find("EditableValue", true)
                            .SingleOrDefault() is not TextBox edit ||
                        edit.BackColor != WorkerEditorTheme.FieldInk ||
                        edit.ForeColor != WorkerEditorTheme.FieldText ||
                        !edit.Enabled || edit.ReadOnly)
                        throw new Exception(
                            "Contract value is not consistently typeable: " + field);
                }
                var wage = contractView.Controls.Find("ContractField_Wage", true)
                    .Single().Controls.Find("EditableValue", true)
                    .OfType<TextBox>().Single();
                string originalWage = wage.Text;
                wage.Text = "2750";
                if (wage.Text != "2750")
                    throw new Exception("Direct contract wage typing failed.");
                wage.Text = originalWage;

                using (var snapshot = new System.Drawing.Bitmap(
                    contractView.Width, contractView.Height))
                {
                    contractView.DrawToBitmap(snapshot,
                        new System.Drawing.Rectangle(0, 0, snapshot.Width, snapshot.Height));
                    snapshot.Save(Path.Combine(Environment.CurrentDirectory, "worker-contract-editor-preview.png"));
                }
            }

            using (var workerEditor = new WorkerEditorForm(paths, skin, profile))
            using (var workerHost = new Panel
            {
                Name = "WorkerVisualTestHost",
                Size = new System.Drawing.Size(SkinArt.WindowWidth, SkinArt.WindowHeight)
            })
            {
                // The CI desktop may be smaller than the fixed game canvas.
                // Hosting the form as a child avoids OS-level top-window
                // clamping and exercises the exact in-game popup layout.
                workerHost.Controls.Add(workerEditor);
                workerEditor.FitInside(workerHost);
                workerEditor.Show();
                Application.DoEvents();
                if (workerEditor.Controls.Find("WorkerEditorBody", true).Length != 1 ||
                    workerEditor.Controls.Find("WorkerGeneralGrid", true).Length != 1 ||
                    workerEditor.Controls.Find("WorkerPersonalData", true).Length != 1 ||
                    workerEditor.Controls.Find("WorkerSettings", true).Length != 1 ||
                    workerEditor.Controls.Find("WorkerActiveAreas", true).Length != 1 ||
                    workerEditor.Controls.Find("WorkerLanguages", true).Length != 1 ||
                    workerEditor.Controls.Find("WorkerBiography", true).Length != 1 ||
                    workerEditor.Controls.Find("WorkerPopupLayout", true).Length != 1 ||
                    workerEditor.Controls.Find("WorkerPage", true).Length != 1 ||
                    workerEditor.Controls.Find("WorkerSectionNavigation", true).Length != 1)
                    throw new Exception("Worker Editor fixed general layout is missing.");
                var workerPortrait = workerEditor.Controls.Find("WorkerPicture", true)
                    .OfType<PictureBox>().Single();
                if (workerPortrait.Size != new Size(91, 91) ||
                    workerPortrait.BorderStyle != BorderStyle.FixedSingle ||
                    workerPortrait.SizeMode != PictureBoxSizeMode.Zoom)
                    throw new Exception("General Worker portrait template changed.");
                var workerBio = workerEditor.Controls.Find("WorkerBiography", true)
                    .OfType<RichTextBox>().Single();
                if (workerBio.BackColor != WorkerEditorTheme.FieldInk ||
                    workerBio.ForeColor != Color.FromArgb(16, 28, 41))
                    throw new Exception("General Worker biography template changed.");
                if (workerEditor.Controls.Find("WorkerBackToDatabase", true).Length != 0)
                    throw new Exception("Worker Editor must not add an extra Back to Database button.");
                if (workerEditor.Controls.Find("WorkerPictureTypeLabel", true).Length != 1 ||
                    workerEditor.Controls.Find("WorkerField_BiographyType", true).Length != 1)
                    throw new Exception("Worker identity was not restored to v0.6.2.");
                var expectedRoleLabels = new Dictionary<string, string>
                {
                    ["RoleWrestler"] = "Wrestler",
                    ["RolePartTimeWrestler"] = "Occasional Wrestler",
                    ["RoleManager"] = "Manager",
                    ["RoleOnScreenPersonality"] = "On-Screen Personality",
                    ["RolePlayByPlay"] = "Play By Play",
                    ["RoleColour"] = "Colour Commentator",
                    ["RoleReferee"] = "Referee",
                    ["RoleProducer"] = "Road Agent"
                };
                foreach (var (field, label) in expectedRoleLabels)
                    if (workerEditor.Controls.Find("WorkerField_" + field, true)
                            .SingleOrDefault() is not CheckBox role ||
                        role.Text != label)
                        throw new Exception("Worker role label mismatch: " + field);
                if (workerEditor.Controls.Find("WorkerField_CompetesVs", true)
                        .SingleOrDefault()?.AccessibleRole != AccessibleRole.PushButton ||
                    workerEditor.Controls.Find("WorkerField_Born", true)
                        .SingleOrDefault()?.AccessibleRole != AccessibleRole.PushButton ||
                    workerEditor.Controls.Find("WorkerField_FaceGimmick", true)
                        .SingleOrDefault()?.AccessibleRole != AccessibleRole.PushButton)
                    throw new Exception("v0.6.2 Worker input controls were not restored.");
                if (workerEditor.Controls.Find("WorkerSection_Skills", true).Length != 1 ||
                    workerEditor.Controls.Find("WorkerSection_Contracts", true).Length != 1 ||
                    workerEditor.Controls.Find("WorkerSection_AlterEgo", true).Length != 1)
                    throw new Exception("Worker Editor navigation sections are incomplete.");
                var save = workerEditor.Controls.Find("WorkerSave", true)
                    .SingleOrDefault() as Button;
                if (save is null || save.Enabled)
                    throw new Exception("Worker Editor cannot claim unsupported MDB saving.");
                if (workerEditor.AutoScroll ||
                    workerEditor.Controls.Find("WorkerSectionContent", true)
                        .OfType<ScrollableControl>().Any(control => control.AutoScroll))
                    throw new Exception("Worker Editor may not use scrolling in its fixed layout.");

                // A form with an assigned native file path exposes functional
                // actions without requiring the Access provider just to draw them.
                using (var editableWorker = new WorkerEditorForm(
                    paths, skin, profile, Path.Combine(root, "worker-smoke.mdb")))
                {
                    foreach (string action in new[]
                        { "WorkerSave", "WorkerCreateMoveset",
                          "WorkerSuggestName", "WorkerMakeBiography" })
                    {
                        if (editableWorker.Controls.Find(action, true)
                                .SingleOrDefault() is not Button active || !active.Enabled)
                            throw new Exception(action + " is not enabled in editing mode.");
                    }
                    if (editableWorker.Controls.Find("WorkerField_Name", true)
                            .SingleOrDefault()?.AccessibleRole != AccessibleRole.PushButton)
                        throw new Exception("Worker name must be an accessible editing control.");

                    // Simulate consecutive dropdown openings. Both must use
                    // the same live themed menu; Closed must never dispose it.
                    var getter = typeof(WorkerEditorForm).GetMethod("GetFieldMenu",
                        System.Reflection.BindingFlags.Instance |
                        System.Reflection.BindingFlags.NonPublic)
                        ?? throw new Exception("The Worker menu factory is missing.");
                    var original = getter.Invoke(editableWorker, null)
                        as ContextMenuStrip
                        ?? throw new Exception("The Worker dropdown is missing.");
                    original.Items.Add(new ToolStripMenuItem("Regression Choice"));
                    var again = getter.Invoke(editableWorker, null) as ContextMenuStrip;
                    if (!ReferenceEquals(original, again) ||
                        again is null || again.IsDisposed ||
                        again.Renderer is not ToolStripProfessionalRenderer ||
                        again.Items.Count != 0 ||
                        again.BackColor == SystemColors.Control)
                        throw new Exception("Worker dropdown theme/lifetime regression.");
                }

                // Performance investigation: the old editor rebuilt and
                // disposed each section every time the player clicked a tab.
                // A profile now keeps each tab view once, even across dozens
                // of navigation clicks. The cache belongs to one worker.
                var switchSection = typeof(WorkerEditorForm).GetMethod(
                    "ShowSection", System.Reflection.BindingFlags.Instance |
                    System.Reflection.BindingFlags.NonPublic)
                    ?? throw new Exception("Missing Worker tab switch handler.");
                var sectionsToProfile = new[]
                {
                    "Contracts", "Skills", "Popularity", "Attributes",
                    "Business Skills", "Job History", "Business History",
                    "Legacy", "Alter Ego", "General"
                };
                foreach (string section in sectionsToProfile)
                {
                    var started = System.Diagnostics.Stopwatch.StartNew();
                    switchSection.Invoke(workerEditor, [section]);
                    workerEditor.PerformLayout();
                    Application.DoEvents();
                    Console.WriteLine("Worker tab first visit: " + section + " = " +
                        started.ElapsedMilliseconds + " ms");
                }
                int builtViews = workerEditor.SectionViewBuildCount;
                if (workerEditor.CachedSectionCount != sectionsToProfile.Length)
                    throw new Exception("Not every Worker tab was cached.");

                foreach (string section in sectionsToProfile.Reverse())
                {
                    var started = System.Diagnostics.Stopwatch.StartNew();
                    switchSection.Invoke(workerEditor, [section]);
                    workerEditor.PerformLayout();
                    Application.DoEvents();
                    Console.WriteLine("Worker tab cached visit: " + section + " = " +
                        started.ElapsedMilliseconds + " ms");
                    if (started.ElapsedMilliseconds > 1500)
                        throw new Exception(
                            "Revisiting " + section + " still blocks the UI for over 1.5 seconds.");
                }
                if (workerEditor.SectionViewBuildCount != builtViews ||
                    workerEditor.CachedSectionCount != sectionsToProfile.Length)
                    throw new Exception("Worker tabs are still rebuilt on repeat navigation.");

                // Cached views must be thrown away when the profile changes,
                // or edits could affect the previous worker's DataRows.
                workerEditor.LoadProfile(profile);
                if (workerEditor.CachedSectionCount != 1 ||
                    workerEditor.ActiveSection != "General")
                    throw new Exception("Switching worker did not invalidate its cached tab views.");
                Console.WriteLine("Worker tab cache lifecycle and performance passed.");

                // Previous "0 ms" tests were performed in a UserControl
                // without a visible parent Form. They tested control caching,
                // not the expensive WinForms repaint on a real screen.
                // Now time first and repeated transitions inside a visible
                // top-level window, explicitly flushing paint messages.
                using (var visibleWindow = new Form
                {
                    Text = "PWR Worker Rendering Smoke",
                    ClientSize = new System.Drawing.Size(1220, 740),
                    ShowInTaskbar = false,
                    StartPosition = FormStartPosition.Manual,
                    Location = new System.Drawing.Point(10, 10)
                })
                using (var visibleWorker = new WorkerEditorForm(paths, skin, profile))
                {
                    visibleWorker.Dock = DockStyle.Fill;
                    visibleWindow.Controls.Add(visibleWorker);
                    visibleWindow.Show();
                    Application.DoEvents();
                    foreach (string section in new[] { "Contracts", "Skills", "General",
                            "Contracts", "Skills", "General" })
                    {
                        var paint = System.Diagnostics.Stopwatch.StartNew();
                        switchSection.Invoke(visibleWorker, [section]);
                        visibleWorker.Invalidate(true);
                        visibleWorker.Update();
                        Application.DoEvents();
                        Console.WriteLine("Worker VISIBLE tab + repaint: " + section +
                            " = " + paint.ElapsedMilliseconds + " ms");
                    }
                    // Diagnostic A/B only: temporarily hide the already
                    // decoded background to quantify repeated image paints.
                    // Restore the original bitmap before closing the window;
                    // this never changes a skin or universe on disk.
                    var runtimePage = visibleWorker.Controls
                        .Find("WorkerPage", true).Single() as TableLayoutPanel;
                    if (runtimePage is not null)
                    {
                        Image? originalBackdrop = runtimePage.BackgroundImage;
                        runtimePage.BackgroundImage = null;
                        foreach (string section in new[] { "General", "Skills", "General" })
                        {
                            var noBackdropTime = System.Diagnostics.Stopwatch.StartNew();
                            switchSection.Invoke(visibleWorker, [section]);
                            visibleWorker.Invalidate(true);
                            visibleWorker.Update();
                            Application.DoEvents();
                            Console.WriteLine(
                                "Worker VISIBLE tab NO-BACKDROP: " + section +
                                " = " + noBackdropTime.ElapsedMilliseconds + " ms");
                        }
                        runtimePage.BackgroundImage = originalBackdrop;
                    }
                    visibleWindow.Hide();
                }

                // Verify the new dedicated, database-bound Skills workspace
                // without duplicating identity, biography or global navigation.
                using (var skillsEditor = new WorkerSkillsEditor(profile, true, () => { }))
                {
                    skillsEditor.Size = new System.Drawing.Size(1150, 690);
                    skillsEditor.CreateControl();
                    skillsEditor.PerformLayout();
                    if (skillsEditor.Controls.Find("SkillsFourPanels", true)
                            .SingleOrDefault() is not TableLayoutPanel groups ||
                        groups.ColumnCount != 2 || groups.RowCount != 2 ||
                        skillsEditor.Controls.Find("SkillSlider_Brawling", true)
                            .SingleOrDefault() is not SkillGlowSlider ||
                        skillsEditor.Controls.Find("SkillSpinner_Brawling", true)
                            .SingleOrDefault() is not SkillNumberSpinner ||
                        skillsEditor.Controls.Find("SkillNumber_PotentialRefereeing", true)
                            .SingleOrDefault() is not TextBox ||
                        skillsEditor.Controls.Find("SkillPanel_In-Ring", true)
                            .SingleOrDefault() is not SkillGlassPanel ||
                        skillsEditor.Controls.Find("SkillNumber_", true).Length != 0 ||
                        skillsEditor.Controls.Find("SkillsPreset", true)
                            .SingleOrDefault() is not SkillChromePreset ||
                        skillsEditor.Controls.Find("SkillsReset", true)
                            .SingleOrDefault() is not SkillChromeButton ||
                        skillsEditor.Controls.Find("SkillsSave", true)
                            .SingleOrDefault() is not SkillChromeButton ||
                        skillsEditor.Controls.Find("WorkerBiography", true).Length != 0 ||
                        skillsEditor.Controls.Find("WorkerPicture", true).Length != 0)
                        throw new Exception(
                            "The illustrated four-panel Skills layout or real footer controls are incomplete.");

                    if (skillsEditor.Controls.Find("SkillsAutoBalance", true).Length != 0)
                        throw new Exception("Auto Balance should not appear on the Skills page.");
                    if (groups.Controls.Cast<Control>().OfType<SkillGlassPanel>().Count() != 4 ||
                        skillsEditor.Controls.Find("SkillsHeader_0", true).Length != 1 ||
                        skillsEditor.Controls.Find("SkillsHeader_3", true).Length != 1)
                        throw new Exception("The four custom painted Skills headers are missing.");

                    skillsEditor.SetRatingForSmoke("Brawling", 80);
                    skillsEditor.SetRatingForSmoke("Aerial", 0);
                    if (!Equals(sampleSkills["Brawling"], 80) ||
                        !Equals(sampleSkills["Aerial"], 0) ||
                        !Equals(sampleSkills["Technical"], 55))
                        throw new Exception("Skills changes must preserve unset versus zero.");
                    skillsEditor.ResetForSmoke();
                    if (!Equals(sampleSkills["Brawling"], 60) ||
                        sampleSkills["Aerial"] is not DBNull ||
                        !Equals(sampleSkills["Charisma"], 72))
                        throw new Exception("Skills Reset must restore original values.");
                }

                // The real game uses a 1366x768 window, leaving roughly
                // 712 pixels below the Skills header. All 36 ratings must
                // remain visible in that viewport without vertical scrolling.
                using (var compactSkills = new WorkerSkillsEditor(
                    profile, true, () => { })
                { Size = new System.Drawing.Size(1366, 712) })
                {
                    compactSkills.CreateControl();
                    compactSkills.PerformLayout();
                    Application.DoEvents();
                    var categoryPanels = compactSkills.Controls
                        .Find("SkillsFourPanels", true)
                        .Single() as TableLayoutPanel
                        ?? throw new Exception("The compact Skills layout is missing.");
                    foreach (var category in categoryPanels.Controls
                        .Cast<Control>().OfType<SkillGlassPanel>())
                    {
                        var rows = category.Controls
                            .OfType<TableLayoutPanel>()
                            .SelectMany(panel => panel.Controls.OfType<FlowLayoutPanel>())
                            .Single();
                        rows.PerformLayout();
                        if (rows.VerticalScroll.Visible ||
                            rows.Controls.Cast<Control>().Last().Bottom >
                            rows.ClientSize.Height - rows.Padding.Bottom + 2)
                            throw new Exception(
                                "At game resolution, one or more Skills ratings are clipped or scroll.");
                        if (rows.Controls.Cast<Control>().Any(rating =>
                            rating.Right > rows.ClientSize.Width -
                                rows.Padding.Right + 3))
                            throw new Exception(
                                "Compact Skills rating inputs exceed the panel's right gutter.");
                        foreach (Control rating in rows.Controls)
                        {
                            // Inspect nested controls without relying on a prefix search.
                            var numeric = rating.Controls.Cast<Control>()
                                .SelectMany(child => child.Controls.Cast<Control>())
                                .OfType<SkillNumberSpinner>().FirstOrDefault();
                            if (numeric is null ||
                                numeric.Margin.Top < 2 ||
                                numeric.Margin.Bottom < 2 ||
                                numeric.Controls.OfType<TextBox>().Any(editor =>
                                    editor.Top < 0 || editor.Bottom > numeric.Height))
                                throw new Exception(
                                    "Compact Skills numeric editors touch or overflow adjacent rows.");
                        }
                    }
                    var longestSkill = compactSkills.Controls
                        .Find("SkillLabel_PotentialFundamentals", true)
                        .SingleOrDefault() as Label
                        ?? throw new Exception("The Potential Fundamentals label is missing.");
                    int requiredWidth = TextRenderer.MeasureText(
                        longestSkill.Text, longestSkill.Font,
                        new System.Drawing.Size(1000, 50),
                        TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix |
                        TextFormatFlags.SingleLine).Width;
                    if (requiredWidth > longestSkill.ClientSize.Width)
                        throw new Exception(
                            "The compact Skills workspace clips a full skill label.");

                    string? fullPreview =
                        Environment.GetEnvironmentVariable("PWR_WORKER_PREVIEW");
                    if (!string.IsNullOrWhiteSpace(fullPreview))
                    {
                        string compactPath = Path.Combine(
                            Path.GetDirectoryName(fullPreview)!,
                            "worker-skills-compact-preview.png");
                        using var image = new System.Drawing.Bitmap(
                            compactSkills.ClientSize.Width, compactSkills.ClientSize.Height);
                        compactSkills.DrawToBitmap(image,
                            new System.Drawing.Rectangle(
                                System.Drawing.Point.Empty, image.Size));
                        image.Save(compactPath,
                            System.Drawing.Imaging.ImageFormat.Png);
                        Console.WriteLine("Compact Skills preview saved to " + compactPath);
                    }
                }

                // The current Worker Editor has a single-column popup shell
                // containing a two-column page: 158px section navigation and
                // the main data body. Validate this layout instead of requiring
                // the legacy 61px icon rail, which this screen no longer uses.
                workerEditor.PerformLayout();
                var popupLayout = workerEditor.Controls.Find("WorkerPopupLayout", true)
                    .Single() as TableLayoutPanel;
                var tabColumn = workerEditor.Controls.Find("WorkerSectionNavigation", true).Single();
                var workerBody = workerEditor.Controls.Find("WorkerEditorBody", true).Single();
                var workerPage = workerEditor.Controls.Find("WorkerPage", true)
                    .SingleOrDefault() as TableLayoutPanel;
                if (popupLayout is null || popupLayout.ColumnCount != 1 ||
                    workerPage is null || workerPage.ColumnCount != 2 ||
                    tabColumn.Width != 158 ||
                    tabColumn.Right > workerBody.Left || workerBody.Width < 1000)
                    throw new Exception("Worker editor navigation and data area must not overlap. " +
                        $"Form={workerEditor.Size}, tabs={tabColumn.Bounds}, " +
                        $"body={workerBody.Bounds}, page={workerPage?.Bounds}, " +
                        $"popup={popupLayout?.Bounds}.");
                var personal = workerEditor.Controls.Find("WorkerPersonalData", true).Single();
                var personalFields = workerEditor.Controls.Find("WorkerPersonalDataFields", true).Single();
                personal.PerformLayout();
                if (personalFields.Top < personal.Padding.Top ||
                    personalFields.Bottom > personal.ClientSize.Height)
                    throw new Exception("Worker fields overlap their painted section header or card bounds.");
                var picture = workerEditor.Controls.Find("WorkerPicture", true).Single();
                var biography = workerEditor.Controls.Find("WorkerBiography", true).Single();
                var identity = workerEditor.Controls.Find("WorkerIdentity", true).Single();
                var titleRow = workerEditor.Controls.Find("WorkerIdentityBiographyRow", true).Single();
                if (titleRow.Height < 140 || picture.Right > identity.Width ||
                    biography.Width < 500 || biography.Height < 115)
                    throw new Exception("Worker top identity/biography panels diverge from the approved mockup.");
                var footer = workerEditor.Controls.Find("WorkerEditorActions", true).Single();
                var actionArea = workerEditor.Controls.Find("WorkerActionArea", true).Single();
                var generalCanvas = workerEditor.Controls.Find("WorkerGeneralCanvas", true).Single();
                var generalGrid = workerEditor.Controls.Find("WorkerGeneralGrid", true)
                    .SingleOrDefault() as TableLayoutPanel;
                if (footer.Width < 540 || footer.Top < 0 ||
                    generalGrid is null || generalGrid.RowCount != 2 ||
                    actionArea.Parent != generalGrid ||
                    generalGrid.GetRow(actionArea) != 1 ||
                    generalGrid.GetColumn(actionArea) != 2 ||
                    generalGrid.GetColumnSpan(actionArea) != 2 ||
                    actionArea.Height < 65)
                    throw new Exception("Worker actions must use the dedicated lower-right grid row.");

                // Render the actual WinForms control tree on the CI runner.
                // This preview is a diagnostic artifact and does not alter
                // installed artwork or any skin files.
                string? previewPath = Environment.GetEnvironmentVariable("PWR_WORKER_PREVIEW");
                if (!string.IsNullOrWhiteSpace(previewPath))
                {
                    workerEditor.CreateControl();
                    workerEditor.PerformLayout();
                    using var preview = new System.Drawing.Bitmap(
                        workerEditor.ClientSize.Width, workerEditor.ClientSize.Height);
                    workerEditor.DrawToBitmap(preview,
                        new System.Drawing.Rectangle(
                            System.Drawing.Point.Empty, preview.Size));
                    preview.Save(previewPath,
                        System.Drawing.Imaging.ImageFormat.Png);
                    Console.WriteLine("Worker preview saved to " + previewPath);

                    // Separate reference image for the dedicated 36-rating
                    // Skills screen; no real player universe is opened.
                    typeof(WorkerEditorForm).GetMethod("ShowSection",
                        System.Reflection.BindingFlags.Instance |
                        System.Reflection.BindingFlags.NonPublic)!
                        .Invoke(workerEditor, ["Skills"]);
                    Application.DoEvents();
                    var livePage = workerEditor.Controls.Find("WorkerPage", true)
                        .Single() as TableLayoutPanel;
                    var liveBody = workerEditor.Controls.Find("WorkerEditorBody", true).Single();
                    var liveRail = workerEditor.Controls.Find("WorkerSectionNavigation", true).Single();
                    var liveSkills = workerEditor.Controls.Find("WorkerSkillsEditor", true)
                        .SingleOrDefault() as WorkerSkillsEditor;
                    if (livePage is null || liveSkills is null ||
                        livePage.ColumnStyles[0].Width != 0 ||
                        liveRail.Visible ||
                        liveBody.Width < livePage.ClientSize.Width - 12 ||
                        workerEditor.Controls.Find("WorkerIdentityBiographyRow", true)
                            .Single().Visible)
                        throw new Exception(
                            "Skills must occupy the full Worker Editor width without tabs or biography.");

                    // Back leaves the dedicated Skills screen, preserving
                    // the Worker profile and restoring its normal navigation.
                    workerEditor.Close();
                    Application.DoEvents();
                    if (workerEditor.ActiveSection != "General" ||
                        livePage.ColumnStyles[0].Width != 158 ||
                        liveBody.Width > livePage.ClientSize.Width - 140)
                        throw new Exception("Back did not restore the standard Worker profile.");

                    string skillPreview = Path.Combine(
                        Path.GetDirectoryName(previewPath)!,
                        "worker-skills-preview.png");
                    // Preview editable skill controls, not disabled placeholders
                    // from the deliberately read-only Worker Editor fixture.
                    using var editableSkillsPreview = new WorkerSkillsEditor(
                        profile, true, () => { })
                    {
                        // Reference comparison is authored for a 1650x928
                        // desktop; render a wide editor viewport separately
                        // from the actual 1366px in-game smoke layout.
                        Size = new System.Drawing.Size(1672, 885)
                    };
                    editableSkillsPreview.CreateControl();
                    editableSkillsPreview.PerformLayout();
                    Application.DoEvents();
                    foreach (var glass in editableSkillsPreview.Controls
                        .Find("SkillsFourPanels", true).Single()
                        .Controls.Cast<Control>().OfType<SkillGlassPanel>())
                    {
                        var rows = glass.Controls.OfType<TableLayoutPanel>()
                            .SelectMany(c => c.Controls.OfType<FlowLayoutPanel>())
                            .Single();
                        if (rows.Controls.Cast<Control>().Any(rating =>
                            rating.Right > rows.ClientSize.Width -
                                rows.Padding.Right + 3))
                            throw new Exception(
                                "Reference-sized Skills numeric controls spill into the frame.");
                    }
                    using var skillImage = new System.Drawing.Bitmap(
                        editableSkillsPreview.ClientSize.Width,
                        editableSkillsPreview.ClientSize.Height);
                    editableSkillsPreview.DrawToBitmap(skillImage,
                        new System.Drawing.Rectangle(
                            System.Drawing.Point.Empty, skillImage.Size));
                    skillImage.Save(skillPreview,
                        System.Drawing.Imaging.ImageFormat.Png);
                    Console.WriteLine("Skills preview saved to " + skillPreview);

                }
            }

            // Update installers now accept a raw Windows executable rather
            // than extracting arbitrary files from release ZIP archives.
            var sampleExe = Path.Combine(root, "sample-new.exe");
            var sampleBytes = new byte[256];
            sampleBytes[0] = (byte)'M';
            sampleBytes[1] = (byte)'Z';
            BitConverter.GetBytes(128).CopyTo(sampleBytes, 0x3C);
            sampleBytes[128] = (byte)'P';
            sampleBytes[129] = (byte)'E';
            File.WriteAllBytes(sampleExe, sampleBytes);
            GameUpdate.ValidateApplication(sampleExe);

            var fakeZip = Path.Combine(root, "not-an-exe.exe");
            var fakeArchiveBytes = new byte[256];
            fakeArchiveBytes[0] = (byte)'P';
            fakeArchiveBytes[1] = (byte)'K';
            File.WriteAllBytes(fakeZip, fakeArchiveBytes);
            bool zipRejected = false;
            try { GameUpdate.ValidateApplication(fakeZip); }
            catch (InvalidDataException) { zipRejected = true; }
            if (!zipRejected)
                throw new Exception("ZIP data was incorrectly accepted as an update EXE.");

            // Protect source builds from an in-place upgrade to the published
            // executable, which would change their game asset directory.
            if (!GameUpdate.IsSourceBuildOutput(Path.Combine(
                    debugOutput, "ProWrestlingRevolution.exe")) ||
                !GameUpdate.IsSourceBuildOutput(Path.Combine(
                    sourceCheckout, "bin", "Release", "net10.0-windows",
                    "ProWrestlingRevolution.exe")) ||
                GameUpdate.IsSourceBuildOutput(Path.Combine(
                    standaloneOutput, "ProWrestlingRevolution.exe")))
                throw new Exception("Developer build updater protection failed.");

            var fixture = Path.Combine(root, "sample.mdb");
            var header = new byte[2048];
            Encoding.ASCII.GetBytes("Standard Jet DB").CopyTo(header, 4);
            File.WriteAllBytes(fixture, header);
            // Fixture only checks file management. It is not a valid Access database.
            var world = paths.ImportUniverse(fixture, "Example World");
            if (!File.Exists(world.MdbPath)) throw new Exception("Universe copy missing.");
            var career = paths.CreateCareer(world, "Career 01");
            if (!File.Exists(career.MdbPath)) throw new Exception("Career copy missing.");
            if (!File.Exists(Path.Combine(Path.GetDirectoryName(career.MdbPath)!, "Backups", "initial.mdb")))
                throw new Exception("Recovery snapshot missing.");
            if (paths.Careers().Count != 1 || paths.Universes().Count != 1)
                throw new Exception("Universe enumeration failed.");
            bool unsafeRejected = false;
            try { paths.CreateCareer(world, "../bad"); }
            catch (ArgumentException) { unsafeRejected = true; }
            if (!unsafeRejected) throw new Exception("Unsafe path was allowed.");
            // Both save selection flows retain working modal controls. Database
            // selection is compact; Load Game fills the launcher as its own screen.
            using (var selector = new DatabaseSelectorDialog(paths, skin))
            using (var saves = new LoadGameDialog(paths, skin))
            {
                foreach (var popup in new UtilityPopupForm[] { selector, saves })
                {
                    popup.AttachMainNavigation(popup == saves ? "load" : "database");
                    var nav = popup.Controls.Find("PopupMainNavigation", false)
                        .SingleOrDefault() as MetalSidebar;
                    if (nav is null || nav.Width != SkinArt.SidebarWidth ||
                        nav.Controls.OfType<SidebarMenuRow>().Count() != SkinArt.MenuIcons.Length ||
                        nav.Controls.OfType<SidebarMenuRow>().Any(x => !x.IconOnly) ||
                        nav.Controls.Find("Menu_back", false).Length != 1 ||
                        nav.Controls.Find("Menu_load", false).Length != 1 ||
                        nav.Controls.Find("Menu_database", false).Length != 1)
                        throw new Exception("The main menu must appear on every utility screen.");
                    if (popup.FormBorderStyle != FormBorderStyle.None ||
                        popup.Controls.Find("PopupWorkspace", true).Length != 1 ||
                        popup.Controls.Find("PopupItemList", true).Length != 1 ||
                        popup.Controls.Find("PopupInformation", true).Length != 1 ||
                        popup.Controls.Find("PopupActions", true).Length != 1)
                        throw new Exception("Shared utility popup layout is missing.");
                    popup.PerformLayout();
                }
                if (selector.Controls.Find("ActionUseThisDatabase", true).Length != 1)
                    throw new Exception("Database selection action is missing.");
                if (selector.Controls.Find("ActionCreateDatabase", true).Length != 1)
                    throw new Exception("Database creation action is missing.");
                var load = saves.Controls.Find("ActionLoad", true).SingleOrDefault() as Button;
                if (load is null || load.Enabled)
                    throw new Exception("Unimplemented gameplay loading must stay disabled.");
                using var launcherBounds = new Form
                {
                    FormBorderStyle = FormBorderStyle.None,
                    AutoScaleMode = AutoScaleMode.None,
                    ClientSize = new Size(SkinArt.WindowWidth, SkinArt.WindowHeight)
                };
                saves.FillLauncherWindow(launcherBounds);
                if (saves.Bounds != launcherBounds.Bounds ||
                    saves.StartPosition != FormStartPosition.Manual)
                    throw new Exception(
                        "Load Game must cover the whole launcher, not a floating popup. " +
                        $"Load bounds={saves.Bounds}; Launcher bounds={launcherBounds.Bounds}; " +
                        $"Start={saves.StartPosition}.");
            }

            // Exercise safe save operations using a file-management fixture.
            var renamed = paths.RenameCareer(career, "Renamed Career");
            if (!File.Exists(renamed.MdbPath) || File.Exists(career.MdbPath))
                throw new Exception("Rename did not move the career safely.");
            var copied = paths.CopyCareer(renamed, "Copied Career");
            if (!File.Exists(copied.MdbPath) ||
                !File.Exists(Path.Combine(Path.GetDirectoryName(copied.MdbPath)!,
                    "Backups", "initial.mdb")))
                throw new Exception("Career copy did not retain its recovery data.");
            File.WriteAllBytes(renamed.MdbPath, [1, 2, 3, 4, 5]);
            string preserved = paths.RestoreCareerInitialBackup(renamed);
            if (!File.Exists(preserved) || !AccessDatabase.LooksLikeMdb(renamed.MdbPath))
                throw new Exception("Recovery did not preserve and restore MDB files.");
            string archived = paths.ArchiveCareer(renamed);
            if (!Directory.Exists(archived) || File.Exists(renamed.MdbPath) ||
                !File.Exists(copied.MdbPath))
                throw new Exception("Archive must preserve the removed save and other careers.");

            Console.WriteLine("Popup and save-management smoke tests passed.");
        }
        finally
        {
            try { Directory.Delete(root, true); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }
}