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

        if (_artwork is null || Width <= 0 || Height <= 0)
            return;

        var g = e.Graphics;

        g.Clear(BackColor);
        g.InterpolationMode =
            InterpolationMode.HighQualityBicubic;

        double scale = Math.Min(
            (double)Width / _artwork.Width,
            (double)Height / _artwork.Height
        );

        int imageWidth = (int)(_artwork.Width * scale);
        int imageHeight = (int)(_artwork.Height * scale);

        int x = (Width - imageWidth) / 2;
        int y = (Height - imageHeight) / 2;

        g.DrawImage(
            _artwork,
            new Rectangle(x, y, imageWidth, imageHeight)
        );
    }

}
