# Local release pipeline for SandboxStudio.
#
# Runs the full release gate locally — build, tests, packaging, checksum,
# release notes — and optionally pushes a tag and GitHub release via gh.
#
# Typical use:
#   .\scripts\release.ps1                          # package only, project Version
#   .\scripts\release.ps1 -Version 0.2.0           # package a specific version
#   .\scripts\release.ps1 -PublishRelease          # also create GitHub release
#   .\scripts\release.ps1 -SkipTests -AllowDirty   # quick repackage during dev
#   .\scripts\release.ps1 -Launch                  # smoke-launch the exe after packaging
param(
    [string]$Version = "",
    [string]$RuntimeIdentifier = "win-x64",
    [string]$OutputRoot = "dist",
    [switch]$SkipTests,
    [switch]$AllowDirty,
    [switch]$PublishRelease,
    [switch]$Draft,
    [switch]$Launch
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot
Push-Location $repoRoot
try {
    # ------------------------------------------------------------------ SDK
    $localDotnet = Join-Path $HOME '.dotnet'
    if (Test-Path (Join-Path $localDotnet 'dotnet.exe')) {
        $env:DOTNET_ROOT = $localDotnet
        $env:PATH = "$localDotnet;$env:PATH"
    }
    $dotnet = Get-Command dotnet -ErrorAction SilentlyContinue
    if (-not $dotnet) { throw "dotnet SDK not found. Install .NET 10 SDK or add it to PATH." }
    Write-Host "==> dotnet $((& dotnet --version))"

    # -------------------------------------------------------------- version
    $uiProject = Join-Path $repoRoot "src/SandboxStudio.UI/SandboxStudio.UI.csproj"
    if ([string]::IsNullOrWhiteSpace($Version)) {
        $Version = (dotnet msbuild $uiProject -getProperty:Version).Trim()
        if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($Version)) {
            throw "Could not resolve the project Version; pass -Version explicitly."
        }
    }
    if ($Version -notmatch '^\d+\.\d+\.\d+(-[0-9A-Za-z.-]+)?$') {
        throw "Version '$Version' is not a valid semver (e.g. 1.0.0 or 1.0.0-beta.1)."
    }
    $tag = "v$Version"
    Write-Host "==> Release version: $Version  (tag $tag, $RuntimeIdentifier)"

    # --------------------------------------------------------------- checks
    if (-not $AllowDirty) {
        $dirty = git status --porcelain
        if ($LASTEXITCODE -ne 0) { throw "git status failed." }
        if ($dirty) {
            throw "Working tree is dirty. Commit or stash changes, or pass -AllowDirty.`n$dirty"
        }
    }

    # ----------------------------------------------------------------- build
    Write-Host "`n==> Building solution (Release)..."
    dotnet build SandboxStudio.sln -c Release
    if ($LASTEXITCODE -ne 0) { throw "Build failed with exit code $LASTEXITCODE." }

    # ----------------------------------------------------------------- tests
    if ($SkipTests) {
        Write-Host "`n==> Skipping tests (-SkipTests)."
    }
    else {
        foreach ($suite in @("Unit", "Integration", "Smoke")) {
            $proj = "tests/SandboxStudio.Tests.$suite/SandboxStudio.Tests.$suite.csproj"
            Write-Host "`n==> Running $suite tests..."
            dotnet test $proj -c Release --no-build
            if ($LASTEXITCODE -ne 0) { throw "$suite tests failed." }
        }
    }

    # -------------------------------------------------------------- package
    Write-Host "`n==> Publishing single-file executable..."
    & (Join-Path $PSScriptRoot "publish.ps1") -Version $Version -RuntimeIdentifier $RuntimeIdentifier -OutputRoot $OutputRoot
    if ($LASTEXITCODE -ne 0) { throw "publish.ps1 failed." }

    $exeName = "SandboxStudio-$Version-$RuntimeIdentifier.exe"
    $exePath = Join-Path $repoRoot "$OutputRoot/$exeName"
    $shaFile = "$exePath.sha256"
    if (-not (Test-Path $exePath)) { throw "Expected executable not found: $exePath" }
    if (-not (Test-Path $shaFile)) { throw "Expected checksum not found: $shaFile" }
    $sha = ((Get-Content $shaFile -Raw) -split '\s+')[0]
    Write-Host "`n==> SHA256: $sha"

    # ---------------------------------------------------------- release notes
    $notesFile = Join-Path $repoRoot "$OutputRoot/release-notes-$Version.md"
    $prevTag = git tag --sort=-version:refname | Where-Object { $_ -ne $tag } | Select-Object -First 1
    $range = if ($prevTag) { "$prevTag..HEAD" } else { "HEAD" }
    $commitLines = git log $range --pretty=format:"- %s (%h)" --no-merges
    $commits = if ($commitLines) { $commitLines -join "`n" } else { "- No new commits since $prevTag" }
    $exeMB = [math]::Round((Get-Item $exePath).Length / 1MB, 1)

    @"
## SandboxStudio $tag

Portable Windows x64 app in a single file. Download ``$exeName`` and run it — no installer, no .NET or Windows App SDK install needed. The first launch of each version takes a few extra seconds while it unpacks to ``%TEMP%\.net``.

### What's in this release
$commits

### Artifacts
| File | Size | SHA-256 |
|---|---|---|
| ``$exeName`` | $exeMB MB | ``$sha`` |

*Runtime: $RuntimeIdentifier · self-contained single file*

> Built locally by ``scripts/release.ps1``.
"@ | Out-File -FilePath $notesFile -Encoding utf8
    Write-Host "==> Release notes: $notesFile"

    # ------------------------------------------------------------ smoke test
    if ($Launch) {
        Write-Host "`n==> Smoke-launching $exePath ..."
        $proc = Start-Process $exePath -PassThru
        # The first launch of a new build unpacks the bundle before showing a window.
        Start-Sleep -Seconds 30
        if ($proc.HasExited) {
            throw "App exited prematurely (code $($proc.ExitCode))."
        }
        Write-Host "    App running (pid $($proc.Id)); closing."
        Stop-Process -Id $proc.Id -Force
    }

    # --------------------------------------------------------- github release
    if ($PublishRelease) {
        $gh = Get-Command gh -ErrorAction SilentlyContinue
        if (-not $gh) { throw "GitHub CLI (gh) not found on PATH." }
        Write-Host "`n==> Checking gh auth..."
        gh auth status 2>&1 | Out-Null
        if ($LASTEXITCODE -ne 0) { throw "gh not authenticated. Run 'gh auth login' first." }

        $existing = git tag -l $tag
        if ($existing -and -not $Draft) {
            throw "Tag $tag already exists. Bump -Version or delete the tag first."
        }

        Write-Host "==> Creating annotated tag $tag ..."
        git tag -a $tag -m "SandboxStudio $Version"
        if ($LASTEXITCODE -ne 0) { throw "git tag failed." }
        git push origin $tag
        if ($LASTEXITCODE -ne 0) { throw "git push tag failed." }

        $ghArgs = @("release", "create", $tag, $exePath, $shaFile,
                    "--title", "SandboxStudio $tag",
                    "--notes-file", $notesFile,
                    "--repo", "PAumedes/SandboxStudio")
        if ($Draft) { $ghArgs += "--draft" }
        Write-Host "==> gh release create $tag ..."
        gh @ghArgs
        if ($LASTEXITCODE -ne 0) { throw "gh release create failed." }
        Write-Host "==> GitHub release $tag created."
    }

    # --------------------------------------------------------------- summary
    Write-Host "`n=================================================="
    Write-Host "  Release $Version complete"
    Write-Host "  Exe    : $exePath  ($exeMB MB)"
    Write-Host "  SHA256 : $shaFile"
    Write-Host "  Notes  : $notesFile"
    if (-not $PublishRelease) {
        Write-Host ""
        Write-Host "  To publish to GitHub later:"
        Write-Host "    gh release create $tag `"$exePath`" `"$shaFile`" --title `"SandboxStudio $tag`" --notes-file `"$notesFile`""
    }
    Write-Host "=================================================="
}
finally {
    Pop-Location
}
