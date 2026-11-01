// DeskAuth, based on WinAuth. Copyright (C) 2011 Colin Mackie and contributors.
// Licensed under the GNU General Public License v3 or later. See LICENSE.

using System.Windows;

namespace DeskAuth.Views;

/// <summary>
/// Asks for a line of text
/// </summary>
public partial class InputDialog : Window
{
    private InputDialog(string title, string prompt, string text)
    {
        InitializeComponent();
        Title = title;
        PromptText.Text = prompt;
        InputField.Text = text;
        InputField.SelectAll();
    }

    /// <returns>the trimmed text, or null if cancelled or empty</returns>
    public static string? Ask(Window owner, string title, string prompt, string text = "")
    {
        var dialog = new InputDialog(title, prompt, text) { Owner = owner };
        if (dialog.ShowDialog() != true)
        {
            return null;
        }
        string value = dialog.InputField.Text.Trim();
        return value.Length == 0 ? null : value;
    }

    private void OkButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
    }
}
