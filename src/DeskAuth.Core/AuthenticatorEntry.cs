// DeskAuth, based on WinAuth. Copyright (C) 2011 Colin Mackie and contributors.
// Licensed under the GNU General Public License v3 or later. See LICENSE.

using System.Globalization;
using System.Text;
using System.Xml.Linq;

namespace DeskAuth.Core;

public enum AuthenticatorKind
{
    /// <summary>Time-based code</summary>
    Totp,
    /// <summary>Counter-based code</summary>
    Hotp,
    /// <summary>A WinAuth 3 type that is no longer supported, e.g. Steam or Battle.net. Kept so it isn't lost on save.</summary>
    Unsupported,
}

/// <summary>
/// An authenticator in the config, with its display settings and (possibly password protected) secret.
/// </summary>
public sealed class AuthenticatorEntry
{
    // type names used by WinAuth 3, kept so files stay readable by older versions
    internal const string TotpTypeName = "WinAuth.GoogleAuthenticator";
    internal const string HotpTypeName = "WinAuth.HOTPAuthenticator";

    // WinAuth 3 types that were plain TOTP authenticators with a different icon
    private static readonly HashSet<string> TotpTypeNames = new(StringComparer.OrdinalIgnoreCase)
    {
        TotpTypeName,
        "WinAuth.MicrosoftAuthenticator",
        "WinAuth.OktaVerifyAuthenticator",
        "WinAuth.GuildWarsAuthenticator",
    };

    private string? _password;
    private bool _secretChanged;
    private XElement? _unsupportedXml;

    private AuthenticatorEntry(AuthenticatorKind kind)
    {
        Kind = kind;
    }

    public Guid Id { get; private set; } = Guid.NewGuid();

    public string Name { get; set; } = string.Empty;

    public DateTimeOffset Created { get; private set; } = DateTimeOffset.Now;

    public AuthenticatorKind Kind { get; }

    /// <summary>
    /// WinAuth 3 type of an authenticator read from a file, e.g. "MicrosoftAuthenticator". Used to pick a default icon.
    /// </summary>
    public string? LegacyType { get; private set; }

    /// <summary>
    /// Show the code continuously rather than on demand (always false for HOTP)
    /// </summary>
    public bool AutoRefresh { get; set; } = true;

    /// <summary>
    /// Copy each new code to the clipboard
    /// </summary>
    public bool CopyOnCode { get; set; }

    /// <summary>
    /// Icon: the name of a built-in icon (e.g. "GitHubIcon.png"), "base64:" followed by PNG data, or null for the default
    /// </summary>
    public string? Skin { get; set; }

    public HotKeySetting? HotKey { get; set; }

    /// <summary>
    /// Code generator; null while the secret is locked by a password or for unsupported types
    /// </summary>
    public OtpGenerator? Generator { get; private set; }

    /// <summary>
    /// Protection on this authenticator's secret, in addition to any protection on the whole file
    /// </summary>
    public ProtectionTypes SecretProtection { get; private set; }

    /// <summary>
    /// The protected secret as stored in the file
    /// </summary>
    private string? EncryptedSecret { get; set; }

    public bool IsLocked => Kind != AuthenticatorKind.Unsupported && Generator == null;

    public bool HasPassword => SecretProtection.HasFlag(ProtectionTypes.Password);

    public static AuthenticatorEntry CreateTotp(string name, byte[] secret, int digits = OtpGenerator.DefaultDigits,
        OtpAlgorithm algorithm = OtpAlgorithm.SHA1, int period = OtpGenerator.DefaultPeriod) =>
        new(AuthenticatorKind.Totp)
        {
            Name = name,
            Generator = new TotpGenerator(secret) { Digits = digits, Algorithm = algorithm, Period = period },
        };

    public static AuthenticatorEntry CreateHotp(string name, byte[] secret, long counter, int digits = OtpGenerator.DefaultDigits,
        OtpAlgorithm algorithm = OtpAlgorithm.SHA1) =>
        new(AuthenticatorKind.Hotp)
        {
            Name = name,
            AutoRefresh = false,
            Generator = new HotpGenerator(secret, counter) { Digits = digits, Algorithm = algorithm },
        };

    /// <summary>
    /// Get the current code. For HOTP this advances the counter, so the config should be saved afterwards.
    /// </summary>
    public string GetCode(DateTimeOffset now)
    {
        switch (Generator)
        {
            case TotpGenerator totp:
                return totp.GetCode(now);
            case HotpGenerator hotp:
                string code = hotp.NextCode();
                _secretChanged = true;
                return code;
            default:
                throw new InvalidOperationException(Kind == AuthenticatorKind.Unsupported
                    ? "This authenticator type is not supported"
                    : "Authenticator is locked");
        }
    }

    /// <summary>
    /// Decrypt a password-protected secret so codes can be generated
    /// </summary>
    /// <exception cref="BadPasswordException">password is wrong</exception>
    public void Unlock(string password)
    {
        if (!IsLocked || EncryptedSecret == null)
        {
            return;
        }
        string plainHex = SecretProtector.Decrypt(EncryptedSecret, SecretProtection, password);
        XElement data = ParseXml(Convert.FromHexString(plainHex));
        Generator = ReadSecret(data, Kind);
        _password = password;
        _secretChanged = false;
    }

    /// <summary>
    /// Forget the decrypted secret of a password-protected authenticator
    /// </summary>
    public void Lock()
    {
        if (!HasPassword || Generator == null)
        {
            return;
        }
        if (_secretChanged && _password != null)
        {
            EncryptedSecret = SecretProtector.Encrypt(SecretToHex(), SecretProtection, _password);
            _secretChanged = false;
        }
        Generator = null;
        _password = null;
    }

    /// <summary>
    /// Set or remove (null) a password on this authenticator. The authenticator must be unlocked.
    /// </summary>
    public void SetPassword(string? password)
    {
        if (Generator == null)
        {
            throw new InvalidOperationException("Authenticator must be unlocked to change its password");
        }
        if (string.IsNullOrEmpty(password))
        {
            SecretProtection = ProtectionTypes.None;
            EncryptedSecret = null;
            _password = null;
        }
        else
        {
            SecretProtection = ProtectionTypes.Password;
            EncryptedSecret = SecretProtector.Encrypt(SecretToHex(), SecretProtection, password);
            _password = password;
        }
        _secretChanged = false;
    }

    #region Xml

    internal static AuthenticatorEntry FromXml(XElement element)
    {
        string type = (string?)element.Attribute("type") ?? string.Empty;
        AuthenticatorKind kind = TotpTypeNames.Contains(type) ? AuthenticatorKind.Totp
            : string.Equals(type, HotpTypeName, StringComparison.OrdinalIgnoreCase) ? AuthenticatorKind.Hotp
            : AuthenticatorKind.Unsupported;

        var entry = new AuthenticatorEntry(kind)
        {
            Name = (string?)element.Element("name") ?? string.Empty,
            AutoRefresh = kind != AuthenticatorKind.Hotp && ((bool?)element.Element("autorefresh") ?? true),
            CopyOnCode = (bool?)element.Element("copyoncode") ?? false,
        };
        entry.LegacyType = type.StartsWith("WinAuth.", StringComparison.Ordinal) ? type["WinAuth.".Length..] : type;
        if (Guid.TryParse((string?)element.Attribute("id"), out Guid id))
        {
            entry.Id = id;
        }
        if (long.TryParse((string?)element.Element("created"), NumberStyles.Integer, CultureInfo.InvariantCulture, out long created) && created > 0)
        {
            entry.Created = DateTimeOffset.FromUnixTimeMilliseconds(created).ToLocalTime();
        }
        string? skin = (string?)element.Element("skin");
        entry.Skin = string.IsNullOrEmpty(skin) ? null : skin;
        if (element.Element("hotkey") is XElement hotkey)
        {
            entry.HotKey = HotKeySetting.FromXml(hotkey);
        }

        if (kind == AuthenticatorKind.Unsupported)
        {
            entry._unsupportedXml = new XElement(element);
            return entry;
        }

        XElement data = element.Element("authenticatordata")
            ?? throw new InvalidConfigException($"Authenticator '{entry.Name}' has no data");
        entry.SecretProtection = SecretProtector.DecodeTypes((string?)data.Attribute("encrypted"));
        if (entry.SecretProtection == ProtectionTypes.None)
        {
            entry.Generator = ReadSecret(data, kind);
        }
        else
        {
            entry.EncryptedSecret = data.Value.Trim();
            if (!entry.HasPassword)
            {
                // protected with only the Windows account, so it can be unlocked now
                string plainHex = SecretProtector.Decrypt(entry.EncryptedSecret, entry.SecretProtection, null);
                entry.Generator = ReadSecret(ParseXml(Convert.FromHexString(plainHex)), kind);
            }
        }
        return entry;
    }

    internal XElement ToXml()
    {
        if (_unsupportedXml != null)
        {
            // write back as read, with any rename
            var copy = new XElement(_unsupportedXml);
            copy.SetElementValue("name", Name);
            return copy;
        }

        var element = new XElement("WinAuthAuthenticator",
            new XAttribute("id", Id),
            new XAttribute("type", Kind == AuthenticatorKind.Hotp ? HotpTypeName : TotpTypeName),
            new XElement("name", Name),
            new XElement("created", Created.ToUnixTimeMilliseconds()),
            new XElement("autorefresh", AutoRefresh),
            new XElement("copyoncode", CopyOnCode),
            new XElement("skin", Skin ?? string.Empty));
        if (HotKey != null)
        {
            element.Add(HotKey.ToXml());
        }

        if (SecretProtection == ProtectionTypes.None)
        {
            element.Add(SecretToXml());
        }
        else
        {
            if (_secretChanged && Generator != null && (_password != null || !HasPassword))
            {
                EncryptedSecret = SecretProtector.Encrypt(SecretToHex(), SecretProtection, _password);
                _secretChanged = false;
            }
            element.Add(new XElement("authenticatordata",
                new XAttribute("encrypted", SecretProtector.EncodeTypes(SecretProtection)),
                EncryptedSecret));
        }
        return element;
    }

    /// <summary>
    /// Read the secret from an authenticatordata element. The secretdata value is
    /// "hexkey\tdigits\talgorithm\tperiod", with "|counter" appended for HOTP.
    /// </summary>
    private static OtpGenerator ReadSecret(XElement data, AuthenticatorKind kind)
    {
        string secretData = (string?)data.Element("secretdata")
            ?? throw new InvalidConfigException("Authenticator has no secret");
        string[] sections = secretData.Split('|');
        string[] parts = sections[0].Split('\t');

        byte[] key = Convert.FromHexString(parts[0]);
        OtpGenerator generator = kind == AuthenticatorKind.Hotp
            ? new HotpGenerator(key, sections.Length > 1 ? long.Parse(sections[1], CultureInfo.InvariantCulture) : 0)
            : new TotpGenerator(key);
        if (parts.Length > 1 && int.TryParse(parts[1], out int digits))
        {
            generator.Digits = digits;
        }
        if (parts.Length > 2 && Enum.TryParse(parts[2], true, out OtpAlgorithm algorithm))
        {
            generator.Algorithm = algorithm;
        }
        if (parts.Length > 3 && int.TryParse(parts[3], out int period) && period > 0)
        {
            generator.Period = period;
        }
        return generator;
    }

    private XElement SecretToXml()
    {
        OtpGenerator generator = Generator ?? throw new InvalidOperationException("Authenticator is locked");
        string secretData = string.Join('\t',
            Convert.ToHexString(generator.SecretKey),
            generator.Digits.ToString(CultureInfo.InvariantCulture),
            generator.Algorithm.ToString(),
            generator.Period.ToString(CultureInfo.InvariantCulture));
        if (generator is HotpGenerator hotp)
        {
            secretData += "|" + hotp.Counter.ToString(CultureInfo.InvariantCulture);
        }
        return new XElement("authenticatordata",
            new XElement("servertimediff", 0),
            new XElement("lastservertime", 0),
            new XElement("secretdata", secretData));
    }

    private string SecretToHex() => Convert.ToHexString(Encoding.UTF8.GetBytes(SecretToXml().ToString(SaveOptions.DisableFormatting)));

    private static XElement ParseXml(byte[] utf8)
    {
        using var stream = new MemoryStream(utf8);
        return XDocument.Load(stream).Root ?? throw new InvalidConfigException("Empty authenticator data");
    }

    #endregion
}
