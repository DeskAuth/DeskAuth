// DeskAuth, based on WinAuth.
// Copyright (C) 2010-2017 Colin Mackie (WinAuth).
// Copyright (C) 2026 Nick Morphis and contributors (DeskAuth, modified from WinAuth).
// Licensed under the GNU General Public License v3 or later. See LICENSE.

using System.Net;
using System.Text.RegularExpressions;

namespace DeskAuth.Core;

/// <summary>
/// Finds the icons a web page declares for browsers, best first
/// </summary>
public static partial class FaviconFinder
{
    /// <summary>
    /// Size assumed for an apple-touch-icon without a sizes attribute
    /// </summary>
    private const int AppleTouchIconSize = 180;

    public sealed record IconLink(Uri Url, int Size);

    /// <summary>
    /// Get the icon links from a page's HTML, largest first, followed by /favicon.ico
    /// </summary>
    /// <param name="pageUri">address the page was loaded from, after any redirects</param>
    public static IReadOnlyList<IconLink> FindIcons(string html, Uri pageUri)
    {
        Uri baseUri = pageUri;
        if (BaseTag().Match(html) is { Success: true } baseMatch
            && Attributes(baseMatch.Value).TryGetValue("href", out string? baseHref)
            && Uri.TryCreate(pageUri, baseHref, out Uri? declaredBase))
        {
            baseUri = declaredBase;
        }

        var links = new List<(IconLink Link, bool Apple)>();
        foreach (Match tag in LinkTag().Matches(html))
        {
            var attributes = Attributes(tag.Value);
            if (!attributes.TryGetValue("rel", out string? rel) || !attributes.TryGetValue("href", out string? href) || href.Length == 0)
            {
                continue;
            }
            string[] relTokens = rel.ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            bool apple = relTokens.Any(t => t.StartsWith("apple-touch-icon", StringComparison.Ordinal));
            if (!apple && !relTokens.Contains("icon"))
            {
                continue;
            }

            // Windows can't decode SVG icons
            attributes.TryGetValue("type", out string? type);
            if (string.Equals(type, "image/svg+xml", StringComparison.OrdinalIgnoreCase)
                || href.StartsWith("data:image/svg", StringComparison.OrdinalIgnoreCase)
                || href.Split('?', '#')[0].EndsWith(".svg", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }
            if (!Uri.TryCreate(baseUri, href, out Uri? url) || url.Scheme is not ("http" or "https" or "data"))
            {
                continue;
            }

            attributes.TryGetValue("sizes", out string? sizes);
            int size = LargestSize(sizes);
            if (size == 0 && apple)
            {
                size = AppleTouchIconSize;
            }
            links.Add((new IconLink(url, size), apple));
        }

        var result = links
            .OrderByDescending(l => l.Link.Size)
            .ThenByDescending(l => l.Apple)
            .Select(l => l.Link)
            .DistinctBy(l => l.Url)
            .ToList();

        var favicon = new Uri(pageUri, "/favicon.ico");
        if (!result.Any(l => l.Url == favicon))
        {
            result.Add(new IconLink(favicon, 0));
        }
        return result;
    }

    /// <summary>
    /// Largest dimension in a sizes attribute such as "16x16 32x32"
    /// </summary>
    private static int LargestSize(string? sizes)
    {
        int largest = 0;
        foreach (Match match in SizeValue().Matches(sizes ?? string.Empty))
        {
            largest = Math.Max(largest, Math.Max(int.Parse(match.Groups[1].Value), int.Parse(match.Groups[2].Value)));
        }
        return largest;
    }

    private static Dictionary<string, string> Attributes(string tag)
    {
        var attributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (Match match in Attribute().Matches(tag))
        {
            string value = match.Groups[2].Success ? match.Groups[2].Value
                : match.Groups[3].Success ? match.Groups[3].Value
                : match.Groups[4].Value;
            attributes.TryAdd(match.Groups[1].Value, WebUtility.HtmlDecode(value).Trim());
        }
        return attributes;
    }

    [GeneratedRegex(@"<link\b[^>]*>", RegexOptions.IgnoreCase)]
    private static partial Regex LinkTag();

    [GeneratedRegex(@"<base\b[^>]*>", RegexOptions.IgnoreCase)]
    private static partial Regex BaseTag();

    [GeneratedRegex(@"([\w-]+)\s*=\s*(?:""([^""]*)""|'([^']*)'|([^\s""'>]+))")]
    private static partial Regex Attribute();

    [GeneratedRegex(@"(\d+)\s*[xX]\s*(\d+)")]
    private static partial Regex SizeValue();
}
