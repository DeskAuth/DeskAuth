// DeskAuth, based on WinAuth.
// Copyright (C) 2010-2017 Colin Mackie (WinAuth).
// Copyright (C) 2026 Nick Morphis and contributors (DeskAuth, modified from WinAuth).
// Licensed under the GNU General Public License v3 or later. See LICENSE.

using System.Windows;
using System.Windows.Input;
using DeskAuth.Core;
using DeskAuth.Services;

namespace DeskAuth.Views;

/// <summary>
/// Choose a system-wide shortcut key for an authenticator
/// </summary>
public partial class HotKeyDialog : Window
{
    private HotKeyModifiers _modifiers;
    private int _virtualKey;

    public HotKeyDialog(HotKeySetting? current)
    {
        InitializeComponent();
        if (current != null)
        {
            _modifiers = current.Modifiers;
            _virtualKey = current.VirtualKey;
            KeyField.Text = HotKeyManager.Describe(current);
            CopyRadio.IsChecked = current.Action == HotKeyAction.Copy;
            NotifyRadio.IsChecked = current.Action == HotKeyAction.Notify;
            InjectRadio.IsChecked = current.Action == HotKeyAction.Inject;
        }
    }

    /// <summary>
    /// The chosen hotkey, or null to remove it
    /// </summary>
    public HotKeySetting? Result { get; private set; }

    private void KeyField_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        e.Handled = true;
        Key key = e.Key == Key.System ? e.SystemKey : e.Key;
        if (key is Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt or Key.LeftShift or Key.RightShift
            or Key.LWin or Key.RWin or Key.Tab or Key.Escape or Key.Enter)
        {
            if (key is Key.Tab or Key.Escape or Key.Enter)
            {
                e.Handled = false;
            }
            return;
        }

        var modifiers = HotKeyModifiers.None;
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control)) modifiers |= HotKeyModifiers.Control;
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Alt)) modifiers |= HotKeyModifiers.Alt;
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift)) modifiers |= HotKeyModifiers.Shift;
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Windows)) modifiers |= HotKeyModifiers.Windows;

        _modifiers = modifiers;
        _virtualKey = KeyInterop.VirtualKeyFromKey(key);
        KeyField.Text = HotKeyManager.Describe(new HotKeySetting { Modifiers = _modifiers, VirtualKey = _virtualKey });
        ErrorText.Visibility = Visibility.Collapsed;
    }

    private void OkButton_Click(object sender, RoutedEventArgs e)
    {
        if (_virtualKey == 0)
        {
            ShowError("Press a key combination");
            return;
        }
        bool functionKey = _virtualKey is >= 0x70 and <= 0x87;
        if (_modifiers == HotKeyModifiers.None && !functionKey)
        {
            ShowError("Include Ctrl, Alt or Win so the key doesn't stop working in other programs");
            return;
        }

        Result = new HotKeySetting
        {
            Modifiers = _modifiers,
            VirtualKey = _virtualKey,
            Action = CopyRadio.IsChecked == true ? HotKeyAction.Copy
                : NotifyRadio.IsChecked == true ? HotKeyAction.Notify
                : HotKeyAction.Inject,
        };
        DialogResult = true;
    }

    private void RemoveButton_Click(object sender, RoutedEventArgs e)
    {
        Result = null;
        DialogResult = true;
    }

    private void ShowError(string message)
    {
        ErrorText.Text = message;
        ErrorText.Visibility = Visibility.Visible;
    }
}
