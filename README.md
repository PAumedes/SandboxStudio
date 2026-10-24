# SandboxStudio

<p align="center">
  <strong>A Windows desktop application for working with Windows Sandbox profiles.</strong>
</p>

<p align="center">
  <img src="https://img.shields.io/badge/.NET-10.0-512BD4?logo=dotnet" alt=".NET 10" />
  <img src="https://img.shields.io/badge/Windows-App%20SDK%201.6-0078D4?logo=windows" alt="Windows App SDK" />
  <img src="https://img.shields.io/badge/License-MIT-green" alt="License" />
</p>

---

## Overview

SandboxStudio is a .NET 10 and WinUI 3 source project for managing Windows Sandbox profiles. The public repository is [PAumedes/SandboxStudio](https://github.com/PAumedes/SandboxStudio).

## Current implementation

### Core

- Profile models, validation, and `.wsb` XML serialization.
- Local JSON profile storage and built-in presets adapted to the host (five on Windows 11 24H2+, plus a pre-24H2 preset that swaps mouse buttons since host mouse settings only flow into the sandbox on 24H2); stores seeded by an earlier release receive presets added since then, while manually curated stores are left untouched.
- A launcher that validates a profile, then either starts it through the `wsb` CLI (Windows 11 24H2+, reporting the sandbox ID) or writes a temporary `.wsb` file for Windows Sandbox to open.

### Dashboard

- Displays saved profiles and supports refresh, creation of a basic profile, deletion with confirmation, and a launch action.
- The card menu's **Edit Profile** dialog edits a profile's name, description, networking, vGPU, memory, microphone and camera input, protected-client mode, clipboard and printer sharing, mapped host/sandbox folders, and an optional startup command. The startup command's script-mode checkbox packages each line into a `.cmd` delivered through a managed read-only mapped folder (the recipe is saved on the profile so it can be edited later). New mapped folders are read-only by default; writable mappings require acknowledgement before Save, and launching a profile with writable mappings requires confirmation.
- The card menu also exports validated configurations to `.wsb` files through a file picker.
- The card menu duplicates a profile under a unique "(copy)"-suffixed name with fresh creation metadata.
- The toolbar imports existing `.wsb` files into profiles, reporting validation errors and warnings on import.
- Profiles carry comma-separated tags edited in the dialog and shown on each card; the header filter box narrows the dashboard to profiles containing every entered tag.
- The **Sessions** dialog lists live sandbox sessions reported by the `wsb` CLI (Windows 11 24H2+) with status and uptime, and can connect to, stop, run commands inside, query the IP of, or share a host folder into a session.
- All currently modeled `.wsb` settings are exposed above.
- The app follows the operating-system light/dark theme, and all UI chrome is localized in English and Spanish; the **Language** menu stores an override that applies on the next launch. Host-capability reasons, policy-override text, validator messages, and `wsb` CLI errors are also localized — Core emits structured message keys that the UI translates, while raw `wsb.exe` stderr text stays in its original language inside the localized wrapper.
- An **Update available** banner appears when GitHub Releases reports a newer tag than the running build; **View release** opens the release page in the browser. Discovery only — nothing is downloaded or installed.

The dashboard has been opened locally and displayed the seeded presets. Windows Sandbox launching, packaged-app runtime, and packaging on a clean host have not been verified. The workflow below produces a packaging preview only; it does not publish a GitHub release.

## Architecture

```text
SandboxStudio/
├── src/
│   ├── SandboxStudio.Core/       # Models, storage, serialization, validation, launcher
│   └── SandboxStudio.UI/         # WinUI 3 dashboard
├── tests/
│   ├── SandboxStudio.Tests.Unit/
│   ├── SandboxStudio.Tests.Integration/
│   └── SandboxStudio.Tests.Smoke/
└── .github/workflows/
```

See [ARCHITECTURE.md](ARCHITECTURE.md) for full design documentation.

## Windows prerequisites

- A Windows 10 or Windows 11 x64 development machine.
- The [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) and Visual Studio Build Tools 2022 with the Windows App SDK/WinUI build dependencies and Windows 10 SDK (10.0.19041.0).
- On this machine, .NET SDK 10.0.402 is installed per-user at `C:\Users\patri\.dotnet`; the system-wide `dotnet` installation has only the .NET 8 runtime. Visual Studio Build Tools 2022 is installed, but the full Visual Studio IDE is not.
- The full Visual Studio IDE is optional for command-line builds. The IDE and WinUI support are required only for the Visual Studio **F5** debugging route.
- To launch Windows Sandbox, use a Windows edition that supports the feature (such as Pro, Enterprise, or Education), enable Windows Sandbox, and enable hardware virtualization. The optional feature can be enabled from an elevated PowerShell session:

  ```powershell
  Enable-WindowsOptionalFeature -Online -FeatureName Containers-DisposableClientVM -All
  ```

## Build and test

From one PowerShell session in the repository root, first select the per-user SDK when it exists. If it is absent, the commands use `dotnet` on `PATH`:

```powershell
$localDotnet = Join-Path $HOME '.dotnet'
if (Test-Path (Join-Path $localDotnet 'dotnet.exe')) {
    $env:DOTNET_ROOT = $localDotnet
    $env:PATH = "$localDotnet;$env:PATH"
}
```

The Release build restores NuGet dependencies. Run the build and tests in the same session:

```powershell
dotnet build SandboxStudio.sln -c Release
dotnet test SandboxStudio.sln -c Release --no-build
```

To build and open the Debug x64 dashboard in that same session:

```powershell
dotnet build .\SandboxStudio.sln -c Debug -p:Platform=x64
if ($LASTEXITCODE -ne 0) {
    throw "Debug x64 build failed with exit code $LASTEXITCODE."
}

$debugOutput = Join-Path $PWD 'src\SandboxStudio.UI\bin\x64\Debug\net10.0-windows10.0.19041.0'
Start-Process -FilePath (Join-Path $debugOutput 'SandboxStudio.UI.exe') -WorkingDirectory $debugOutput
```

The dashboard opens without Windows Sandbox enabled. Do not click a profile's **Launch** button unless you intend to start a Windows Sandbox VM. **New Profile** saves its JSON file under `%LOCALAPPDATA%\SandboxStudio\Profiles`. If the full Visual Studio IDE is installed, the alternative F5 route is to open `SandboxStudio.sln`, set `SandboxStudio.UI` as the startup project, select the `Debug` configuration and `x64` platform, then press **F5**.

### Distribution

`scripts/publish.ps1` produces the self-contained Windows x64 folder (`dist/SandboxStudio`) plus a versioned zip (`dist/SandboxStudio-<version>-win-x64.zip`). The build embeds the Windows App SDK (`WindowsAppSDKSelfContained`), so no runtime prerequisites are needed on the target machine — unpack the folder and run `SandboxStudio.UI.exe`. Requires Windows 10 1809 (build 17763) or later with Windows Sandbox enabled; the app runs unpackaged (no MSIX package identity).

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\publish.ps1            # project Version
powershell -ExecutionPolicy Bypass -File .\scripts\publish.ps1 -Version 0.2.0
```

The **Package Preview** GitHub Actions workflow is manually triggered and uploads the same `SandboxStudio-<version>-win-x64` zip artifact. It does not create a release.

## Contributing

Please review [CONTRIBUTING.md](CONTRIBUTING.md) for architectural guidelines, code styling standards, and our TDD workflow.

## License

Distributed under the [MIT License](LICENSE).
