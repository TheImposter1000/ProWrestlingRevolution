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
            if (args.Contains("--generate-assets", StringComparer.OrdinalIgnoreCase))
            {
                var workingDirectory = new AppPaths(Directory.GetCurrentDirectory());
                workingDirectory.EnsureLayout();
                return 0;
            }
            if (args.Contains("--smoke", StringComparer.OrdinalIgnoreCase))
            {
                SmokeTest();
                return 0;
            }

            ApplicationConfiguration.Initialize();
            var paths = new AppPaths(AppContext.BaseDirectory);
            paths.EnsureLayout();
            Application.Run(new LauncherForm(paths));
            return 0;
        }
        catch (Exception e)
        {
            Console.Error.WriteLine(e);
            if (!args.Contains("--smoke", StringComparer.OrdinalIgnoreCase))
                MessageBox.Show(e.Message + "\n\nIf the folder is protected, extract the game to a writable location.",
                    "Pro Wrestling Revolution", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return 1;
        }
    }

    private static void SmokeTest()
    {
        var root = Path.Combine(Path.GetTempPath(), "PWR-phase1-" + Guid.NewGuid().ToString("N"));
        try
        {
            var paths = new AppPaths(root);
            paths.EnsureLayout();
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
            using (var launcher = new LauncherForm(paths))
            {
                if (launcher.ClientSize.Width != SkinArt.WindowWidth ||
                    launcher.ClientSize.Height != SkinArt.WindowHeight)
                    throw new Exception(
                        "Launcher client dimensions must be 1366 x 768, received " +
                        launcher.ClientSize.Width + " x " + launcher.ClientSize.Height + ".");
                if (launcher.FormBorderStyle != FormBorderStyle.None)
                    throw new Exception("Launcher must not use a resizable standard border.");
                var sidebar = launcher.Controls.OfType<Panel>()
                    .SingleOrDefault(control => control.Dock == DockStyle.Left);
                if (sidebar is null || sidebar.Width != 240)
                    throw new Exception("Unified navigation width must be 240.");
                if (sidebar.Controls.OfType<PictureBox>().Any())
                    throw new Exception("Old graphical icon strip was still added to sidebar.");
                var rows = sidebar.Controls.OfType<SidebarMenuRow>().ToList();
                if (rows.Count != SkinArt.MenuIcons.Length)
                    throw new Exception("Sidebar must contain eight unified navigation rows.");
                if (rows.Any(row => row.AccessibleRole != AccessibleRole.PushButton))
                    throw new Exception("Navigation row is not accessible as a button.");
            }

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
            Console.WriteLine("Phase One smoke tests passed.");
        }
        finally
        {
            try { Directory.Delete(root, true); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }
}