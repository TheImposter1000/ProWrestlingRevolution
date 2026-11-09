using System.Drawing;

namespace ProWrestlingRevolution;

/// <summary>
/// Holds colors for native secondary-screen controls. The title screen itself
/// is painted from categorised JPG assets, never from a theme config file.
/// </summary>
public sealed class SkinPalette
{
    public string Id { get; init; } = "Obsidian";
    public string Folder { get; init; } = "";
    public Color Background { get; init; }
    public Color Sidebar { get; init; }
    public Color Surface { get; init; }
    public Color Accent { get; init; }
    public Color AccentSecondary { get; init; }
    public Color Foreground { get; init; }
    public Color Muted { get; init; }
    public Color Border { get; init; }
    public Color Button { get; init; }

    public string Asset(string relativeFile) => Path.Combine(Folder, relativeFile);
    public string? BackgroundImage => Asset(Path.Combine("Backgrounds", "Title_Screen.jpg"));
    public string? LogoImage => null;

    public static SkinPalette FromImages(string name, string folder)
    {
        bool steel = name.Equals("Steel", StringComparison.OrdinalIgnoreCase);
        return new SkinPalette
        {
            Id = name,
            Folder = folder,
            Background = steel ? Color.FromArgb(16, 20, 28) : Color.FromArgb(10, 15, 27),
            Sidebar = steel ? Color.FromArgb(16, 19, 26) : Color.FromArgb(15, 18, 29),
            Surface = steel ? Color.FromArgb(34, 39, 49) : Color.FromArgb(24, 34, 50),
            Accent = steel ? Color.FromArgb(221, 173, 83) : Color.FromArgb(29, 170, 213),
            AccentSecondary = steel ? Color.FromArgb(255, 222, 157) : Color.FromArgb(111, 233, 250),
            Foreground = Color.White,
            Muted = steel ? Color.FromArgb(172, 180, 198) : Color.FromArgb(170, 196, 213),
            Border = steel ? Color.FromArgb(70, 78, 91) : Color.FromArgb(42, 76, 104),
            Button = steel ? Color.FromArgb(42, 49, 61) : Color.FromArgb(25, 48, 69)
        };
    }
}
