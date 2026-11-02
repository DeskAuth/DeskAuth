// DeskAuth, based on WinAuth.
// Copyright (C) 2010-2017 Colin Mackie (WinAuth).
// Copyright (C) 2026 Nick Morphis and contributors (DeskAuth, modified from WinAuth).
// Licensed under the GNU General Public License v3 or later. See LICENSE.

using System.Diagnostics;
using System.Windows;
using System.Windows.Navigation;

namespace DeskAuth.Views;

/// <summary>
/// Asks whether to send anonymous usage statistics. Neither choice is the default.
/// </summary>
public partial class UsageStatisticsDialog : Window
{
    public UsageStatisticsDialog()
    {
        InitializeComponent();
    }

    /// <summary>
    /// The user agreed; false if they declined or closed the dialog
    /// </summary>
    public bool Send { get; private set; }

    private void SendButton_Click(object sender, RoutedEventArgs e)
    {
        Send = true;
        DialogResult = true;
    }

    private void DontSendButton_Click(object sender, RoutedEventArgs e)
    {
        Send = false;
        DialogResult = true;
    }

    private void PrivacyLink_RequestNavigate(object sender, RequestNavigateEventArgs e)
    {
        Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
        e.Handled = true;
    }
}
