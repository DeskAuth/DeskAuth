// DeskAuth, based on WinAuth. Copyright (C) 2011 Colin Mackie and contributors.
// Licensed under the GNU General Public License v3 or later. See LICENSE.

using System.Runtime.InteropServices;
using System.Windows;
using Microsoft.Win32;

namespace DeskAuth.Services;

/// <summary>
/// Clipboard and Windows startup helpers
/// </summary>
internal static class SystemIntegration
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string RunValue = "DeskAuth";

    /// <summary>
    /// Startup entry added by WinAuth, replaced by DeskAuth's
    /// </summary>
    private const string LegacyRunValue = "WinAuth";

    /// <summary>
    /// Copy text to the clipboard, retrying while another program has it open
    /// </summary>
    /// <returns>false if the clipboard could not be opened</returns>
    public static bool CopyToClipboard(string text)
    {
        for (int attempt = 0; attempt < 5; attempt++)
        {
            try
            {
                Clipboard.SetDataObject(text, copy: true);
                return true;
            }
            catch (COMException)
            {
                Thread.Sleep(100);
            }
        }
        return false;
    }

    /// <summary>
    /// Add or remove DeskAuth from the current user's startup programs
    /// </summary>
    public static void SetStartWithWindows(bool enabled)
    {
        using RegistryKey key = Registry.CurrentUser.CreateSubKey(RunKey);
        key.DeleteValue(LegacyRunValue, throwOnMissingValue: false);
        if (enabled)
        {
            key.SetValue(RunValue, $"\"{Environment.ProcessPath}\" -min");
        }
        else
        {
            key.DeleteValue(RunValue, throwOnMissingValue: false);
        }
    }
}
