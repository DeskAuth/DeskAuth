// DeskAuth, based on WinAuth. Copyright (C) 2011 Colin Mackie and contributors.
// Licensed under the GNU General Public License v3 or later. See LICENSE.

using System.Windows.Input;
using System.Windows.Interop;
using DeskAuth.Core;

namespace DeskAuth.Services;

/// <summary>
/// Registers system-wide hotkeys for a window
/// </summary>
internal sealed class HotKeyManager : IDisposable
{
    private readonly HwndSource _source;
    private readonly Dictionary<int, AuthenticatorEntry> _registered = [];
    private int _nextId = 1;

    public HotKeyManager(HwndSource source)
    {
        _source = source;
        _source.AddHook(WndProc);
    }

    public event EventHandler<AuthenticatorEntry>? HotKeyPressed;

    /// <summary>
    /// Register hotkeys for all authenticators that have one
    /// </summary>
    /// <returns>authenticators whose hotkey is in use by another program</returns>
    public List<AuthenticatorEntry> Register(IEnumerable<AuthenticatorEntry> entries)
    {
        UnregisterAll();
        var failed = new List<AuthenticatorEntry>();
        foreach (var entry in entries.Where(e => e.HotKey != null))
        {
            int id = _nextId++;
            uint modifiers = (uint)entry.HotKey!.Modifiers | NativeMethods.MOD_NOREPEAT;
            if (NativeMethods.RegisterHotKey(_source.Handle, id, modifiers, (uint)entry.HotKey.VirtualKey))
            {
                _registered[id] = entry;
            }
            else
            {
                failed.Add(entry);
            }
        }
        return failed;
    }

    public void UnregisterAll()
    {
        foreach (int id in _registered.Keys)
        {
            NativeMethods.UnregisterHotKey(_source.Handle, id);
        }
        _registered.Clear();
    }

    /// <summary>
    /// Display text for a hotkey, e.g. "Ctrl+Alt+C"
    /// </summary>
    public static string Describe(HotKeySetting hotkey)
    {
        var parts = new List<string>();
        if (hotkey.Modifiers.HasFlag(HotKeyModifiers.Control)) parts.Add("Ctrl");
        if (hotkey.Modifiers.HasFlag(HotKeyModifiers.Alt)) parts.Add("Alt");
        if (hotkey.Modifiers.HasFlag(HotKeyModifiers.Shift)) parts.Add("Shift");
        if (hotkey.Modifiers.HasFlag(HotKeyModifiers.Windows)) parts.Add("Win");
        Key key = KeyInterop.KeyFromVirtualKey(hotkey.VirtualKey);
        parts.Add(key is >= Key.D0 and <= Key.D9
            ? ((char)('0' + (key - Key.D0))).ToString()
            : new KeyConverter().ConvertToString(key) ?? key.ToString());
        return string.Join("+", parts);
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == NativeMethods.WM_HOTKEY && _registered.TryGetValue((int)wParam, out var entry))
        {
            handled = true;
            HotKeyPressed?.Invoke(this, entry);
        }
        return IntPtr.Zero;
    }

    public void Dispose()
    {
        UnregisterAll();
        _source.RemoveHook(WndProc);
    }
}
