# Privacy Policy

_Last updated: 2026-10-25_

SandboxStudio is a local desktop app. It has no accounts, no telemetry, no
analytics and no advertising, and it does not send your profiles, files or usage
to the project or to anyone else.

## Network access

- **Update check (portable exe only).** When the portable
  `SandboxStudio-<version>-win-x64.exe` starts, it asks the GitHub API
  (`api.github.com`) for the latest SandboxStudio release, to show an "Update
  available" banner. The request carries no personal data, only the fixed
  User-Agent `SandboxStudio-UpdateChecker`; GitHub receives your IP address, as
  with any web request. See the [GitHub Privacy Statement](https://docs.github.com/site-policy/privacy-policies/github-general-privacy-statement).
  The Microsoft Store version never makes this request: the Store handles its
  updates.
- **Links you click.** Buttons such as **View release** open pages in your
  browser; nothing is sent until you click them.
- **Inside a sandbox.** Network access of the Windows Sandbox environments you
  launch is governed by your profile's Networking setting and by Windows, not by
  SandboxStudio.

## Data stored on your computer

| Location | Contents |
|---|---|
| `%LOCALAPPDATA%\SandboxStudio` | Profiles (JSON) and app settings such as the language |
| `%USERPROFILE%\.sandboxstudio` | Logon scripts mapped into sandboxes, and temporary `.wsb` files for launches |
| `%TEMP%\.net\SandboxStudio-*` | The portable exe's unpacked runtime files |

The Microsoft Store version may keep its `%LOCALAPPDATA%` data in Windows' private
per-app storage instead; Windows removes it when you uninstall the app.

## Microsoft Store

If you install SandboxStudio from the Microsoft Store, Microsoft processes data
about the download and installation under the [Microsoft Privacy
Statement](https://privacy.microsoft.com/privacystatement). SandboxStudio itself
receives none of it.

## Uninstalling

- **Microsoft Store version:** uninstall it from **Settings › Apps**.
- **Portable exe:** delete the exe, then optionally the three folders above.

## Contact

Questions about this policy: open an issue at
<https://github.com/PAumedes/SandboxStudio/issues>.
