// Icons drawn procedurally via GDI+ instead of embedded image resources
// - keeps the project at zero binary assets (consistent with the
// reproducible-build goal: nothing here depends on how an image file
// got encoded/re-saved by whatever tool touched it last). Caller owns
// the returned Bitmap and must Dispose it - see
// KeePassCopyKeyExt.Terminate.
//
// Export and Import icons deliberately mirror each other: same tray
// shape anchored at the bottom (y=10-13 in both), only the arrow
// direction differs. Coordinates below are symmetric on purpose - each
// arrow's shaft-and-head lines meet at exactly the same point (the tip),
// and both arrows occupy the same y=1..9 vertical span above the tray.
// A previous version had the import arrowhead's tip one pixel past the
// shaft's end (y=10 vs y=9), which visually "sank" the whole arrow
// toward the tray compared to the export icon - fixed by making both
// arrows' tip coordinates match their shaft's end coordinate exactly.

using System.Drawing;
using System.Drawing.Drawing2D;

namespace KeePassCopyKey.UI;

internal static class ToolbarIcons
{
    public static Bitmap CreateExportIcon()
    {
        var bmp = new Bitmap(16, 16);
        using var g = Graphics.FromImage(bmp);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(Color.Transparent);

        using var pen = new Pen(Color.FromArgb(150, 90, 20), 1.8f);
        g.DrawLine(pen, 8, 9, 8, 1);            // arrow shaft, tip at y=1
        g.DrawLine(pen, 4, 5, 8, 1);            // arrow head, left - meets shaft tip exactly
        g.DrawLine(pen, 12, 5, 8, 1);           // arrow head, right - meets shaft tip exactly
        g.DrawLine(pen, 2, 13, 14, 13);         // tray bottom
        g.DrawLine(pen, 2, 13, 2, 10);          // tray left wall
        g.DrawLine(pen, 14, 13, 14, 10);        // tray right wall

        return bmp;
    }

    public static Bitmap CreateImportIcon()
    {
        var bmp = new Bitmap(16, 16);
        using var g = Graphics.FromImage(bmp);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(Color.Transparent);

        using var pen = new Pen(Color.FromArgb(40, 110, 50), 1.8f);
        g.DrawLine(pen, 8, 1, 8, 9);            // arrow shaft, tip at y=9
        g.DrawLine(pen, 4, 5, 8, 9);            // arrow head, left - meets shaft tip exactly
        g.DrawLine(pen, 12, 5, 8, 9);           // arrow head, right - meets shaft tip exactly
        g.DrawLine(pen, 2, 13, 14, 13);         // tray bottom
        g.DrawLine(pen, 2, 13, 2, 10);          // tray left wall
        g.DrawLine(pen, 14, 13, 14, 10);        // tray right wall

        return bmp;
    }
}