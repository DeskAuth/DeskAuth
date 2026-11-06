// DeskAuth, based on WinAuth.
// Copyright (C) 2026 Nick Morphis and contributors (DeskAuth).
// Licensed under the GNU General Public License v3 or later. See LICENSE.

using System.Runtime.InteropServices;
using System.Text;

namespace DeskAuth.Core;

/// <summary>
/// Whether DeskAuth is running as an installed app package, which is how the Microsoft Store version runs
/// </summary>
public static class PackageIdentity
{
    private const int Success = 0;
    private const int InsufficientBuffer = 122;

    /// <summary>
    /// The package family name, such as DeskAuth.DeskAuth_abc123, or null when not running from a package
    /// </summary>
    public static string? FamilyName { get; } = GetFamilyName();

    /// <summary>
    /// Running from an app package (the Microsoft Store version)
    /// </summary>
    public static bool IsPackaged => FamilyName != null;

    /// <summary>
    /// The package's own folder for its data: %LOCALAPPDATA%\Packages\[family name]\LocalState.
    /// Windows deletes it when the app is uninstalled.
    /// </summary>
    public static string? LocalStateFolder => FamilyName == null ? null :
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Packages", FamilyName, "LocalState");

    private static string? GetFamilyName()
    {
        if (!OperatingSystem.IsWindows())
        {
            return null;
        }
        try
        {
            int length = 0;
            // APPMODEL_ERROR_NO_PACKAGE when not packaged
            if (GetCurrentPackageFamilyName(ref length, null) != InsufficientBuffer)
            {
                return null;
            }
            var name = new StringBuilder(length);
            return GetCurrentPackageFamilyName(ref length, name) == Success ? name.ToString() : null;
        }
        catch (EntryPointNotFoundException)
        {
            return null;
        }
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetCurrentPackageFamilyName(ref int packageFamilyNameLength, StringBuilder? packageFamilyName);
}
