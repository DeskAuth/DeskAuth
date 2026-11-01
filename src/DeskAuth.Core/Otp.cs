// DeskAuth, based on WinAuth. Copyright (C) 2011 Colin Mackie and contributors.
// Licensed under the GNU General Public License v3 or later. See LICENSE.

using System.Buffers.Binary;
using System.Security.Cryptography;

namespace DeskAuth.Core;

/// <summary>
/// HMAC algorithm used to generate codes. Names match the values stored in config files.
/// </summary>
public enum OtpAlgorithm
{
    SHA1 = 0,
    SHA256 = 1,
    SHA512 = 2,
}

/// <summary>
/// Base for RFC 4226 (HOTP) and RFC 6238 (TOTP) one-time password generators.
/// </summary>
public abstract class OtpGenerator
{
    public const int DefaultDigits = 6;
    public const int DefaultPeriod = 30;

    protected OtpGenerator(byte[] secretKey)
    {
        SecretKey = secretKey;
    }

    public byte[] SecretKey { get; set; }

    public int Digits { get; set; } = DefaultDigits;

    public OtpAlgorithm Algorithm { get; set; } = OtpAlgorithm.SHA1;

    /// <summary>
    /// Code lifetime in seconds (TOTP only, but stored for both types)
    /// </summary>
    public int Period { get; set; } = DefaultPeriod;

    /// <summary>
    /// Compute the RFC 4226 code for a moving factor
    /// </summary>
    public string ComputeCode(long movingFactor)
    {
        Span<byte> message = stackalloc byte[8];
        BinaryPrimitives.WriteInt64BigEndian(message, movingFactor);

        byte[] mac = Algorithm switch
        {
            OtpAlgorithm.SHA1 => HMACSHA1.HashData(SecretKey, message),
            OtpAlgorithm.SHA256 => HMACSHA256.HashData(SecretKey, message),
            OtpAlgorithm.SHA512 => HMACSHA512.HashData(SecretKey, message),
            _ => throw new InvalidOperationException($"Unknown algorithm {Algorithm}"),
        };

        // dynamic truncation: the low 4 bits of the last byte give the offset of a 31-bit value
        int offset = mac[^1] & 0x0f;
        uint value = BinaryPrimitives.ReadUInt32BigEndian(mac.AsSpan(offset, 4)) & 0x7fffffff;
        ulong modulus = (ulong)Math.Pow(10, Digits);
        return (value % modulus).ToString().PadLeft(Digits, '0');
    }
}

/// <summary>
/// Time-based (RFC 6238) code generator
/// </summary>
public sealed class TotpGenerator : OtpGenerator
{
    public TotpGenerator(byte[] secretKey) : base(secretKey)
    {
    }

    public string GetCode(DateTimeOffset now) => ComputeCode(now.ToUnixTimeSeconds() / Period);

    /// <summary>
    /// Seconds until the code shown at <paramref name="now"/> expires
    /// </summary>
    public double SecondsRemaining(DateTimeOffset now)
    {
        double elapsed = now.ToUnixTimeMilliseconds() / 1000.0 % Period;
        return Period - elapsed;
    }
}

/// <summary>
/// Counter-based (RFC 4226) code generator
/// </summary>
public sealed class HotpGenerator : OtpGenerator
{
    public HotpGenerator(byte[] secretKey, long counter = 0) : base(secretKey)
    {
        Counter = counter;
    }

    /// <summary>
    /// Counter value used for the most recently generated code
    /// </summary>
    public long Counter { get; set; }

    /// <summary>
    /// Advance the counter and return the next code
    /// </summary>
    public string NextCode()
    {
        Counter++;
        return ComputeCode(Counter);
    }
}
