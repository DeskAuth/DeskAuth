// DeskAuth, based on WinAuth.
// Copyright (C) 2010-2017 Colin Mackie (WinAuth).
// Copyright (C) 2026 Nick Morphis and contributors (DeskAuth, modified from WinAuth).
// Licensed under the GNU General Public License v3 or later. See LICENSE.

using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using System.Web;

namespace DeskAuth.Core;

/// <summary>
/// Reads and writes otpauth:// URIs (the Key Uri Format used in QR codes and exports)
/// </summary>
public static partial class OtpAuthUri
{
    /// <summary>
    /// Create an authenticator from an otpauth:// URI
    /// </summary>
    /// <exception cref="FormatException">URI is invalid or not supported</exception>
    public static AuthenticatorEntry Parse(string text)
    {
        text = text.Trim();
        // a '#' in the label would otherwise be treated as a fragment
        int hash = text.IndexOf('#');
        int query = text.IndexOf('?');
        if (hash != -1 && hash < query)
        {
            text = text[..hash] + "%23" + text[(hash + 1)..];
        }

        if (!Uri.TryCreate(text, UriKind.Absolute, out Uri? uri) || uri.Scheme != "otpauth")
        {
            throw new FormatException("Not an otpauth:// link");
        }
        string type = uri.Host.ToLowerInvariant();
        if (type != "totp" && type != "hotp")
        {
            throw new FormatException("Only otpauth://totp and otpauth://hotp links are supported");
        }

        string label = Uri.UnescapeDataString(uri.AbsolutePath.TrimStart('/')).Replace('+', ' ');
        string issuer = string.Empty;
        int colon = label.IndexOf(':');
        if (colon != -1)
        {
            issuer = label[..colon].Trim();
            label = label[(colon + 1)..].Trim();
        }

        var parameters = HttpUtility.ParseQueryString(uri.Query);
        if (string.IsNullOrEmpty(issuer))
        {
            issuer = parameters["issuer"] ?? string.Empty;
        }
        if (!string.IsNullOrEmpty(parameters["deviceid"]) || string.Equals(issuer, "Steam", StringComparison.OrdinalIgnoreCase))
        {
            throw new FormatException("Steam authenticators are not supported");
        }

        string secret = parameters["secret"] ?? throw new FormatException("The link does not contain a secret");
        byte[] key = Base32.Decode(secret);
        if (key.Length == 0)
        {
            throw new FormatException("The link does not contain a secret");
        }

        int digits = ParseInt(parameters["digits"], OtpGenerator.DefaultDigits);
        int period = ParseInt(parameters["period"], OtpGenerator.DefaultPeriod);
        if (digits < 6 || digits > 10)
        {
            throw new FormatException("Codes must have between 6 and 10 digits");
        }
        OtpAlgorithm algorithm = OtpAlgorithm.SHA1;
        if (!string.IsNullOrEmpty(parameters["algorithm"]) && !Enum.TryParse(parameters["algorithm"], true, out algorithm))
        {
            throw new FormatException("Unsupported algorithm " + parameters["algorithm"]);
        }

        string name = (label.Length, issuer.Length) switch
        {
            ( > 0, > 0) when !string.Equals(label, issuer, StringComparison.OrdinalIgnoreCase) => $"{issuer} ({label})",
            ( > 0, _) => label,
            (_, > 0) => issuer,
            _ => "Imported",
        };

        AuthenticatorEntry entry;
        if (type == "hotp")
        {
            long counter = long.TryParse(parameters["counter"], NumberStyles.Integer, CultureInfo.InvariantCulture, out long c) ? c : 0;
            entry = AuthenticatorEntry.CreateHotp(name, key, counter, digits, algorithm);
        }
        else
        {
            entry = AuthenticatorEntry.CreateTotp(name, key, digits, algorithm, period);
        }

        // DeskAuth exports include the icon
        string? icon = parameters["icon"];
        if (!string.IsNullOrEmpty(icon))
        {
            entry.Skin = icon.StartsWith("base64:", StringComparison.Ordinal)
                ? "base64:" + Convert.ToBase64String(Base32.Decode(icon["base64:".Length..]))
                : icon + "Icon.png";
        }
        return entry;
    }

    /// <summary>
    /// Parse text with one otpauth:// link per line. Blank lines and lines starting with # are ignored.
    /// </summary>
    /// <exception cref="FormatException">a line is invalid; the message includes the line number</exception>
    public static List<AuthenticatorEntry> ParseLines(string text)
    {
        var entries = new List<AuthenticatorEntry>();
        int lineNumber = 0;
        using var reader = new StringReader(text);
        while (reader.ReadLine() is string line)
        {
            lineNumber++;
            line = line.Trim();
            if (line.Length == 0 || line.StartsWith('#'))
            {
                continue;
            }
            try
            {
                entries.Add(Parse(line));
            }
            catch (FormatException ex)
            {
                throw new FormatException($"Line {lineNumber}: {ex.Message}", ex);
            }
        }
        return entries;
    }

    /// <summary>
    /// Create an otpauth:// URI for an unlocked authenticator
    /// </summary>
    /// <param name="includeIcon">add DeskAuth's icon parameter (not understood by other apps)</param>
    public static string Format(AuthenticatorEntry entry, bool includeIcon = true)
    {
        OtpGenerator generator = entry.Generator ?? throw new InvalidOperationException("Authenticator is locked");

        // names created from links are "Issuer (account)"
        string label = entry.Name;
        string issuer = string.Empty;
        Match match = IssuerLabelRegex().Match(label);
        if (match.Success)
        {
            issuer = match.Groups[1].Value.Trim();
            label = match.Groups[2].Value + match.Groups[3].Value;
        }

        var uri = new StringBuilder("otpauth://");
        uri.Append(generator is HotpGenerator ? "hotp" : "totp").Append('/');
        if (issuer.Length != 0)
        {
            uri.Append(Uri.EscapeDataString(issuer)).Append(':');
        }
        uri.Append(Uri.EscapeDataString(label));
        uri.Append("?secret=").Append(Base32.Encode(generator.SecretKey));
        if (issuer.Length != 0)
        {
            uri.Append("&issuer=").Append(Uri.EscapeDataString(issuer));
        }
        uri.Append("&digits=").Append(generator.Digits);
        if (generator.Algorithm != OtpAlgorithm.SHA1)
        {
            uri.Append("&algorithm=").Append(generator.Algorithm);
        }
        if (generator is HotpGenerator hotp)
        {
            uri.Append("&counter=").Append(hotp.Counter);
        }
        else if (generator.Period != OtpGenerator.DefaultPeriod)
        {
            uri.Append("&period=").Append(generator.Period);
        }
        if (includeIcon && !string.IsNullOrEmpty(entry.Skin))
        {
            string icon = entry.Skin.StartsWith("base64:", StringComparison.Ordinal)
                ? "base64:" + Base32.Encode(Convert.FromBase64String(entry.Skin["base64:".Length..]))
                : entry.Skin.Replace("Icon.png", string.Empty, StringComparison.Ordinal);
            uri.Append("&icon=").Append(Uri.EscapeDataString(icon));
        }
        return uri.ToString();
    }

    private static int ParseInt(string? value, int defaultValue) =>
        int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int result) && result > 0 ? result : defaultValue;

    [GeneratedRegex(@"^([^\(]+)\s+\((.*?)\)(.*)$")]
    private static partial Regex IssuerLabelRegex();
}
