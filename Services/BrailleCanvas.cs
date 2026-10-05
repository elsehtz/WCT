using System.Text;

namespace WorldCupTerminal.Services;

/// <summary>
/// A monochrome dot canvas rendered with Unicode braille patterns (U+2800..U+28FF).
/// Each braille glyph packs a 2-wide by 4-tall grid of dots, so the canvas has
/// 2x horizontal and 4x vertical resolution compared to the character grid — the
/// classic trick for drawing "graphics" in a text terminal.
/// </summary>
public class BrailleCanvas
{
    private readonly bool[,] _dots;   // [x, y]
    public int Width { get; }
    public int Height { get; }

    // Dot bit for a position within a 2x4 cell, per the braille numbering scheme.
    private static readonly int[,] DotBits =
    {
        { 0x01, 0x02, 0x04, 0x40 },   // left column:  dots 1,2,3,7
        { 0x08, 0x10, 0x20, 0x80 },   // right column: dots 4,5,6,8
    };

    public BrailleCanvas(int width, int height)
    {
        Width = width;
        Height = height;
        _dots = new bool[width, height];
    }

    public void Set(int x, int y)
    {
        if (x < 0 || y < 0 || x >= Width || y >= Height) return;
        _dots[x, y] = true;
    }

    /// <summary>Bresenham line between two dot coordinates.</summary>
    public void Line(int x0, int y0, int x1, int y1)
    {
        int dx = Math.Abs(x1 - x0), dy = -Math.Abs(y1 - y0);
        int sx = x0 < x1 ? 1 : -1, sy = y0 < y1 ? 1 : -1;
        int err = dx + dy;
        while (true)
        {
            Set(x0, y0);
            if (x0 == x1 && y0 == y1) break;
            int e2 = 2 * err;
            if (e2 >= dy) { err += dy; x0 += sx; }
            if (e2 <= dx) { err += dx; y0 += sy; }
        }
    }

    public void Polygon(IReadOnlyList<(int x, int y)> pts, bool close = true)
    {
        for (int i = 0; i < pts.Count - 1; i++)
            Line(pts[i].x, pts[i].y, pts[i + 1].x, pts[i + 1].y);
        if (close && pts.Count > 2)
            Line(pts[^1].x, pts[^1].y, pts[0].x, pts[0].y);
    }

    public string Render()
    {
        var sb = new StringBuilder();
        int cols = (Width + 1) / 2;
        int rows = (Height + 3) / 4;
        for (int cy = 0; cy < rows; cy++)
        {
            for (int cx = 0; cx < cols; cx++)
            {
                int bits = 0;
                for (int ix = 0; ix < 2; ix++)
                    for (int iy = 0; iy < 4; iy++)
                    {
                        int x = cx * 2 + ix;
                        int y = cy * 4 + iy;
                        if (x < Width && y < Height && _dots[x, y])
                            bits |= DotBits[ix, iy];
                    }
                sb.Append((char)(0x2800 + bits));
            }
            sb.Append('\n');
        }
        return sb.ToString().TrimEnd('\n');
    }
}
