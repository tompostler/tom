using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Text;

namespace Unlimitedinf.Utilities;

/// <summary>
/// Generates identicon images — deterministic geometric icons derived from a string seed.
/// Ported from the WP_Identicon WordPress plugin (v2.0) by Scott Sherrill-Mix.
/// https://scott.sherrillmix.com/blog/blogger/wp_identicon/
/// </summary>
public class IdenticonGenerator
{
    private const int BlockSize = 80;

    // Precomputed once — shapes depend only on BlockSize which is const.
    private static readonly double s_half = BlockSize / 2.0;
    private static readonly double s_quarter = BlockSize / 4.0;
    private static readonly double s_diagonal = Math.Sqrt(2.0) * s_half;
    private static readonly double s_halfdiag = s_diagonal / 2.0;
    private static readonly (double[][][][] Shapes, int[] Rotatable) s_shapes = BuildShapes(s_half, s_quarter, s_diagonal, s_halfdiag);
    private static readonly int[] s_rotationFill = [0, 270, 180, 90];

    /// <summary>Number of cells along each side of the grid.</summary>
    public int BlockCount { get; init; } = 4;

    /// <summary>Background red channel range 0–255. Defaults all background channels to [0,0] for transparent.</summary>
    public ColorChannelRange BackgroundRedRange { get; init; } = new(0, 0);
    /// <summary>Background green channel range 0–255.</summary>
    public ColorChannelRange BackgroundGreenRange { get; init; } = new(0, 0);
    /// <summary>Background blue channel range 0–255.</summary>
    public ColorChannelRange BackgroundBlueRange { get; init; } = new(0, 0);

    /// <summary>Foreground red channel range 0–255.</summary>
    public ColorChannelRange ForegroundRedRange { get; init; } = new(1, 255);
    /// <summary>Foreground green channel range 0–255.</summary>
    public ColorChannelRange ForegroundGreenRange { get; init; } = new(1, 255);
    /// <summary>Foreground blue channel range 0–255.</summary>
    public ColorChannelRange ForegroundBlueRange { get; init; } = new(1, 255);

    /// <summary>If true, use only the Red channel value for all three channels.</summary>
    public bool Grayscale { get; init; }

    /// <summary>
    /// Generates an SVG identicon from the given seed string.
    /// Works on all platforms.
    /// </summary>
    /// <param name="seed">Input string (e.g. email address) that determines the identicon.</param>
    /// <param name="outputSize">Output image size in pixels (square). Default: 64.</param>
    /// <returns>SVG markup as a string.</returns>
    public string GenerateSvg(string seed, int outputSize = 64)
    {
        RenderState state = this.BuildRenderState(seed);
        int size = this.BlockCount * BlockSize;
        string foreHex = ColorToHex(state.ForeColor);
        string backHex = ColorToHex(state.BackColor);
        double[][] square = state.Shapes[1][0];

        var sb = new StringBuilder();
        _ = sb.Append($"<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 {size} {size}\" width=\"{outputSize}\" height=\"{outputSize}\">");

        if (!state.Transparent)
        {
            _ = sb.Append($"<rect width=\"{size}\" height=\"{size}\" fill=\"{backHex}\"/>");
        }

        for (int i = 0; i < this.BlockCount; i++)
        {
            for (int j = 0; j < this.BlockCount; j++)
            {
                int symIdx = this.Xy2Symmetric(i, j);
                double[][][] shape = state.Shapes[state.ShapesMat[symIdx]];
                int invert = state.InvertMat[symIdx];
                int rotation = state.RotMat[symIdx] + state.Rotations[i, j];
                var center = new PointF((float)(s_half + (BlockSize * j)), (float)(s_half + (BlockSize * i)));

                string squareFill = invert == 0 ? foreHex : backHex;
                string shapeFill = invert == 0 ? backHex : foreHex;

                // In transparent mode, skip polygons that would be painted with the transparent
                // background color. Note: for invert=0 cells this means the shape subshapes are
                // skipped (they can't punch holes through the square in SVG's compositing model);
                // the cell appears as a solid foreground square rather than a square with cutouts.
                // This is a known SVG limitation vs. the PNG output for transparent backgrounds.
                bool squareIsTransparent = state.Transparent && invert == 1;
                bool shapeIsTransparent = state.Transparent && invert == 0;

                if (!squareIsTransparent)
                {
                    _ = sb.Append(SvgPolygon(CalcXY(square, center, 0), squareFill));
                }

                if (!shapeIsTransparent)
                {
                    foreach (double[][] subshape in shape)
                    {
                        _ = sb.Append(SvgPolygon(CalcXY(subshape, center, rotation), shapeFill));
                    }
                }
            }
        }

        _ = sb.Append("</svg>");
        return sb.ToString();
    }

    /// <summary>
    /// Generates a PNG identicon image from the given seed string.
    /// Requires Windows (uses System.Drawing/GDI+).
    /// </summary>
    /// <param name="seed">Input string (e.g. email address) that determines the identicon.</param>
    /// <param name="outputSize">Output image size in pixels (square). Default: 64.</param>
    /// <returns>PNG image data as a byte array.</returns>
    [SupportedOSPlatform("windows")]
    public byte[] GeneratePng(string seed, int outputSize = 64)
    {
        RenderState state = this.BuildRenderState(seed);
        int size = this.BlockCount * BlockSize;
        double[][] square = state.Shapes[1][0];

        using var bmp = new Bitmap(size, size, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(bmp))
        {
            // SourceCopy so a transparent brush writes alpha=0, matching PHP GD behavior
            g.CompositingMode = CompositingMode.SourceCopy;
            g.Clear(state.BackColor);

            using var foreBrush = new SolidBrush(state.ForeColor);
            using var backBrush = new SolidBrush(state.BackColor);

            for (int i = 0; i < this.BlockCount; i++)
            {
                for (int j = 0; j < this.BlockCount; j++)
                {
                    int symIdx = this.Xy2Symmetric(i, j);
                    double[][][] shape = state.Shapes[state.ShapesMat[symIdx]];
                    int invert = state.InvertMat[symIdx];
                    int rotation = state.RotMat[symIdx] + state.Rotations[i, j];
                    var center = new PointF((float)(s_half + (BlockSize * j)), (float)(s_half + (BlockSize * i)));

                    // colors[1-invert] fills the background square; colors[invert] fills the shape
                    SolidBrush squareBrush = invert == 0 ? foreBrush : backBrush;
                    SolidBrush shapeBrush = invert == 0 ? backBrush : foreBrush;

                    g.FillPolygon(squareBrush, CalcXY(square, center, 0));
                    foreach (double[][] subshape in shape)
                    {
                        g.FillPolygon(shapeBrush, CalcXY(subshape, center, rotation));
                    }
                }
            }
        }

        // Scale to requested output size (matching PHP imagecopyresampled)
        using var scaled = new Bitmap(outputSize, outputSize, PixelFormat.Format32bppArgb);
        using (var gs = Graphics.FromImage(scaled))
        {
            gs.InterpolationMode = InterpolationMode.HighQualityBicubic;
            gs.DrawImage(bmp, 0, 0, outputSize, outputSize);
        }

        using var ms = new MemoryStream();
        scaled.Save(ms, ImageFormat.Png);
        return ms.ToArray();
    }

    // Runs the PRNG and builds all per-cell lookup tables shared by both rendering methods.
    private RenderState BuildRenderState(string seed)
    {
        // Match PHP: $id = substr(sha1($seed), 0, 10); hexdec($id) forced to 32-bit
        string id = ComputeSha256Hex(seed)[..10];
        ulong hexVal = Convert.ToUInt64(id, 16);
        var twister = new MersenneTwister((uint)(hexVal & 0xFFFFFFFF));

        (double[][][][] shapes, int[] rotatable) = s_shapes;

        // Collect unique symmetric indices in PHP insertion order (row-major grid scan)
        var orderedSymmetrics = new List<int>();
        var seen = new HashSet<int>();
        for (int i = 0; i < this.BlockCount; i++)
        {
            for (int j = 0; j < this.BlockCount; j++)
            {
                int sym = this.Xy2Symmetric(i, j);
                if (seen.Add(sym))
                {
                    orderedSymmetrics.Add(sym);
                }
            }
        }

        int maxIdx = orderedSymmetrics.Max();
        int[] shapesMat = new int[maxIdx + 1];
        int[] rotMat = new int[maxIdx + 1];
        int[] invertMat = new int[maxIdx + 1];

        // 4-fold symmetry rotations — constant, not PRNG-driven
        int[,] rotations = new int[this.BlockCount, this.BlockCount];
        for (int i = 0; i < this.BlockCount; i++)
        {
            for (int j = 0; j < this.BlockCount; j++)
            {
                if (Math.Floor(((this.BlockCount - 1) / 2.0) - i) >= 0 &&
                    Math.Floor(((this.BlockCount - 1) / 2.0) - j) >= 0 &&
                    (j >= i || this.BlockCount % 2 == 0))
                {
                    int inversei = this.BlockCount - 1 - i;
                    int inversej = this.BlockCount - 1 - j;
                    int[][] symmetrics = [[i, j], [inversej, i], [inversei, inversej], [j, inversei]];
                    for (int k = 0; k < 4; k++)
                    {
                        rotations[symmetrics[k][0], symmetrics[k][1]] = s_rotationFill[k];
                    }
                }
            }
        }

        // Consume PRNG in PHP insertion order: rotation, invert, shape per symmetric cell
        foreach (int key in orderedSymmetrics)
        {
            rotMat[key] = twister.Rand(0, 3) * 90;
            invertMat[key] = twister.Rand(0, 1);
            shapesMat[key] = key == 0
                ? rotatable[twister.ArrayRand(rotatable.Length)]
                : twister.ArrayRand(shapes.Length);
        }

        // Foreground color (always 3 PRNG calls regardless of transparent/grayscale)
        int fr = twister.Rand(this.ForegroundRedRange.Min, this.ForegroundRedRange.Max);
        int fg = twister.Rand(this.ForegroundGreenRange.Min, this.ForegroundGreenRange.Max);
        int fb = twister.Rand(this.ForegroundBlueRange.Min, this.ForegroundBlueRange.Max);
        if (this.Grayscale) { fg = fr; fb = fr; }

        // Background: transparent when all back ranges are [0,0]; otherwise 3 more PRNG calls
        bool transparent = this.BackgroundRedRange.Min + this.BackgroundRedRange.Max + this.BackgroundGreenRange.Min + this.BackgroundGreenRange.Max + this.BackgroundBlueRange.Min + this.BackgroundBlueRange.Max == 0;
        Color backColor;
        if (transparent)
        {
            backColor = Color.Transparent;
        }
        else
        {
            int br = twister.Rand(this.BackgroundRedRange.Min, this.BackgroundRedRange.Max);
            int bg = twister.Rand(this.BackgroundGreenRange.Min, this.BackgroundGreenRange.Max);
            int bb = twister.Rand(this.BackgroundBlueRange.Min, this.BackgroundBlueRange.Max);
            if (this.Grayscale) { bg = br; bb = br; }
            backColor = Color.FromArgb(br, bg, bb);
        }

        return new RenderState(shapes, shapesMat, rotMat, invertMat, rotations, Color.FromArgb(fr, fg, fb), backColor, transparent);
    }

    // Maps a grid cell (x=row, y=col) to its symmetric cell index.
    // Cells that are rotationally equivalent share the same index.
    private int Xy2Symmetric(int x, int y)
    {
        int[] idx =
        [
            (int)Math.Floor(Math.Abs(((this.BlockCount - 1) / 2.0) - x)),
            (int)Math.Floor(Math.Abs(((this.BlockCount - 1) / 2.0) - y))
        ];
        Array.Sort(idx);
        idx[1] *= (int)Math.Ceiling(this.BlockCount / 2.0);
        return idx[0] + idx[1];
    }

    // Converts polar-coordinate points {angle°, distance} to absolute PointF vertices.
    // Processed in reverse order to match PHP's array_pop behavior.
    private static PointF[] CalcXY(double[][] subshape, PointF center, double rotation)
    {
        var pts = new PointF[subshape.Length];
        for (int i = subshape.Length - 1; i >= 0; i--)
        {
            double rad = (subshape[i][0] + rotation) * Math.PI / 180.0;
            float px = (float)Math.Round(center.X + (Math.Cos(rad) * subshape[i][1]));
            float py = (float)Math.Round(center.Y + (Math.Sin(rad) * subshape[i][1]));
            pts[subshape.Length - 1 - i] = new PointF(px, py);
        }
        return pts;
    }

    // Builds an SVG <polygon> element. Uses fill-rule="evenodd" to match GDI+'s Alternate
    // fill mode, which correctly handles self-intersecting shapes (31: diamond C, 34: donut).
    private static string SvgPolygon(PointF[] pts, string fill)
    {
        string points = string.Join(" ", pts.Select(p => $"{p.X:G},{p.Y:G}"));
        return $"<polygon fill=\"{fill}\" fill-rule=\"evenodd\" points=\"{points}\"/>";
    }

    private static string ColorToHex(Color c) => $"#{c.R:x2}{c.G:x2}{c.B:x2}";

    private static string ComputeSha256Hex(string input)
    {
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    // All 44 shapes defined as polar coordinate arrays:
    // shapes[i] = list of subshapes; subshape = list of {angle°, distance} points.
    // Multiple subshapes per shape are overlaid as separate filled polygons.
    private static (double[][][][] shapes, int[] rotatable) BuildShapes(double half, double quarter, double diagonal, double halfdiag)
    {
        double[][][][] shapes =
        [
            // 0: rectangular half block
            [[[90, half], [135, diagonal], [225, diagonal], [270, half]]],
            // 1: full block (also used as the per-cell background square)
            [[[45, diagonal], [135, diagonal], [225, diagonal], [315, diagonal]]],
            // 2: diagonal half block
            [[[45, diagonal], [135, diagonal], [225, diagonal]]],
            // 3: triangle
            [[[90, half], [225, diagonal], [315, diagonal]]],
            // 4: diamond
            [[[0, half], [90, half], [180, half], [270, half]]],
            // 5: stretched diamond
            [[[0, half], [135, diagonal], [270, half], [315, diagonal]]],
            // 6: triple triangle
            [
                [[0, quarter], [90, half], [180, quarter]],
                [[0, quarter], [315, diagonal], [270, half]],
                [[270, half], [180, quarter], [225, diagonal]]
            ],
            // 7: pointer
            [[[0, half], [135, diagonal], [270, half]]],
            // 8: center square
            [[[45, halfdiag], [135, halfdiag], [225, halfdiag], [315, halfdiag]]],
            // 9: double triangle diagonal
            [
                [[180, half], [225, diagonal], [0, 0]],
                [[45, diagonal], [90, half], [0, 0]]
            ],
            // 10: diagonal square
            [[[90, half], [135, diagonal], [180, half], [0, 0]]],
            // 11: quarter triangle out
            [[[0, half], [180, half], [270, half]]],
            // 12: quarter triangle in
            [[[315, diagonal], [225, diagonal], [0, 0]]],
            // 13: eighth triangle in
            [[[90, half], [180, half], [0, 0]]],
            // 14: eighth triangle out
            [[[90, half], [135, diagonal], [180, half]]],
            // 15: double corner square
            [
                [[90, half], [135, diagonal], [180, half], [0, 0]],
                [[0, half], [315, diagonal], [270, half], [0, 0]]
            ],
            // 16: double quarter triangle in
            [
                [[315, diagonal], [225, diagonal], [0, 0]],
                [[45, diagonal], [135, diagonal], [0, 0]]
            ],
            // 17: tall quarter triangle
            [[[90, half], [135, diagonal], [225, diagonal]]],
            // 18: double tall quarter triangle
            [
                [[90, half], [135, diagonal], [225, diagonal]],
                [[45, diagonal], [90, half], [270, half]]
            ],
            // 19: tall quarter + eighth triangles
            [
                [[90, half], [135, diagonal], [225, diagonal]],
                [[45, diagonal], [90, half], [0, 0]]
            ],
            // 20: tipped over tall triangle
            [[[135, diagonal], [270, half], [315, diagonal]]],
            // 21: triple triangle diagonal
            [
                [[180, half], [225, diagonal], [0, 0]],
                [[45, diagonal], [90, half], [0, 0]],
                [[0, half], [0, 0], [270, half]]
            ],
            // 22: double triangle flat
            [
                [[0, quarter], [315, diagonal], [270, half]],
                [[270, half], [180, quarter], [225, diagonal]]
            ],
            // 23: opposite 8th triangles
            [
                [[0, quarter], [45, diagonal], [315, diagonal]],
                [[180, quarter], [135, diagonal], [225, diagonal]]
            ],
            // 24: opposite 8th triangles + diamond
            [
                [[0, quarter], [45, diagonal], [315, diagonal]],
                [[180, quarter], [135, diagonal], [225, diagonal]],
                [[180, quarter], [90, half], [0, quarter], [270, half]]
            ],
            // 25: small diamond
            [[[0, quarter], [90, quarter], [180, quarter], [270, quarter]]],
            // 26: 4 opposite 8th triangles
            [
                [[0, quarter], [45, diagonal], [315, diagonal]],
                [[180, quarter], [135, diagonal], [225, diagonal]],
                [[270, quarter], [225, diagonal], [315, diagonal]],
                [[90, quarter], [135, diagonal], [45, diagonal]]
            ],
            // 27: double quarter triangle parallel
            [
                [[315, diagonal], [225, diagonal], [0, 0]],
                [[0, half], [90, half], [180, half]]
            ],
            // 28: double overlapping tipped over tall triangle
            [
                [[135, diagonal], [270, half], [315, diagonal]],
                [[225, diagonal], [90, half], [45, diagonal]]
            ],
            // 29: opposite double tall quarter triangle
            [
                [[90, half], [135, diagonal], [225, diagonal]],
                [[315, diagonal], [45, diagonal], [270, half]]
            ],
            // 30: 4 opposite 8th triangles + tiny diamond
            [
                [[0, quarter], [45, diagonal], [315, diagonal]],
                [[180, quarter], [135, diagonal], [225, diagonal]],
                [[270, quarter], [225, diagonal], [315, diagonal]],
                [[90, quarter], [135, diagonal], [45, diagonal]],
                [[0, quarter], [90, quarter], [180, quarter], [270, quarter]]
            ],
            // 31: diamond C (self-intersecting — fill-rule="evenodd" required)
            [[[0, half], [90, half], [180, half], [270, half], [270, quarter], [180, quarter], [90, quarter], [0, quarter]]],
            // 32: narrow diamond
            [[[0, quarter], [90, half], [180, quarter], [270, half]]],
            // 33: quadruple triangle diagonal
            [
                [[180, half], [225, diagonal], [0, 0]],
                [[45, diagonal], [90, half], [0, 0]],
                [[0, half], [0, 0], [270, half]],
                [[90, half], [135, diagonal], [180, half]]
            ],
            // 34: diamond donut (self-intersecting — fill-rule="evenodd" required)
            [[[0, half], [90, half], [180, half], [270, half], [0, half], [0, quarter], [270, quarter], [180, quarter], [90, quarter], [0, quarter]]],
            // 35: triple turning triangle
            [
                [[90, half], [45, diagonal], [0, quarter]],
                [[0, half], [315, diagonal], [270, quarter]],
                [[270, half], [225, diagonal], [180, quarter]]
            ],
            // 36: double turning triangle
            [
                [[90, half], [45, diagonal], [0, quarter]],
                [[0, half], [315, diagonal], [270, quarter]]
            ],
            // 37: diagonal opposite inward double triangle
            [
                [[90, half], [45, diagonal], [0, quarter]],
                [[270, half], [225, diagonal], [180, quarter]]
            ],
            // 38: star fleet
            [[[90, half], [225, diagonal], [0, 0], [315, diagonal]]],
            // 39: hollow half triangle
            [[[90, half], [225, diagonal], [0, 0], [315, halfdiag], [225, halfdiag], [225, diagonal], [315, diagonal]]],
            // 40: double eighth triangle out
            [
                [[90, half], [135, diagonal], [180, half]],
                [[270, half], [315, diagonal], [0, half]]
            ],
            // 41: double slanted square
            [
                [[90, half], [135, diagonal], [180, half], [180, quarter]],
                [[270, half], [315, diagonal], [0, half], [0, quarter]]
            ],
            // 42: double diamond
            [
                [[0, half], [45, halfdiag], [0, 0], [315, halfdiag]],
                [[180, half], [135, halfdiag], [0, 0], [225, halfdiag]]
            ],
            // 43: double pointer
            [
                [[0, half], [45, diagonal], [0, 0], [315, halfdiag]],
                [[180, half], [135, halfdiag], [0, 0], [225, diagonal]]
            ],
        ];

        // Indices of shapes suitable for the center cell (visually symmetric under 90° rotation)
        int[] rotatable = [1, 4, 8, 25, 26, 30, 34];

        return (shapes, rotatable);
    }

    /// <summary>
    /// Represents an inclusive [min, max] range for a single color channel (0–255).
    /// </summary>
    public readonly record struct ColorChannelRange(int Min, int Max);

    private record RenderState(
        double[][][][] Shapes,
        int[] ShapesMat,
        int[] RotMat,
        int[] InvertMat,
        int[,] Rotations,
        Color ForeColor,
        Color BackColor,
        bool Transparent);

    // Standard MT19937 Mersenne Twister using uint arithmetic.
    // Matches the PHP identicon_mersenne_twister implementation which works around
    // PHP's lack of native unsigned 32-bit integers.
    private sealed class MersenneTwister
    {
        private const int N = 624;
        private const int M = 397;
        private const uint MatrixA = 0x9908B0DFu;
        private const uint UpperMask = 0x80000000u;
        private const uint LowerMask = 0x7FFFFFFFu;

        private readonly uint[] _mt = new uint[N];
        private int _mti;

        public MersenneTwister(uint seed)
        {
            this._mt[0] = seed;
            for (this._mti = 1; this._mti < N; this._mti++)
            {
                this._mt[this._mti] = (1812433253u * (this._mt[this._mti - 1] ^ (this._mt[this._mti - 1] >> 30))) + (uint)this._mti;
            }
        }

        private uint NextUInt32()
        {
            if (this._mti >= N)
            {
                int kk;
                for (kk = 0; kk < N - M; kk++)
                {
                    uint y = (this._mt[kk] & UpperMask) | (this._mt[kk + 1] & LowerMask);
                    this._mt[kk] = this._mt[kk + M] ^ (y >> 1) ^ ((y & 1u) * MatrixA);
                }
                for (; kk < N - 1; kk++)
                {
                    uint y = (this._mt[kk] & UpperMask) | (this._mt[kk + 1] & LowerMask);
                    this._mt[kk] = this._mt[kk + (M - N)] ^ (y >> 1) ^ ((y & 1u) * MatrixA);
                }
                {
                    uint y = (this._mt[N - 1] & UpperMask) | (this._mt[0] & LowerMask);
                    this._mt[N - 1] = this._mt[M - 1] ^ (y >> 1) ^ ((y & 1u) * MatrixA);
                }
                this._mti = 0;
            }

            uint r = this._mt[this._mti++];
            // Tempering
            r ^= r >> 11;
            r ^= (r << 7) & 0x9D2C5680u;
            r ^= (r << 15) & 0xEFC60000u;
            r ^= r >> 18;
            return r;
        }

        // Returns a double in [0, 1) — same as PHP real_halfopen()
        private double RealHalfOpen() => this.NextUInt32() * (1.0 / 4294967296.0);

        // Returns an integer in [low, high] inclusive — same as PHP rand()
        public int Rand(int low, int high) =>
            (int)Math.Floor(low + ((high - low + 1) * this.RealHalfOpen()));

        // Returns a random index into an array of the given count — same as PHP array_rand()
        public int ArrayRand(int count) => this.Rand(0, count - 1);
    }
}
