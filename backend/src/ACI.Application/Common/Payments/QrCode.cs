namespace ACI.Application.Common.Payments;

/// <summary>
/// A QR code (ISO/IEC 18004) for bytes, at error correction level M: what the Swiss QR Code on
/// a QR-bill is (UTF-8 in byte mode, level M, version 25 at most).
/// </summary>
/// <remarks>
/// <para>
/// Written here rather than taken from a package because it is small, the QR-bill needs only
/// this one case, and the PDF it is drawn into is built by hand too. It follows the reference
/// steps of Project Nayuki's QR Code generator (MIT licence): the data as one byte segment,
/// padded to the version's capacity, split into Reed-Solomon blocks over GF(2^8) and
/// interleaved, then placed in the zig-zag around the function patterns and masked with the
/// one of the eight masks the standard's penalty rules score lowest.
/// </para>
/// <para>
/// <see cref="Modules"/> is [row, column], true for a dark module, without the quiet zone.
/// </para>
/// </remarks>
public sealed class QrCode
{
    // Error correction codewords per block, and blocks, for level M by version (index 0 unused).
    private static readonly int[] EccCodewordsPerBlock =
    [
        -1, 10, 16, 26, 18, 24, 16, 18, 22, 22, 26, 30, 22, 22, 24, 24, 28, 28, 26, 26, 26,
        26, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28,
    ];

    private static readonly int[] ErrorCorrectionBlocks =
    [
        -1, 1, 1, 1, 2, 2, 4, 4, 4, 5, 5, 5, 8, 9, 9, 10, 10, 11, 13, 14, 16,
        17, 17, 18, 20, 21, 23, 25, 26, 28, 29, 31, 33, 35, 37, 38, 40, 43, 45, 47, 49,
    ];

    /// <summary>Level M's two format bits (L 01, M 00, Q 11, H 10).</summary>
    private const int FormatBitsM = 0;

    private const int PenaltyN1 = 3;
    private const int PenaltyN2 = 3;
    private const int PenaltyN3 = 40;
    private const int PenaltyN4 = 10;

    private readonly bool[,] _modules;
    private readonly bool[,] _isFunction;

    private QrCode(int version, byte[] dataCodewords, int? forcedMask)
    {
        Version = version;
        Size = version * 4 + 17;
        _modules = new bool[Size, Size];
        _isFunction = new bool[Size, Size];

        DrawFunctionPatterns();
        var allCodewords = AddEccAndInterleave(dataCodewords);
        DrawCodewords(allCodewords);

        var mask = forcedMask ?? -1;
        if (mask < 0)
        {
            var minPenalty = int.MaxValue;
            for (var i = 0; i < 8; i++)
            {
                ApplyMask(i);
                DrawFormatBits(i);
                var penalty = PenaltyScore();
                if (penalty < minPenalty)
                {
                    mask = i;
                    minPenalty = penalty;
                }
                ApplyMask(i); // masks are an XOR: a second pass undoes the first
            }
        }
        Mask = mask;
        ApplyMask(mask);
        DrawFormatBits(mask);
    }

    /// <summary>1 to 40: the code is 4 × version + 17 modules on a side.</summary>
    public int Version { get; }

    public int Size { get; }

    /// <summary>The mask pattern used, 0 to 7.</summary>
    public int Mask { get; }

    /// <summary>[row, column], true where the module is dark.</summary>
    public bool[,] Modules => (bool[,])_modules.Clone();

    public bool IsDark(int row, int column) => _modules[row, column];

    /// <summary>
    /// The smallest QR code at level M from <paramref name="minVersion"/> to <paramref name="maxVersion"/>
    /// that holds <paramref name="data"/> as one byte segment.
    /// </summary>
    /// <param name="mask">A mask to use instead of the best-scoring one; tests compare with other encoders this way.</param>
    /// <exception cref="ArgumentException">The data does not fit in <paramref name="maxVersion"/>.</exception>
    public static QrCode EncodeBytes(byte[] data, int minVersion = 1, int maxVersion = 40, int? mask = null)
    {
        ArgumentNullException.ThrowIfNull(data);
        if (minVersion < 1 || maxVersion > 40 || minVersion > maxVersion) throw new ArgumentOutOfRangeException(nameof(minVersion));
        if (mask is < 0 or > 7) throw new ArgumentOutOfRangeException(nameof(mask));

        int version;
        for (version = minVersion; ; version++)
        {
            var capacityBits = DataCodewords(version) * 8;
            var usedBits = 4 + CountBits(version) + data.Length * 8;
            if (usedBits <= capacityBits) break;
            if (version >= maxVersion) throw new ArgumentException($"The data ({data.Length} bytes) does not fit in a version {maxVersion} QR code at level M.", nameof(data));
        }

        var bits = new List<bool>();
        AppendBits(bits, 0x4, 4); // byte mode
        AppendBits(bits, data.Length, CountBits(version));
        foreach (var b in data) AppendBits(bits, b, 8);

        var capacity = DataCodewords(version) * 8;
        AppendBits(bits, 0, Math.Min(4, capacity - bits.Count)); // terminator
        AppendBits(bits, 0, (8 - bits.Count % 8) % 8);             // to a byte boundary
        for (var pad = 0xEC; bits.Count < capacity; pad ^= 0xEC ^ 0x11) AppendBits(bits, pad, 8);

        var codewords = new byte[bits.Count / 8];
        for (var i = 0; i < bits.Count; i++)
            if (bits[i]) codewords[i >> 3] |= (byte)(1 << (7 - (i & 7)));
        return new QrCode(version, codewords, mask);
    }

    /// <summary>The bits of a byte segment's character count: 8 up to version 9, 16 after.</summary>
    private static int CountBits(int version) => version <= 9 ? 8 : 16;

    private static void AppendBits(List<bool> bits, int value, int length)
    {
        for (var i = length - 1; i >= 0; i--) bits.Add(((value >> i) & 1) != 0);
    }

    // ── function patterns ────────────────────────────────────────────────────

    private void DrawFunctionPatterns()
    {
        for (var i = 0; i < Size; i++)
        {
            SetFunction(6, i, i % 2 == 0);
            SetFunction(i, 6, i % 2 == 0);
        }
        DrawFinderPattern(3, 3);
        DrawFinderPattern(Size - 4, 3);
        DrawFinderPattern(3, Size - 4);

        var positions = AlignmentPatternPositions(Version);
        var count = positions.Length;
        for (var i = 0; i < count; i++)
            for (var j = 0; j < count; j++)
                // Not over the three finder patterns.
                if (!(i == 0 && j == 0 || i == 0 && j == count - 1 || i == count - 1 && j == 0))
                    DrawAlignmentPattern(positions[i], positions[j]);

        DrawFormatBits(0); // reserves the area; the real bits come with the mask
        DrawVersion();
    }

    /// <summary>The rows and columns alignment patterns are centred on, for a version.</summary>
    public static int[] AlignmentPatternPositions(int version)
    {
        if (version == 1) return [];
        var count = version / 7 + 2;
        var step = (version * 8 + count * 3 + 5) / (count * 4 - 4) * 2;
        var result = new int[count];
        result[0] = 6;
        for (int i = count - 1, position = version * 4 + 17 - 7; i >= 1; i--, position -= step) result[i] = position;
        return result;
    }

    private void DrawFormatBits(int mask)
    {
        var data = FormatBitsM << 3 | mask;
        var remainder = data;
        for (var i = 0; i < 10; i++) remainder = (remainder << 1) ^ ((remainder >> 9) * 0x537);
        var bits = (data << 10 | remainder) ^ 0x5412;

        // The first copy, around the top-left finder.
        for (var i = 0; i <= 5; i++) SetFunction(8, i, Bit(bits, i));
        SetFunction(8, 7, Bit(bits, 6));
        SetFunction(8, 8, Bit(bits, 7));
        SetFunction(7, 8, Bit(bits, 8));
        for (var i = 9; i < 15; i++) SetFunction(14 - i, 8, Bit(bits, i));

        // The second copy, split between the other two finders.
        for (var i = 0; i < 8; i++) SetFunction(Size - 1 - i, 8, Bit(bits, i));
        for (var i = 8; i < 15; i++) SetFunction(8, Size - 15 + i, Bit(bits, i));
        SetFunction(8, Size - 8, true); // the dark module
    }

    private void DrawVersion()
    {
        if (Version < 7) return;
        var remainder = Version;
        for (var i = 0; i < 12; i++) remainder = (remainder << 1) ^ ((remainder >> 11) * 0x1F25);
        var bits = Version << 12 | remainder;
        for (var i = 0; i < 18; i++)
        {
            var bit = Bit(bits, i);
            var a = Size - 11 + i % 3;
            var b = i / 3;
            SetFunction(a, b, bit);
            SetFunction(b, a, bit);
        }
    }

    private void DrawFinderPattern(int x, int y)
    {
        for (var dy = -4; dy <= 4; dy++)
            for (var dx = -4; dx <= 4; dx++)
            {
                var distance = Math.Max(Math.Abs(dx), Math.Abs(dy));
                var xx = x + dx;
                var yy = y + dy;
                if (xx >= 0 && xx < Size && yy >= 0 && yy < Size) SetFunction(xx, yy, distance != 2 && distance != 4);
            }
    }

    private void DrawAlignmentPattern(int x, int y)
    {
        for (var dy = -2; dy <= 2; dy++)
            for (var dx = -2; dx <= 2; dx++)
                SetFunction(x + dx, y + dy, Math.Max(Math.Abs(dx), Math.Abs(dy)) != 1);
    }

    private void SetFunction(int x, int y, bool dark)
    {
        _modules[y, x] = dark;
        _isFunction[y, x] = true;
    }

    // ── data ─────────────────────────────────────────────────────────────────

    private byte[] AddEccAndInterleave(byte[] data)
    {
        var blocks = ErrorCorrectionBlocks[Version];
        var eccLength = EccCodewordsPerBlock[Version];
        var rawCodewords = RawDataModules(Version) / 8;
        var shortBlocks = blocks - rawCodewords % blocks;
        var shortBlockLength = rawCodewords / blocks;

        var divisor = ReedSolomonDivisor(eccLength);
        var split = new byte[blocks][];
        for (int i = 0, k = 0; i < blocks; i++)
        {
            var length = shortBlockLength - eccLength + (i < shortBlocks ? 0 : 1);
            var dat = data.AsSpan(k, length).ToArray();
            k += length;
            var block = new byte[shortBlockLength + 1];
            dat.CopyTo(block, 0);
            var ecc = ReedSolomonRemainder(dat, divisor);
            ecc.CopyTo(block, block.Length - eccLength);
            split[i] = block;
        }

        var result = new byte[rawCodewords];
        for (int i = 0, k = 0; i < split[0].Length; i++)
            for (var j = 0; j < split.Length; j++)
                // A short block has one data codeword fewer; its gap is skipped.
                if (i != shortBlockLength - eccLength || j >= shortBlocks) result[k++] = split[j][i];
        return result;
    }

    private void DrawCodewords(byte[] data)
    {
        var i = 0;
        for (var right = Size - 1; right >= 1; right -= 2)
        {
            if (right == 6) right = 5; // the vertical timing pattern
            for (var vertical = 0; vertical < Size; vertical++)
                for (var j = 0; j < 2; j++)
                {
                    var x = right - j;
                    var upward = ((right + 1) & 2) == 0;
                    var y = upward ? Size - 1 - vertical : vertical;
                    if (!_isFunction[y, x] && i < data.Length * 8)
                    {
                        _modules[y, x] = Bit(data[i >> 3], 7 - (i & 7));
                        i++;
                    }
                }
        }
    }

    private void ApplyMask(int mask)
    {
        for (var y = 0; y < Size; y++)
            for (var x = 0; x < Size; x++)
            {
                var invert = mask switch
                {
                    0 => (x + y) % 2 == 0,
                    1 => y % 2 == 0,
                    2 => x % 3 == 0,
                    3 => (x + y) % 3 == 0,
                    4 => (x / 3 + y / 2) % 2 == 0,
                    5 => x * y % 2 + x * y % 3 == 0,
                    6 => (x * y % 2 + x * y % 3) % 2 == 0,
                    7 => ((x + y) % 2 + x * y % 3) % 2 == 0,
                    _ => throw new ArgumentOutOfRangeException(nameof(mask)),
                };
                if (!_isFunction[y, x] && invert) _modules[y, x] = !_modules[y, x];
            }
    }

    // ── the mask's penalty ───────────────────────────────────────────────────

    private int PenaltyScore()
    {
        var result = 0;
        var history = new int[7];

        // Runs of one colour in a row or a column, and patterns that look like a finder.
        for (var pass = 0; pass < 2; pass++)
            for (var a = 0; a < Size; a++)
            {
                var runColour = false;
                var run = 0;
                Array.Clear(history);
                for (var b = 0; b < Size; b++)
                {
                    var colour = pass == 0 ? _modules[a, b] : _modules[b, a];
                    if (colour == runColour)
                    {
                        run++;
                        if (run == 5) result += PenaltyN1;
                        else if (run > 5) result++;
                    }
                    else
                    {
                        AddHistory(run, history);
                        if (!runColour) result += CountFinderPatterns(history) * PenaltyN3;
                        runColour = colour;
                        run = 1;
                    }
                }
                result += TerminateAndCount(runColour, run, history) * PenaltyN3;
            }

        // Two-by-two blocks of one colour.
        for (var y = 0; y < Size - 1; y++)
            for (var x = 0; x < Size - 1; x++)
            {
                var colour = _modules[y, x];
                if (colour == _modules[y, x + 1] && colour == _modules[y + 1, x] && colour == _modules[y + 1, x + 1]) result += PenaltyN2;
            }

        // The balance of dark and light.
        var dark = 0;
        foreach (var module in _modules) if (module) dark++;
        var total = Size * Size;
        var k = (Math.Abs(dark * 20 - total * 10) + total - 1) / total - 1;
        result += k * PenaltyN4;
        return result;
    }

    /// <summary>0, 1 or 2: called just after a light run is added to the history.</summary>
    private int CountFinderPatterns(int[] history)
    {
        var n = history[1];
        var core = n > 0 && history[2] == n && history[3] == n * 3 && history[4] == n && history[5] == n;
        return (core && history[0] >= n * 4 && history[6] >= n ? 1 : 0)
             + (core && history[6] >= n * 4 && history[0] >= n ? 1 : 0);
    }

    private int TerminateAndCount(bool runColour, int run, int[] history)
    {
        if (runColour)
        {
            AddHistory(run, history);
            run = 0;
        }
        run += Size; // the light border past the end
        AddHistory(run, history);
        return CountFinderPatterns(history);
    }

    private void AddHistory(int run, int[] history)
    {
        if (history[0] == 0) run += Size; // the light border before the start
        Array.Copy(history, 0, history, 1, history.Length - 1);
        history[0] = run;
    }

    // ── sizes and Reed-Solomon ───────────────────────────────────────────────

    /// <summary>Modules left for data and error correction once the function patterns are drawn.</summary>
    private static int RawDataModules(int version)
    {
        var result = (16 * version + 128) * version + 64;
        if (version >= 2)
        {
            var alignments = version / 7 + 2;
            result -= (25 * alignments - 10) * alignments - 55;
            if (version >= 7) result -= 36;
        }
        return result;
    }

    /// <summary>Data codewords a version holds at level M.</summary>
    public static int DataCodewords(int version) =>
        RawDataModules(version) / 8 - EccCodewordsPerBlock[version] * ErrorCorrectionBlocks[version];

    private static byte[] ReedSolomonDivisor(int degree)
    {
        var result = new byte[degree];
        result[degree - 1] = 1;
        var root = 1;
        for (var i = 0; i < degree; i++)
        {
            for (var j = 0; j < result.Length; j++)
            {
                result[j] = (byte)Multiply(result[j], root);
                if (j + 1 < result.Length) result[j] ^= result[j + 1];
            }
            root = Multiply(root, 0x02);
        }
        return result;
    }

    private static byte[] ReedSolomonRemainder(byte[] data, byte[] divisor)
    {
        var result = new byte[divisor.Length];
        foreach (var b in data)
        {
            var factor = b ^ result[0];
            Array.Copy(result, 1, result, 0, result.Length - 1);
            result[^1] = 0;
            for (var i = 0; i < result.Length; i++) result[i] ^= (byte)Multiply(divisor[i], factor);
        }
        return result;
    }

    /// <summary>The product in GF(2^8) modulo x^8 + x^4 + x^3 + x^2 + 1.</summary>
    private static int Multiply(int x, int y)
    {
        var z = 0;
        for (var i = 7; i >= 0; i--)
        {
            z = (z << 1) ^ ((z >> 7) * 0x11D);
            z ^= ((y >> i) & 1) * x;
        }
        return z;
    }

    private static bool Bit(int value, int index) => ((value >> index) & 1) != 0;
}
