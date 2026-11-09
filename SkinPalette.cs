using System.Drawing;

namespace ProWrestlingRevolution;

public sealed class SkinPalette
{
    public string Id { get; init; } = "Obsidian";
    public Color Background { get; init; }
    public Color Sidebar { get; init; }
    public Color Surface { get; init; }
    public Color Accent { get; init; }
    public Color AccentSecondary { get; init; }
    public Color Foreground { get; init; }
    public Color Muted { get; init; }
    public Color Border { get; init; }
    public Color Button { get; init; }
    public string? BackgroundImage { get; init; }
    public string? LogoImage { get; init; }

    public static SkinPalette Parse(string name, Dictionary<string, string> settings, string folder)
    {
        Color C(string key, string fallback)
        {
            string candidate = settings.GetValueOrDefault(key, fallback);
            try { return ColorTranslator.FromHtml(candidate); }
            catch { return ColorTranslator.FromHtml(fallback); }
        }

        string? ImageFile(string key)
        {
            if (!settings.TryGetValue(key, out var filename)) return null;
            var safe = Path.GetFileName(filename);
            if (!safe.Equals(filename, StringComparison.OrdinalIgnoreCase)) return null;
            var candidate = Path.Combine(folder, safe);
            return File.Exists(candidate) && new[] { ".png", ".jpg", ".jpeg" }
                .Contains(Path.GetExtension(candidate).ToLowerInvariant()) ? candidate : null;
        }

        return new SkinPalette
        {
            Id = name,
            Background = C("Background", "#0B111D"),
            Sidebar = C("Sidebar", "#101A2A"),
            Surface = C("Surface", "#172438"),
            Accent = C("Accent", "#24B8DE"),
            AccentSecondary = C("AccentSecondary", "#6FE5EF"),
            Foreground = C("Foreground", "#F1F7FA"),
            Muted = C("Muted", "#9EB1C4"),
            Border = C("Border", "#314A63"),
            Button = C("Button", "#1C334B"),
            BackgroundImage = ImageFile("BackgroundImage"),
            LogoImage = ImageFile("LogoImage")
        };
    }
}