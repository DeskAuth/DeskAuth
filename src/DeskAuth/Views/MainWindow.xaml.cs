// DeskAuth, based on WinAuth. Copyright (C) 2011 Colin Mackie and contributors.
// Licensed under the GNU General Public License v3 or later. See LICENSE.

using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Win32;
using DeskAuth.Core;
using DeskAuth.Services;
using DeskAuth.ViewModels;

namespace DeskAuth.Views;

/// <summary>
/// The authenticator list
/// </summary>
public partial class MainWindow : Window
{
    private const double DefaultWidth = 420;

    private readonly DeskAuthConfig _config;
    private readonly ObservableCollection<AuthenticatorItem> _items = [];
    private readonly DispatcherTimer _timer;
    private readonly TrayIcon _tray = new();
    private HotKeyManager? _hotKeys;
    private bool _exiting;

    // drag to reorder
    private Point _dragStart;
    private AuthenticatorItem? _dragItem;

    internal MainWindow(DeskAuthConfig config)
    {
        InitializeComponent();
        _config = config;

        foreach (var entry in config.Authenticators)
        {
            AddItem(entry);
        }
        AuthenticatorList.ItemsSource = _items;
        _items.CollectionChanged += (_, _) => UpdateEmptyText();
        UpdateEmptyText();

        _tray.Click += (_, _) => ToggleFromTray();
        _tray.ContextMenuRequested += (_, _) => ShowTrayMenu();

        _timer = new DispatcherTimer(TimeSpan.FromMilliseconds(200), DispatcherPriority.Render, (_, _) => RefreshCodes(), Dispatcher);
        RefreshCodes();

        RestorePosition();
        ApplySettings();
        if (_config.IsReadOnly)
        {
            Title += " (read only)";
        }
    }

    /// <summary>
    /// Show the window, from the tray or another instance
    /// </summary>
    public void ShowFromBackground()
    {
        Show();
        if (WindowState == WindowState.Minimized)
        {
            WindowState = WindowState.Normal;
        }
        Activate();
    }

    /// <summary>
    /// Start in the tray, or minimized if there is no tray icon
    /// </summary>
    public void StartMinimized()
    {
        if (_config.UseTrayIcon)
        {
            // create the window handle so hotkeys work while hidden
            new WindowInteropHelper(this).EnsureHandle();
        }
        else
        {
            WindowState = WindowState.Minimized;
            Show();
        }
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        _hotKeys = new HotKeyManager(HwndSource.FromHwnd(new WindowInteropHelper(this).Handle));
        _hotKeys.HotKeyPressed += (_, entry) => _ = HandleHotKeyAsync(entry);
        RegisterHotKeys(showErrors: false);
        _timer.Start();
    }

    protected override void OnStateChanged(EventArgs e)
    {
        base.OnStateChanged(e);
        if (WindowState == WindowState.Minimized && _config.UseTrayIcon)
        {
            Hide();
        }
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        base.OnClosing(e);
        if (!_exiting && _config.UseTrayIcon)
        {
            // closing hides to the tray; Exit in the menu really exits
            e.Cancel = true;
            Hide();
            return;
        }
        SavePosition();
    }

    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);
        _timer.Stop();
        _hotKeys?.Dispose();
        _tray.Dispose();
        foreach (var item in _items)
        {
            item.Entry.Lock();
        }
        Save();
        Application.Current.Shutdown();
    }

    private void Exit()
    {
        _exiting = true;
        Close();
    }

    #region Items

    private AuthenticatorItem AddItem(AuthenticatorEntry entry)
    {
        var item = new AuthenticatorItem(entry);
        item.CodeChanged += (_, code) =>
        {
            if (item.Entry.CopyOnCode)
            {
                SystemIntegration.CopyToClipboard(code);
            }
        };
        _items.Add(item);
        return item;
    }

    private void RefreshCodes()
    {
        var now = DateTimeOffset.Now;
        foreach (var item in _items)
        {
            item.Refresh(now);
        }
    }

    private void UpdateEmptyText()
    {
        EmptyText.Visibility = _items.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        AuthenticatorList.Visibility = _items.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
    }

    /// <summary>
    /// Ask for an authenticator's password if it is locked
    /// </summary>
    /// <returns>false if cancelled</returns>
    private bool EnsureUnlocked(AuthenticatorItem item, Window? owner)
    {
        if (!item.Entry.IsLocked)
        {
            return true;
        }
        string? password = PasswordDialog.Ask(owner, $"Enter the password for \"{item.Name}\".", p =>
        {
            try
            {
                item.Entry.Unlock(p);
                return null;
            }
            catch (BadPasswordException)
            {
                return "Invalid password";
            }
        });
        return password != null;
    }

    /// <summary>
    /// Get the current code, asking for a password if needed
    /// </summary>
    /// <returns>the code, or null if cancelled or not supported</returns>
    private string? GetCode(AuthenticatorItem item, Window? owner)
    {
        if (!item.IsSupported || !EnsureUnlocked(item, owner))
        {
            return null;
        }
        string code = item.Reveal(DateTimeOffset.Now);
        if (item.Entry.Kind == AuthenticatorKind.Hotp)
        {
            Save();
        }
        return code;
    }

    private void CopyCode(AuthenticatorItem item, Window? owner)
    {
        string? code = GetCode(item, owner);
        if (code != null && !SystemIntegration.CopyToClipboard(code))
        {
            ShowError("The clipboard is in use by another program. Try again.");
        }
    }

    private AuthenticatorItem? ItemFromElement(DependencyObject? element)
    {
        var container = ItemsControl.ContainerFromElement(AuthenticatorList, element ?? AuthenticatorList) as ListBoxItem;
        return container?.DataContext as AuthenticatorItem;
    }

    private void RevealButton_Click(object sender, RoutedEventArgs e)
    {
        if (((FrameworkElement)sender).DataContext is AuthenticatorItem item)
        {
            GetCode(item, this);
        }
    }

    private void AuthenticatorList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is DependencyObject source && FindParent<Button>(source) == null
            && ItemFromElement(source) is AuthenticatorItem item)
        {
            CopyCode(item, this);
        }
    }

    private void AuthenticatorList_KeyDown(object sender, KeyEventArgs e)
    {
        if (AuthenticatorList.SelectedItem is not AuthenticatorItem item)
        {
            return;
        }
        if (e.Key == Key.Enter || (e.Key == Key.C && Keyboard.Modifiers == ModifierKeys.Control))
        {
            CopyCode(item, this);
            e.Handled = true;
        }
        else if (e.Key == Key.Delete)
        {
            Delete(item);
            e.Handled = true;
        }
        else if (e.Key == Key.F2)
        {
            Rename(item);
            e.Handled = true;
        }
    }

    #endregion

    #region Item menu

    private void AuthenticatorList_ContextMenuOpening(object sender, ContextMenuEventArgs e)
    {
        var item = ItemFromElement(e.OriginalSource as DependencyObject) ?? AuthenticatorList.SelectedItem as AuthenticatorItem;
        if (item == null)
        {
            e.Handled = true;
            return;
        }
        AuthenticatorList.SelectedItem = item;
        AuthenticatorList.ContextMenu = BuildItemMenu(item);
    }

    private ContextMenu BuildItemMenu(AuthenticatorItem item)
    {
        var entry = item.Entry;
        var menu = new ContextMenu();
        bool supported = item.IsSupported;

        if (supported)
        {
            menu.Items.Add(MenuItem("Show code", () => GetCode(item, this)));
            menu.Items.Add(MenuItem("Copy code", () => CopyCode(item, this), "Ctrl+C"));
            menu.Items.Add(new Separator());
        }
        menu.Items.Add(MenuItem("Rename...", () => Rename(item), "F2"));
        if (supported)
        {
            menu.Items.Add(MenuItem("Icon...", () => ChooseIcon(item)));
            menu.Items.Add(MenuItem(entry.HasPassword ? "Change or remove password..." : "Set password...", () => SetPassword(item)));
            menu.Items.Add(MenuItem("Show secret key...", () => ShowSecret(item)));
            menu.Items.Add(new Separator());
            if (entry.Kind == AuthenticatorKind.Totp)
            {
                menu.Items.Add(CheckItem("Show code automatically", entry.AutoRefresh && !entry.HasPassword, () =>
                {
                    entry.AutoRefresh = !entry.AutoRefresh;
                    SettingsChanged(item);
                }, enabled: !entry.HasPassword));
                menu.Items.Add(CheckItem("Copy new codes to clipboard", entry.CopyOnCode, () =>
                {
                    entry.CopyOnCode = !entry.CopyOnCode;
                    SettingsChanged(item);
                }, enabled: item.IsAutoRefresh));
            }
            string shortcut = entry.HotKey == null ? "Shortcut key..." : $"Shortcut key ({HotKeyManager.Describe(entry.HotKey)})...";
            menu.Items.Add(MenuItem(shortcut, () => SetHotKey(item)));
            menu.Items.Add(new Separator());
        }
        menu.Items.Add(MenuItem("Delete", () => Delete(item), "Del"));
        return menu;
    }

    private void SettingsChanged(AuthenticatorItem item)
    {
        item.SettingsChanged();
        item.Refresh(DateTimeOffset.Now);
        Save();
    }

    private void Rename(AuthenticatorItem item)
    {
        string? name = InputDialog.Ask(this, "Rename", "Name", item.Name);
        if (name != null)
        {
            item.Name = name;
            Save();
        }
    }

    private void ChooseIcon(AuthenticatorItem item)
    {
        var dialog = new IconPickerDialog(item.Entry.Skin) { Owner = this };
        if (dialog.ShowDialog() == true)
        {
            item.Entry.Skin = dialog.Skin;
            SettingsChanged(item);
        }
    }

    private void SetPassword(AuthenticatorItem item)
    {
        if (!EnsureUnlocked(item, this))
        {
            return;
        }
        bool hasPassword = item.Entry.HasPassword;
        var dialog = new PasswordDialog(
            hasPassword
                ? $"Enter a new password for \"{item.Name}\", or remove its password."
                : $"Enter a password for \"{item.Name}\". You will need it each time you show the code.",
            confirm: true, allowRemove: hasPassword)
        { Owner = this, Title = "Authenticator password" };
        if (dialog.ShowDialog() == true)
        {
            using (new WaitCursor())
            {
                item.Entry.SetPassword(dialog.RemovePassword ? null : dialog.Password);
            }
            if (!dialog.RemovePassword)
            {
                item.Entry.Lock();
            }
            SettingsChanged(item);
        }
        else
        {
            item.Entry.Lock();
        }
    }

    private void ShowSecret(AuthenticatorItem item)
    {
        if (!EnsureUnlocked(item, this))
        {
            return;
        }
        new SecretDialog(item.Entry) { Owner = this }.ShowDialog();
        item.Entry.Lock();
        item.Refresh(DateTimeOffset.Now);
    }

    private void SetHotKey(AuthenticatorItem item)
    {
        var dialog = new HotKeyDialog(item.Entry.HotKey) { Owner = this };
        if (dialog.ShowDialog() == true)
        {
            item.Entry.HotKey = dialog.Result;
            Save();
            RegisterHotKeys(showErrors: true);
        }
    }

    private void Delete(AuthenticatorItem item)
    {
        if (MessageBox.Show(this,
            $"Delete \"{item.Name}\"?\n\nMake sure you have turned off two-factor authentication on the website, or have another way to sign in, before deleting it.",
            "Delete authenticator", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No) != MessageBoxResult.Yes)
        {
            return;
        }
        _items.Remove(item);
        _config.Authenticators.Remove(item.Entry);
        Save();
        RegisterHotKeys(showErrors: false);
        ApplySize();
    }

    #endregion

    #region Drag to reorder

    private void AuthenticatorList_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _dragStart = e.GetPosition(AuthenticatorList);
        _dragItem = e.OriginalSource is DependencyObject source && FindParent<Button>(source) == null
            ? ItemFromElement(source)
            : null;
    }

    private void AuthenticatorList_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (_dragItem == null || e.LeftButton != MouseButtonState.Pressed)
        {
            return;
        }
        Vector moved = e.GetPosition(AuthenticatorList) - _dragStart;
        if (Math.Abs(moved.Y) > SystemParameters.MinimumVerticalDragDistance)
        {
            var item = _dragItem;
            _dragItem = null;
            DragDrop.DoDragDrop(AuthenticatorList, item, DragDropEffects.Move);
        }
    }

    private void AuthenticatorList_DragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(typeof(AuthenticatorItem)) ? DragDropEffects.Move : DragDropEffects.None;
        e.Handled = true;
    }

    private void AuthenticatorList_Drop(object sender, DragEventArgs e)
    {
        if (e.Data.GetData(typeof(AuthenticatorItem)) is not AuthenticatorItem dragged)
        {
            return;
        }
        var target = ItemFromElement(e.OriginalSource as DependencyObject);
        int from = _items.IndexOf(dragged);
        int to = target == null ? _items.Count - 1 : _items.IndexOf(target);
        if (from < 0 || to < 0 || from == to)
        {
            return;
        }
        _items.Move(from, to);
        _config.Authenticators.Remove(dragged.Entry);
        _config.Authenticators.Insert(to, dragged.Entry);
        AuthenticatorList.SelectedItem = dragged;
        Save();
    }

    #endregion

    #region Add, import and export

    private void AddButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new AddAuthenticatorDialog { Owner = this };
        if (dialog.ShowDialog() == true && dialog.Result != null)
        {
            AddAuthenticators([dialog.Result]);
        }
    }

    private void AddAuthenticators(IReadOnlyList<AuthenticatorEntry> entries)
    {
        bool first = _config.Authenticators.Count == 0;
        foreach (var entry in entries)
        {
            _config.Authenticators.Add(entry);
            AddItem(entry).Refresh(DateTimeOffset.Now);
        }
        AuthenticatorList.ScrollIntoView(_items[^1]);
        ApplySize();

        // suggest protection when the first authenticator is added
        if (first && _config.Protection == ProtectionTypes.None)
        {
            ChangeProtection(askCurrentPassword: false);
        }
        Save();
    }

    private void Import()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Import authenticators",
            Filter = "Text files (*.txt)|*.txt|All files|*.*",
        };
        if (dialog.ShowDialog(this) != true)
        {
            return;
        }
        try
        {
            var entries = OtpAuthUri.ParseLines(File.ReadAllText(dialog.FileName));
            if (entries.Count == 0)
            {
                ShowError("The file does not contain any otpauth:// links.");
                return;
            }
            AddAuthenticators(entries);
            MessageBox.Show(this, $"Imported {entries.Count} authenticator{(entries.Count == 1 ? "" : "s")}.", Title,
                MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex) when (ex is FormatException or IOException or UnauthorizedAccessException)
        {
            ShowError("Cannot import the file. " + ex.Message);
        }
    }

    private void Export()
    {
        var supported = _items.Where(i => i.IsSupported).ToList();
        if (supported.Count == 0)
        {
            ShowError("There are no authenticators to export.");
            return;
        }
        if (MessageBox.Show(this,
            "The export file contains your secret keys as plain text. Anyone who reads it can generate your codes.\n\nStore it somewhere safe, or delete it once you've used it.",
            "Export", MessageBoxButton.OKCancel, MessageBoxImage.Warning) != MessageBoxResult.OK)
        {
            return;
        }
        var dialog = new SaveFileDialog
        {
            Title = "Export authenticators",
            FileName = "deskauth-export.txt",
            Filter = "Text files (*.txt)|*.txt",
        };
        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        var lines = new StringBuilder();
        int skipped = 0;
        foreach (var item in supported)
        {
            if (!EnsureUnlocked(item, this))
            {
                skipped++;
                continue;
            }
            lines.AppendLine(OtpAuthUri.Format(item.Entry));
            item.Entry.Lock();
        }
        try
        {
            File.WriteAllText(dialog.FileName, lines.ToString(), new UTF8Encoding(false));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            ShowError("Cannot save the file. " + ex.Message);
            return;
        }
        if (skipped > 0)
        {
            MessageBox.Show(this, $"{skipped} password-protected authenticator{(skipped == 1 ? " was" : "s were")} not exported.",
                Title, MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    #endregion

    #region Options

    private void OptionsButton_Click(object sender, RoutedEventArgs e)
    {
        var menu = new ContextMenu
        {
            PlacementTarget = OptionsButton,
            Placement = System.Windows.Controls.Primitives.PlacementMode.Top,
        };
        if (!_config.IsReadOnly)
        {
            menu.Items.Add(MenuItem("Change protection...", () => ChangeProtection(askCurrentPassword: true)));
            menu.Items.Add(new Separator());
        }
        menu.Items.Add(CheckItem("Start with Windows", _config.StartWithWindows, () =>
        {
            _config.StartWithWindows = !_config.StartWithWindows;
            SystemIntegration.SetStartWithWindows(_config.StartWithWindows);
            Save();
        }));
        menu.Items.Add(CheckItem("Always on top", _config.AlwaysOnTop, () =>
        {
            _config.AlwaysOnTop = !_config.AlwaysOnTop;
            ApplySettings();
            Save();
        }));
        menu.Items.Add(CheckItem("Use system tray icon", _config.UseTrayIcon, () =>
        {
            _config.UseTrayIcon = !_config.UseTrayIcon;
            ApplySettings();
            Save();
        }));
        menu.Items.Add(CheckItem("Size to fit authenticators", _config.AutoSize, () =>
        {
            _config.AutoSize = !_config.AutoSize;
            ApplySettings();
            Save();
        }));
        menu.Items.Add(new Separator());
        if (!_config.IsReadOnly)
        {
            menu.Items.Add(MenuItem("Import...", Import));
        }
        menu.Items.Add(MenuItem("Export...", Export));
        menu.Items.Add(new Separator());
        menu.Items.Add(MenuItem("About", ShowAbout));
        menu.Items.Add(MenuItem("Exit", Exit));
        menu.IsOpen = true;
    }

    private void ChangeProtection(bool askCurrentPassword)
    {
        if (askCurrentPassword && _config.Protection.HasFlag(ProtectionTypes.Password)
            && PasswordDialog.Ask(this, "Enter your current password.", p => _config.IsPassword(p) ? null : "Invalid password") == null)
        {
            return;
        }
        var dialog = new ProtectionDialog(_config.Protection) { Owner = this };
        if (dialog.ShowDialog() == true)
        {
            _config.SetProtection(dialog.Protection, dialog.Password);
            Save();
        }
    }

    private void ShowAbout()
    {
        var version = typeof(MainWindow).Assembly.GetName().Version;
        MessageBox.Show(this,
            $"DeskAuth {version?.ToString(3)}\n\n" +
            "Authenticator for Windows.\n\n" +
            "Copyright (C) 2011 Colin Mackie and contributors.\n" +
            "Distributed under the GNU General Public License v3.\n\n" +
            "https://github.com/DeskAuth/DeskAuth\n\n" +
            "Config file:\n" + _config.FilePath,
            "About DeskAuth", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    /// <summary>
    /// Apply window settings from the config
    /// </summary>
    private void ApplySettings()
    {
        Topmost = _config.AlwaysOnTop;
        _tray.Visible = _config.UseTrayIcon;
        ShowInTaskbar = true;
        ApplySize();
    }

    private void ApplySize()
    {
        if (_config.AutoSize)
        {
            // fit the list, up to most of the screen height
            MaxHeight = SystemParameters.WorkArea.Height * 0.8;
            SizeToContent = SizeToContent.Height;
            ResizeMode = ResizeMode.CanMinimize;
        }
        else
        {
            MaxHeight = double.PositiveInfinity;
            SizeToContent = SizeToContent.Manual;
            ResizeMode = ResizeMode.CanResize;
            if (_config.Height > 0)
            {
                Height = _config.Height;
            }
        }
    }

    private void RestorePosition()
    {
        Width = _config.Width > 0 ? _config.Width : DefaultWidth;
        if (_config.Left is int left && _config.Top is int top
            && left >= SystemParameters.VirtualScreenLeft && top >= SystemParameters.VirtualScreenTop
            && left + 100 < SystemParameters.VirtualScreenLeft + SystemParameters.VirtualScreenWidth
            && top + 100 < SystemParameters.VirtualScreenTop + SystemParameters.VirtualScreenHeight)
        {
            WindowStartupLocation = WindowStartupLocation.Manual;
            Left = left;
            Top = top;
        }
        else
        {
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
        }
    }

    private void SavePosition()
    {
        var bounds = WindowState == WindowState.Normal ? new Rect(Left, Top, ActualWidth, ActualHeight) : RestoreBounds;
        if (bounds.IsEmpty || double.IsNaN(bounds.Left))
        {
            return;
        }
        _config.Left = (int)bounds.Left;
        _config.Top = (int)bounds.Top;
        _config.Width = (int)bounds.Width;
        _config.Height = _config.AutoSize ? _config.Height : (int)bounds.Height;
    }

    #endregion

    #region Tray and hotkeys

    private void ToggleFromTray()
    {
        if (IsVisible && WindowState != WindowState.Minimized && IsActive)
        {
            Hide();
        }
        else
        {
            ShowFromBackground();
        }
    }

    private void ShowTrayMenu()
    {
        var menu = new ContextMenu { Placement = System.Windows.Controls.Primitives.PlacementMode.MousePoint };
        menu.Items.Add(MenuItem("Open DeskAuth", ShowFromBackground));
        var supported = _items.Where(i => i.IsSupported).ToList();
        if (supported.Count > 0)
        {
            menu.Items.Add(new Separator());
            foreach (var item in supported)
            {
                var menuItem = MenuItem(item.Name, () => NotifyCode(item, copy: true));
                menuItem.Icon = new Image { Source = item.Icon, Width = 16, Height = 16 };
                menuItem.ToolTip = "Copy code";
                menu.Items.Add(menuItem);
            }
        }
        menu.Items.Add(new Separator());
        menu.Items.Add(MenuItem("Exit", Exit));

        // the menu only closes on clicking elsewhere if one of our windows is in the foreground
        NativeMethods.SetForegroundWindow(_tray.Handle);
        menu.IsOpen = true;
    }

    /// <summary>
    /// Show a code in a notification, optionally copying it
    /// </summary>
    private void NotifyCode(AuthenticatorItem item, bool copy)
    {
        string? code = GetCode(item, IsVisible ? this : null);
        if (code == null)
        {
            return;
        }
        if (copy)
        {
            SystemIntegration.CopyToClipboard(code);
        }
        string display = code.Length > 5 ? code.Insert(code.Length / 2, " ") : code;
        NotificationPopup.Show(item.Name, copy ? display + "  copied" : display, item.Icon, copy ? null : code);
    }

    private void RegisterHotKeys(bool showErrors)
    {
        if (_hotKeys == null)
        {
            return;
        }
        var failed = _hotKeys.Register(_items.Where(i => i.IsSupported).Select(i => i.Entry));
        if (showErrors && failed.Count > 0)
        {
            ShowError("These shortcut keys are already in use by another program:\n\n"
                + string.Join("\n", failed.Select(f => $"{HotKeyManager.Describe(f.HotKey!)} ({f.Name})")));
        }
    }

    private async Task HandleHotKeyAsync(AuthenticatorEntry entry)
    {
        var item = _items.FirstOrDefault(i => i.Entry == entry);
        if (item == null || entry.HotKey == null)
        {
            return;
        }

        switch (entry.HotKey.Action)
        {
            case HotKeyAction.Copy:
                NotifyCode(item, copy: true);
                break;
            case HotKeyAction.Notify:
                NotifyCode(item, copy: false);
                break;
            default:
                // keep the window the user is typing into, in case we have to ask for a password
                IntPtr target = NativeMethods.GetForegroundWindow();
                string? code = GetCode(item, null);
                if (code == null)
                {
                    return;
                }
                NativeMethods.SetForegroundWindow(target);
                if (await KeyboardTyper.WaitForModifiersReleasedAsync(TimeSpan.FromSeconds(5)))
                {
                    KeyboardTyper.Type(code);
                }
                break;
        }
    }

    #endregion

    #region Helpers

    private void Save()
    {
        if (_config.IsReadOnly)
        {
            return;
        }
        try
        {
            _config.Save();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or DeskAuthException)
        {
            ShowError("Your changes could not be saved. " + ex.Message);
        }
    }

    private void ShowError(string message)
    {
        if (IsVisible)
        {
            MessageBox.Show(this, message, "DeskAuth", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        else
        {
            MessageBox.Show(message, "DeskAuth", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private static MenuItem MenuItem(string header, Action action, string? gesture = null)
    {
        var item = new MenuItem { Header = header, InputGestureText = gesture ?? string.Empty };
        item.Click += (_, _) => action();
        return item;
    }

    private static MenuItem CheckItem(string header, bool isChecked, Action action, bool enabled = true)
    {
        var item = MenuItem(header, action);
        item.IsCheckable = true;
        item.IsChecked = isChecked;
        item.IsEnabled = enabled;
        return item;
    }

    private static T? FindParent<T>(DependencyObject? element) where T : DependencyObject
    {
        while (element != null && element is not T)
        {
            element = element is Visual or System.Windows.Media.Media3D.Visual3D
                ? VisualTreeHelper.GetParent(element)
                : LogicalTreeHelper.GetParent(element);
        }
        return element as T;
    }

    #endregion
}
