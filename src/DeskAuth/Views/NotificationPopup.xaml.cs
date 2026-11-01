// DeskAuth, based on WinAuth. Copyright (C) 2011 Colin Mackie and contributors.
// Licensed under the GNU General Public License v3 or later. See LICENSE.

using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using DeskAuth.Services;

namespace DeskAuth.Views;

/// <summary>
/// Small window above the notification area, used to show a code without opening DeskAuth
/// </summary>
public partial class NotificationPopup : Window
{
    private static NotificationPopup? _current;

    private readonly string? _copyText;

    private NotificationPopup(string title, string message, ImageSource? icon, string? copyText)
    {
        InitializeComponent();
        TitleText.Text = title;
        MessageText.Text = message;
        IconImage.Source = icon;
        IconImage.Visibility = icon == null ? Visibility.Collapsed : Visibility.Visible;
        _copyText = copyText;
        HintText.Visibility = copyText == null ? Visibility.Collapsed : Visibility.Visible;

        var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(10) };
        timer.Tick += (_, _) => Close();
        timer.Start();
        Closed += (_, _) => timer.Stop();
    }

    /// <summary>
    /// Show a notification, replacing any already showing
    /// </summary>
    /// <param name="copyText">text copied to the clipboard when clicked</param>
    public static void Show(string title, string message, ImageSource? icon = null, string? copyText = null)
    {
        _current?.Close();
        var popup = new NotificationPopup(title, message, icon, copyText);
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
    }
}
