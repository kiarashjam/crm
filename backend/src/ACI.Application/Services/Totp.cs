using System.Security.Cryptography;

namespace ACI.Application.Services;

/// <summary>
/// Minimal RFC 6238 TOTP implementation (SHA1, 30s step, 6 digits).
/// </summary>
internal static class Totp
{
    public static string GenerateBase32Secret(int bytes = 20)
    {
        var data = RandomNumberGenerator.GetBytes(bytes);
        return Base32.Encode(data);
    }

    public static bool VerifyCode(string base32Secret, string code, DateTime utcNow, int stepSeconds = 30, int digits = 6, int allowedDriftSteps = 1)
    {
        code = NormalizeCode(code);
        if (code.Length != digits || !code.All(char.IsDigit))
            return false;

        var secret = Base32.Decode(base32Secret);
        var counter = GetCurrentCounter(utcNow, stepSeconds);

        for (var drift = -allowedDriftSteps; drift <= allowedDriftSteps; drift++)
        {
            var candidate = GenerateCode(secret, counter + drift, digits);
            if (ConstantTimeEquals(candidate, code))
                return true;
        }

        return false;
    }

    private static long GetCurrentCounter(DateTime utcNow, int stepSeconds)
    {
        var unix = (long)Math.Floor((utcNow - DateTime.UnixEpoch).TotalSeconds);
        return unix / stepSeconds;
    }

    private static string GenerateCode(byte[] secret, long counter, int digits)
    {
        Span<byte> counterBytes = stackalloc byte[8];
        WriteBigEndian(counterBytes, counter);

        using var hmac = new HMACSHA1(secret);
        var hash = hmac.ComputeHash(counterBytes.ToArray());

        var offset = hash[^1] & 0x0F;
        var binary =
            ((hash[offset] & 0x7f) << 24) |
            ((hash[offset + 1] & 0xff) << 16) |
            ((hash[offset + 2] & 0xff) << 8) |
            (hash[offset + 3] & 0xff);

        var otp = binary % (int)Math.Pow(10, digits);
        return otp.ToString(new string('0', digits));
    }

    private static void WriteBigEndian(Span<byte> buffer, long value)
    {
        for (var i = 7; i >= 0; i--)
        {
            buffer[i] = (byte)(value & 0xFF);
            value >>= 8;
        }
    }

    private static bool ConstantTimeEquals(string a, string b)
    {
        if (a.Length != b.Length) return false;
        var diff = 0;
        for (var i = 0; i < a.Length; i++)
            diff |= a[i] ^ b[i];
        return diff == 0;
    }

    private static string NormalizeCode(string code) =>
        new string(code.Where(char.IsDigit).ToArray());

    private static class Base32
    {
        private const string Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";

        public static string Encode(byte[] data)
        {
            if (data.Length == 0) return string.Empty;

            var output = new List<char>((data.Length + 4) / 5 * 8);
            var bitBuffer = 0;
            var bitsInBuffer = 0;

            foreach (var b in data)
            {
                bitBuffer = (bitBuffer << 8) | b;
                bitsInBuffer += 8;
                while (bitsInBuffer >= 5)
                {
                    bitsInBuffer -= 5;
                    var index = (bitBuffer >> bitsInBuffer) & 0x1F;
                    output.Add(Alphabet[index]);
                }
            }

            if (bitsInBuffer > 0)
            {
                var index = (bitBuffer << (5 - bitsInBuffer)) & 0x1F;
                output.Add(Alphabet[index]);
            }

            return new string(output.ToArray());
        }

        public static byte[] Decode(string base32)
        {
            if (string.IsNullOrWhiteSpace(base32)) return Array.Empty<byte>();
            var cleaned = new string(base32.Trim().ToUpperInvariant().Where(c => Alphabet.Contains(c)).ToArray());
            var output = new List<byte>(cleaned.Length * 5 / 8);

            var bitBuffer = 0;
            var bitsInBuffer = 0;
            foreach (var c in cleaned)
            {
                var val = Alphabet.IndexOf(c);
                if (val < 0) continue;
                bitBuffer = (bitBuffer << 5) | val;
                bitsInBuffer += 5;
                if (bitsInBuffer >= 8)
                {
                    bitsInBuffer -= 8;
                    output.Add((byte)((bitBuffer >> bitsInBuffer) & 0xFF));
                }
            }
            return output.ToArray();
        }
    }
}

