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
New-Item -ItemType Directory -Force -Path $outDir | Out-Null

Copy-Item $dllPath -Destination $outDir
Copy-Item "$root\README.md" -Destination $outDir
Copy-Item "$root\README_RU.md" -Destination $outDir
Copy-Item "$root\LICENSE" -Destination $outDir -ErrorAction SilentlyContinue
Copy-Item "$root\CHANGELOG.md" -Destination $outDir

$zipPath = "$root\release\KeePassCopyKey-v$version.zip"
if (Test-Path $zipPath) { Remove-Item $zipPath }
Compress-Archive -Path "$outDir\*" -DestinationPath $zipPath

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
Write-Host "  Create a GitHub release for the tag, attach the zip and SHA256SUMS file,"
Write-Host "  paste the matching section from CHANGELOG.md as the release notes."
