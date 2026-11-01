// DeskAuth, based on WinAuth.
// Copyright (C) 2010-2017 Colin Mackie (WinAuth).
// Copyright (C) 2026 Nick Morphis and contributors (DeskAuth, modified from WinAuth).
// Licensed under the GNU General Public License v3 or later. See LICENSE.

using System.Windows;
using DeskAuth.Core;

namespace DeskAuth.Views;

/// <summary>
/// Choose how the config file is protected
/// </summary>
public partial class ProtectionDialog : Window
{
    // shown in place of the current password, which we don't display
    private const string ExistingPassword = "\u0001\u0002existing";

    public ProtectionDialog(ProtectionTypes current)
    {
        InitializeComponent();

        PasswordCheck.IsChecked = current.HasFlag(ProtectionTypes.Password);
        if (current.HasFlag(ProtectionTypes.Password))
        {
            PasswordField.Password = ExistingPassword;
            ConfirmField.Password = ExistingPassword;
        }
        MachineCheck.IsChecked = current.HasFlag(ProtectionTypes.Machine) || current.HasFlag(ProtectionTypes.User);
        UserCheck.IsChecked = current.HasFlag(ProtectionTypes.User);

        Loaded += (_, _) => (PasswordCheck.IsChecked == true ? (UIElement)PasswordField : PasswordCheck).Focus();
    }

    public ProtectionTypes Protection { get; private set; }

    /// <summary>
    /// New password, or null to keep the existing one
    /// </summary>
    public string? Password { get; private set; }

    private void PasswordCheck_Changed(object sender, RoutedEventArgs e)
    {
        PasswordFields.IsEnabled = PasswordCheck.IsChecked == true;
        if (PasswordFields.IsEnabled)
        {
            PasswordField.Focus();
        }
    }

    private void MachineCheck_Changed(object sender, RoutedEventArgs e)
    {
        UserCheck.IsEnabled = MachineCheck.IsChecked == true;
        if (!UserCheck.IsEnabled)
        {
            UserCheck.IsChecked = false;
        }
    }

    private void OkButton_Click(object sender, RoutedEventArgs e)
    {
        var protection = ProtectionTypes.None;
        if (PasswordCheck.IsChecked == true)
        {
            if (PasswordField.Password.Length == 0)
            {
                ShowError("Enter a password");
                return;
            }
            if (PasswordField.Password != ConfirmField.Password)
            {
                ShowError("The passwords don't match");
                return;
            }
            protection |= ProtectionTypes.Password;
            Password = PasswordField.Password == ExistingPassword ? null : PasswordField.Password;
        }
        if (UserCheck.IsChecked == true)
        {
            protection |= ProtectionTypes.User;
        }
        else if (MachineCheck.IsChecked == true)
        {
            protection |= ProtectionTypes.Machine;
        }

        Protection = protection;
        DialogResult = true;
    }

    private void ShowError(string message)
    {
        ErrorText.Text = message;
        ErrorText.Visibility = Visibility.Visible;
    }
}
