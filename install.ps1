#Requires -Version 5.1
[CmdletBinding()]
param(
    [string]$Version
)

$ErrorActionPreference = 'Stop'

$Repo       = "jamescanady/ce-platform-symplr-cli"
$InstallDir = Join-Path $env:LOCALAPPDATA "Programs\symplr"
$Rid        = "win-x64"

# ─── version ──────────────────────────────────────────────────────────────────

if (-not $Version) {
    $latest  = Invoke-RestMethod "https://api.github.com/repos/$Repo/releases/latest"
    $Version = $latest.tag_name
}

if (-not $Version.StartsWith('v')) { $Version = "v$Version" }
$VersionNum = $Version.TrimStart('v')

# ─── download & install ───────────────────────────────────────────────────────

$Filename = "symplr-$VersionNum-$Rid.zip"
$Url      = "https://github.com/$Repo/releases/download/$Version/$Filename"

Write-Host "Installing symplr $Version ($Rid)..."

$Tmp = Join-Path ([System.IO.Path]::GetTempPath()) ([System.IO.Path]::GetRandomFileName())
New-Item -ItemType Directory -Path $Tmp | Out-Null
try {
    $ZipPath = Join-Path $Tmp $Filename
    Invoke-WebRequest -Uri $Url -OutFile $ZipPath -UseBasicParsing
    Expand-Archive -Path $ZipPath -DestinationPath $Tmp -Force

    New-Item -ItemType Directory -Path $InstallDir -Force | Out-Null
    Copy-Item (Join-Path $Tmp "symplr.exe") (Join-Path $InstallDir "symplr.exe") -Force
}
finally {
    Remove-Item $Tmp -Recurse -Force -ErrorAction SilentlyContinue
}

# ─── PATH ─────────────────────────────────────────────────────────────────────

$UserPath = [Environment]::GetEnvironmentVariable('PATH', 'User')
if ($UserPath -notlike "*$InstallDir*") {
    [Environment]::SetEnvironmentVariable('PATH', "$UserPath;$InstallDir", 'User')
    Write-Host "Added to PATH: $InstallDir"
    Write-Host "Restart your terminal for the PATH change to take effect."
}

Write-Host "Installed: $(Join-Path $InstallDir 'symplr.exe')"
