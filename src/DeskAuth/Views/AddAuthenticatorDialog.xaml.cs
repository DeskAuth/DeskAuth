// DeskAuth, based on WinAuth. Copyright (C) 2011 Colin Mackie and contributors.
// Licensed under the GNU General Public License v3 or later. See LICENSE.

using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Microsoft.Win32;
using DeskAuth.Core;
using DeskAuth.Services;

namespace DeskAuth.Views;

/// <summary>
/// Add a TOTP or HOTP authenticator from a secret key, otpauth:// link or QR code
/// </summary>
public partial class AddAuthenticatorDialog : Window
{
    private readonly DispatcherTimer _timer;

    // icon from an imported DeskAuth link
    private string? _skin;

    public AddAuthenticatorDialog()
    {
        InitializeComponent();
        _timer = new DispatcherTimer(TimeSpan.FromMilliseconds(500), DispatcherPriority.Normal, (_, _) => UpdateCode(), Dispatcher);
        Closed += (_, _) => _timer.Stop();
    }

    public AuthenticatorEntry? Result { get; private set; }

    private void SecretField_LostFocus(object sender, RoutedEventArgs e) => ApplyLink(SecretField.Text);

    private void SecretField_TextChanged(object sender, TextChangedEventArgs e)
    {
        // a pasted link fills in the other fields
        if (SecretField.Text.TrimStart().StartsWith("otpauth://", StringComparison.OrdinalIgnoreCase))
        {
            ApplyLink(SecretField.Text);
        }
        UpdateCode();
    }

    private void Type_Changed(object sender, RoutedEventArgs e)
    {
        if (PeriodField == null)
        {
            return; // during InitializeComponent
        }
        bool hotp = HotpRadio.IsChecked == true;
        PeriodLabel.Visibility = PeriodField.Visibility = hotp ? Visibility.Collapsed : Visibility.Visible;
        CounterLabel.Visibility = CounterField.Visibility = hotp ? Visibility.Visible : Visibility.Collapsed;
        if (hotp)
        {
            AdvancedExpander.IsExpanded = true;
        }
        UpdateCode();
    }

    private void Options_Changed(object sender, RoutedEventArgs e) => UpdateCode();

    private void PasteQrButton_Click(object sender, RoutedEventArgs e)
    {
        var image = Clipboard.ContainsImage() ? Clipboard.GetImage() : null;
        if (image == null)
        {
            ShowError("The clipboard doesn't contain an image. Copy the QR code (for example with Win+Shift+S) and try again.");
            return;
        }
        ApplyQrText(QrCodes.Decode(image));
    }

    private void OpenQrButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Open QR code image",
            Filter = "Images|*.png;*.jpg;*.jpeg;*.gif;*.bmp|All files|*.*",
        };
        if (dialog.ShowDialog(this) != true)
        {
            return;
        }
        try
        {
            ApplyQrText(QrCodes.DecodeFile(dialog.FileName));
        }
        catch (Exception ex) when (ex is NotSupportedException or System.IO.IOException or ArgumentException)
        {
            ShowError("Cannot open the image: " + ex.Message);
        }
    }

    private void ApplyQrText(string? text)
    {
        if (text == null)
        {
            ShowError("No QR code was found in the image.");
            return;
        }
        SecretField.Text = text;
        ApplyLink(text);
    }

    /// <summary>
    /// Fill in the fields from an otpauth:// link
    /// </summary>
    private void ApplyLink(string text)
    {
        if (!text.TrimStart().StartsWith("otpauth://", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }
        AuthenticatorEntry entry;
        try
        {
            entry = OtpAuthUri.Parse(text);
        }
        catch (FormatException ex)
        {
            ShowError(ex.Message);
            return;
        }

        var generator = entry.Generator!;
        if (NameField.Text.Trim().Length == 0)
        {
            NameField.Text = entry.Name;
        }
        _skin = entry.Skin;
        HotpRadio.IsChecked = generator is HotpGenerator;
        TotpRadio.IsChecked = generator is TotpGenerator;
        DigitsCombo.SelectedIndex = Math.Clamp(generator.Digits - 6, 0, 2);
        AlgorithmCombo.SelectedIndex = (int)generator.Algorithm;
        PeriodField.Text = generator.Period.ToString(CultureInfo.InvariantCulture);
        CounterField.Text = generator is HotpGenerator hotp ? hotp.Counter.ToString(CultureInfo.InvariantCulture) : "0";
        if (generator.Digits != 6 || generator.Algorithm != OtpAlgorithm.SHA1 || generator.Period != 30 || generator is HotpGenerator)
        {
            AdvancedExpander.IsExpanded = true;
        }
        // show just the key, so the link isn't parsed again
        SecretField.Text = Base32.Encode(generator.SecretKey);
        ErrorText.Visibility = Visibility.Collapsed;
        UpdateCode();
    }

    /// <summary>
    /// Build an authenticator from the fields
    /// </summary>
    /// <returns>the authenticator, or null with <paramref name="error"/> set</returns>
    private AuthenticatorEntry? Build(string name, out string? error)
    {
        error = null;
        string secret = SecretField.Text.Trim();
        if (secret.Length == 0)
        {
            error = "Enter the secret key";
            return null;
        }
        byte[] key;
        try
        {
            key = Base32.Decode(secret);
        }
        catch (FormatException)
        {
            error = "The secret key should only contain the letters A-Z and the digits 2-7";
            return null;
        }
        if (key.Length == 0)
        {
            error = "Enter the secret key";
            return null;
        }

        int digits = DigitsCombo.SelectedIndex + 6;
        var algorithm = (OtpAlgorithm)AlgorithmCombo.SelectedIndex;
        if (HotpRadio.IsChecked == true)
        {
            if (!long.TryParse(CounterField.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out long counter) || counter < 0)
            {
                error = "The counter must be a number";
                return null;
            }
            return AuthenticatorEntry.CreateHotp(name, key, counter, digits, algorithm);
        }

        if (!int.TryParse(PeriodField.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int period) || period < 1)
        {
            error = "The period must be a number of seconds";
            return null;
        }
        return AuthenticatorEntry.CreateTotp(name, key, digits, algorithm, period);
    }

    private void UpdateCode()
    {
        if (CodeText == null)
        {
            return; // during InitializeComponent
        }
        var entry = Build("check", out _);
        switch (entry?.Generator)
        {
            case TotpGenerator totp:
                string code = totp.GetCode(DateTimeOffset.Now);
                CodeText.Text = code.Insert(code.Length / 2, " ");
                _timer.Start();
                break;
            case HotpGenerator hotp:
                // the code the next click will produce, without using it up
                string next = hotp.ComputeCode(hotp.Counter + 1);
                CodeText.Text = next.Insert(next.Length / 2, " ");
                _timer.Stop();
                break;
            default:
                CodeText.Text = string.Empty;
                _timer.Stop();
                break;
        }
    }

    private void OkButton_Click(object sender, RoutedEventArgs e)
    {
        string name = NameField.Text.Trim();
        if (name.Length == 0)
        {
            ShowError("Enter a name for this authenticator");
            NameField.Focus();
            return;
        }
        var entry = Build(name, out string? error);
        if (entry == null)
        {
            ShowError(error!);
            SecretField.Focus();
            return;
        }
        entry.Skin = _skin;
        Result = entry;
        DialogResult = true;
    }

    private void ShowError(string message)
    {
        ErrorText.Text = message;
        ErrorText.Visibility = Visibility.Visible;
    }
}
