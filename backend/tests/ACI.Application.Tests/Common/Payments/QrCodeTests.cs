using System.Security.Cryptography;
using System.Text;
using ACI.Application.Common.Payments;

namespace ACI.Application.Tests.Common.Payments;

/// <summary>
/// The QR code encoder behind the Swiss QR Code (byte mode, error correction level M).
/// </summary>
/// <remarks>
/// The known answers are SHA-256 hashes of the module rows ('1' dark, '0' light, one line per
/// row) that the Python "qrcode" package (8.2) draws for the same data, version and mask. Each
/// case was also checked by reading the code back with the ZXing decoder. A mask is forced in
/// each, because encoders score the eight masks with slightly different penalty rules, and
/// any mask gives a valid code.
/// </remarks>
public class QrCodeTests
{
    private const string BillPayload =
        "SPC\n0200\n1\nCH4431999123000889012\nS\nRobert Schneider AG\nRue du Lac\n1268\n2501\nBiel\nCH\n\n\n\n\n\n\n\n" +
        "1949.75\nCHF\nS\nPia-Maria Rutschmann-Schnyder\nGrosse Marktgasse\n28\n9400\nRorschach\nCH\nQRR\n" +
        "210000000003139471430009017\nOrder dated 18.06.2020\nEPD";

    public static IEnumerable<object[]> KnownAnswers()
    {
        yield return [Encoding.ASCII.GetBytes("Cadence"), 1, 0, "fbaa0f2d4aebdc308ee50d0df9eb66865922a3db0cac68a367fbf52120947a21"];
        yield return [Enumerable.Range(0, 150).Select(i => (byte)i).ToArray(), 8, 3, "65204b099aeeee40478645d58074dc13b93eb9a87871abff6b5fd2374be6e0fe"];
        yield return [Encoding.UTF8.GetBytes(BillPayload), 11, 2, "840ed859b76d0934a461130ddd5d4bc6f9b42405f8b3bfa1e478e4991b38b065"];
        yield return [Enumerable.Range(0, 1100).Select(i => (byte)(i % 251)).ToArray(), 27, 5, "bcd0cba6f29024e39d60e3d2e89bc46137d795fd45df739402a3f844b30feba7"];
    }

    [Theory]
    [MemberData(nameof(KnownAnswers))]
    public void TheModulesAreThoseAnotherEncoderDraws(byte[] data, int version, int mask, string sha256)
    {
        var qr = QrCode.EncodeBytes(data, mask: mask);

        qr.Version.Should().Be(version, "the smallest version that holds the data at level M");
        qr.Size.Should().Be(version * 4 + 17);
        Hash(qr).Should().Be(sha256);
    }

    [Fact]
    public void TheBestMaskIsChosenWhenNoneIsGiven()
    {
        var qr = QrCode.EncodeBytes(Encoding.UTF8.GetBytes(BillPayload));

        qr.Mask.Should().BeInRange(0, 7);
        Hash(qr).Should().Be(Hash(QrCode.EncodeBytes(Encoding.UTF8.GetBytes(BillPayload), mask: qr.Mask)),
            "the chosen mask's code is the one drawn with that mask");
    }

    [Fact]
    public void TheFinderPatternsAndTimingAreInPlace()
    {
        var qr = QrCode.EncodeBytes(Encoding.ASCII.GetBytes("Cadence"));
        var last = qr.Size - 1;
        foreach (var (row, column) in new[] { (0, 0), (0, last - 6), (last - 6, 0) })
        {
            for (var i = 0; i < 7; i++)
            {
                qr.IsDark(row, column + i).Should().BeTrue("a finder's outer ring is dark");
                qr.IsDark(row + 6, column + i).Should().BeTrue();
            }
            qr.IsDark(row + 1, column + 1).Should().BeFalse("inside the ring is a light one");
            qr.IsDark(row + 3, column + 3).Should().BeTrue("and the dark centre");
        }
        for (var i = 8; i < qr.Size - 8; i++) qr.IsDark(6, i).Should().Be(i % 2 == 0, "the timing pattern alternates");
        qr.IsDark(qr.Size - 8, 8).Should().BeTrue("the dark module beside the bottom-left finder is always dark");
    }

    [Theory]
    [InlineData(1, new int[0])]
    [InlineData(2, new[] { 6, 18 })]
    [InlineData(7, new[] { 6, 22, 38 })]
    [InlineData(10, new[] { 6, 28, 50 })]
    [InlineData(14, new[] { 6, 26, 46, 66 })]
    [InlineData(25, new[] { 6, 32, 58, 84, 110 })]
    [InlineData(32, new[] { 6, 34, 60, 86, 112, 138 })]
    public void AlignmentPatternsSitWhereTheStandardsTableSays(int version, int[] positions)
    {
        QrCode.AlignmentPatternPositions(version).Should().Equal(positions);
    }

    [Fact]
    public void DataTooLongForTheLargestVersionIsRefused()
    {
        var act = () => QrCode.EncodeBytes(new byte[1500], maxVersion: 25);
        act.Should().Throw<ArgumentException>("the Swiss QR Code is version 25 at most");
        QrCode.DataCodewords(25).Should().Be(1000, "version 25 at level M holds 1,000 data codewords: 997 bytes, the payload's limit");
        QrCode.EncodeBytes(new byte[SwissQrBill.MaxPayloadLength], maxVersion: 25).Version.Should().Be(25);
    }

    private static string Hash(QrCode qr)
    {
        var rows = new StringBuilder();
        for (var r = 0; r < qr.Size; r++)
        {
            if (r > 0) rows.Append('\n');
            for (var c = 0; c < qr.Size; c++) rows.Append(qr.IsDark(r, c) ? '1' : '0');
        }
        return Convert.ToHexString(SHA256.HashData(Encoding.ASCII.GetBytes(rows.ToString()))).ToLowerInvariant();
    }
}
