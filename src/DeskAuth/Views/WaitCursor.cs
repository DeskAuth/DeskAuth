// DeskAuth, based on WinAuth.
// Copyright (C) 2010-2017 Colin Mackie (WinAuth).
// Copyright (C) 2026 Nick Morphis and contributors (DeskAuth, modified from WinAuth).
// Licensed under the GNU General Public License v3 or later. See LICENSE.

using System.Windows.Input;

namespace DeskAuth.Views;

/// <summary>
/// Shows the wait cursor until disposed
/// </summary>
internal sealed class WaitCursor : IDisposable
{
    private readonly Cursor? _previous = Mouse.OverrideCursor;

    public WaitCursor()
    {
        Mouse.OverrideCursor = Cursors.Wait;
    }

    public void Dispose()
    {
        Mouse.OverrideCursor = _previous;
    }
}
