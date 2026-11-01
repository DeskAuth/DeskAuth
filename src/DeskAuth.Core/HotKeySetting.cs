// DeskAuth, based on WinAuth.
// Copyright (C) 2010-2017 Colin Mackie (WinAuth).
// Copyright (C) 2026 Nick Morphis and contributors (DeskAuth, modified from WinAuth).
// Licensed under the GNU General Public License v3 or later. See LICENSE.

using System.Xml.Linq;

namespace DeskAuth.Core;

/// <summary>
/// Modifier keys, with the same values as the Win32 RegisterHotKey flags
/// </summary>
[Flags]
public enum HotKeyModifiers
{
    None = 0,
    Alt = 1,
    Control = 2,
    Shift = 4,
    Windows = 8,
}

/// <summary>
/// What a hotkey does with the current code
/// </summary>
public enum HotKeyAction
{
    /// <summary>Type the code into the active window</summary>
    Inject,
    /// <summary>Copy the code to the clipboard</summary>
    Copy,
    /// <summary>Show the code in a notification</summary>
    Notify,
}

/// <summary>
/// System-wide hotkey assigned to an authenticator
/// </summary>
public sealed class HotKeySetting
{
    public HotKeyModifiers Modifiers { get; set; }

    /// <summary>
    /// Win32 virtual key code
    /// </summary>
    public int VirtualKey { get; set; }

    public HotKeyAction Action { get; set; } = HotKeyAction.Inject;

    internal static HotKeySetting? FromXml(XElement element)
    {
        var hotkey = new HotKeySetting();
        string? modifiers = (string?)element.Element("modifiers");
        string? key = (string?)element.Element("key");
        if (string.IsNullOrEmpty(key))
        {
            return null;
        }
        // values are stored as hex of the little-endian bytes
        if (!string.IsNullOrEmpty(modifiers))
        {
            hotkey.Modifiers = (HotKeyModifiers)(BitConverter.ToInt32(Convert.FromHexString(modifiers)) & 0x0f);
        }
        hotkey.VirtualKey = BitConverter.ToUInt16(Convert.FromHexString(key));

        // WinAuth 3 "Advanced" scripts are no longer supported, so they fall back to typing the code
        if (Enum.TryParse((string?)element.Element("action"), true, out HotKeyAction action))
        {
            hotkey.Action = action;
        }
        return hotkey.VirtualKey == 0 ? null : hotkey;
    }

    internal XElement ToXml() =>
        new("hotkey",
            new XElement("modifiers", Convert.ToHexString(BitConverter.GetBytes((int)Modifiers))),
            new XElement("key", Convert.ToHexString(BitConverter.GetBytes((ushort)VirtualKey))),
            new XElement("action", Action.ToString()));
}
