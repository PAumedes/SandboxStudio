# Publishes SandboxStudio as a single portable executable under dist/:
#   dist/SandboxStudio-<version>-<rid>.exe
#   dist/SandboxStudio-<version>-<rid>.exe.sha256
# The project bundles the .NET runtime, the Windows App SDK payload, and the
# app's content into that one file (see SandboxStudio.UI.csproj).
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

$outputDir = Join-Path $repoRoot $OutputRoot
$publishDir = Join-Path $outputDir "publish"
$exeName = "SandboxStudio-$Version-$RuntimeIdentifier.exe"
$exePath = Join-Path $outputDir $exeName
$shaPath = "$exePath.sha256"

Write-Host "Publishing SandboxStudio $Version ($RuntimeIdentifier) to $exePath"
# dotnet publish never deletes existing files, so clear leftovers from an earlier build.
if (Test-Path $publishDir) { Remove-Item $publishDir -Recurse -Force }
dotnet publish $uiProject `
    -c Release `
    -r $RuntimeIdentifier `
    -p:Version=$Version `
    -o $publishDir
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed with exit code $LASTEXITCODE." }

# Everything except debug symbols must be inside the bundle; a loose file here
# means the single-file publish regressed and the exe would not run alone.
$shipped = @(Get-ChildItem $publishDir -Recurse -File | Where-Object { $_.Extension -ne ".pdb" })
if ($shipped.Count -ne 1 -or $shipped[0].Name -ne "SandboxStudio.UI.exe") {
    throw "Expected only SandboxStudio.UI.exe in $publishDir, found: $(($shipped | ForEach-Object Name) -join ', ')"
}

Copy-Item $shipped[0].FullName $exePath -Force
$sha = (Get-FileHash $exePath -Algorithm SHA256).Hash.ToLower()
"$sha  $exeName" | Out-File -FilePath $shaPath -Encoding ascii -NoNewline

Write-Host "Executable written to $exePath"
Write-Host "SHA256 $sha written to $shaPath"
