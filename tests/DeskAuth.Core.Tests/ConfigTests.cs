// DeskAuth, based on WinAuth.
// Copyright (C) 2010-2017 Colin Mackie (WinAuth).
// Copyright (C) 2026 Nick Morphis and contributors (DeskAuth, modified from WinAuth).
// Licensed under the GNU General Public License v3 or later. See LICENSE.

using System.Text;
using DeskAuth.Core;

namespace DeskAuth.Core.Tests;

/// <summary>
/// Fixtures were written by WinAuth 3.6, with the same authenticators in each file
/// and different protection on the whole file.
/// </summary>
public class ConfigTests
{
    [Theory]
    [InlineData("legacy-plain.xml", null, ProtectionTypes.None)]
    [InlineData("legacy-password.xml", "configpass", ProtectionTypes.Password)]
    [InlineData("legacy-user.xml", null, ProtectionTypes.User)]
    [InlineData("legacy-password-user.xml", "configpass", ProtectionTypes.Password | ProtectionTypes.User)]
    public void LoadsLegacyConfigs(string file, string? password, ProtectionTypes protection)
    {
        var config = DeskAuthConfig.Load(Fixtures.PathOf(file), password);

        Assert.Equal(protection, config.Protection);
        AssertFixtureContents(config);
    }

    [Theory]
    [InlineData("legacy-password.xml")]
    [InlineData("legacy-password-user.xml")]
    public void LegacyPasswordRequired(string file)
    {
        Assert.Throws<PasswordRequiredException>(() => DeskAuthConfig.Load(Fixtures.PathOf(file)));
        Assert.Throws<BadPasswordException>(() => DeskAuthConfig.Load(Fixtures.PathOf(file), "wrong"));
    }

    [Theory]
    [InlineData(ProtectionTypes.None, null)]
    [InlineData(ProtectionTypes.Password, "newpass")]
    [InlineData(ProtectionTypes.User, null)]
    [InlineData(ProtectionTypes.Machine, null)]
    [InlineData(ProtectionTypes.Password | ProtectionTypes.User, "newpass")]
    public void RoundTripsWithCurrentFormat(ProtectionTypes protection, string? password)
    {
        var config = DeskAuthConfig.Load(Fixtures.PathOf("legacy-password.xml"), "configpass");
        config.SetProtection(protection, password);

        byte[] saved = config.Serialize();
        if (protection != ProtectionTypes.None)
        {
            Assert.DoesNotContain("48656C6C6F21DEADBEEF", Encoding.UTF8.GetString(saved));
        }

        var reloaded = DeskAuthConfig.Parse(saved, password);
        Assert.Equal(protection, reloaded.Protection);
        AssertFixtureContents(reloaded);
    }

    [Fact]
    public void WrongPasswordRejectedInCurrentFormat()
    {
        var config = DeskAuthConfig.Load(Fixtures.PathOf("legacy-plain.xml"));
        config.SetProtection(ProtectionTypes.Password, "right");
        byte[] saved = config.Serialize();

        Assert.Throws<PasswordRequiredException>(() => DeskAuthConfig.Parse(saved));
        Assert.Throws<BadPasswordException>(() => DeskAuthConfig.Parse(saved, "wrong"));
    }

    [Fact]
    public void AuthenticatorPasswordCanBeChangedAndRemoved()
    {
        var config = DeskAuthConfig.Load(Fixtures.PathOf("legacy-plain.xml"));
        var entry = config.Authenticators.Single(a => a.Name == "Protected");
        entry.Unlock("authpass");
        entry.SetPassword("other");
        entry.Lock();

        var reloaded = DeskAuthConfig.Parse(config.Serialize());
        var reloadedEntry = reloaded.Authenticators.Single(a => a.Name == "Protected");
        Assert.True(reloadedEntry.IsLocked);
        Assert.Throws<BadPasswordException>(() => reloadedEntry.Unlock("authpass"));
        reloadedEntry.Unlock("other");
        reloadedEntry.SetPassword(null);

        var unprotected = DeskAuthConfig.Parse(reloaded.Serialize()).Authenticators.Single(a => a.Name == "Protected");
        Assert.False(unprotected.IsLocked);
        Assert.False(unprotected.HasPassword);
    }

    [Fact]
    public void HotpCounterIsSaved()
    {
        var config = DeskAuthConfig.Load(Fixtures.PathOf("legacy-plain.xml"));
        var hotp = config.Authenticators.Single(a => a.Kind == AuthenticatorKind.Hotp);
        hotp.GetCode(DateTimeOffset.Now);

        var reloaded = DeskAuthConfig.Parse(config.Serialize()).Authenticators.Single(a => a.Kind == AuthenticatorKind.Hotp);
        Assert.Equal(6, ((HotpGenerator)reloaded.Generator!).Counter);
    }

    [Fact]
    public void SaveKeepsBackup()
    {
        string dir = Path.Combine(Path.GetTempPath(), "deskauth-test-" + Guid.NewGuid());
        try
        {
            string path = Path.Combine(dir, "winauth.xml");
            var config = DeskAuthConfig.Load(Fixtures.PathOf("legacy-plain.xml"));
            config.FilePath = path;
            config.Save();
            config.Authenticators.RemoveAt(0);
            config.Save();

            Assert.Equal(5, DeskAuthConfig.Load(path).Authenticators.Count);
            Assert.Equal(6, DeskAuthConfig.Load(path + ".bak").Authenticators.Count);
            Assert.False(File.Exists(path + ".tmp"));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CopiesWinAuthFileToNewPath(bool legacyReadOnly)
    {
        string dir = Path.Combine(Path.GetTempPath(), "deskauth-test-" + Guid.NewGuid());
        Directory.CreateDirectory(dir);
        string legacy = Path.Combine(dir, "winauth.xml");
        string path = Path.Combine(dir, "DeskAuth", "deskauth.xml");
        try
        {
            File.Copy(Fixtures.PathOf("legacy-password.xml"), legacy);
            File.SetAttributes(legacy, legacyReadOnly ? FileAttributes.ReadOnly : FileAttributes.Normal);
            byte[] original = File.ReadAllBytes(legacy);

            // what DeskAuth does the first time it runs
            var config = DeskAuthConfig.Load(legacy, "configpass");
            Assert.Equal(legacyReadOnly, config.IsReadOnly);
            config.FilePath = path;
            Assert.False(config.IsReadOnly);
            config.Save();

            Assert.Equal(original, File.ReadAllBytes(legacy));
            Assert.Equal(6, DeskAuthConfig.Load(path, "configpass").Authenticators.Count);
        }
        finally
        {
            File.SetAttributes(legacy, FileAttributes.Normal);
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void UsageStatisticsChoiceRoundTrips()
    {
        var config = DeskAuthConfig.Load(Fixtures.PathOf("legacy-plain.xml"));
        Assert.Null(config.SendUsageStatistics);
        Assert.Null(config.InstallId);

        var id = Guid.NewGuid();
        var reported = new DateTimeOffset(2026, 11, 1, 18, 30, 0, TimeSpan.Zero);
        config.SendUsageStatistics = true;
        config.InstallId = id;
        config.LastUsageReport = reported;
        config.SetProtection(ProtectionTypes.Password, "pass");

        var reloaded = DeskAuthConfig.Parse(config.Serialize(), "pass");
        Assert.True(reloaded.SendUsageStatistics);
        Assert.Equal(id, reloaded.InstallId);
        Assert.Equal(reported, reloaded.LastUsageReport);

        reloaded.SendUsageStatistics = false;
        reloaded.InstallId = null;
        var declined = DeskAuthConfig.Parse(reloaded.Serialize(), "pass");
        Assert.False(declined.SendUsageStatistics);
        Assert.Null(declined.InstallId);
    }

    [Fact]
    public void RejectsYubiKeyProtection()
    {
        string xml = "<WinAuth version=\"3.6\"><data encrypted=\"a\">00</data></WinAuth>";
        Assert.Throws<InvalidConfigException>(() => DeskAuthConfig.Parse(Encoding.UTF8.GetBytes(xml)));
    }

    private static void AssertFixtureContents(DeskAuthConfig config)
    {
        Assert.True(config.AlwaysOnTop);
        Assert.True(config.UseTrayIcon);
        Assert.Equal(
            ["Totp SHA1", "Totp SHA256", "Hotp", "Protected", "Microsoft", "Battle.net"],
            config.Authenticators.Select(a => a.Name));

        var sha1 = config.Authenticators[0];
        Assert.Equal(AuthenticatorKind.Totp, sha1.Kind);
        Assert.Equal("GitHubIcon.png", sha1.Skin);
        Assert.Equal("48656C6C6F21DEADBEEF", Convert.ToHexString(sha1.Generator!.SecretKey));
        Assert.Equal(6, sha1.Generator.Digits);

        var sha256 = config.Authenticators[1].Generator!;
        Assert.Equal(OtpAlgorithm.SHA256, sha256.Algorithm);
        Assert.Equal(8, sha256.Digits);
        Assert.Equal(60, sha256.Period);

        var hotp = config.Authenticators[2];
        Assert.Equal(AuthenticatorKind.Hotp, hotp.Kind);
        Assert.Equal(5, ((HotpGenerator)hotp.Generator!).Counter);
        Assert.False(hotp.AutoRefresh);

        var locked = config.Authenticators[3];
        Assert.True(locked.IsLocked);
        Assert.True(locked.HasPassword);
        Assert.Throws<BadPasswordException>(() => locked.Unlock("wrong"));
        locked.Unlock("authpass");
        Assert.Equal("546573745365637265745375706572536563726574", Convert.ToHexString(locked.Generator!.SecretKey));
        locked.Lock();
        Assert.True(locked.IsLocked);

        // Microsoft authenticators were standard TOTP
        Assert.Equal(AuthenticatorKind.Totp, config.Authenticators[4].Kind);

        var battleNet = config.Authenticators[5];
        Assert.Equal(AuthenticatorKind.Unsupported, battleNet.Kind);
        Assert.Equal("BattleNetAuthenticator", battleNet.LegacyType);
    }
}
