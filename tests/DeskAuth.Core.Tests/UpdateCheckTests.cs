// DeskAuth, based on WinAuth.
// Copyright (C) 2026 Nick Morphis and contributors (DeskAuth).
// Licensed under the GNU General Public License v3 or later. See LICENSE.

using System.Text.Json;
using DeskAuth.Core;

namespace DeskAuth.Core.Tests;

public class UpdateCheckTests
{
    private static string ReleaseJson(string tag, bool prerelease = false, bool draft = false) =>
        JsonSerializer.Serialize(new
        {
            tag_name = tag,
            name = "DeskAuth",
            draft,
            prerelease,
            html_url = "https://example.com/not-used",
        });

    [Theory]
    [InlineData("v4.2.0", "4.1.0.0", "4.2.0")]
    [InlineData("4.2.0", "4.1.0.0", "4.2.0")]
    [InlineData("v4.1.1", "4.1.0.0", "4.1.1")]
    [InlineData("v5.0.0", "4.9.9", "5.0.0")]
    [InlineData("v4.10.0", "4.9.0", "4.10.0")]
    public void FindsNewerRelease(string tag, string current, string expected)
    {
        var release = UpdateCheck.FindNewerRelease(ReleaseJson(tag), Version.Parse(current));

        Assert.NotNull(release);
        Assert.Equal(Version.Parse(expected), release.Version);
        Assert.Equal("https://github.com/DeskAuth/DeskAuth/releases/tag/" + tag, release.Page.AbsoluteUri);
    }

    [Theory]
    [InlineData("v4.1.0", "4.1.0.0")]
    [InlineData("v4.1.0", "4.1.0")]
    [InlineData("v4.0.0", "4.1.0.0")]
    [InlineData("v4.1.0", "4.2.0.0")]
    public void IgnoresSameOrOlderRelease(string tag, string current)
    {
        Assert.Null(UpdateCheck.FindNewerRelease(ReleaseJson(tag), Version.Parse(current)));
    }

    [Theory]
    [InlineData("v4.2.0-beta.1")]
    [InlineData("v4.2.0-rc1")]
    [InlineData("v4.2")]
    [InlineData("v4.2.0.1")]
    [InlineData("latest")]
    [InlineData("")]
    public void IgnoresTagsThatAreNotPlainVersions(string tag)
    {
        Assert.Null(UpdateCheck.FindNewerRelease(ReleaseJson(tag), new Version(4, 1, 0)));
    }

    [Fact]
    public void IgnoresPreReleasesAndDrafts()
    {
        Assert.Null(UpdateCheck.FindNewerRelease(ReleaseJson("v9.0.0", prerelease: true), new Version(4, 1, 0)));
        Assert.Null(UpdateCheck.FindNewerRelease(ReleaseJson("v9.0.0", draft: true), new Version(4, 1, 0)));
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("[]")]
    [InlineData("{\"tag_name\": 420}")]
    [InlineData("{\"message\": \"Not Found\"}")]
    public void IgnoresUnexpectedResponses(string json)
    {
        Assert.Null(UpdateCheck.FindNewerRelease(json, new Version(4, 1, 0)));
    }

    [Fact]
    public void InvalidJsonThrows()
    {
        Assert.ThrowsAny<JsonException>(() => UpdateCheck.FindNewerRelease("<html>", new Version(4, 1, 0)));
    }
}
