#Requires -Version 5.1
<#
.SYNOPSIS
    Builds KeePassCopyKey in Release, runs tests, and packages a
    versioned release zip with a SHA256SUMS file next to it.

.DESCRIPTION
    Deliberately a plain script, not a GitHub Actions workflow - the
    reproducible-build guarantee (Directory.Build.props) only holds if
    everyone building a release uses the same KeePass.exe version, which
    is a local machine concern, not something CI can silently guarantee
    for you. Run this after pinning KeePassDir to the release-designated
    KeePass version.

    Note: if you publish this on GitHub, every tag automatically gets a
    "Source code (zip/tar.gz)" archive of the whole repo - that's git
    hosting behavior, not something this script (or any script)
    controls. The zip this script produces is a separate, additional
    release asset containing only the compiled DLL and docs - link to
    THAT asset specifically in release notes if you don't want people
    grabbing the raw source archive by mistake.

.EXAMPLE
    .\release.ps1
    .\release.ps1 -KeePassDir "D:\program\TC UP\MEDIA\Programs\KeePass"
#>

param(
    [string]$KeePassDir = ""
)

$ErrorActionPreference = "Stop"
$root = $PSScriptRoot
$version = ([xml](Get-Content "$root\KeePassCopyKey\KeePassCopyKey.csproj")).Project.PropertyGroup.Version | Where-Object { $_ } | Select-Object -First 1

if (-not $version) {
    throw "Could not read <Version> from KeePassCopyKey.csproj"
}

Write-Host "Building KeePassCopyKey v$version (Release)..." -ForegroundColor Cyan

$buildArgs = @("build", "$root\KeePassCopyKey.sln", "-c", "Release")
if ($KeePassDir) { $buildArgs += "-p:KeePassDir=$KeePassDir" }

& dotnet @buildArgs
if ($LASTEXITCODE -ne 0) { throw "Build failed (dotnet exit code $LASTEXITCODE) - this also runs the test suite via Directory.Build.targets, so a test failure stops the release here too." }

$dllPath = "$root\KeePassCopyKey\bin\Release\net48\KeePassCopyKey.dll"
if (-not (Test-Path $dllPath)) {
    throw "Expected DLL not found at $dllPath"
}

$outDir = "$root\release\v$version"
if (Test-Path $outDir) { Remove-Item $outDir -Recurse -Force }
New-Item -ItemType Directory -Force -Path $outDir | Out-Null

# Explicit allow-list, not a folder copy - this is the only thing that
# goes into the release zip. If you ever need to add a file, add it
# here by name; never change this to "copy everything from X".
$filesToPackage = @(
    $dllPath,
    "$root\README.md",
    "$root\README_RU.md",
    "$root\LICENSE",
    "$root\CHANGELOG.md"
)

foreach ($file in $filesToPackage) {
    if (-not (Test-Path $file)) {
        Write-Warning "Skipping missing file: $file"
        continue
    }
    Copy-Item $file -Destination $outDir
}

Write-Host ""
Write-Host "Files staged for the release zip:" -ForegroundColor Cyan
Get-ChildItem $outDir | ForEach-Object { Write-Host "  $($_.Name)" }

# Hard safety check: this package must never contain source files.
$sourceLeaks = Get-ChildItem $outDir -Recurse -Include *.cs, *.csproj, *.sln
if ($sourceLeaks) {
    throw "Refusing to package: found source files in the staged release folder: $($sourceLeaks.Name -join ', ')"
}

$zipPath = "$root\release\KeePassCopyKey-v$version.zip"
if (Test-Path $zipPath) { Remove-Item $zipPath }
Compress-Archive -Path "$outDir\*" -DestinationPath $zipPath -Force

$hash = Get-FileHash $dllPath -Algorithm SHA256
$sumsPath = "$root\release\SHA256SUMS-v$version.txt"
"$($hash.Hash.ToLower())  KeePassCopyKey.dll" | Out-File -FilePath $sumsPath -Encoding ascii

Write-Host ""
Write-Host "Release v$version ready:" -ForegroundColor Green
Write-Host "  Zip:  $zipPath"
Write-Host "  Hash: $sumsPath"
Write-Host "  SHA256(KeePassCopyKey.dll) = $($hash.Hash)"
Write-Host ""
Write-Host "Next steps:" -ForegroundColor Yellow
Write-Host "  git tag v$version"
Write-Host "  git push origin v$version"
Write-Host "  Create a GitHub release for the tag, attach $([System.IO.Path]::GetFileName($zipPath)) and the SHA256SUMS file"
Write-Host "  as release assets. GitHub will ALSO auto-attach a full source-code zip to the tag -"
Write-Host "  that's separate and unavoidable; make it clear in the release notes which link is which."