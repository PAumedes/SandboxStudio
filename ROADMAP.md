# Roadmap

This roadmap describes planned work, not a claim that all phases are already implemented.

SandboxStudio aims to be the graphical manager for the entire Windows Sandbox surface: `.wsb` profiles, live sandbox sessions, and host policy awareness — every documented capability, exposed through an easy GUI.

## Phase 1 — Visual shell

Status: Implemented and locally build- and visually verified; native title-bar interactions still require broader keyboard, snapping, DPI, and accessibility validation.

- Add a native custom title bar with the Windows caption controls and interactions retained.
- Create an original SandboxStudio wordmark and application icon, with reproducible SVG and local raster/icon assets.

## Phase 2 — Profile editor and exports

- Status: Partially implemented. All currently modeled `.wsb` settings are exposed in the editor, and validated export is wired. New mapped folders are read-only by default; writable access requires acknowledgement before Save and confirmation before Launch. Windows Sandbox runtime and packaging verification remain unverified.
- Keep configuration behavior in testable core logic with focused automated tests.
- Track spec nuances in the model and validator: environment variables in mapped folder paths (Windows 11 23H2+), the documented 2048 MB auto-bump floor for `MemoryInMB`, and the vGPU default differing on Arm64 devices.

## Phase 3 — Live session management

- Status: Implemented for the `wsb` surface. `WsbSessionManager` in Core detects the `wsb` CLI (build 26100+ plus `wsb.exe` presence, resolving the MSIX App Execution Alias when System32 lacks it), lists sessions by parsing `--raw` JSON, and stops or connects to sessions; the toolbar **Sessions** dialog shows status/uptime with refresh. Launch prefers `wsb start --config` on 24H2+, reporting the assigned sandbox ID and falling back to `.wsb` file-association launch when the CLI never ran (oversized configs, spawn failures) or is absent. Commands run inside a session via `wsb exec` (System or ExistingLogin context, optional working directory — process output cannot be captured), session IPs come from `wsb ip`, and host folders are shared into a running session via `wsb share` (optional `--allow-write`), all from the Sessions dialog row actions. The WinApp CLI evaluation is complete — see the decision below.
- Detect and use the `wsb` CLI (Windows 11 24H2+) for `start`, `list`, `exec`, `share`, `stop`, `connect`, and `ip`, parsing `--raw` JSON output; fall back to `.wsb` file-association launch on older builds.
- Launch via `wsb start --config` to capture sandbox IDs, and show running sessions in a sessions view with status and uptime; connect and stop sessions from the GUI.
- Run commands inside a running sandbox via `wsb exec` (`ExistingLogin` or `System` context, optional working directory); note that process output cannot be captured and `exec` requires an active user session.
- Share host folders into an already-running session via `wsb share` (with `--allow-write` for writable access), covering what `.wsb` startup mappings cannot.
- ~~Evaluate WinApp CLI (`winget install Microsoft.WinAppCLI`) as an alternative or complement for guest automation~~ — **evaluated; decision: `wsb` remains the baseline; `winapp` is a candidate optional provider.** Findings:
  - **What it adds over `wsb`**: `winapp target exec sandbox -- <cmd>` forwards the guest process's stdout/stderr and returns its exit code (`wsb exec` reports the guest exit code in `--raw` JSON but cannot capture the process's stdout/stderr at all); `target push`/`pull` transfer files in and out (paths are relative to a `workRoot` the guest agent reports); `target screenshot`/`record` capture the whole guest desktop; `winapp run --on sandbox` deploys + launches apps and auto-provisions missing shared runtimes (e.g. .NET, Windows App SDK) in the guest; `target snapshot` reports desktop readiness. All verbs emit `--json` for automation.
  - **Session model**: winapp maintains **one adopted, persistent sandbox** prepared with its guest agent, Developer Mode, and an inbound firewall rule — it can adopt a sandbox the user (or SandboxStudio) started, and `wsb list`/`connect` operate on that same instance (Windows permits one Sandbox at a time). The per-profile `.wsb` launch lifecycle differs: a winapp-adopted sandbox is prepared and long-lived rather than re-created per profile.
  - **Costs**: an external dependency (`winget install Microsoft.WinAppCLI`) rather than the inbox `wsb`; guest-agent versioning (`sandbox_agent_incompatible`) adds a host↔guest upgrade burden; preparing a sandbox mutates it (agent + Developer Mode + firewall rule) — notable for users who treat sandboxes as pristine; real input and recording need a connected, non-minimized Sandbox client plus `WINAPP_UI_WORKFLOW_ID` coordination (read-only inspection still works without one).
  - **Direction**: a `winapp`-backed provider behind `ISandboxSessionManager`-adjacent seams would close the exec-output and file-transfer gaps without replacing `wsb`. Not scheduled — revisit if captured-output exec, push/pull, or guest screenshots become wanted features.

## Phase 4 — Host capability and policy awareness

- Status: Implemented for the `.wsb` surface. `IHostCapabilities` in Core computes per-feature availability (OS build gate **and** policy state) with injectable probes, reads the WindowsSandbox policy values (`SOFTWARE\Policies\Microsoft\Windows\Sandbox`), and reports host status including unsupported editions; the dashboard surfaces a specific reason when Sandbox is unavailable, including unsupported editions and firmware virtualization being disabled or hypervisor-managed. The profile editor shows a per-control availability caption under every build- or policy-gated setting (features introduced in Windows 10 2004 report `RequiresNewerWindows` on earlier hosts; policy-blocked features report the blocking policy name), while unsupported controls remain editable to preserve profile portability. Settings a host policy will override are flagged in the edit dialog and in the launch status, and the curated preset catalog adapts to the host build (the left-handed mouse-swap preset is only seeded where `HostMouseSettings` is unavailable). Phase 4 is complete for the documented `.wsb` surface.
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
- ~~Adapt presets per host~~ — implemented: `CuratedPresets` gates presets on host capability (the mouse-button-swap logon preset seeds only on pre-24H2 builds).

## Phase 5 — Library, presets, and import

- Status: Implemented. Existing `.wsb` files can be imported into profiles from the dashboard toolbar (`WsbImportService` deserializes, names the profile after the file, validates, and stores it even when invalid so it can be corrected in the editor). Profiles can be duplicated from the card menu; `ProfileDuplicationService` deep-copies configuration under a unique "(copy)"-suffixed name with fresh identity and launch metadata (script-backed profiles are re-packaged for the duplicate's id). Profiles carry comma-separated tags edited in the dialog (`ProfileTags` normalizes input), displayed on each card, and filtered from the dashboard header (profiles must contain every entered tag). Curated presets are seeded from versioned, host-adapted storage — five on 24H2+ plus a sixth `Left-Handed Desktop` preset on pre-24H2 builds (`Conferencing Test Bed` and `Isolated Printer Bench` joined the original three at seed version 2; earlier stores upgrade without resurrecting deleted presets or touching curated ones). The editor's script-mode checkbox packages each line of the startup command into a `.cmd` delivered through a managed read-only mapped folder at `C:\SandboxStudio\Scripts` (`ILogonScriptService` in Core), per Microsoft's recommended pattern for multi-step setup; the recipe persists on the profile and unchecking restores a plain logon command.
- Import existing `.wsb` files into profiles via the existing deserializer.
- Ship curated presets modeled on Microsoft's sample configurations (e.g., isolated testing of unknown downloads, development environment with tool bootstrap).
- Tag editing and filtering.
- ~~Logon-command builder that packages multi-step setup into a script delivered through a mapped folder, per Microsoft's recommended pattern.~~ Implemented via `ILogonScriptService` + the editor's script-mode checkbox.

## Phase 6 — Theme and localization

- Status: Implemented. The app follows the operating-system theme (no pinned `RequestedTheme`); `MainWindow` syncs `AppWindow.TitleBar` caption-button colors on `ActualThemeChanged`. `Strings/en-US` and `Strings/es-ES` `.resw` resources cover all UI chrome (dashboard, edit dialog, sessions dialog), UI-generated status text, and Core-originated messages: Core records carry a `MessageKey`/`ErrorKey` + args alongside their English fallback (`ValidationMessage`, `FeatureAvailability`, `PolicyOverride`, session and launch results), and the UI renders them through `Localizer.Core` with graceful fallback. A stored `settings.json` language override (System default / English / Español) applies via `ApplicationLanguages.PrimaryLanguageOverride` on the next launch, with a restart notice InfoBar. Arbitrary OS/`wsb.exe` stderr text stays in its original language inside the localized wrapper.
- ~~Follow the operating-system theme with `ElementTheme.Default` and matching title-bar caption foreground.~~ Implemented — `ApplyTitleBarCaptionColors` tracks `ActualThemeChanged`.
- Add English and Spanish `.resw` resources.
- Use the operating-system language on first launch and provide a stored language override; initially apply a language change after restart.

## Phase 7 — Portable Windows distribution

- Status: Implemented. `scripts/publish.ps1` produces a self-contained Windows x64 folder (`dist/SandboxStudio`) and `SandboxStudio-<version>-win-x64.zip` — the folder keeps the Windows App SDK self-contained payload beside the app; it is not a single executable. The `release.yml` workflow_dispatch produces the same versioned artifact (optional `version` input overrides the `Directory.Build.props` `Version`). The published build launches on this host; verification on a fully clean machine (no dev tooling) is still worth doing before a real release.
- Profiles and settings stay under the per-user `%LOCALAPPDATA%` location; no portable-data mode is planned.
- ~~Verify installation and runtime behavior on a clean Windows host~~ — partially verified: the published folder runs on the development host; a from-scratch machine check remains a manual pre-release step.

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
