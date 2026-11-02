// DeskAuth, based on WinAuth.
// Copyright (C) 2026 Nick Morphis and contributors (DeskAuth).
// Licensed under the GNU General Public License v3 or later. See LICENSE.

using DeskAuth.Core;

namespace DeskAuth.Services;

/// <summary>
/// The running version of DeskAuth, set by the build (from the release tag when built by GitHub Actions)
/// </summary>
internal static class AppInfo
{
    /// <summary>
    /// Three-part version, such as 4.1.0
    /// </summary>
    public static Version Version { get; } =
        UpdateCheck.Normalize(typeof(AppInfo).Assembly.GetName().Version ?? new Version(0, 0, 0));

    public static string VersionText { get; } = Version.ToString(3);
}
