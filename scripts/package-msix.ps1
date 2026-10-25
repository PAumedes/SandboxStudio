# Builds the Microsoft Store packages under dist/msix/:
#   dist/msix/SandboxStudio_<version>.0.msixbundle    (all architectures; upload this)
#   dist/msix/SandboxStudio_<version>.0_<arch>.msix   (one per architecture)
# Packages are unsigned on purpose: Partner Center signs Store packages with
# Microsoft's certificate. The Identity values must match the app reserved in
# Partner Center (Product management > Product identity); the defaults only
# suit local builds.
param(
    [string]$Version = "",
    [string[]]$Platforms = @("x64", "arm64"),
    [string]$IdentityName = "SandboxStudio.Dev",
    [string]$Publisher = "CN=SandboxStudio Dev",
    [string]$PublisherDisplayName = "SandboxStudio Contributors",
    [string]$OutputRoot = "dist"
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot
$uiDir = Join-Path $repoRoot "src/SandboxStudio.UI"
$uiProject = Join-Path $uiDir "SandboxStudio.UI.csproj"
$manifestPath = Join-Path $uiDir "Package.appxmanifest"

if ([string]::IsNullOrWhiteSpace($Version)) {
    $Version = dotnet msbuild $uiProject -getProperty:Version
    if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($Version)) {
        throw "Could not resolve the project Version; pass -Version explicitly."
    }
    $Version = $Version.Trim()
}

# MSIX versions are four numeric parts, and the Store requires the last to be 0.
if ($Version -notmatch '^(\d+)\.(\d+)\.(\d+)(-[0-9A-Za-z.-]+)?$') {
    throw "Version '$Version' is not a valid semver."
}
$packageVersion = "$($Matches[1]).$($Matches[2]).$($Matches[3]).0"

$outputDir = Join-Path $repoRoot (Join-Path $OutputRoot "msix")
if (Test-Path $outputDir) { Remove-Item $outputDir -Recurse -Force }
New-Item -ItemType Directory -Path $outputDir | Out-Null

# Stamp the identity and version into the manifest for this build only.
$originalManifest = [System.IO.File]::ReadAllText($manifestPath)
try {
    [xml]$manifest = $originalManifest
    $manifest.Package.Identity.Name = $IdentityName
    $manifest.Package.Identity.Publisher = $Publisher
    $manifest.Package.Identity.Version = $packageVersion
    $manifest.Package.Properties.PublisherDisplayName = $PublisherDisplayName
    $manifest.Save($manifestPath)

    foreach ($platform in $Platforms) {
        $rid = "win-$platform"
        Write-Host "Packaging SandboxStudio $packageVersion ($platform) as $IdentityName"
        dotnet build $uiProject `
            -c Release `
            -r $rid `
            -p:Platform=$platform `
            -p:WindowsPackageType=MSIX `
            -p:Version=$Version `
            -p:AppxPackageDir="$outputDir\$platform\"
        if ($LASTEXITCODE -ne 0) { throw "MSIX build for $platform failed with exit code $LASTEXITCODE." }
    }
}
finally {
    [System.IO.File]::WriteAllText($manifestPath, $originalManifest)
}

# Flatten the per-platform layouts into the upload files Partner Center needs.
$packages = @(Get-ChildItem $outputDir -Recurse -File -Filter *.msix)
if ($packages.Count -ne $Platforms.Count) {
    throw "Expected $($Platforms.Count) .msix packages under $outputDir, found $($packages.Count)."
}
foreach ($package in $packages) {
    $name = $package.Name -replace '^SandboxStudio\.UI_', 'SandboxStudio_'
    Move-Item $package.FullName (Join-Path $outputDir $name) -Force
}
Get-ChildItem $outputDir -Directory | Remove-Item -Recurse -Force

# One bundle with every architecture, so a Store submission is a single upload.
# makeappx ships in the Windows SDK build-tools package the project restores.
[xml]$packageVersions = Get-Content (Join-Path $repoRoot "Directory.Packages.props")
$buildToolsVersion = ($packageVersions.Project.ItemGroup.PackageVersion |
    Where-Object Include -eq "Microsoft.Windows.SDK.BuildTools").Version
$nugetRoot = if ($env:NUGET_PACKAGES) { $env:NUGET_PACKAGES } else { Join-Path $HOME ".nuget/packages" }
$makeAppx = Get-ChildItem (Join-Path $nugetRoot "microsoft.windows.sdk.buildtools/$buildToolsVersion/bin") -Recurse -Filter makeappx.exe |
    Where-Object { $_.Directory.Name -eq "x64" } | Select-Object -First 1
if (-not $makeAppx) { throw "makeappx.exe not found in Microsoft.Windows.SDK.BuildTools $buildToolsVersion." }

$bundleInput = Join-Path $outputDir "bundle-input"
New-Item -ItemType Directory -Path $bundleInput | Out-Null
Get-ChildItem $outputDir -File -Filter *.msix | Copy-Item -Destination $bundleInput
$bundlePath = Join-Path $outputDir "SandboxStudio_$packageVersion.msixbundle"
& $makeAppx.FullName bundle /d $bundleInput /p $bundlePath /bv $packageVersion /o | Out-Null
if ($LASTEXITCODE -ne 0) { throw "makeappx bundle failed with exit code $LASTEXITCODE." }
Remove-Item $bundleInput -Recurse -Force

Get-ChildItem $outputDir -File | ForEach-Object {
    Write-Host ("{0}  {1:N1} MB" -f $_.Name, ($_.Length / 1MB))
}
