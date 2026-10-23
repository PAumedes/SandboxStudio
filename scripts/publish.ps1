# Publishes a self-contained Windows x64 folder distribution of SandboxStudio
# and packages it as a zip under dist/. This is a folder distribution — not a
# single executable — so the Windows App SDK self-contained payload stays
# intact next to the app.
param(
    [string]$Version = "",
    [string]$RuntimeIdentifier = "win-x64",
    [string]$OutputRoot = "dist"
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot
$uiProject = Join-Path $repoRoot "src/SandboxStudio.UI/SandboxStudio.UI.csproj"

# Resolve the version from MSBuild unless the caller overrode it.
if ([string]::IsNullOrWhiteSpace($Version)) {
    $Version = dotnet msbuild $uiProject -getProperty:Version
    if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($Version)) {
        throw "Could not resolve the project Version; pass -Version explicitly."
    }
    $Version = $Version.Trim()
}

$publishDir = Join-Path $repoRoot "$OutputRoot/SandboxStudio"
$zipName = "SandboxStudio-$Version-$RuntimeIdentifier.zip"
$zipPath = Join-Path $repoRoot "$OutputRoot/$zipName"

Write-Host "Publishing SandboxStudio $Version ($RuntimeIdentifier) to $publishDir"
dotnet publish $uiProject `
    -c Release `
    -r $RuntimeIdentifier `
    --self-contained true `
    -p:PublishSingleFile=false `
    -p:Version=$Version `
    -o $publishDir
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed with exit code $LASTEXITCODE." }

# Strip debug artifacts from the shipped folder; only known doc-xml names are
# removed so a future runtime .xml is never silently deleted.
Get-ChildItem $publishDir -Recurse -Include *.pdb, SandboxStudio.*.xml | Remove-Item -Force

if (Test-Path $zipPath) { Remove-Item $zipPath -Force }
# The zip contains the SandboxStudio folder itself so "extract" yields one tidy directory.
Compress-Archive -Path $publishDir -DestinationPath $zipPath -CompressionLevel Optimal

Write-Host "Distribution written to $publishDir"
Write-Host "Archive written to $zipPath"
