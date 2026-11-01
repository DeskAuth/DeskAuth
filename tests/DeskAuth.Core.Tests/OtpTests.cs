// DeskAuth, based on WinAuth.
// Copyright (C) 2010-2017 Colin Mackie (WinAuth).
// Copyright (C) 2026 Nick Morphis and contributors (DeskAuth, modified from WinAuth).
// Licensed under the GNU General Public License v3 or later. See LICENSE.

using System.Text;
using DeskAuth.Core;

namespace DeskAuth.Core.Tests;

public class OtpTests
{
    private static readonly byte[] Rfc4226Secret = Encoding.ASCII.GetBytes("12345678901234567890");

    [Theory]
    [InlineData(0, "755224")]
    [InlineData(1, "287082")]
    [InlineData(2, "359152")]
    [InlineData(3, "969429")]
    [InlineData(4, "338314")]
    [InlineData(5, "254676")]
    [InlineData(6, "287922")]
    [InlineData(7, "162583")]
    [InlineData(8, "399871")]
    [InlineData(9, "520489")]
    public void Rfc4226Vectors(long counter, string expected)
    {
        Assert.Equal(expected, new HotpGenerator(Rfc4226Secret).ComputeCode(counter));
    }

    [Fact]
    public void HotpNextCodeAdvancesCounter()
    {
        var hotp = new HotpGenerator(Rfc4226Secret, counter: 0);
        Assert.Equal("287082", hotp.NextCode());
        Assert.Equal(1, hotp.Counter);
        Assert.Equal("359152", hotp.NextCode());
    }

    [Theory]
    [InlineData(59, "94287082", OtpAlgorithm.SHA1)]
    [InlineData(59, "46119246", OtpAlgorithm.SHA256)]
    [InlineData(59, "90693936", OtpAlgorithm.SHA512)]
    [InlineData(1111111109, "07081804", OtpAlgorithm.SHA1)]
    [InlineData(1111111109, "68084774", OtpAlgorithm.SHA256)]
    [InlineData(1111111109, "25091201", OtpAlgorithm.SHA512)]
    [InlineData(20000000000, "65353130", OtpAlgorithm.SHA1)]
    [InlineData(20000000000, "77737706", OtpAlgorithm.SHA256)]
    [InlineData(20000000000, "47863826", OtpAlgorithm.SHA512)]
    public void Rfc6238Vectors(long unixTime, string expected, OtpAlgorithm algorithm)
    {
        // RFC 6238 uses a key of the hash's output length
        string seed = algorithm switch
        {
            OtpAlgorithm.SHA256 => "12345678901234567890123456789012",
            OtpAlgorithm.SHA512 => "1234567890123456789012345678901234567890123456789012345678901234",
            _ => "12345678901234567890",
        };
        var totp = new TotpGenerator(Encoding.ASCII.GetBytes(seed)) { Digits = 8, Algorithm = algorithm };
        Assert.Equal(expected, totp.GetCode(DateTimeOffset.FromUnixTimeSeconds(unixTime)));
    }

    [Fact]
    public void SecondsRemaining()
    {
        var totp = new TotpGenerator(Rfc4226Secret);
        Assert.Equal(20, totp.SecondsRemaining(DateTimeOffset.FromUnixTimeSeconds(70)), 3);
    }

    [Theory]
    [InlineData("JBSWY3DPEHPK3PXP", "48656C6C6F21DEADBEEF")]
    [InlineData("jbsw y3dp-ehpk 3pxp====", "48656C6C6F21DEADBEEF")]
    [InlineData("GEZDGNBVGY3TQOJQGEZDGNBVGY3TQOJQ", "3132333435363738393031323334353637383930")]
    public void Base32Decode(string text, string hex)
    {
        Assert.Equal(hex, Convert.ToHexString(Base32.Decode(text)));
    }

    [Fact]
    public void Base32RoundTrip()
    {
        for (int length = 0; length < 40; length++)
        {
            byte[] data = Enumerable.Range(0, length).Select(i => (byte)(i * 37)).ToArray();
            Assert.Equal(data, Base32.Decode(Base32.Encode(data)));
        }
    }

    [Fact]
    public void Base32RejectsInvalid()
    {
        Assert.Throws<FormatException>(() => Base32.Decode("ABC1"));
    }
}
