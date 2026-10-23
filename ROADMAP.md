# Roadmap

This roadmap describes planned work, not a claim that all phases are already implemented.

## Phase 1 — Visual shell

Status: Implemented and locally build- and visually verified; native title-bar interactions still require broader keyboard, snapping, DPI, and accessibility validation.

- Add a native custom title bar with the Windows caption controls and interactions retained.
- Create an original SandboxStudio wordmark and application icon, with reproducible SVG and local raster/icon assets.

## Phase 2 - Profile editor and exports

- Status: Partially implemented. The dashboard editor supports name, description, networking, vGPU, memory, and mapped host/sandbox folders; new mappings are read-only by default, and writable access requires acknowledgement before Save and confirmation before Launch. Validated `.wsb` export is wired. Remaining advanced controls are still planned.
- Keep configuration behavior in testable core logic with focused automated tests.

## Phase 3 — Theme and localization

- Follow the operating-system theme with `ElementTheme.Default` and matching title-bar caption foreground.
- Add English and Spanish `.resw` resources.
- Use the operating-system language on first launch and provide a stored language override; initially apply a language change after restart.

## Phase 4 — Portable Windows distribution

- Produce a self-contained Windows x64 folder distribution and zip, not a single executable.
- Keep profiles and settings under the per-user `%LOCALAPPDATA%` location; no portable-data mode is planned.
- Verify installation and runtime behavior on a clean Windows host.

## Phase 5 — Update discovery

- Add an in-app check for updates against GitHub Releases that prompts and opens the release page in a browser.
- Never silently overwrite a running executable.
- Consider replacement updates only after signed and integrity-checked update handling is designed and verified.

## Quality gates

- Apply RED/GREEN/REFACTOR to testable logic.
- Validate UI behavior visually and at runtime, including keyboard use, snapping, theme, DPI, and accessibility.
- Keep refactors small and behavior-preserving, following Fowler-style refactoring discipline.
- Require adversarial diff review by the lead and passing CI before release.
