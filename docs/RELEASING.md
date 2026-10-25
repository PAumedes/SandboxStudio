# Releasing SandboxStudio

SandboxStudio ships through two channels, both built from the same commit by the
**Release** workflow (`.github/workflows/release.yml`):

| Channel | Artifact | Signed by | Updates |
|---|---|---|---|
| GitHub Releases | `SandboxStudio-<version>-win-x64.exe` (portable, single file) | SignPath Foundation (Authenticode) | In-app "Update available" banner |
| Microsoft Store | `SandboxStudio_<version>.0.msixbundle` (x64 + arm64) | Microsoft, at Store ingestion | Automatic, through the Store |

Signing and Store submission are opt-in: until the variables below exist, the
workflow still builds, tests and packages everything, and publishes an unsigned exe.

## Cutting a release

1. Bump `<Version>` in `Directory.Build.props` and move the `Unreleased` notes in
   `CHANGELOG.md` under the new version. Merge to `main`.
2. Run **Actions › Release › Run workflow** on `main` with **create_release** (and
   **submit_to_store** once the Store is set up).
3. Approve the signing request in SignPath (the job waits up to an hour). The
   release job verifies the Authenticode signature, writes the SHA-256, tags
   `v<version>` and publishes the GitHub release.
4. In Partner Center, open the draft submission the workflow created, update the
   release notes and **Submit to the Store** for certification.

## One-time setup: SignPath (free code signing for the exe)

SignPath Foundation signs open-source projects for free. SandboxStudio already
meets the published conditions: OSI license (MIT), public source, binaries built
by an automated CI workflow, a **Code signing policy** section in the README with
the team roles, and a privacy policy ([PRIVACY.md](../PRIVACY.md)).

1. Enable multi-factor authentication on GitHub (required for every team member).
2. Apply at <https://signpath.org/apply> with the repository URL.
3. Once accepted, in the SignPath organization:
   - Add the predefined **GitHub.com** trusted build system and link it to the project.
   - Project slug `SandboxStudio`; signing policy `release-signing`, with manual
     approval by an approver listed in the README.
   - Artifact configuration: paste [.signpath/artifact-configuration.xml](../.signpath/artifact-configuration.xml).
   - Create a CI user with the *submitter* role and generate an API token.
4. In GitHub (**Settings › Secrets and variables › Actions**):

| Kind | Name | Value |
|---|---|---|
| Secret | `SIGNPATH_API_TOKEN` | The CI user's API token |
| Variable | `SIGNPATH_ORGANIZATION_ID` | SignPath organization ID (enables the signing job) |
| Variable (optional) | `SIGNPATH_PROJECT_SLUG` | Defaults to `SandboxStudio` |
| Variable (optional) | `SIGNPATH_SIGNING_POLICY_SLUG` | Defaults to `release-signing` |
| Variable (optional) | `SIGNPATH_ARTIFACT_CONFIGURATION_SLUG` | Defaults to the project's default configuration |

Signing builds SmartScreen reputation over time; the first signed releases may
still show a warning until enough people have run them.

## One-time setup: Microsoft Store

1. Register a developer account at <https://storedeveloper.microsoft.com> (free
   for individuals).
2. In Partner Center, **reserve the name** `SandboxStudio`, then copy the values
   from **Product management › Product identity** into GitHub variables:

| Variable | Partner Center field |
|---|---|
| `STORE_IDENTITY_NAME` | Package/Identity/Name |
| `STORE_PUBLISHER` | Package/Identity/Publisher (`CN=…`) |
| `STORE_PUBLISHER_DISPLAY_NAME` | Package/Properties/PublisherDisplayName |
| `STORE_PRODUCT_ID` | Store ID (12 characters, e.g. `9N…`) |

3. **First submission (manual):** run the Release workflow, download the
   `SandboxStudio-<version>-msix` artifact and upload the `.msixbundle` in a new
   submission. Fill in:
   - **Privacy policy URL:** `https://github.com/PAumedes/SandboxStudio/blob/main/PRIVACY.md`
   - **Restricted capability `runFullTrust`:** *SandboxStudio is a desktop
     (Win32) app that manages Windows Sandbox. It starts the inbox `wsb.exe` and
     `WindowsSandbox.exe` tools on the host to launch and control sandboxes,
     which requires full trust.*
   - **System requirements:** Windows 10 1903 or later on Pro, Enterprise or
     Education with the Windows Sandbox optional feature enabled.
   - Screenshots, description, category (Developer tools) and age rating.
4. **Automated draft submissions:** in Partner Center, **Account settings › User
   management › Microsoft Entra applications**, add an application with the
   *Manager* role and create a client secret. Then add these GitHub secrets:
   `PARTNER_CENTER_TENANT_ID`, `PARTNER_CENTER_SELLER_ID`,
   `PARTNER_CENTER_CLIENT_ID`, `PARTNER_CENTER_CLIENT_SECRET`.

The workflow uploads with `msstore publish --noCommit`, so every Store release
stays a draft until someone submits it in Partner Center.

## Local builds

```powershell
.\scripts\publish.ps1                    # dist\SandboxStudio-<version>-win-x64.exe (+ .sha256)
.\scripts\package-msix.ps1               # dist\msix\*.msix and the .msixbundle
.\scripts\release.ps1 -Launch            # build, test, package and smoke-launch the exe
```

Local builds are unsigned and for testing only; signed releases come from the
workflow, as the SignPath policy requires.

To run the Store build locally, turn on **Developer Mode** (Settings › System ›
For developers) and register the packaged layout instead of installing the
unsigned package:

```powershell
.\scripts\package-msix.ps1 -Platforms x64
Add-AppxPackage -Register .\src\SandboxStudio.UI\bin\x64\Release\net10.0-windows10.0.19041.0\win-x64\AppxManifest.xml
```

Remove it afterwards with `Get-AppxPackage SandboxStudio.Dev | Remove-AppxPackage`.
