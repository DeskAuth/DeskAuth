// DeskAuth, based on WinAuth. Copyright (C) 2011 Colin Mackie and contributors.
// Licensed under the GNU General Public License v3 or later. See LICENSE.

using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Win32;
using DeskAuth.Services;

namespace DeskAuth.Views;

/// <summary>
/// Choose an authenticator icon from a website, image address or file
/// </summary>
public partial class IconPickerDialog : Window
{
    private readonly CancellationTokenSource _cancel = new();

    internal IconPickerDialog(string? current)
    {
        InitializeComponent();
        Closed += (_, _) => _cancel.Cancel();
        CurrentIcon.Source = IconLibrary.ForSkin(current);
    }

    /// <summary>
    /// The chosen icon, or null for the DeskAuth logo
    /// </summary>
    public string? Skin { get; private set; }

    private async void GetIconButton_Click(object sender, RoutedEventArgs e)
    {
        if (UrlField.Text.Trim().Length == 0)
        {
            ShowUrlStatus("Enter a website or image address.", error: true);
            UrlField.Focus();
            return;
        }

        GetIconButton.IsEnabled = false;
        UrlField.IsEnabled = false;
        ShowUrlStatus("Getting icon...", error: false);
        try
        {
            using (new WaitCursor())
            {
                Skin = await IconDownloader.GetSkinAsync(UrlField.Text, IconLibrary.CreateCustomSkin, _cancel.Token);
            }
            DialogResult = true;
        }
        catch (IconDownloader.DownloadException ex)
        {
            ShowUrlStatus(ex.Message, error: true);
        }
        catch (OperationCanceledException)
        {
            // dialog closed
        }
        finally
        {
            if (IsLoaded)
            {
                GetIconButton.IsEnabled = true;
                UrlField.IsEnabled = true;
            }
        }
    }

    private void UrlField_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        // Enter gets the icon rather than closing the dialog
        if (e.Key == Key.Enter)
        {
            e.Handled = true;
            GetIconButton_Click(sender, e);
        }
    }

    private void ShowUrlStatus(string message, bool error)
    {
        UrlStatus.Text = message;
        UrlStatus.Opacity = error ? 1 : 0.8;
        if (error)
        {
            UrlStatus.SetResourceReference(TextBlock.ForegroundProperty, "SystemFillColorCriticalBrush");
        }
        else
        {
            UrlStatus.ClearValue(TextBlock.ForegroundProperty);
        }
    }

    private void DefaultButton_Click(object sender, RoutedEventArgs e)
    {
        Skin = null;
        DialogResult = true;
    }

    private void FileButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Choose icon image",
            Filter = "Images|*.png;*.jpg;*.jpeg;*.gif;*.bmp;*.ico|All files|*.*",
        };
        if (dialog.ShowDialog(this) != true)
        {
            return;
        }
        try
        {
            Skin = IconLibrary.CreateCustomSkin(dialog.FileName);
            DialogResult = true;
        }
        catch (Exception ex) when (ex is NotSupportedException or System.IO.IOException or ArgumentException)
        {
            MessageBox.Show(this, "Cannot open the image: " + ex.Message, Title, MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }
}
