// DeskAuth, based on WinAuth.
// Copyright (C) 2010-2017 Colin Mackie (WinAuth).
// Copyright (C) 2026 Nick Morphis and contributors (DeskAuth, modified from WinAuth).
// Licensed under the GNU General Public License v3 or later. See LICENSE.

using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Windows;
using System.Windows.Threading;
using DeskAuth.Core;
using DeskAuth.Services;
using DeskAuth.Views;

namespace DeskAuth;

/// <summary>
/// Startup: single instance per config file, unlock the config and show the main window.
///
/// Command line: DeskAuth.exe [config file] [-min]
/// </summary>
public partial class App : Application
{
    private Mutex? _instanceMutex;
    private EventWaitHandle? _showEvent;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        DispatcherUnhandledException += OnUnhandledException;

        bool minimize = e.Args.Any(a => a is "-min" or "--minimize" or "/min");
        string? file = e.Args.FirstOrDefault(a => !a.StartsWith('-') && !a.StartsWith('/'));
        string path = Path.GetFullPath(file ?? DeskAuthConfig.GetDefaultPath());

        // one instance per config file; a second launch shows the first
        string instanceName = "DeskAuth-" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(path.ToUpperInvariant())))[..16];
        _instanceMutex = new Mutex(true, instanceName, out bool isFirst);
        if (!isFirst)
        {
            if (EventWaitHandle.TryOpenExisting(instanceName + "-show", out var show))
            {
                show.Set();
                show.Dispose();
            }
            Shutdown();
            return;
        }

        DeskAuthConfig? config = LoadConfig(path, isDefaultFile: file == null);
        if (config == null)
        {
            Shutdown();
            return;
        }
        if (config.StartWithWindows)
        {
            // keep the startup entry pointing at this program
            SystemIntegration.SetStartWithWindows(true);
        }

        var window = new MainWindow(config);
        MainWindow = window;
        ListenForShowRequests(instanceName + "-show", window);

        if (minimize)
        {
            window.StartMinimized();
        }
        else
        {
            window.Show();
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _showEvent?.Set();
        _instanceMutex?.Dispose();
        base.OnExit(e);
    }

    /// <summary>
    /// Load the config, asking for its password if needed. The first time DeskAuth runs, authenticators are
    /// copied from WinAuth's file if there is one; the WinAuth file is left as it is.
    /// </summary>
    /// <returns>the config, or null to exit</returns>
    private static DeskAuthConfig? LoadConfig(string path, bool isDefaultFile)
    {
        if (File.Exists(path))
        {
            return LoadFile(path);
        }
        if (!isDefaultFile || DeskAuthConfig.FindLegacyFile() is not string legacyPath)
        {
            return new DeskAuthConfig { FilePath = path };
        }

        DeskAuthConfig? config = LoadFile(legacyPath);
        if (config != null)
        {
            config.FilePath = path;
            try
            {
                config.Save();
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or DeskAuthException)
            {
                ShowStartupError($"Your WinAuth authenticators could not be copied to {path}\n\n{ex.Message}");
            }
        }
        return config;
    }

    private static DeskAuthConfig? LoadFile(string path)
    {
        try
        {
            return DeskAuthConfig.Load(path);
        }
        catch (PasswordRequiredException)
        {
            DeskAuthConfig? config = null;
            PasswordDialog.Ask(null, "Enter your password to open your authenticators.", password =>
            {
                try
                {
                    config = DeskAuthConfig.Load(path, password);
                    return null;
                }
                catch (BadPasswordException)
                {
                    return "Invalid password";
                }
            }, title: "DeskAuth");
            return config;
        }
        catch (BadPasswordException ex)
        {
            ShowStartupError($"{ex.Message}.\n\nThe file is protected so it can only be opened on the computer or Windows account that created it.\n\n{path}");
        }
        catch (Exception ex) when (ex is InvalidConfigException or IOException or UnauthorizedAccessException)
        {
            ShowStartupError($"Cannot open {path}\n\n{ex.Message}");
        }
        return null;
    }

    private void ListenForShowRequests(string name, MainWindow window)
    {
        _showEvent = new EventWaitHandle(false, EventResetMode.AutoReset, name);
        var thread = new Thread(() =>
        {
            while (_showEvent.WaitOne())
            {
                if (Dispatcher.HasShutdownStarted)
                {
                    return;
                }
                Dispatcher.BeginInvoke(window.ShowFromBackground);
            }
        })
        { IsBackground = true, Name = "Show requests" };
        thread.Start();
    }

    private static void ShowStartupError(string message) =>
        MessageBox.Show(message, "DeskAuth", MessageBoxButton.OK, MessageBoxImage.Error);

    private void OnUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        MessageBox.Show($"Something went wrong:\n\n{e.Exception.Message}", "DeskAuth", MessageBoxButton.OK, MessageBoxImage.Error);
        e.Handled = true;
    }
}
