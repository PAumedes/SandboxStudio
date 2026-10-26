# Changelog

All notable changes to SandboxStudio are documented here. The format follows
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and versions follow
[Semantic Versioning](https://semver.org/).

## [Unreleased]

## [0.3.0] - 2026-10-26

### Added
- Microsoft Store distribution: an MSIX bundle (x64 and arm64) with Store tile,
  logo and splash assets, built by `scripts/package-msix.ps1` and optionally
  submitted as a draft by the Release workflow.
- Authenticode signing of the portable exe through SignPath Foundation (free for
  open source), with signature verification before a release is published.
- Open a `.wsb` file with SandboxStudio (Explorer "Open with", or a path on the
  command line) to import it as a profile.
- The validator warns when a mapped host folder is under AppData, which Windows
  Sandbox cannot map.
- Privacy policy, security policy, release guide, issue and pull-request
  templates, and Dependabot updates.

### Changed
- The release is a single portable exe (`SandboxStudio-<version>-win-x64.exe`)
  instead of a zip of files; Windows App SDK upgraded to 2.5.1.
- Logon scripts and temporary launch files moved to `%USERPROFILE%\.sandboxstudio`;
  existing scripts are migrated automatically.
- The Store build skips the GitHub update check; the Store updates it.

### Fixed
- Script-mode profiles failed to launch (`0x80070002`) because their scripts were
  mapped from `%LOCALAPPDATA%`.
- Saving a script-mode profile no longer warns that its script folder is missing.
- The profile card's "More options" button now has an accessible name and tooltip.

### Removed
- Curated presets: Windows Sandbox has no profile store of its own, so the app
  ships no pre-existing profiles. Profiles already seeded on existing
  installations stay untouched — they are ordinary profiles now.

## [0.2.0] - 2026-10-25

### Fixed
- Review findings in the release scripts, validation and packaging.
- Release notes list one commit per line.

### Changed
- GitHub Actions moved to their Node 24 versions.

## [0.1.0] - 2026-10-25

First public release: profile library with curated presets, profile editor for
every modeled `.wsb` setting, mapped-folder safety checks, logon-command script
builder, `.wsb` import and export, tags, live session management through the
`wsb` CLI (Windows 11 24H2+), host capability and policy awareness, English and
Spanish localization, and GitHub release update discovery.

[Unreleased]: https://github.com/PAumedes/SandboxStudio/compare/v0.3.0...HEAD
[0.3.0]: https://github.com/PAumedes/SandboxStudio/compare/v0.2.0...v0.3.0
[0.2.0]: https://github.com/PAumedes/SandboxStudio/compare/v0.1.0...v0.2.0
[0.1.0]: https://github.com/PAumedes/SandboxStudio/releases/tag/v0.1.0
