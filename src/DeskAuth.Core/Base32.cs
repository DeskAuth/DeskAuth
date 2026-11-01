// DeskAuth, based on WinAuth.
// Copyright (C) 2010-2017 Colin Mackie (WinAuth).
// Copyright (C) 2026 Nick Morphis and contributors (DeskAuth, modified from WinAuth).
// Licensed under the GNU General Public License v3 or later. See LICENSE.

using System.Text;

namespace DeskAuth.Core;

/// <summary>
/// RFC 4648 Base32 encoding, as used for authenticator secret keys.
/// </summary>
public static class Base32
{
    private const string Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";

    /// <summary>
    /// Encode bytes as unpadded Base32.
    /// </summary>
    public static string Encode(ReadOnlySpan<byte> data)
    {
        var result = new StringBuilder((data.Length * 8 + 4) / 5);
        int buffer = 0;
        int bits = 0;
        foreach (byte b in data)
        {
            buffer = (buffer << 8) | b;
            bits += 8;
            while (bits >= 5)
            {
                result.Append(Alphabet[(buffer >> (bits - 5)) & 0x1f]);
                bits -= 5;
            }
        }
        if (bits > 0)
        {
            result.Append(Alphabet[(buffer << (5 - bits)) & 0x1f]);
        }
        return result.ToString();
    }

    /// <summary>
    /// Decode Base32 text. Case, whitespace, dashes and padding are ignored.
    /// </summary>
    /// <exception cref="FormatException">text contains characters outside the Base32 alphabet</exception>
    public static byte[] Decode(string text)
    {
        var output = new List<byte>(text.Length * 5 / 8);
        int buffer = 0;
        int bits = 0;
        foreach (char c in text)
        {
            if (char.IsWhiteSpace(c) || c == '-' || c == '=')
            {
                continue;
            }
            int value = Alphabet.IndexOf(char.ToUpperInvariant(c));
            if (value < 0)
            {
                throw new FormatException($"'{c}' is not a valid Base32 character");
            }
            buffer = (buffer << 5) | value;
            bits += 5;
            if (bits >= 8)
            {
                output.Add((byte)(buffer >> (bits - 8)));
                bits -= 8;
            }
        }
        return output.ToArray();
    }
}
