# How to compile DeskAuth

## Prerequisites

* [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
* Optionally, Visual Studio 2026 with the **.NET desktop development** workload

## Projects

| Project | Description |
|---|---|
| `src/DeskAuth.Core` | Authenticators, encryption and the config file format. No UI. |
| `src/DeskAuth` | The WPF application |
| `tests/DeskAuth.Core.Tests` | Unit tests, including WinAuth 3 config files in `Fixtures` |

The only third-party package used by the app is [ZXing.Net](https://github.com/micjahn/ZXing.Net), for reading and creating QR codes.

## Building

Before testing with your own authenticators, back up `%APPDATA%\DeskAuth\deskauth.xml`, or run DeskAuth with a copy: `DeskAuth.exe path\to\copy.xml`.

From the command line:

```
dotnet build
dotnet test
```

Or open `DeskAuth.sln` in Visual Studio and build. The app is built to `src\DeskAuth\bin\Debug\net10.0-windows\DeskAuth.exe`.

## Publishing

To create a single `DeskAuth.exe` that needs the .NET 10 Desktop Runtime installed:

```
dotnet publish src/DeskAuth -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -o publish
```

For a larger exe that runs without .NET installed, use `--self-contained true`. Use `-r win-arm64` for ARM devices, or `-r win-x86` for 32-bit Windows.

## Releases

GitHub Actions builds and tests every push and pull request to `main` (`.github/workflows/build.yml`).

To make a release, create a new release on GitHub with a tag such as `v4.1.0`, and publish it. The release workflow (`.github/workflows/release.yml`) then runs the tests and attaches these files to the release, with the version taken from the tag:

* `DeskAuth-4.1.0-x64.exe`, `DeskAuth-4.1.0-arm64.exe` and `DeskAuth-4.1.0-x86.exe`, which need the .NET 10 Desktop Runtime
* `DeskAuth-4.1.0-x64-standalone.exe`, which includes .NET
* `SHA256SUMS.txt`

When the files are uploaded, the **Website version** workflow (`.github/workflows/website-version.yml`) sets the version and download links on the website (`docs/`) to the latest published release, using `tools/Update-WebsiteVersion.ps1`, and pushes the change to `main`. Pre-releases are never shown on the website or offered as an update in the app. The workflow can also be run by hand from the Actions tab.

If the release workflow fails, fix the problem, move the tag if needed, and run the **Release** workflow by hand from the Actions tab with the release's tag. It replaces any files already attached.

### Code signing

Releases are signed by [SignPath](https://signpath.io) under the [code signing policy](https://deskauth.com/code-signing.html). Signing is off until it is set up in the repository's **Settings → Secrets and variables → Actions**:

| Name | Kind | Value |
|---|---|---|
| `SIGNPATH_API_TOKEN` | Secret | API token of a SignPath user with the submitter role |
| `SIGNPATH_ORGANIZATION_ID` | Variable | SignPath organization ID. Setting this turns signing on. |
| `SIGNPATH_PROJECT_SLUG` | Variable | SignPath project slug, for example `DeskAuth` |
| `SIGNPATH_SIGNING_POLICY_SLUG` | Variable | `test-signing` to try it out, then `release-signing` |
| `SIGNPATH_ARTIFACT_CONFIGURATION_SLUG` | Variable | Optional; SignPath's default artifact configuration is used if empty |

The artifact configuration to paste into SignPath is in `.signpath/artifact-configuration.xml`. With signing on, the release workflow waits up to 3 hours for an approver to approve the signing request in SignPath, then attaches the signed files and their checksums to the release.

Version, product name and copyright for all projects are in `Directory.Build.props`. Release builds take the version from the tag.

## Testing

`dotnet test` covers the code generation (RFC 4226 and RFC 6238 test vectors), encryption, reading WinAuth 3 files, and `otpauth://` links.

Also check by hand, using a copy of a config file:

1. Add a TOTP authenticator from a key, a link and a QR code image, and check the codes match another authenticator app.
2. Set a password on the file with **Change protection**, restart DeskAuth and unlock it.
3. Set a password on one authenticator, then show its code.
4. Assign a shortcut key and use it to type a code into another program.
5. Export, then import the export into an empty config.
6. Check the window at 100% and 200% display scaling, and in light and dark mode.
