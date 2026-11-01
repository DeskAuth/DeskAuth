// DeskAuth, based on WinAuth.
// Copyright (C) 2010-2017 Colin Mackie (WinAuth).
// Copyright (C) 2026 Nick Morphis and contributors (DeskAuth, modified from WinAuth).
// Licensed under the GNU General Public License v3 or later. See LICENSE.

namespace DeskAuth.Core;

/// <summary>
/// Base for errors raised by DeskAuth
/// </summary>
public class DeskAuthException : Exception
{
    public DeskAuthException(string message, Exception? inner = null) : base(message, inner)
    {
    }
}

/// <summary>
/// Data is protected with a password and none was supplied
/// </summary>
public class PasswordRequiredException : DeskAuthException
{
    public PasswordRequiredException() : base("A password is required")
    {
    }
}

/// <summary>
/// Data could not be decrypted with the supplied password or Windows account
/// </summary>
public class BadPasswordException : DeskAuthException
{
    public BadPasswordException(string? message = null, Exception? inner = null)
        : base(message ?? "Invalid password", inner)
    {
    }
}

/// <summary>
/// Config file cannot be read
/// </summary>
public class InvalidConfigException : DeskAuthException
{
    public InvalidConfigException(string message, Exception? inner = null) : base(message, inner)
    {
    }
}
