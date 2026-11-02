// DeskAuth, based on WinAuth.
// Copyright (C) 2010-2017 Colin Mackie (WinAuth).
// Copyright (C) 2026 Nick Morphis and contributors (DeskAuth, modified from WinAuth).
// Licensed under the GNU General Public License v3 or later. See LICENSE.

using System.Globalization;
using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace DeskAuth.Core;

/// <summary>
/// Application settings and authenticators, stored in deskauth.xml.
///
/// The file format is the one used by WinAuth, which DeskAuth is based on, so element names such as
/// "WinAuth" and "WinAuthAuthenticator" are kept for compatibility.
/// </summary>
public sealed class DeskAuthConfig
{
    public const string DefaultFileName = "deskauth.xml";

    /// <summary>
    /// File name used by WinAuth
    /// </summary>
    public const string LegacyFileName = "winauth.xml";

    /// <summary>
    /// Root element of the file, from WinAuth
    /// </summary>
    private const string RootElement = "WinAuth";

    /// <summary>
    /// Version written to the file. WinAuth 3 refuses to open files with a newer version.
    /// </summary>
    private const string FileVersion = "4.0";

    public List<AuthenticatorEntry> Authenticators { get; } = [];

    public bool AlwaysOnTop { get; set; }

    public bool UseTrayIcon { get; set; }

    public bool StartWithWindows { get; set; }

    /// <summary>
    /// Size the window to fit the authenticators
    /// </summary>
    public bool AutoSize { get; set; } = true;

    /// <summary>
    /// Look for a newer release when DeskAuth starts
    /// </summary>
    public bool CheckForUpdates { get; set; } = true;

    public int? Left { get; set; }

    public int? Top { get; set; }

    public int Width { get; set; }

    public int Height { get; set; }

    /// <summary>
    /// The user agreed to send anonymous usage statistics; null until they have been asked
    /// </summary>
    public bool? SendUsageStatistics { get; set; }

    /// <summary>
    /// Random ID sent with usage statistics, so repeat use from one copy of DeskAuth counts as one user.
    /// Created when the user agrees and discarded when they stop.
    /// </summary>
    public Guid? InstallId { get; set; }

    /// <summary>
    /// When usage statistics were last sent
    /// </summary>
    public DateTimeOffset? LastUsageReport { get; set; }

    /// <summary>
    /// Protection on the whole file
    /// </summary>
    public ProtectionTypes Protection { get; private set; }

    /// <summary>
    /// File password, kept to re-encrypt on save
    /// </summary>
    private string? Password { get; set; }

    private string? _filePath;

    /// <summary>
    /// File this config was loaded from or will be saved to
    /// </summary>
    public string? FilePath
    {
        get => _filePath;
        set
        {
            _filePath = value;
            IsReadOnly = File.Exists(value) && new FileInfo(value).IsReadOnly;
        }
    }

    /// <summary>
    /// The file is read-only, so changes can't be saved
    /// </summary>
    public bool IsReadOnly { get; private set; }

    /// <summary>
    /// Change the protection of the whole file
    /// </summary>
    /// <param name="password">new password, or null to keep the current one</param>
    public void SetProtection(ProtectionTypes protection, string? password)
    {
        password ??= Password;
        if (protection.HasFlag(ProtectionTypes.Password) && string.IsNullOrEmpty(password))
        {
            throw new PasswordRequiredException();
        }
        Protection = protection;
        Password = protection.HasFlag(ProtectionTypes.Password) ? password : null;
    }

    public bool IsPassword(string? password) => Password != null && string.Equals(Password, password, StringComparison.Ordinal);

    #region Load

    /// <summary>
    /// Load a config file
    /// </summary>
    /// <exception cref="PasswordRequiredException">file is password protected and no password given</exception>
    /// <exception cref="BadPasswordException">password is wrong, or file was protected by another Windows account</exception>
    /// <exception cref="InvalidConfigException">file cannot be read</exception>
    public static DeskAuthConfig Load(string path, string? password = null)
    {
        byte[] data = File.ReadAllBytes(path);
        // an interrupted save in WinAuth 3 could leave an empty file, so fall back to the backup
        if ((data.Length == 0 || data[0] == 0) && File.Exists(path + ".bak"))
        {
            data = File.ReadAllBytes(path + ".bak");
        }

        DeskAuthConfig config = Parse(data, password);
        config.FilePath = path;
        return config;
    }

    /// <summary>
    /// Read a config from its file contents
    /// </summary>
    public static DeskAuthConfig Parse(byte[] data, string? password = null)
    {
        XElement root = ParseXml(data);
        if (root.Name != RootElement)
        {
            throw new InvalidConfigException("This is not a DeskAuth or WinAuth file");
        }

        var config = new DeskAuthConfig();
        config.ReadElement(root, password);
        return config;
    }

    private void ReadElement(XElement element, string? password)
    {
        // files before WinAuth 3.2 encrypted the whole root element
        ProtectionTypes rootProtection = SecretProtector.DecodeTypes((string?)element.Attribute("encrypted"));
        if (rootProtection != ProtectionTypes.None)
        {
            ReadElement(Decrypt(element.Value, rootProtection, password), password);
            SetProtection(rootProtection, password);
            return;
        }

        foreach (XElement child in element.Elements())
        {
            switch (child.Name.LocalName)
            {
                case "config":
                    ReadElement(child, password);
                    break;
                case "data":
                    ProtectionTypes protection = SecretProtector.DecodeTypes((string?)child.Attribute("encrypted"));
                    if (protection == ProtectionTypes.None)
                    {
                        ReadElement(child, password);
                    }
                    else
                    {
                        ReadElement(Decrypt(child.Value, protection, password), password);
                        SetProtection(protection, password);
                    }
                    break;
                case "alwaysontop":
                    AlwaysOnTop = (bool)child;
                    break;
                case "usetrayicon":
                    UseTrayIcon = (bool)child;
                    break;
                case "startwithwindows":
                    StartWithWindows = (bool)child;
                    break;
                case "autosize":
                    AutoSize = (bool)child;
                    break;
                case "checkforupdates":
                    CheckForUpdates = (bool)child;
                    break;
                case "left":
                    Left = (int)child;
                    break;
                case "top":
                    Top = (int)child;
                    break;
                case "width":
                    Width = (int)child;
                    break;
                case "height":
                    Height = (int)child;
                    break;
                case "usagestatistics":
                    SendUsageStatistics = (bool)child;
                    break;
                case "installid":
                    if (Guid.TryParse(child.Value, out Guid installId))
                    {
                        InstallId = installId;
                    }
                    break;
                case "lastusagereport":
                    if (DateTimeOffset.TryParse(child.Value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out DateTimeOffset report))
                    {
                        LastUsageReport = report;
                    }
                    break;
                case "WinAuthAuthenticator":
                    Authenticators.Add(AuthenticatorEntry.FromXml(child));
                    break;
            }
        }
    }

    private static XElement Decrypt(string encrypted, ProtectionTypes protection, string? password)
    {
        string plainHex = SecretProtector.Decrypt(encrypted, protection, password);
        return ParseXml(Convert.FromHexString(plainHex));
    }

    private static XElement ParseXml(byte[] data)
    {
        try
        {
            using var stream = new MemoryStream(data);
            return XDocument.Load(stream).Root ?? throw new InvalidConfigException("The file is empty");
        }
        catch (XmlException ex)
        {
            throw new InvalidConfigException("The file is not valid XML: " + ex.Message, ex);
        }
    }

    #endregion

    #region Save

    /// <summary>
    /// Save to <see cref="FilePath"/>. The file is written to a temporary file first and the previous version kept as .bak.
    /// </summary>
    public void Save()
    {
        if (string.IsNullOrEmpty(FilePath))
        {
            throw new InvalidOperationException("No file path set");
        }
        if (IsReadOnly)
        {
            return;
        }

        byte[] data = Serialize();
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(FilePath))!);

        string tempFile = FilePath + ".tmp";
        File.WriteAllBytes(tempFile, data);
        if (!File.ReadAllBytes(tempFile).AsSpan().SequenceEqual(data))
        {
            File.Delete(tempFile);
            throw new IOException("Saved file did not verify: " + FilePath);
        }
        if (File.Exists(FilePath))
        {
            File.Copy(FilePath, FilePath + ".bak", overwrite: true);
        }
        File.Move(tempFile, FilePath, overwrite: true);
    }

    /// <summary>
    /// Get the file contents
    /// </summary>
    public byte[] Serialize()
    {
        var root = new XElement(RootElement,
            new XAttribute("version", FileVersion),
            new XElement("alwaysontop", AlwaysOnTop),
            new XElement("usetrayicon", UseTrayIcon),
            new XElement("startwithwindows", StartWithWindows),
            new XElement("autosize", AutoSize),
            new XElement("checkforupdates", CheckForUpdates));
        if (Left.HasValue && Top.HasValue)
        {
            root.Add(new XElement("left", Left.Value), new XElement("top", Top.Value));
        }
        root.Add(new XElement("width", Width), new XElement("height", Height));
        if (SendUsageStatistics.HasValue)
        {
            root.Add(new XElement("usagestatistics", SendUsageStatistics.Value));
        }
        if (InstallId.HasValue)
        {
            root.Add(new XElement("installid", InstallId.Value));
        }
        if (LastUsageReport.HasValue)
        {
            root.Add(new XElement("lastusagereport", LastUsageReport.Value.ToUniversalTime().ToString("o", CultureInfo.InvariantCulture)));
        }

        var authenticators = Authenticators.Select(a => a.ToXml());
        if (Protection == ProtectionTypes.None)
        {
            root.Add(authenticators);
        }
        else
        {
            byte[] plain = ToBytes(new XElement("config", authenticators));
            string encrypted = SecretProtector.Encrypt(Convert.ToHexString(plain), Protection, Password);
            root.Add(new XElement("data", new XAttribute("encrypted", SecretProtector.EncodeTypes(Protection)), encrypted));
        }
        return ToBytes(root);
    }

    private static byte[] ToBytes(XElement root)
    {
        using var stream = new MemoryStream();
        var settings = new XmlWriterSettings { Indent = true, Encoding = new UTF8Encoding(false) };
        using (var writer = XmlWriter.Create(stream, settings))
        {
            new XDocument(new XDeclaration("1.0", "utf-8", null), root).Save(writer);
        }
        return stream.ToArray();
    }

    #endregion

    /// <summary>
    /// Default config location: deskauth.xml (or WinAuth's winauth.xml) next to the program if it exists, which is
    /// portable mode, otherwise %APPDATA%\DeskAuth\deskauth.xml
    /// </summary>
    public static string GetDefaultPath()
    {
        foreach (string name in new[] { DefaultFileName, LegacyFileName })
        {
            string portable = Path.Combine(AppContext.BaseDirectory, name);
            if (File.Exists(portable))
            {
                return portable;
            }
        }
        return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "DeskAuth", DefaultFileName);
    }

    /// <summary>
    /// WinAuth's file in %APPDATA%\WinAuth, used to copy authenticators to DeskAuth the first time it runs
    /// </summary>
    /// <returns>the path, or null if there is no WinAuth file</returns>
    public static string? FindLegacyFile()
    {
        string path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "WinAuth", LegacyFileName);
        return File.Exists(path) ? path : null;
    }
}
