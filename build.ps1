# Builds the plugin and packs dist\MapHints-<version>.zip, laid out for extracting into the game folder
# (BepInEx\plugins\MapHints\MapHints.dll), plus SHA256SUMS.txt.
#
# Needs: .NET SDK 6+ and a game install where BepInEx has been started once (it generates the
# BepInEx\interop assemblies this project compiles against). Run:  powershell -File build.ps1
param(
    [string]$GameDir = "C:\Program Files (x86)\Steam\steamapps\common\Castlevania Belmont's Curse\"
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
Set-Location $root
Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem

function Get-Sha256([string]$path) {
    $sha = [System.Security.Cryptography.SHA256]::Create()
    $stream = [System.IO.File]::OpenRead($path)
    try { return ([BitConverter]::ToString($sha.ComputeHash($stream)) -replace '-', '').ToLower() }
    finally { $stream.Dispose(); $sha.Dispose() }
}

# Forward-slash entry names (Compress-Archive in Windows PowerShell 5.1 writes backslashes).
function New-Zip([string]$sourceDir, [string]$zipPath) {
    if (Test-Path $zipPath) { Remove-Item $zipPath }
    $base = (Resolve-Path $sourceDir).Path.TrimEnd('\')
    $fs = [System.IO.File]::Create($zipPath)
    $zip = New-Object System.IO.Compression.ZipArchive($fs, [System.IO.Compression.ZipArchiveMode]::Create)
    try {
        foreach ($f in Get-ChildItem $base -Recurse -File) {
            $name = $f.FullName.Substring($base.Length + 1).Replace('\', '/')
            [void][System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile($zip, $f.FullName, $name, [System.IO.Compression.CompressionLevel]::Optimal)
        }
    } finally { $zip.Dispose(); $fs.Dispose() }
}

if (-not $GameDir.EndsWith("\")) { $GameDir += "\" }
if (-not (Test-Path "$($GameDir)BepInEx\interop\Assembly-CSharp.dll")) {
    throw "Assembly-CSharp.dll not found under '$($GameDir)BepInEx\interop'. Install BepInEx and start the game once, or pass -GameDir."
}

$csproj = Join-Path $root "src\MapHints\MapHints.csproj"
$version = ([xml](Get-Content $csproj)).Project.PropertyGroup.Version | Where-Object { $_ } | Select-Object -First 1
Write-Host "Building Map Hints $version ..."
dotnet build $csproj -c Release -p:Deploy=false "-p:GameDir=$($GameDir.TrimEnd('\'))"
if ($LASTEXITCODE -ne 0) { throw "dotnet build failed" }
$dll = Join-Path $root "src\MapHints\bin\Release\net6.0\MapHints.dll"

$dist = Join-Path $root "dist"
if (Test-Path $dist) { Remove-Item $dist -Recurse -Force }
$name = "MapHints-$version"
$stage = Join-Path $dist $name
$pluginDir = Join-Path $stage "BepInEx\plugins\MapHints"
New-Item -ItemType Directory -Force $pluginDir | Out-Null
Copy-Item $dll $pluginDir
Copy-Item "$root\packaging\README.txt" (Join-Path $stage "README.txt")
Copy-Item "$root\LICENSE", "$root\THIRD-PARTY-NOTICES.md" $stage
$zipPath = Join-Path $dist "$name.zip"
New-Zip $stage $zipPath

$lines = @(("{0} *{1}" -f (Get-Sha256 $zipPath), (Split-Path -Leaf $zipPath)), ("{0} *MapHints.dll (inside the zip)" -f (Get-Sha256 $dll)))
Set-Content -Path (Join-Path $dist "SHA256SUMS.txt") -Value $lines -Encoding ASCII
Write-Host ""
$lines | ForEach-Object { Write-Host $_ }
