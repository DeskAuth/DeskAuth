// DeskAuth, based on WinAuth.
// Copyright (C) 2010-2017 Colin Mackie (WinAuth).
// Copyright (C) 2026 Nick Morphis and contributors (DeskAuth, modified from WinAuth).
// Licensed under the GNU General Public License v3 or later. See LICENSE.

using DeskAuth.Core;

namespace DeskAuth.Core.Tests;

public class FaviconFinderTests
{
    private static readonly Uri Page = new("https://www.example.com/login/page.html");

    [Fact]
    public void OrdersLargestFirstAndAddsFaviconIco()
    {
        string html = """
            <html><head>
            <link rel="icon" href="/small.png" sizes="16x16">
            <link rel="apple-touch-icon" href="touch.png">
            <link rel='icon' type='image/png' href='https://cdn.example.com/big.png' sizes='32x32 192x192'>
            <link rel="stylesheet" href="/site.css">
            </head></html>
            """;

        var icons = FaviconFinder.FindIcons(html, Page);

        Assert.Equal(
            [
                "https://cdn.example.com/big.png",
                "https://www.example.com/login/touch.png",
                "https://www.example.com/small.png",
                "https://www.example.com/favicon.ico",
            ],
            icons.Select(i => i.Url.ToString()));
        Assert.Equal(192, icons[0].Size);
    }

    [Fact]
    public void SkipsSvgAndMaskIcons()
    {
        string html = """
            <link rel="icon" type="image/svg+xml" href="/icon.svg">
            <link rel="icon" href="/logo.svg?v=2">
            <link rel="mask-icon" href="/mask.png">
            <link rel="shortcut icon" href="/shortcut.ico">
            """;

        var icons = FaviconFinder.FindIcons(html, Page);

        Assert.Equal(["https://www.example.com/shortcut.ico", "https://www.example.com/favicon.ico"], icons.Select(i => i.Url.ToString()));
    }

    [Fact]
    public void UsesBaseTagAndDecodesEntities()
    {
        string html = """<base href="https://static.example.org/assets/"><link href="icon.png?a=1&amp;b=2" rel="icon">""";

        var icons = FaviconFinder.FindIcons(html, Page);

        Assert.Equal("https://static.example.org/assets/icon.png?a=1&b=2", icons[0].Url.ToString());
        // favicon.ico is still on the page's own site
        Assert.Equal("https://www.example.com/favicon.ico", icons[^1].Url.ToString());
    }

    [Fact]
    public void FallsBackToFaviconIco()
    {
        var icons = FaviconFinder.FindIcons("<html></html>", Page);

        Assert.Equal("https://www.example.com/favicon.ico", Assert.Single(icons).Url.ToString());
    }

    [Fact]
    public void AcceptsDataUris()
    {
        var icons = FaviconFinder.FindIcons("""<link rel="icon" href="data:image/png;base64,iVBORw0KGgo=">""", Page);

        Assert.Equal("data", icons[0].Url.Scheme);
    }
}
