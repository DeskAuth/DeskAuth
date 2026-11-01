// DeskAuth, based on WinAuth.
// Copyright (C) 2010-2017 Colin Mackie (WinAuth).
// Copyright (C) 2026 Nick Morphis and contributors (DeskAuth, modified from WinAuth).
// Licensed under the GNU General Public License v3 or later. See LICENSE.

using System.Security.Cryptography;
using System.Text;

namespace DeskAuth.Core;

/// <summary>
/// How a config or authenticator secret is protected. Values match the legacy WinAuth flags.
/// </summary>
[Flags]
public enum ProtectionTypes
{
    None = 0,
    /// <summary>Encrypted with a user-chosen password</summary>
    Password = 1,
    /// <summary>Encrypted with DPAPI for the current Windows user</summary>
    User = 2,
    /// <summary>Encrypted with DPAPI for the local machine</summary>
    Machine = 4,
}

/// <summary>
/// Encrypts and decrypts protected data. Data is passed as hex strings, matching the config file format.
///
/// Encrypted data written by this version starts with the "WINAUTH4" marker and uses PBKDF2-SHA256 with
/// AES-256-GCM for password protection. Data written by WinAuth 3.x ("WINAUTH3" marker, or no marker) used
/// PBKDF2-SHA1 with Blowfish and can still be decrypted.
/// </summary>
public static class SecretProtector
{
    private static readonly string Header3 = Convert.ToHexString(Encoding.UTF8.GetBytes("WINAUTH3"));
    private static readonly string Header4 = Convert.ToHexString(Encoding.UTF8.GetBytes("WINAUTH4"));

    // current password encryption
    private const int SaltSize = 16;
    private const int NonceSize = 12;
    private const int TagSize = 16;
    private const int KeySize = 32;
    private const int Pbkdf2Iterations = 600_000;

    // WinAuth 3.x password encryption
    private const int LegacySaltSize = 8;
    private const int LegacyPbkdf2Iterations = 2000;
    private const int LegacyKeySize = 256;

    /// <summary>
    /// Encode protection types for the "encrypted" config attribute
    /// </summary>
    public static string EncodeTypes(ProtectionTypes types)
    {
        var sb = new StringBuilder();
        if (types.HasFlag(ProtectionTypes.Password)) sb.Append('y');
        if (types.HasFlag(ProtectionTypes.User)) sb.Append('u');
        if (types.HasFlag(ProtectionTypes.Machine)) sb.Append('m');
        return sb.ToString();
    }

    /// <summary>
    /// Decode the "encrypted" config attribute
    /// </summary>
    public static ProtectionTypes DecodeTypes(string? encoded)
    {
        var types = ProtectionTypes.None;
        foreach (char c in encoded ?? string.Empty)
        {
            types |= c switch
            {
                'y' => ProtectionTypes.Password,
                'u' => ProtectionTypes.User,
                'm' => ProtectionTypes.Machine,
                'a' or 'b' => throw new InvalidConfigException(
                    "This file is protected with a YubiKey, which is no longer supported. Remove the YubiKey protection using WinAuth 3.x first."),
                _ => ProtectionTypes.None,
            };
        }
        return types;
    }

    /// <summary>
    /// Encrypt hex-encoded data with the given protection
    /// </summary>
    public static string Encrypt(string hexData, ProtectionTypes types, string? password)
    {
        byte[] data = Convert.FromHexString(hexData);
        if (types.HasFlag(ProtectionTypes.Password))
        {
            if (string.IsNullOrEmpty(password))
            {
                throw new PasswordRequiredException();
            }
            data = EncryptWithPassword(data, password);
        }
        if (types.HasFlag(ProtectionTypes.User))
        {
            data = ProtectedData.Protect(data, null, DataProtectionScope.CurrentUser);
        }
        if (types.HasFlag(ProtectionTypes.Machine))
        {
            data = ProtectedData.Protect(data, null, DataProtectionScope.LocalMachine);
        }
        return Header4 + Convert.ToHexString(data);
    }

    /// <summary>
    /// Decrypt data to its hex-encoded plain form
    /// </summary>
    /// <exception cref="PasswordRequiredException">data is password protected and no password given</exception>
    /// <exception cref="BadPasswordException">data could not be decrypted</exception>
    public static string Decrypt(string encrypted, ProtectionTypes types, string? password)
    {
        encrypted = encrypted.Trim();
        if (encrypted.StartsWith(Header4, StringComparison.Ordinal))
        {
            return DecryptCurrent(encrypted[Header4.Length..], types, password);
        }
        if (encrypted.StartsWith(Header3, StringComparison.Ordinal))
        {
            return DecryptLegacyWithHash(encrypted[Header3.Length..], types, password);
        }
        return DecryptLegacy(encrypted, types, password);
    }

    private static string DecryptCurrent(string hex, ProtectionTypes types, string? password)
    {
        byte[] data = Convert.FromHexString(hex);
        try
        {
            data = UnprotectWindows(data, types);
        }
        catch (CryptographicException ex)
        {
            throw new BadPasswordException("Cannot decrypt using this Windows account or computer", ex);
        }
        if (types.HasFlag(ProtectionTypes.Password))
        {
            if (string.IsNullOrEmpty(password))
            {
                throw new PasswordRequiredException();
            }
            data = DecryptWithPassword(data, password);
        }
        return Convert.ToHexString(data);
    }

    private static byte[] UnprotectWindows(byte[] data, ProtectionTypes types)
    {
        // reverse order of encryption
        if (types.HasFlag(ProtectionTypes.Machine))
        {
            data = ProtectedData.Unprotect(data, null, DataProtectionScope.LocalMachine);
        }
        if (types.HasFlag(ProtectionTypes.User))
        {
            data = ProtectedData.Unprotect(data, null, DataProtectionScope.CurrentUser);
        }
        return data;
    }

    private static byte[] EncryptWithPassword(byte[] plain, string password)
    {
        byte[] salt = RandomNumberGenerator.GetBytes(SaltSize);
        byte[] nonce = RandomNumberGenerator.GetBytes(NonceSize);
        byte[] key = Rfc2898DeriveBytes.Pbkdf2(password, salt, Pbkdf2Iterations, HashAlgorithmName.SHA256, KeySize);

        byte[] output = new byte[SaltSize + NonceSize + TagSize + plain.Length];
        salt.CopyTo(output, 0);
        nonce.CopyTo(output, SaltSize);
        using var aes = new AesGcm(key, TagSize);
        aes.Encrypt(nonce, plain,
            output.AsSpan(SaltSize + NonceSize + TagSize),
            output.AsSpan(SaltSize + NonceSize, TagSize));
        return output;
    }

    private static byte[] DecryptWithPassword(byte[] data, string password)
    {
        if (data.Length < SaltSize + NonceSize + TagSize)
        {
            throw new BadPasswordException();
        }
        var salt = data.AsSpan(0, SaltSize);
        var nonce = data.AsSpan(SaltSize, NonceSize);
        var tag = data.AsSpan(SaltSize + NonceSize, TagSize);
        var cipher = data.AsSpan(SaltSize + NonceSize + TagSize);
        byte[] key = Rfc2898DeriveBytes.Pbkdf2(Encoding.UTF8.GetBytes(password), salt, Pbkdf2Iterations, HashAlgorithmName.SHA256, KeySize);

        byte[] plain = new byte[cipher.Length];
        try
        {
            using var aes = new AesGcm(key, TagSize);
            aes.Decrypt(nonce, cipher, tag, plain);
        }
        catch (AuthenticationTagMismatchException ex)
        {
            throw new BadPasswordException(inner: ex);
        }
        return plain;
    }

    #region WinAuth 3.x formats

    /// <summary>
    /// WinAuth 3 format: salt + SHA256(salt + plain) + layered encryption
    /// </summary>
    private static string DecryptLegacyWithHash(string data, ProtectionTypes types, string? password)
    {
        int hashLength = SHA256.HashSizeInBytes * 2;
        if (data.Length < LegacySaltSize * 2 + hashLength)
        {
            throw new BadPasswordException();
        }
        string salt = data[..(LegacySaltSize * 2)];
        string hash = data.Substring(LegacySaltSize * 2, hashLength);
        string plain = DecryptLegacy(data[(LegacySaltSize * 2 + hashLength)..], types, password);

        string check = Convert.ToHexString(SHA256.HashData(Convert.FromHexString(salt + plain)));
        if (!string.Equals(check, hash, StringComparison.OrdinalIgnoreCase))
        {
            throw new BadPasswordException();
        }
        return plain;
    }

    private static string DecryptLegacy(string hex, ProtectionTypes types, string? password)
    {
        byte[] data;
        try
        {
            data = UnprotectWindows(Convert.FromHexString(hex), types);
        }
        catch (Exception ex) when (ex is CryptographicException or FormatException)
        {
            throw new BadPasswordException("Cannot decrypt using this Windows account or computer", ex);
        }
        if (types.HasFlag(ProtectionTypes.Password))
        {
            if (string.IsNullOrEmpty(password))
            {
                throw new PasswordRequiredException();
            }
            data = DecryptLegacyPassword(data, password);
        }
        return Convert.ToHexString(data);
    }

    /// <summary>
    /// WinAuth 3 password encryption: 8 byte salt, PBKDF2-SHA1 key, Blowfish with ISO 10126 padding
    /// </summary>
    private static byte[] DecryptLegacyPassword(byte[] data, string password)
    {
        if (data.Length <= LegacySaltSize)
        {
            throw new BadPasswordException();
        }
        byte[] salt = data[..LegacySaltSize];
        byte[] key = Rfc2898DeriveBytes.Pbkdf2(Encoding.UTF8.GetBytes(password), salt, LegacyPbkdf2Iterations, HashAlgorithmName.SHA1, LegacyKeySize);

        var cipher = data.AsSpan(LegacySaltSize);
        if (cipher.Length % LegacyBlowfish.BlockSize != 0)
        {
            throw new BadPasswordException();
        }
        byte[] plain = new LegacyBlowfish(key).DecryptEcb(cipher);

        // ISO 10126 padding: the last byte is the number of padding bytes
        int padding = plain.Length == 0 ? 0 : plain[^1];
        if (padding < 1 || padding > LegacyBlowfish.BlockSize || padding > plain.Length)
        {
            throw new BadPasswordException();
        }
        return plain[..^padding];
    }

    #endregion
}
