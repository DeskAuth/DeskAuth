<h1><img src="docs/images/logo-64.png" width="40" height="40" alt="" align="top"> DeskAuth</h1>

DeskAuth is a portable, open-source authenticator for Windows. It generates the six or eight digit codes used for two-factor authentication by websites such as GitHub, Amazon, Microsoft, Google and Cloudflare, using the standard time-based (TOTP, RFC 6238) and counter-based (HOTP, RFC 4226) algorithms.

DeskAuth is based on [WinAuth](https://github.com/winauth/winauth) by Colin Mackie, modernized and rewritten for .NET 10 and WPF.

**[deskauth.com](https://deskauth.com/)** · **[Download](https://github.com/DeskAuth/DeskAuth/releases)** · **[Help](https://deskauth.com/help.html)**

<p align="center">
<picture>
  <source media="(prefers-color-scheme: dark)" srcset="docs/images/screenshots/main-dark.png">
  <img src="docs/images/screenshots/main.png" width="420" alt="The DeskAuth window showing codes for GitHub, Amazon, Cloudflare, GitLab, Microsoft and Google">
</picture>
</p>

## Features

* Time-based (TOTP) and counter-based (HOTP) authenticators, with SHA1, SHA256 or SHA512 and 6 to 8 digits
* Add authenticators from a secret key, an `otpauth://` link, or a QR code image (from a file or the clipboard)
* Codes shown continuously with a countdown, or on demand
* Your authenticators are encrypted with your password (PBKDF2-SHA256 and AES-256-GCM), and/or locked to this computer or Windows account
* An extra password on individual authenticators
* System-wide shortcut keys to type, copy or show a code
* System tray icon with quick access to codes
* Icons from a website (the icon your browser shows for it), an image address or an image file, and drag to reorder
* Import and export in the `otpauth://` format used by other authenticator apps
* Show an authenticator's secret key and QR code to move it to another device
* Light and dark themes following Windows, and sharp text at any display scaling
* Portable mode: put `deskauth.xml` next to `DeskAuth.exe` to keep everything in one folder

## Screenshots

| Add an authenticator | Right-click menu |
|---|---|
| <picture>   <source media="(prefers-color-scheme: dark)" srcset="docs/images/screenshots/add-dark.png">   <img src="docs/images/screenshots/add.png" width="400" alt="Adding an authenticator from an otpauth link"> </picture> | <picture>   <source media="(prefers-color-scheme: dark)" srcset="docs/images/screenshots/menu-dark.png">   <img src="docs/images/screenshots/menu.png" width="360" alt="The right-click menu for an authenticator"> </picture> |
| Paste a secret key or `otpauth://` link, or a QR code image, and check the code before adding it. | Copy codes, rename, change icons, set passwords and shortcut keys, and show secret keys. |

| Protection | Shortcut keys |
|---|---|
| <picture>   <source media="(prefers-color-scheme: dark)" srcset="docs/images/screenshots/protection-dark.png">   <img src="docs/images/screenshots/protection.png" width="380" alt="The Protection dialog"> </picture> | <picture>   <source media="(prefers-color-scheme: dark)" srcset="docs/images/screenshots/hotkey-dark.png">   <img src="docs/images/screenshots/hotkey.png" width="360" alt="The Shortcut key dialog"> </picture> |
| Encrypt with your password, and lock to this computer or Windows account. | Type, copy or show a code from any program with a key combination. |

| Icons from websites | Secret key and QR code |
|---|---|
| <picture>   <source media="(prefers-color-scheme: dark)" srcset="docs/images/screenshots/icon-dark.png">   <img src="docs/images/screenshots/icon.png" width="400" alt="Getting an icon from github.com"> </picture> | <picture>   <source media="(prefers-color-scheme: dark)" srcset="docs/images/screenshots/secret-dark.png">   <img src="docs/images/screenshots/secret.png" width="340" alt="An authenticator's secret key and QR code"> </picture> |
| Use the icon your browser shows for a website, an image address, or an image file. | Move an authenticator to your phone or another computer. |

## Requirements

* Windows 10 or 11
* [.NET 10 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/10.0)

## How to use

* Run `DeskAuth.exe`. There is nothing to install.
* Click **Add** and paste the secret key or `otpauth://` link from the website, or use a screenshot of its QR code.
* Right-click an authenticator for more options, such as its icon, password, shortcut key or secret key.
* Double-click an authenticator, or press Ctrl+C, to copy its code.
* Click the gear button for program options, protection, import and export.

Command line: `DeskAuth.exe [config file] [-min]`. `-min` starts in the tray (or minimized), and is used when DeskAuth starts with Windows.

## Where are my authenticators saved?

In `%APPDATA%\DeskAuth\deskauth.xml`, unless there is a `deskauth.xml` (or WinAuth's `winauth.xml`) next to `DeskAuth.exe` (portable mode) or a file is given on the command line. The previous version of the file is kept as `deskauth.xml.bak` each time it is saved.

## Upgrading from WinAuth

The first time DeskAuth runs, it copies your authenticators from WinAuth's `%APPDATA%\WinAuth\winauth.xml` to `%APPDATA%\DeskAuth\deskauth.xml`, keeping your password and Windows account protection. Your WinAuth file is not changed. DeskAuth saves with a new, stronger encryption format, which **WinAuth 3 cannot open**.

Changes from WinAuth 3:

* Only standard TOTP and HOTP authenticators are supported. Battle.net, Steam, Guild Wars 2, Trion/Glyph and other service-specific authenticators have been removed. They are kept in the file and listed as not supported, so you can still delete them; to keep using them, move them to their official apps first.
* YubiKey protection has been removed. Files protected with a YubiKey must have it removed using WinAuth 3 first.
* Codes use your computer's clock, which Windows keeps in sync. DeskAuth no longer contacts Google to check the time.
* Export is to a plain text file of `otpauth://` links. Password-protected zip and PGP exports, the update checker and the registry backups have been removed.
* Shortcut keys can type, copy or show the code. Advanced shortcut scripts are no longer supported and fall back to typing the code.

## Building

See [COMPILING.md](COMPILING.md).

## Is it secure?

An authenticator on your computer is not as secure as a separate device such as a phone or hardware key: anything that can read your files and watch you type your password could read your secret keys. Protect DeskAuth with a password, keep your computer free of malware, and keep a backup of your secret keys somewhere safe.

DeskAuth does not send your information anywhere. The only time it connects to the internet is when you use **Get icon** to download an icon from a website you enter.

## Author

DeskAuth is maintained by Nick Morphis and contributors. Copyright (C) 2026 Nick Morphis and contributors.

DeskAuth is based on [WinAuth](https://github.com/winauth/winauth), written by Colin Mackie. Copyright (C) 2010-2017 Colin Mackie. DeskAuth modified WinAuth in 2026, rewriting it for .NET 10 and WPF.

## License

This program is free software: you can redistribute it and/or modify it under the terms of the GNU General Public License as published by the Free Software Foundation, either version 3 of the License, or (at your option) any later version.

This program is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY; without even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU General Public License for more details.

You should have received a copy of the GNU General Public License along with this program. If not, see https://www.gnu.org/licenses/.
