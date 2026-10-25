# Security Policy

## Supported versions

Security fixes go into the latest release. Please update to the newest version from
[GitHub Releases](https://github.com/PAumedes/SandboxStudio/releases) or the
Microsoft Store before reporting.

## Reporting a vulnerability

**Please do not open a public issue for security problems.**

Report privately through GitHub: **Security › Report a vulnerability** on
<https://github.com/PAumedes/SandboxStudio/security/advisories/new>. Include the
version, Windows build, steps to reproduce and the impact you observed.

You can expect an acknowledgement within 7 days. Once a fix is available it is
released, and the advisory is published with credit to you unless you prefer
otherwise.

## Scope

In scope: SandboxStudio itself — profile handling, `.wsb` import and export, the
logon scripts it writes, the way it starts `wsb.exe` and `WindowsSandbox.exe`, and
the release and signing pipeline.

Out of scope: weaknesses in Windows Sandbox or Windows itself; report those to the
[Microsoft Security Response Center](https://msrc.microsoft.com/report).

Remember that a sandbox's isolation depends on its profile: writable mapped
folders, networking and clipboard sharing all let a sandbox reach the host.
SandboxStudio warns about writable mappings, but the profile settings are yours.
