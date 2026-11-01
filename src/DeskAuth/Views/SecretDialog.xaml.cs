// DeskAuth, based on WinAuth. Copyright (C) 2011 Colin Mackie and contributors.
// Licensed under the GNU General Public License v3 or later. See LICENSE.

using System.Text.RegularExpressions;
using System.Windows;
using DeskAuth.Core;
using DeskAuth.Services;

namespace DeskAuth.Views;

/// <summary>
/// Shows an authenticator's secret key and QR code so it can be added to another device
/// </summary>
public partial class SecretDialog : Window
{
    public SecretDialog(AuthenticatorEntry entry)
    {
        InitializeComponent();
        Title = entry.Name;
        string key = Base32.Encode(entry.Generator!.SecretKey);
        KeyField.Text = Regex.Replace(key, ".{4}(?!$)", "$0 ");
        // other apps don't understand DeskAuth's icon parameter
        LinkField.Text = OtpAuthUri.Format(entry, includeIcon: false);
        QrImage.Source = QrCodes.Encode(LinkField.Text, 440);
    }

    private void CopyKey_Click(object sender, RoutedEventArgs e) => SystemIntegration.CopyToClipboard(KeyField.Text.Replace(" ", string.Empty));

    private void CopyLink_Click(object sender, RoutedEventArgs e) => SystemIntegration.CopyToClipboard(LinkField.Text);
}
