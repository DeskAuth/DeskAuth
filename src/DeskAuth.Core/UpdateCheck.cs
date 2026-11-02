// DeskAuth, based on WinAuth.
// Copyright (C) 2026 Nick Morphis and contributors (DeskAuth).
// Licensed under the GNU General Public License v3 or later. See LICENSE.

using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace DeskAuth.Core;

/// <summary>
/// Reads the latest published release from GitHub, to tell users about a newer version.
/// Drafts, pre-releases and tags that aren't a plain version (such as v4.2.0-beta.1) are never offered.
/// </summary>
public static partial class UpdateCheck
{
    /// <summary>
    /// GitHub's latest release: the most recent one that is published and not a pre-release
    /// </summary>
    public static readonly Uri LatestReleaseApi = new("https://api.github.com/repos/DeskAuth/DeskAuth/releases/latest");

    private const string ReleasePageBase = "https://github.com/DeskAuth/DeskAuth/releases/tag/";

    /// <summary>
    /// A published release
    /// </summary>
    /// <param name="Version">its version, with three parts</param>
    /// <param name="Page">its page on GitHub, with the downloads</param>
    public sealed record Release(Version Version, Uri Page);

    /// <summary>
    /// Get the release from GitHub's latest release response
    /// </summary>
    /// <returns>the release, or null if it is a draft, a pre-release or doesn't have a plain version tag</returns>
    /// <exception cref="JsonException">the response isn't valid JSON</exception>
    public static Release? ParseLatestRelease(string json)
    {
        using var document = JsonDocument.Parse(json);
        JsonElement root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object
            || IsTrue(root, "draft")
            || IsTrue(root, "prerelease")
            || !root.TryGetProperty("tag_name", out JsonElement tagElement)
            || tagElement.ValueKind != JsonValueKind.String)
        {
            return null;
        }
        string tag = tagElement.GetString()!;
        if (!TryParseReleaseVersion(tag, out Version? version))
        {
            return null;
        }
        // the page is made from the checked tag, rather than opening an address from the response
        return new Release(version, new Uri(ReleasePageBase + Uri.EscapeDataString(tag)));
    }

    /// <summary>
    /// Get the newer release from GitHub's latest release response
    /// </summary>
    /// <returns>the release if it is newer than <paramref name="current"/>, otherwise null</returns>
    public static Release? FindNewerRelease(string json, Version current)
    {
        Release? release = ParseLatestRelease(json);
        return release != null && release.Version > Normalize(current) ? release : null;
    }

    /// <summary>
    /// Read a release tag such as v4.1.0 or 4.1.0. Pre-release tags such as v4.2.0-beta.1 are not accepted.
    /// </summary>
    public static bool TryParseReleaseVersion(string tag, [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out Version? version)
    {
        version = null;
        Match match = ReleaseTag().Match(tag);
        if (!match.Success
            || !int.TryParse(match.Groups[1].Value, NumberStyles.None, CultureInfo.InvariantCulture, out int major)
            || !int.TryParse(match.Groups[2].Value, NumberStyles.None, CultureInfo.InvariantCulture, out int minor)
            || !int.TryParse(match.Groups[3].Value, NumberStyles.None, CultureInfo.InvariantCulture, out int patch))
        {
            return false;
        }
        version = new Version(major, minor, patch);
        return true;
    }

    /// <summary>
    /// Three-part version, so 4.1.0.0 from the assembly equals 4.1.0 from a tag
    /// </summary>
    public static Version Normalize(Version version) =>
        new(version.Major, version.Minor, Math.Max(version.Build, 0));

    private static bool IsTrue(JsonElement element, string name) =>
        element.TryGetProperty(name, out JsonElement value) && value.ValueKind == JsonValueKind.True;

    [GeneratedRegex(@"^[vV]?(\d{1,9})\.(\d{1,9})\.(\d{1,9})$")]
    private static partial Regex ReleaseTag();
}
