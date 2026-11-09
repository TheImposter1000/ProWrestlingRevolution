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

    public AppPaths(string root)
    {
        _root = Path.GetFullPath(root);
        _settings = Path.Combine(SystemRoot, "settings.ini");
    }

    public void EnsureLayout()
    {
        foreach (var dir in new[] { Databases, Pictures, SkinsRoot, SystemRoot })
            Directory.CreateDirectory(dir);

        EnsureSkin("Obsidian", """
            Name=Obsidian
            Background=#0B111D
            Sidebar=#101A2A
            Surface=#172438
            Accent=#24B8DE
            AccentSecondary=#6FE5EF
            Foreground=#F1F7FA
            Muted=#9EB1C4
            Border=#314A63
            Button=#1C334B
            """);
        EnsureSkin("Steel", """
            Name=Steel
            Background=#14171E
            Sidebar=#1B202A
            Surface=#272F3B
            Accent=#D7B069
            AccentSecondary=#F7D99D
            Foreground=#FAFAF8
            Muted=#AAB5C1
            Border=#4D5868
            Button=#303B4B
            """);

        if (!File.Exists(_settings))
            File.WriteAllText(_settings, "Skin=Obsidian\nDatabase=\n", Encoding.UTF8);
    }

    private void EnsureSkin(string slug, string contents)
    {
        var folder = Path.Combine(SkinsRoot, slug);
        Directory.CreateDirectory(folder);
        var filename = Path.Combine(folder, "skin.ini");
        if (!File.Exists(filename)) File.WriteAllText(filename, contents + "\n", Encoding.UTF8);
    }

    public IReadOnlyList<string> Skins() =>
        Directory.EnumerateDirectories(SkinsRoot)
            .Where(x => File.Exists(Path.Combine(x, "skin.ini")))
            .Select(Path.GetFileName).Where(x => x is not null).Select(x => x!)
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToList();

    public SkinPalette LoadSkin(string? name)
    {
        var allowed = Skins().FirstOrDefault(s => s.Equals(name, StringComparison.OrdinalIgnoreCase)) ?? "Obsidian";
        var content = Ini.Read(Path.Combine(SkinsRoot, allowed, "skin.ini"));
        return SkinPalette.Parse(allowed, content, Path.Combine(SkinsRoot, allowed));
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