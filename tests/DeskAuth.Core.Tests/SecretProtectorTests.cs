// DeskAuth, based on WinAuth.
// Copyright (C) 2010-2017 Colin Mackie (WinAuth).
// Copyright (C) 2026 Nick Morphis and contributors (DeskAuth, modified from WinAuth).
// Licensed under the GNU General Public License v3 or later. See LICENSE.

using System.Text;
using DeskAuth.Core;

namespace DeskAuth.Core.Tests;

public class SecretProtectorTests
{
    [Fact]
    public void DecryptsLegacyPasswordSequence()
    {
        string encrypted = Fixtures.ReadText("legacy-sequence.txt");
        string plain = SecretProtector.Decrypt(encrypted, ProtectionTypes.Password, "password123");
        Assert.Equal("hello world", Encoding.UTF8.GetString(Convert.FromHexString(plain)));
    }
}
