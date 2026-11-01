// DeskAuth, based on WinAuth. Copyright (C) 2011 Colin Mackie and contributors.
// Licensed under the GNU General Public License v3 or later. See LICENSE.

using System.Runtime.InteropServices;

namespace DeskAuth.Services;

/// <summary>
/// Types text into the foreground window
/// </summary>
internal static class KeyboardTyper
{
    private static readonly int[] ModifierKeys =
        [NativeMethods.VK_SHIFT, NativeMethods.VK_CONTROL, NativeMethods.VK_MENU, NativeMethods.VK_LWIN, NativeMethods.VK_RWIN];

    /// <summary>
    /// Wait for the hotkey's modifier keys to be released, so they don't combine with the typed text
    /// </summary>
    /// <returns>false if keys were still held after the timeout</returns>
    public static async Task<bool> WaitForModifiersReleasedAsync(TimeSpan timeout)
    {
        var until = DateTime.UtcNow + timeout;
        while (ModifierKeys.Any(k => (NativeMethods.GetAsyncKeyState(k) & 0x8000) != 0))
        {
            if (DateTime.UtcNow > until)
            {
                return false;
            }
            await Task.Delay(50);
        }
        return true;
    }

    /// <summary>
    /// Send text as Unicode key presses
    /// </summary>
    public static void Type(string text)
    {
        var inputs = new List<NativeMethods.INPUT>();
        foreach (char c in text)
        {
            inputs.Add(KeyInput(c, NativeMethods.KEYEVENTF_UNICODE));
            inputs.Add(KeyInput(c, NativeMethods.KEYEVENTF_UNICODE | NativeMethods.KEYEVENTF_KEYUP));
        }
        NativeMethods.SendInput((uint)inputs.Count, inputs.ToArray(), Marshal.SizeOf<NativeMethods.INPUT>());
    }

    private static NativeMethods.INPUT KeyInput(char c, uint flags) => new()
    {
        type = NativeMethods.INPUT_KEYBOARD,
        u = new NativeMethods.INPUTUNION
        {
            ki = new NativeMethods.KEYBDINPUT { wScan = c, dwFlags = flags },
        },
    };
}
