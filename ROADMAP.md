# Roadmap

This roadmap describes planned work, not a claim that all phases are already implemented.

SandboxStudio aims to be the graphical manager for the entire Windows Sandbox surface: `.wsb` profiles, live sandbox sessions, and host policy awareness — every documented capability, exposed through an easy GUI.

## Phase 1 — Visual shell

Status: Implemented and locally build- and visually verified; native title-bar interactions still require broader keyboard, snapping, DPI, and accessibility validation.

- Add a native custom title bar with the Windows caption controls and interactions retained.
- Create an original SandboxStudio wordmark and application icon, with reproducible SVG and local raster/icon assets.

## Phase 2 — Profile editor and exports

- Status: Partially implemented. All currently modeled `.wsb` settings are exposed in the editor, and validated export is wired. New mapped folders are read-only by default; writable access requires acknowledgement before Save and confirmation before Launch. Profile duplication and tag management are planned under Phase 5; Windows Sandbox runtime and packaging verification remain unverified.
- Keep configuration behavior in testable core logic with focused automated tests.
- Track spec nuances in the model and validator: environment variables in mapped folder paths (Windows 11 23H2+), the documented 2048 MB auto-bump floor for `MemoryInMB`, and the vGPU default differing on Arm64 devices.

## Phase 3 — Live session management

- Status: Planned.
- Detect and use the `wsb` CLI (Windows 11 24H2+) for `start`, `list`, `exec`, `share`, `stop`, `connect`, and `ip`, parsing `--raw` JSON output; fall back to `.wsb` file-association launch on older builds.
- Launch via `wsb start --config` to capture sandbox IDs, and show running sessions in a sessions view with status and uptime; connect and stop sessions from the GUI.
- Run commands inside a running sandbox via `wsb exec` (`ExistingLogin` or `System` context, optional working directory); note that process output cannot be captured and `exec` requires an active user session.
- Share host folders into an already-running session via `wsb share` (with `--allow-write` for writable access), covering what `.wsb` startup mappings cannot.
- Evaluate WinApp CLI (`winget install Microsoft.WinAppCLI`, v0.7.0+) as an alternative or complement for guest automation: `--on sandbox` keeps a persistent sandbox across commands, and `winapp target` covers guest `exec`, file `push`/`pull`, desktop `screenshot`/`record`, and runtime provisioning — capabilities `wsb` lacks (notably captured output and file transfer). Trade-off: external tool dependency vs. inbox `wsb` CLI; `wsb` remains the zero-dependency baseline.

## Phase 4 — Host capability and policy awareness

- Status: Partially implemented. `IHostCapabilities` in Core computes per-feature availability (OS build gate **and** policy state) with injectable probes, reads the WindowsSandbox policy values (`SOFTWARE\Policies\Microsoft\Windows\Sandbox`), and reports host status including unsupported editions; the dashboard surfaces a specific reason when Sandbox is unavailable. Settings a host policy will override are flagged in the edit dialog and in the launch status. Remaining: per-feature availability badges next to editor controls, virtualization detection, and policy-driven preset adaptation.
- Build a testable host-capability service in Core that computes effective availability per feature: OS build supports it **and** host policy allows it.
- Detect whether Windows Sandbox is available on the host (optional feature enabled, supported edition — Sandbox requires Pro/Enterprise/Education — and virtualization) and guide the user when it is missing.
- Read the WindowsSandbox Policy CSP registry values (`SOFTWARE\Policies\Microsoft\Windows\Sandbox`) and surface which capabilities an administrator has disabled: audio input, clipboard redirection, mapped folders and write access (24H2+), networking, printer redirection, vGPU, and video input.
- Warn in the editor and at launch when a profile setting will be overridden by host policy.
- Centralize every version-gated capability in that service:

  | Capability | Requires |
  | --- | --- |
  | `.wsb` configuration files | Windows 10 build 18342+ / Windows 11 |
  | Environment variables in mapped folder paths | Windows 11 23H2+ |
  | Host mouse settings applied automatically | Windows 11 24H2+ (older builds need a logon-script workaround) |
  | `wsb` CLI session management (Phase 3) | Windows 11 24H2+ |
  | `AllowMappedFolders` / `AllowWriteToMappedFolders` policies | Windows 11 24H2+ |
  | Remaining Sandbox policies (audio, clipboard, networking, printer, vGPU, video) | Windows 10 2004+ / Windows 11 21H2+ |
  | vGPU enabled by default | non-Arm64 devices |

- Show unavailable features in the UI with an explanation of the requirement (for example, "requires Windows 11 24H2") rather than hiding them silently.
- Treat settings newer than the host as warnings, not hard errors: profiles are portable and may target a newer machine than the authoring one.
- Adapt presets per host; for example, include the mouse-button-swap logon script only on pre-24H2 builds.

## Phase 5 — Library, presets, and import

- Status: Partially implemented. Existing `.wsb` files can be imported into profiles from the dashboard toolbar (`WsbImportService` deserializes, names the profile after the file, validates, and stores it even when invalid so it can be corrected in the editor). Remaining: curated presets beyond the three built-ins, profile duplication and tag management, and the logon-command builder.
- Import existing `.wsb` files into profiles via the existing deserializer.
- Ship curated presets modeled on Microsoft's sample configurations (e.g., isolated testing of unknown downloads, development environment with tool bootstrap).
- Profile duplication and tag management.
- Logon-command builder that packages multi-step setup into a script delivered through a mapped folder, per Microsoft's recommended pattern.

## Phase 6 — Theme and localization

- Follow the operating-system theme with `ElementTheme.Default` and matching title-bar caption foreground.
- Add English and Spanish `.resw` resources.
- Use the operating-system language on first launch and provide a stored language override; initially apply a language change after restart.

## Phase 7 — Portable Windows distribution

- Produce a self-contained Windows x64 folder distribution and zip, not a single executable.
- Keep profiles and settings under the per-user `%LOCALAPPDATA%` location; no portable-data mode is planned.
- Verify installation and runtime behavior on a clean Windows host.

## Phase 8 — Update discovery

- Add an in-app check for updates against GitHub Releases that prompts and opens the release page in a browser.
- Never silently overwrite a running executable.
- Consider replacement updates only after signed and integrity-checked update handling is designed and verified.

## Evaluated alternatives

Findings from research into replacing the `WindowsSandbox.exe` launch path; recorded to avoid re-investigation.

### `CreateProcessInSandbox` API — not a substitute for Windows Sandbox

`Experimental_CreateProcessInSandbox` / `Experimental_CreateProcessAsUserInSandbox` (`processmodel.dll`, `GetProcAddress` only, no public header) launch a process on the **host** inside a "composable sandbox": AppContainer profile, configurable integrity level, win32k syscall disable, Job Object UI restrictions, Bound File System read/write and read-only path grants, named capabilities, proxy policy. Same kernel, same OS instance — containment, not virtualization.

- Most `.wsb` settings have no host-process equivalent: `MemoryInMB`, `VGpu`, `ProtectedClient`, `PrinterRedirection`, `AudioInput`, `VideoInput`, `LogonCommand` all configure VM devices/boot. Approximate analogues exist only for `MappedFolders` (BFS `fs_read_write`/`fs_read_only`) and `Networking` (AppContainer capabilities).
- Isolation is weaker than the VM: AppContainer shares the host kernel, so a kernel exploit escapes the boundary.
- Practical blockers: experimental and subject to change; spec buffer is a FlatBuffer blob (`SandboxSpec.fbs`, file id `SBOX`) whose schema is not published in the API reference; requires Windows 11 experimental builds; identities must not collide with installed MSIX package family names.

Conclusion: `WindowsSandbox.exe` + `.wsb` remains the only documented interface to the Windows Sandbox VM — there is no public API that instantiates it. The composable-sandbox API could only ever support a separate "run a host process in an AppContainer sandbox" mode, and should be revisited only if it stabilizes past the experimental stage.

### WinApp CLI v0.7.0 — complementary, see Phase 3

Drives real Windows Sandbox via `--on sandbox` and adds guest exec, file transfer, screenshots, and runtime provisioning. Relevant to Phase 3 session management; adds a non-inbox dependency.

## Quality gates

- Apply RED/GREEN/REFACTOR to testable logic.
- Validate UI behavior visually and at runtime, including keyboard use, snapping, theme, DPI, and accessibility.
- Keep refactors small and behavior-preserving, following Fowler-style refactoring discipline.
- Require adversarial diff review by the lead and passing CI before release.
