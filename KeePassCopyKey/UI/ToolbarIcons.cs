// Icons drawn procedurally via GDI+ instead of embedded image resources
// - keeps the project at zero binary assets (consistent with the
// reproducible-build goal: nothing here depends on how an image file
// got encoded/re-saved by whatever tool touched it last). Caller owns
// the returned Bitmap and must Dispose it - see
// KeePassCopyKeyExt.Terminate.

using System.Drawing;
using System.Drawing.Drawing2D;

namespace KeePassCopyKey.UI;

internal static class ToolbarIcons
{
    public static Bitmap CreateKeyIcon()
    {
        var bmp = new Bitmap(16, 16);
        using var g = Graphics.FromImage(bmp);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(Color.Transparent);

        using var pen = new Pen(Color.FromArgb(150, 110, 20), 1.6f);
        g.DrawEllipse(pen, 1, 4, 7, 7);       // bow of the key
        g.DrawEllipse(pen, 3.2f, 6.2f, 2.6f, 2.6f); // hole in the bow
        g.DrawLine(pen, 7.5f, 7.5f, 14, 7.5f);      // shaft
        g.DrawLine(pen, 11, 7.5f, 11, 10.5f);        // tooth
        g.DrawLine(pen, 13, 7.5f, 13, 9.5f);         // tooth

        return bmp;
    }

    public static Bitmap CreateImportIcon()
    {
        var bmp = new Bitmap(16, 16);
        using var g = Graphics.FromImage(bmp);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(Color.Transparent);

        using var pen = new Pen(Color.FromArgb(40, 110, 50), 1.8f);
        g.DrawLine(pen, 8, 1, 8, 9);           // arrow shaft (downward)
        g.DrawLine(pen, 4, 6, 8, 10);          // arrow head, left
        g.DrawLine(pen, 12, 6, 8, 10);         // arrow head, right
        g.DrawLine(pen, 2, 13, 14, 13);        // tray bottom
        g.DrawLine(pen, 2, 13, 2, 10);         // tray left wall
        g.DrawLine(pen, 14, 13, 14, 10);       // tray right wall

        return bmp;
    }
}
