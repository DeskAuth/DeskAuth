// DeskAuth, based on WinAuth. Copyright (C) 2011 Colin Mackie and contributors.
// Licensed under the GNU General Public License v3 or later. See LICENSE.

using System.Windows;

namespace DeskAuth.Views;

/// <summary>
/// Asks for a password, optionally with confirmation, and optionally checks it before closing
/// </summary>
public partial class PasswordDialog : Window
{
    private readonly Func<string, string?>? _validate;

    /// <param name="prompt">message shown above the password</param>
    /// <param name="validate">checks the password, returning an error message or null if it is correct</param>
    /// <param name="confirm">ask for the password twice, for a new password</param>
    /// <param name="allowRemove">show a Remove password button</param>
    public PasswordDialog(string prompt, Func<string, string?>? validate = null, bool confirm = false, bool allowRemove = false)
    {
        InitializeComponent();
        PromptText.Text = prompt;
        _validate = validate;
        ConfirmPanel.Visibility = confirm ? Visibility.Visible : Visibility.Collapsed;
        RemoveButton.Visibility = allowRemove ? Visibility.Visible : Visibility.Collapsed;
    }

    public string Password { get; private set; } = string.Empty;

    /// <summary>
    /// The Remove password button was clicked
    /// </summary>
    public bool RemovePassword { get; private set; }

    /// <summary>
    /// Show the dialog
    /// </summary>
    /// <returns>the password, or null if cancelled</returns>
    public static string? Ask(Window? owner, string prompt, Func<string, string?>? validate = null, string? title = null)
    {
        var dialog = new PasswordDialog(prompt, validate) { Owner = owner };
        if (title != null)
        {
            dialog.Title = title;
        }
        if (owner == null)
        {
            // shown at startup or from a hotkey, so make sure it isn't hidden behind other windows
            dialog.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            dialog.ShowInTaskbar = true;
            dialog.Topmost = true;
            dialog.Loaded += (_, _) => dialog.Activate();
        }
        return dialog.ShowDialog() == true ? dialog.Password : null;
    }

    private void OkButton_Click(object sender, RoutedEventArgs e)
    {
        string password = PasswordField.Password;
        if (password.Length == 0)
        {
            ShowError("Enter a password");
            return;
        }
        if (ConfirmPanel.IsVisible && password != ConfirmField.Password)
        {
            ShowError("The passwords don't match");
            return;
        }

        string? error = null;
        if (_validate != null)
        {
            using (new WaitCursor())
            {
                error = _validate(password);
            }
        }
        if (error != null)
        {
            ShowError(error);
            PasswordField.SelectAll();
            PasswordField.Focus();
            return;
        }

        Password = password;
        DialogResult = true;
    }

    private void RemoveButton_Click(object sender, RoutedEventArgs e)
    {
        RemovePassword = true;
        DialogResult = true;
    }

    private void ShowError(string message)
    {
        ErrorText.Text = message;
        ErrorText.Visibility = Visibility.Visible;
    }
}
