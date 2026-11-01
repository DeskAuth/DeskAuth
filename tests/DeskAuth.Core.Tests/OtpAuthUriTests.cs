using DeskAuth.Core;

namespace DeskAuth.Core.Tests;

public class OtpAuthUriTests
{
    [Fact]
    public void ParsesTotpWithIssuer()
    {
        var entry = OtpAuthUri.Parse("otpauth://totp/GitHub:alice%40example.com?secret=JBSWY3DPEHPK3PXP&issuer=GitHub");

        Assert.Equal("GitHub (alice@example.com)", entry.Name);
        Assert.Equal(AuthenticatorKind.Totp, entry.Kind);
        Assert.Equal("48656C6C6F21DEADBEEF", Convert.ToHexString(entry.Generator!.SecretKey));
        Assert.Equal(6, entry.Generator.Digits);
        Assert.Equal(30, entry.Generator.Period);
    }

    [Fact]
    public void ParsesOptions()
    {
        var entry = OtpAuthUri.Parse("otpauth://totp/Example?secret=JBSWY3DPEHPK3PXP&digits=8&period=60&algorithm=SHA256");

        Assert.Equal("Example", entry.Name);
        Assert.Equal(8, entry.Generator!.Digits);
        Assert.Equal(60, entry.Generator.Period);
        Assert.Equal(OtpAlgorithm.SHA256, entry.Generator.Algorithm);
    }

    [Fact]
    public void ParsesHotp()
    {
        var entry = OtpAuthUri.Parse("otpauth://hotp/Counter?secret=JBSWY3DPEHPK3PXP&counter=42");

        Assert.Equal(AuthenticatorKind.Hotp, entry.Kind);
        Assert.Equal(42, ((HotpGenerator)entry.Generator!).Counter);
        Assert.False(entry.AutoRefresh);
    }

    [Theory]
    [InlineData("https://example.com")]
    [InlineData("otpauth://totp/NoSecret")]
    [InlineData("otpauth://steam/Steam?secret=JBSWY3DPEHPK3PXP")]
    [InlineData("otpauth://totp/Steam:me?secret=JBSWY3DPEHPK3PXP&deviceid=android:123")]
    [InlineData("otpauth://totp/Bad?secret=JBSWY3DPEHPK3PX1")]
    public void RejectsInvalid(string uri)
    {
        Assert.Throws<FormatException>(() => OtpAuthUri.Parse(uri));
    }

    [Theory]
    [InlineData("otpauth://totp/GitHub:alice%40example.com?secret=JBSWY3DPEHPK3PXP&issuer=GitHub&digits=6")]
    [InlineData("otpauth://totp/Plain?secret=JBSWY3DPEHPK3PXP&digits=8&algorithm=SHA512&period=60")]
    [InlineData("otpauth://hotp/Counter?secret=JBSWY3DPEHPK3PXP&digits=6&counter=42")]
    public void FormatRoundTrips(string uri)
    {
        var entry = OtpAuthUri.Parse(uri);
        Assert.Equal(uri, OtpAuthUri.Format(entry));
    }

    [Fact]
    public void RoundTripsCustomIcon()
    {
        var entry = OtpAuthUri.Parse("otpauth://totp/Icon?secret=JBSWY3DPEHPK3PXP");
        entry.Skin = "base64:" + Convert.ToBase64String([1, 2, 3, 4, 5]);

        var copy = OtpAuthUri.Parse(OtpAuthUri.Format(entry));
        Assert.Equal(entry.Skin, copy.Skin);
    }

    [Fact]
    public void ParseLinesReportsLineNumber()
    {
        string text = "# export\n\notpauth://totp/A?secret=JBSWY3DPEHPK3PXP\nnot a link\n";
        var ex = Assert.Throws<FormatException>(() => OtpAuthUri.ParseLines(text));
        Assert.StartsWith("Line 4:", ex.Message);

        Assert.Single(OtpAuthUri.ParseLines("otpauth://totp/A?secret=JBSWY3DPEHPK3PXP\n# comment"));
    }
}
