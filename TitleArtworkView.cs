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

        if (_artwork == null || Width <= 0 || Height <= 0)
            return;

        var g = e.Graphics;
        g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
        g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;

        // "Cover" behavior: fill the whole area while preserving aspect ratio
        float scale = Math.Max((float)Width / _artwork.Width, (float)Height / _artwork.Height);

        int drawWidth = (int)Math.Ceiling(_artwork.Width * scale);
        int drawHeight = (int)Math.Ceiling(_artwork.Height * scale);

        int drawX = (Width - drawWidth) / 2;
        int drawY = (Height - drawHeight) / 2;

        g.DrawImage(_artwork, new Rectangle(drawX, drawY, drawWidth, drawHeight));

    }
}
