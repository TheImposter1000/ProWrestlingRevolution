using System.Drawing;
using System.Windows.Forms;

namespace ProWrestlingRevolution;

/// <summary>
/// Reuses the Worker Editor's dark steel and cyan PWR skin colors rather than
/// drawing unthemed native Windows context menus over the game interface.
/// </summary>
internal sealed class WorkerMenuColorTable : ProfessionalColorTable
{
    private readonly Color _accent;
    private readonly Color _highlight;

    internal WorkerMenuColorTable(SkinPalette palette)
    {
        _accent = palette.Accent;
        _highlight = palette.AccentSecondary;
        UseSystemColors = false;
    }

    public override Color ToolStripDropDownBackground => Color.FromArgb(10, 24, 42);
    public override Color MenuBorder => _accent;
    public override Color MenuItemBorder => _highlight;
    public override Color MenuItemSelected => Color.FromArgb(30, 91, 145);
    public override Color MenuItemSelectedGradientBegin => Color.FromArgb(40, 113, 175);
    public override Color MenuItemSelectedGradientEnd => Color.FromArgb(21, 61, 105);
    public override Color MenuItemPressedGradientBegin => Color.FromArgb(38, 106, 159);
    public override Color MenuItemPressedGradientMiddle => Color.FromArgb(29, 85, 136);
    public override Color MenuItemPressedGradientEnd => Color.FromArgb(19, 52, 87);
    public override Color ImageMarginGradientBegin => ToolStripDropDownBackground;
    public override Color ImageMarginGradientMiddle => ToolStripDropDownBackground;
    public override Color ImageMarginGradientEnd => ToolStripDropDownBackground;
    public override Color SeparatorDark => Color.FromArgb(70, 111, 153);
    public override Color SeparatorLight => Color.FromArgb(16, 40, 67);
}
