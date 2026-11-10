using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace ProWrestlingRevolution;

/// <summary>
/// Preserves the selected skin's actual Title_Screen.jpg without editing it,
/// adding a second logo, or stretching the logo's aspect ratio.
/// </summary>
public sealed class TitleArtworkView : Control
{
    private Image? _artwork;
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    [System.ComponentModel.Browsable(false)]
    internal Image? Artwork
    {
        get => _artwork;
        set { _artwork = value; Invalidate(); }
    }

    public TitleArtworkView()
    {
        DoubleBuffered = true;
        ResizeRedraw = true;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        if (_artwork is null || Width < 1 || Height < 1) return;
        var g = e.Graphics;
        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
        int fittedHeight = (int)Math.Ceiling((double)Width * _artwork.Height / _artwork.Width);
        if (fittedHeight >= Height)
        {
            // Do not distort the artwork, even if future window dimensions change.
            var sourceHeight = Math.Min(_artwork.Height,
                (int)Math.Ceiling((double)Height * _artwork.Width / Width));
            g.DrawImage(_artwork, new Rectangle(0, 0, Width, Height),
                new Rectangle(0, (_artwork.Height - sourceHeight) / 2,
                    _artwork.Width, sourceHeight), GraphicsUnit.Pixel);
            return;
        }

        // Show the complete artwork without stretching or repeating its lettering.
        // Any remaining space uses the theme background with a subtle fade.
        g.DrawImage(_artwork, new Rectangle(0, 0, Width, fittedHeight));
        int fadeHeight = Math.Min(48, fittedHeight);
        var fadeBounds = new Rectangle(0, fittedHeight - fadeHeight, Width,
            Height - fittedHeight + fadeHeight);
        using var fade = new LinearGradientBrush(fadeBounds,
            Color.FromArgb(0, BackColor), BackColor, LinearGradientMode.Vertical);
        g.FillRectangle(fade, fadeBounds);
    }
}
