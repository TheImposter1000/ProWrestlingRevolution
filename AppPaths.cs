using System.Text;

namespace ProWrestlingRevolution;

public sealed record UniverseEntry(string Name, string MdbPath, string DirectoryPath);
public sealed record CareerEntry(string Name, string UniverseName, string MdbPath);

public sealed class AppPaths
{
    private readonly string _root;
    private readonly string _settings;
    public string Root => _root;
    public string Databases => Path.Combine(_root, "Databases");
    public string Pictures => Path.Combine(_root, "Pictures");
    public string SkinsRoot => Path.Combine(_root, "Skins");
    public string SystemRoot => Path.Combine(_root, "System");
    public string DefaultDatabase => Path.Combine(SystemRoot, PrwDefaultTemplate.Filename);

    public AppPaths(string root)
    {
        _root = Path.GetFullPath(root);
        _settings = Path.Combine(SystemRoot, "settings.ini");
    }

    public void EnsureLayout()
    {
        foreach (var dir in new[] { Databases, Pictures, SkinsRoot, SystemRoot })
            Directory.CreateDirectory(dir);

        // The bundled base MDB is installed locally once. Never read from a
        // repository checkout or overwrite a user-maintained template.
        PrwDefaultTemplate.EnsureInstalled(SystemRoot);

        // Build missing, original raster skin assets; existing user artwork is never overwritten.
        SkinArt.EnsurePack(Path.Combine(SkinsRoot, "Obsidian"), "Obsidian");
        SkinArt.EnsurePack(Path.Combine(SkinsRoot, "Steel"), "Steel");

        // Migrate older flat folders for user-installed custom skins, without deleting originals.
        foreach (var folder in Directory.EnumerateDirectories(SkinsRoot))
        {
            if (!Path.GetFileName(folder).Equals("Obsidian", StringComparison.OrdinalIgnoreCase) &&
                !Path.GetFileName(folder).Equals("Steel", StringComparison.OrdinalIgnoreCase))
                SkinArt.MigrateFlatPack(folder);
        }

        if (!File.Exists(_settings))
            File.WriteAllText(_settings, "Skin=Obsidian\nDatabase=\n", Encoding.UTF8);
    }

    // Every folder is selectable, including a work-in-progress skin with
    // only one image. Missing assets are resolved by the launcher against
    // the built-in Obsidian image pack.
    public IReadOnlyList<string> Skins() =>
        Directory.EnumerateDirectories(SkinsRoot)
            .Select(x => Path.GetFileName(x)!)
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToList();

    public SkinPalette LoadSkin(string? name)
    {
        var selected = Skins().FirstOrDefault(s => s.Equals(name, StringComparison.OrdinalIgnoreCase))
            ?? "Obsidian";
        return SkinPalette.FromImages(selected, Path.Combine(SkinsRoot, selected));
    }

    public string ReadSetting(string key, string fallback) =>
        Ini.Read(_settings).TryGetValue(key, out var value) ? value : fallback;

    public void WriteSetting(string key, string value)
    {
        var values = Ini.Read(_settings);
        values[key] = value;
        Ini.Write(_settings, values);
    }

    public IReadOnlyList<UniverseEntry> Universes()
    {
        if (!Directory.Exists(Databases)) return [];
        var result = new List<UniverseEntry>();
        foreach (var dir in Directory.EnumerateDirectories(Databases))
        {
            var mdb = Directory.EnumerateFiles(dir, "*.mdb", SearchOption.TopDirectoryOnly)
                .OrderBy(p => Path.GetFileName(p).Equals("PWR.mdb", StringComparison.OrdinalIgnoreCase) ? 0 : 1)
                .FirstOrDefault();
            if (mdb is not null) result.Add(new(Path.GetFileName(dir), mdb, dir));
        }
        return result.OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase).ToList();
    }

    public IReadOnlyList<CareerEntry> Careers()
    {
        var result = new List<CareerEntry>();
        foreach (var universe in Universes())
        {
            var parent = Path.Combine(universe.DirectoryPath, "SaveGames");
            if (!Directory.Exists(parent)) continue;
            foreach (var folder in Directory.EnumerateDirectories(parent))
            {
                var mdb = Directory.EnumerateFiles(folder, "*.mdb", SearchOption.TopDirectoryOnly)
                    .FirstOrDefault(f => Path.GetFileName(f).Equals("PWRSave.mdb", StringComparison.OrdinalIgnoreCase));
                if (mdb is not null) result.Add(new(Path.GetFileName(folder), universe.Name, mdb));
            }
        }
        return result.OrderBy(x => x.UniverseName).ThenBy(x => x.Name).ToList();
    }

    /// <summary>
    /// Creates an independent game universe from the local PRWDefault.mdb.
    /// Every subsequent read and edit uses Databases/{Name}/PWR.mdb, not the
    /// bundled template and never the source repository.
    /// </summary>
    public UniverseEntry CreateUniverse(string displayName)
    {
        string name = SafeName(displayName);
        string template = PrwDefaultTemplate.EnsureInstalled(SystemRoot);
        if (!AccessDatabase.LooksLikeMdb(template))
            throw new InvalidDataException("The local default database is invalid.");

        string folder = Path.Combine(Databases, name);
        if (Directory.Exists(folder) || File.Exists(folder))
            throw new IOException("A database with that name already exists.");

        Directory.CreateDirectory(folder);
        try
        {
            string target = Path.Combine(folder, "PWR.mdb");
            File.Copy(template, target);
            Directory.CreateDirectory(Path.Combine(folder, "SaveGames"));
            return new UniverseEntry(name, target, folder);
        }
        catch
        {
            Directory.Delete(folder, recursive: true);
            throw;
        }
    }

    public UniverseEntry ImportUniverse(string sourceFile, string displayName)
    {
        string name = SafeName(displayName);
        if (!File.Exists(sourceFile)) throw new FileNotFoundException("The selected database does not exist.", sourceFile);
        if (!sourceFile.EndsWith(".mdb", StringComparison.OrdinalIgnoreCase) || !AccessDatabase.LooksLikeMdb(sourceFile))
            throw new InvalidDataException("Choose a genuine Microsoft Access .mdb file.");
        var folder = Path.Combine(Databases, name);
        if (Directory.Exists(folder)) throw new IOException("A universe with that name already exists.");
        Directory.CreateDirectory(folder);
        try
        {
            var target = Path.Combine(folder, "PWR.mdb");
            File.Copy(sourceFile, target);
            Directory.CreateDirectory(Path.Combine(folder, "SaveGames"));
            return new(name, target, folder);
        }
        catch
        {
            Directory.Delete(folder, true);
            throw;
        }
    }

    public CareerEntry CreateCareer(UniverseEntry world, string careerName)
    {
        var name = SafeName(careerName);
        if (!File.Exists(world.MdbPath)) throw new FileNotFoundException("The universe file is missing.", world.MdbPath);
        var saveRoot = Path.Combine(world.DirectoryPath, "SaveGames");
        Directory.CreateDirectory(saveRoot);
        var careerDir = Path.Combine(saveRoot, name);
        if (Directory.Exists(careerDir)) throw new IOException("A career with that name already exists in this universe.");
        Directory.CreateDirectory(careerDir);
        try
        {
            var destination = Path.Combine(careerDir, "PWRSave.mdb");
            File.Copy(world.MdbPath, destination);
            var backupDir = Path.Combine(careerDir, "Backups");
            Directory.CreateDirectory(backupDir);
            File.Copy(destination, Path.Combine(backupDir, "initial.mdb"));
            return new(name, world.Name, destination);
        }
        catch
        {
            Directory.Delete(careerDir, true);
            throw;
        }
    }

    /// <summary>
    /// Save operations are restricted to careers currently discovered under
    /// Databases/{Universe}/SaveGames. These routines never write to a universe MDB.
    /// </summary>
    private string LocateCareerDirectory(CareerEntry career)
    {
        var existing = Careers().FirstOrDefault(x =>
            string.Equals(x.MdbPath, career.MdbPath, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(x.UniverseName, career.UniverseName, StringComparison.OrdinalIgnoreCase));
        if (existing is null)
            throw new FileNotFoundException("This saved career is no longer available.");
        var directory = Path.GetDirectoryName(existing.MdbPath)
            ?? throw new InvalidDataException("The save's folder is missing.");
        return directory;
    }

    public CareerEntry RenameCareer(CareerEntry career, string newName)
    {
        string name = SafeName(newName);
        string source = LocateCareerDirectory(career);
        if (name.Equals(career.Name, StringComparison.OrdinalIgnoreCase))
            return career;
        string destination = Path.Combine(Path.GetDirectoryName(source)!, name);
        if (Directory.Exists(destination))
            throw new IOException("A saved career with that name already exists.");
        Directory.Move(source, destination);
        return new CareerEntry(name, career.UniverseName,
            Path.Combine(destination, "PWRSave.mdb"));
    }

    public CareerEntry CopyCareer(CareerEntry career, string copyName)
    {
        string name = SafeName(copyName);
        string source = LocateCareerDirectory(career);
        string destination = Path.Combine(Path.GetDirectoryName(source)!, name);
        if (Directory.Exists(destination))
            throw new IOException("A saved career with that name already exists.");
        Directory.CreateDirectory(destination);
        try
        {
            // Retain the entire save directory, including recovery data and any
            // future career metadata, but reject filesystem links.
            CopyCareerFiles(source, destination);
            if (!File.Exists(Path.Combine(destination, "PWRSave.mdb")))
                throw new IOException("The copied career has no PWRSave.mdb.");
            return new CareerEntry(name, career.UniverseName,
                Path.Combine(destination, "PWRSave.mdb"));
        }
        catch
        {
            Directory.Delete(destination, true);
            throw;
        }
    }

    private static void CopyCareerFiles(string source, string target)
    {
        foreach (string file in Directory.EnumerateFiles(source))
        {
            if (File.GetAttributes(file).HasFlag(FileAttributes.ReparsePoint))
                throw new IOException("Linked files cannot be copied as career data.");
            File.Copy(file, Path.Combine(target, Path.GetFileName(file)));
        }
        foreach (string folder in Directory.EnumerateDirectories(source))
        {
            if (File.GetAttributes(folder).HasFlag(FileAttributes.ReparsePoint))
                throw new IOException("Linked folders cannot be copied as career data.");
            string subfolder = Path.Combine(target, Path.GetFileName(folder));
            Directory.CreateDirectory(subfolder);
            CopyCareerFiles(folder, subfolder);
        }
    }

    public string RestoreCareerInitialBackup(CareerEntry career)
    {
        string directory = LocateCareerDirectory(career);
        string active = Path.Combine(directory, "PWRSave.mdb");
        string backupFolder = Path.Combine(directory, "Backups");
        string initial = Path.Combine(backupFolder, "initial.mdb");
        if (!File.Exists(initial) || !AccessDatabase.LooksLikeMdb(initial))
            throw new FileNotFoundException(
                "The initial recovery MDB is missing or unrecognized.", initial);

        Directory.CreateDirectory(backupFolder);
        string oldCopy = Path.Combine(backupFolder,
            "before-recovery-" + DateTime.Now.ToString("yyyyMMdd-HHmmss-fff") +
            "-" + Guid.NewGuid().ToString("N")[..8] + ".mdb");
        File.Copy(active, oldCopy);

        // Prepare the new file alongside the original, then replace it only
        // when the copy succeeds. Never change the parent universe MDB.
        string temporary = Path.Combine(directory, ".restore-" +
            Guid.NewGuid().ToString("N") + ".mdb");
        try
        {
            File.Copy(initial, temporary);
            File.Move(temporary, active, true);
            return oldCopy;
        }
        finally
        {
            if (File.Exists(temporary)) File.Delete(temporary);
        }
    }

    public string ArchiveCareer(CareerEntry career)
    {
        string directory = LocateCareerDirectory(career);
        string archiveRoot = Path.Combine(SystemRoot, "DeletedSaves");
        Directory.CreateDirectory(archiveRoot);
        string folder = Path.Combine(archiveRoot,
            SafeName(career.UniverseName) + "_" + SafeName(career.Name) + "_" +
            DateTime.Now.ToString("yyyyMMdd-HHmmss-fff") + "_" +
            Guid.NewGuid().ToString("N")[..8]);
        Directory.Move(directory, folder);
        return folder;
    }

    public static string SafeName(string value)
    {
        var name = value.Trim();
        if (name.Length is 0 or > 72 || name is "." or ".." ||
            name.EndsWith('.') || name.EndsWith(' ') ||
            name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 ||
            name.Contains('/') || name.Contains('\\') ||
            new[] { "CON", "PRN", "AUX", "NUL", "COM1", "LPT1" }
                .Contains(name.Split('.')[0], StringComparer.OrdinalIgnoreCase))
            throw new ArgumentException("Enter a simple name without special path characters (up to 72 characters).");
        return name;
    }
}

internal static class Ini
{
    public static Dictionary<string, string> Read(string file)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (!File.Exists(file)) return values;
        foreach (var line in File.ReadAllLines(file))
        {
            var trimmed = line.Trim();
            if (trimmed.Length == 0 || trimmed.StartsWith('#') || trimmed.StartsWith(';')) continue;
            int i = trimmed.IndexOf('=');
            if (i > 0) values[trimmed[..i].Trim()] = trimmed[(i + 1)..].Trim();
        }
        return values;
    }

    public static void Write(string file, Dictionary<string, string> values)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        string temp = file + ".tmp";
        File.WriteAllLines(temp, values.Select(kv => kv.Key + "=" + kv.Value), Encoding.UTF8);
        File.Move(temp, file, overwrite: true);
    }
}