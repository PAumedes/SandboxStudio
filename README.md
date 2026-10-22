# SandboxStudio

<p align="center">
  <strong>State-of-the-art Windows Sandbox Orchestrator built with .NET 10 & WinUI 3.</strong>
</p>

<p align="center">
  <img src="https://img.shields.io/badge/.NET-10.0-512BD4?logo=dotnet" alt=".NET 10" />
  <img src="https://img.shields.io/badge/Windows-App%20SDK%201.6-0078D4?logo=windows" alt="Windows App SDK" />
  <img src="https://img.shields.io/badge/Design-Fluent%20Mica-blue" alt="Fluent Design" />
  <img src="https://img.shields.io/badge/Architecture-MVVM%20%7C%20TDD-brightgreen" alt="Architecture" />
  <img src="https://img.shields.io/badge/License-MIT-green" alt="License" />
</p>

---

## Overview

**SandboxStudio** is a modern, high-performance desktop orchestrator for [Windows Sandbox](https://learn.microsoft.com/en-us/windows/security/application-security/application-isolation/windows-sandbox/). It eliminates manual XML authoring and replaces legacy tooling with a fluid, pixel-perfect user interface adhering to Windows 11 Fluent Design principles.

## Features

* **Visual Profile Management:** Create, configure, duplicate, tag, and organize sandbox configurations.
* **Full Spec Coverage:** Comprehensive controls for vGPU, RAM allocation, Network Isolation, Mapped Folders, Startup/Logon commands, Audio/Video passthrough, Clipboard isolation, and Protected Client mode.
* **One-Click Orchestration:** Instant sandbox launching with background configuration generation and validation.
* **Curated Presets:** Ready-to-run profiles for malware analysis, throwaway web browsing, and developer tooling setup.
* **Native Fluent Design:** Deep integration with Windows 11 system theming, Mica material, and smooth layout animations.

## Architecture

```
SandboxStudio/
├── src/
│   ├── SandboxStudio.Core/         # Pure domain logic, models, serialization, validation
│   └── SandboxStudio.UI/           # WinUI 3 Fluent desktop application (Windows App SDK)
├── tests/
│   ├── SandboxStudio.Tests.Unit/   # Unit tests (Serialization, Validation, Storage, Launching)
│   ├── SandboxStudio.Tests.Integration/ # End-to-end profile lifecycle & disk persistence tests
│   └── SandboxStudio.Tests.Smoke/  # Container resolution, schema sanity & preset smoke tests
└── .github/workflows/              # GitHub Actions CI/CD workflows
```

See [ARCHITECTURE.md](ARCHITECTURE.md) for full design documentation.

## Getting Started

### Prerequisites
* Windows 11 (22H2+) or Windows 10 Pro / Enterprise
* Windows Sandbox feature enabled (`Enable-WindowsOptionalFeature -Online -FeatureName Containers-DisposableClientVM`)
* [.NET 10 SDK](https://dotnet.microsoft.com/download)

### Building & Running
```powershell
# Clone the repository
git clone https://github.com/SandboxStudio/SandboxStudio.git
cd SandboxStudio

# Restore and Build
dotnet restore
dotnet build SandboxStudio.sln

# Run Test Suites
dotnet test
```

## Contributing
Please review [CONTRIBUTING.md](CONTRIBUTING.md) for architectural guidelines, code styling standards, and our TDD workflow.

## License
Distributed under the [MIT License](LICENSE).
