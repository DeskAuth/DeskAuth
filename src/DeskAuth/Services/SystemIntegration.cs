// DeskAuth, based on WinAuth.
// Copyright (C) 2010-2017 Colin Mackie (WinAuth).
// Copyright (C) 2026 Nick Morphis and contributors (DeskAuth, modified from WinAuth).
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
    /// The Microsoft Store version's startup task, declared in store/AppxManifest.xml
    /// </summary>
    private const string StartupTaskId = "DeskAuthStartup";

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
    /// Add or remove DeskAuth from the current user's startup programs. Not used by the Microsoft Store version,
    /// which uses <see cref="SetStartupTaskAsync"/>.
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

    /// <summary>
    /// The Microsoft Store version starts with Windows (it can be changed in Windows' startup apps settings too)
    /// </summary>
    public static async Task<bool> IsStartupTaskEnabledAsync()
    {
#if DESKAUTH_STORE
        try
        {
            var task = await Windows.ApplicationModel.StartupTask.GetAsync(StartupTaskId);
            return task.State is Windows.ApplicationModel.StartupTaskState.Enabled
                or Windows.ApplicationModel.StartupTaskState.EnabledByPolicy;
        }
        catch (Exception ex) when (ex is COMException or ArgumentException or InvalidOperationException)
        {
            return false;
        }
#else
        await Task.CompletedTask;
        return false;
#endif
    }

    /// <summary>
    /// Turn the Microsoft Store version's startup task on or off
    /// </summary>
    /// <returns>null if done, otherwise a message for the user</returns>
    public static async Task<string?> SetStartupTaskAsync(bool enabled)
    {
#if DESKAUTH_STORE
        try
        {
            var task = await Windows.ApplicationModel.StartupTask.GetAsync(StartupTaskId);
            if (!enabled)
            {
                task.Disable();
                return null;
            }
            return await task.RequestEnableAsync() switch
            {
                Windows.ApplicationModel.StartupTaskState.Enabled or Windows.ApplicationModel.StartupTaskState.EnabledByPolicy => null,
                Windows.ApplicationModel.StartupTaskState.DisabledByPolicy =>
                    "Starting DeskAuth with Windows has been turned off by your organization.",
                _ =>
                    "Starting DeskAuth with Windows has been turned off in Windows. To turn it on, open Settings, then Apps, " +
                    "Startup, and turn on DeskAuth.",
            };
        }
        catch (Exception ex) when (ex is COMException or ArgumentException or InvalidOperationException)
        {
            return $"Windows could not change whether DeskAuth starts with Windows.\n\n{ex.Message}";
        }
#else
        await Task.CompletedTask;
        return "Starting with Windows is only set this way for the Microsoft Store version.";
#endif
    }

    /// <summary>
    /// The Microsoft Store version was started by its startup task when the user signed in to Windows
    /// </summary>
    public static bool StartedByStartupTask()
    {
#if DESKAUTH_STORE
        try
        {
            return Windows.ApplicationModel.AppInstance.GetActivatedEventArgs()?.Kind ==
                Windows.ApplicationModel.Activation.ActivationKind.StartupTask;
        }
        catch (Exception ex) when (ex is COMException or InvalidOperationException or TypeLoadException)
        {
            return false;
        }
#else
        return false;
#endif
    }
}
