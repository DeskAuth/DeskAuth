// DeskAuth, based on WinAuth. Copyright (C) 2011 Colin Mackie and contributors.
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
