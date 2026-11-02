// DeskAuth, based on WinAuth.
// Copyright (C) 2010-2017 Colin Mackie (WinAuth).
// Copyright (C) 2026 Nick Morphis and contributors (DeskAuth, modified from WinAuth).
// Licensed under the GNU General Public License v3 or later. See LICENSE.

using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using DeskAuth.Services;

namespace DeskAuth.Views;

/// <summary>
/// Small window above the notification area, used to show a code or a message without opening DeskAuth
/// </summary>
public partial class NotificationPopup : Window
{
    private static NotificationPopup? _current;

    private readonly string? _copyText;
    private readonly Action? _onClick;

    private NotificationPopup(string title, string message, ImageSource? icon, string? copyText, Action? onClick = null, string? hint = null)
    {
        InitializeComponent();
        TitleText.Text = title;
        MessageText.Text = message;
        IconImage.Source = icon;
        IconImage.Visibility = icon == null ? Visibility.Collapsed : Visibility.Visible;
        _copyText = copyText;
        _onClick = onClick;
        if (onClick != null)
        {
            // a message in the normal font, rather than a code
            MessageText.ClearValue(TextBlock.FontSizeProperty);
            MessageText.ClearValue(TextBlock.FontFamilyProperty);
            MessageText.Margin = new Thickness(0, 4, 0, 4);
        }
        if (hint != null)
        {
            HintText.Text = hint;
        }
        HintText.Visibility = copyText == null && hint == null ? Visibility.Collapsed : Visibility.Visible;

        var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(10) };
        timer.Tick += (_, _) => Close();
        timer.Start();
        Closed += (_, _) => timer.Stop();
    }

    /// <summary>
    /// Show a notification, replacing any already showing
    /// </summary>
    /// <param name="copyText">text copied to the clipboard when clicked</param>
    public static void Show(string title, string message, ImageSource? icon = null, string? copyText = null) =>
        Show(new NotificationPopup(title, message, icon, copyText));

    /// <summary>
    /// Show a message, replacing any notification already showing
    /// </summary>
    /// <param name="hint">what clicking does</param>
    /// <param name="onClick">run when the message is clicked</param>
    public static void ShowMessage(string title, string message, string hint, Action onClick) =>
        Show(new NotificationPopup(title, message, null, null, onClick, hint));

    private static void Show(NotificationPopup popup)
    {
        _current?.Close();
        popup.Loaded += (_, _) =>
        {
            Rect area = SystemParameters.WorkArea;
            popup.Left = area.Right - popup.ActualWidth - 12;
            popup.Top = area.Bottom - popup.ActualHeight - 12;
        };
        popup.Left = -10000;
        popup.Show();
        _current = popup;
        popup.Closed += (_, _) =>
        {
            if (_current == popup)
            {
                _current = null;
            }
        };
    }

    private void Window_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_copyText != null)
        {
            SystemIntegration.CopyToClipboard(_copyText);
        }
        Close();
        _onClick?.Invoke();
    }
}
