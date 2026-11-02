// DeskAuth, based on WinAuth.
// Copyright (C) 2010-2017 Colin Mackie (WinAuth).
// Copyright (C) 2026 Nick Morphis and contributors (DeskAuth, modified from WinAuth).
// Licensed under the GNU General Public License v3 or later. See LICENSE.

using System.Security.Cryptography;
using DeskAuth.Core;

namespace DeskAuth.Core.Tests;

internal static class Fixtures
{
    public static string PathOf(string name) => Path.Combine(AppContext.BaseDirectory, "Fixtures", name);

    public static string ReadText(string name) => File.ReadAllText(PathOf(name));

    private static readonly Lazy<bool> UserFixturesReadable = new(() =>
    {
        try
        {
            DeskAuthConfig.Load(PathOf("legacy-user.xml"));
            return true;
        }
        catch (BadPasswordException ex) when (ex.InnerException is CryptographicException)
        {
            // Windows refused to decrypt: the file was protected by another account
            return false;
        }
        catch
        {
            // any other failure is a bug, so let the tests run and report it
            return true;
        }
    });

    /// <summary>
    /// The fixtures protected with ProtectionTypes.User can only be decrypted by the Windows account that
    /// wrote them, so not on build servers or other developers' computers.
    /// </summary>
    public static bool CanReadUserFixtures => UserFixturesReadable.Value;
}

/// <summary>
/// A theory that uses fixtures protected by the Windows account that wrote them, and is skipped when
/// running as any other account.
/// </summary>
public sealed class UserFixtureTheoryAttribute : TheoryAttribute
{
    public UserFixtureTheoryAttribute()
    {
        if (!Fixtures.CanReadUserFixtures)
        {
            Skip = "The fixture was protected by another Windows account and can only be decrypted by that account";
        }
    }
}
