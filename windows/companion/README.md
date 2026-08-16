# Hello Companion

Hello Companion is a lightweight Windows tray app with animated desktop pets that periodically say “Hello 👋” in an anchored speech bubble.

## First-version features

- Notification-area icon with Open, Pause/Resume, Say hello now, and Exit commands
- Close-to-tray window behavior
- Configurable interval from 1 minute to 24 hours
- Persisted enabled state and interval
- Next-greeting status
- Single instance per Windows session; launching again restores the existing window
- Cancellable, idle-friendly scheduling that never emits a burst of missed greetings
- x64 and ARM64 configurations
- Multiple transparent desktop pets that roam along the taskbar or across the virtual screen
- JSON-based custom characters with one or more PNG animation frames
- Independent per-pet reminder handlers and declarative behavior sequences
- Speech bubbles anchored to the selected pet, with Windows notifications as a fallback when pets are hidden

Settings are stored locally in `%LOCALAPPDATA%\HelloCompanion\settings.json`. The app has no account, analytics, or network behavior.

Custom character packages are loaded from `%LOCALAPPDATA%\HelloCompanion\Pets`. See [docs/custom-pets.md](docs/custom-pets.md) for the manifest and sprite-frame format.

## Build

Requirements:

- .NET 10 SDK
- Windows App SDK development prerequisites
- MSIX Packaging Tools when producing an installable package

Build either target architecture:

```powershell
dotnet build HelloCompanion.sln -p:Platform=x64
dotnet build HelloCompanion.sln -p:Platform=ARM64
```

Use `x64` for most Intel or AMD Windows laptops. Use `ARM64` only for a Windows-on-ARM device.

## Create an installer for another laptop

The recommended installation format is a signed MSIX package. The destination laptop does not need Visual Studio, the .NET SDK, or the source code.

1. Open `HelloCompanion.sln` in Visual Studio.
2. Select the `Release` configuration and the required architecture (`x64` for most laptops).
3. In Solution Explorer, right-click `HelloCompanion.App`.
4. Select **Publish > Create App Packages**. Depending on the Visual Studio version, this command may be under **Package and Publish**.
5. Select **Sideloading**.
6. Create or select a signing certificate whose publisher matches `CN=HelloCompanion`.
7. Select the target architecture and increase the package version, for example from `1.0.0.0` to `1.0.1.0`.
8. Create the package.

Visual Studio writes the package beneath `src\HelloCompanion.App\AppPackages`. A complete sideloading folder should contain:

- The signed `.msix` package
- The public `.cer` certificate
- `Install.ps1` or `Add-AppDevPackage.ps1`
- The `Dependencies` directory

Copy the entire generated folder to the other laptop. Do not copy only the `.msix`, because the installation script also installs required Windows App Runtime dependencies.

### Install on the other laptop

1. Open **Settings > System > For developers** and enable **Developer Mode**.
2. Right-click `Install.ps1` in the copied package folder and select **Run with PowerShell**.
3. Accept the administrator and certificate prompts.
4. Launch **Hello Companion** from the Start menu.

If Windows reports that the certificate is not trusted, install the included `.cer` certificate into the **Trusted People** certificate store and run `Install.ps1` again.

For later releases, keep using the same signing certificate and increase the package version. For public distribution, use a trusted code-signing certificate or publish through the Microsoft Store.

## Unsigned test package

Create an unsigned local test package:

```powershell
dotnet build HelloCompanion.sln -c Release -p:Platform=x64 -p:GenerateAppxPackageOnBuild=true -p:AppxPackageSigningEnabled=false
```

This package is written beneath `src\HelloCompanion.App\AppPackages`, but Windows will not install it until it is signed with a trusted development or distribution certificate. Visual Studio's Deploy command is the simplest development-time path because the app uses the packaged WinUI 3 model.

Do not launch the raw build-output `.exe` unless the matching Windows App Runtime is installed and registered for unpackaged execution.

## Remaining hardening

- Explicit power resume handling
- Tray icon recreation after Windows Explorer restarts
- Automated scheduler and persistence tests
- Final branded `.ico` asset
- Signed MSIX packaging and install/upgrade verification
