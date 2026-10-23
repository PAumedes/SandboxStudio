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
- Local JSON profile storage and three built-in profiles, initialized once for a new profile store.
- A launcher that validates a profile, writes a temporary `.wsb` file, and asks Windows to open it when Windows Sandbox is available.

### Dashboard

- Displays saved profiles and supports refresh, creation of a basic profile, deletion with confirmation, and a launch action.
- The card menu's **Edit Profile** dialog edits a profile's name, description, networking, vGPU, and memory. It validates changes and provides Save and Cancel; Cancel discards the draft.
- The card menu also exports validated configurations to `.wsb` files through a file picker.
- Advanced configuration options, mapped-folder editing, profile duplication, and tag management are not wired into the dashboard.

The dashboard has been opened locally and displayed all three presets. Windows Sandbox launching and packaged-app runtime behavior have not been verified. The workflow below produces a packaging preview only; it does not publish a GitHub release.

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

The **Package Preview** GitHub Actions workflow is manually triggered and uploads a `win-x64` zip artifact. It does not create a release.

## Contributing

Please review [CONTRIBUTING.md](CONTRIBUTING.md) for architectural guidelines, code styling standards, and our TDD workflow.

## License

Distributed under the [MIT License](LICENSE).
