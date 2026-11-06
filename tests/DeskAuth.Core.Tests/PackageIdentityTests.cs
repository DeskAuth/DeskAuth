// DeskAuth, based on WinAuth.
// Copyright (C) 2026 Nick Morphis and contributors (DeskAuth).
// Licensed under the GNU General Public License v3 or later. See LICENSE.

using DeskAuth.Core;

namespace DeskAuth.Core.Tests;

public class PackageIdentityTests
{
    [Fact]
    public void TestsDoNotRunFromAPackage()
    {
        Assert.False(PackageIdentity.IsPackaged);
        Assert.Null(PackageIdentity.FamilyName);
        Assert.Null(PackageIdentity.LocalStateFolder);
    }

    [Fact]
    public void DownloadedVersionKeepsItsFileInAppData()
    {
        string expected = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "DeskAuth", "deskauth.xml");
        // portable mode only if the test folder has a deskauth.xml, which it doesn't
        Assert.Equal(expected, DeskAuthConfig.GetDefaultPath());
    }
}
