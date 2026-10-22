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
- The profile editor view, `.wsb` export UI, profile duplication, and tag management are not wired into the dashboard. An `EditProfileViewModel` exists, but there is no connected editor screen.

Windows Sandbox availability is checked on the host, but Windows Sandbox launching and packaged-app runtime behavior have not been verified. The workflow below produces a packaging preview only; it does not publish a GitHub release.

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
- The [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0).
- Visual Studio with .NET 10 and WinUI/Windows App SDK support, plus the Windows 10 SDK (10.0.19041.0) for the `net10.0-windows10.0.19041.0` target.
- To launch Windows Sandbox, use a Windows edition that supports the feature (such as Pro, Enterprise, or Education), enable Windows Sandbox, and enable hardware virtualization. The optional feature can be enabled from an elevated PowerShell session:

  ```powershell
  Enable-WindowsOptionalFeature -Online -FeatureName Containers-DisposableClientVM -All
  ```

## Build and test

From PowerShell in the repository root:

```powershell
dotnet build SandboxStudio.sln -c Release
dotnet test SandboxStudio.sln -c Release --no-build
```

The build restores NuGet dependencies. To run the app under the Visual Studio debugger, open `SandboxStudio.sln`, set `SandboxStudio.UI` as the startup project, select the `Debug` configuration and `x64` platform, then press **F5**. Running the dashboard does not by itself verify that Windows Sandbox can launch on the host.

The **Package Preview** GitHub Actions workflow is manually triggered and uploads a `win-x64` zip artifact. It does not create a release.

## Contributing

Please review [CONTRIBUTING.md](CONTRIBUTING.md) for architectural guidelines, code styling standards, and our TDD workflow.

## License

Distributed under the [MIT License](LICENSE).
