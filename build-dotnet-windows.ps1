param([switch]$BundleAdb)
$ErrorActionPreference = "Stop"
$project = Join-Path $PSScriptRoot "src/AndroidCleanerPro/AndroidCleanerPro.csproj"
$assets = Join-Path $PSScriptRoot "src/AndroidCleanerPro/Assets"
New-Item -ItemType Directory -Path $assets -Force | Out-Null
$adbZip = Join-Path $assets "platform-tools.zip"
# Download Google's official Windows Platform Tools during build, embed in EXE as a resource.
Invoke-WebRequest -Uri "https://dl.google.com/android/repository/platform-tools-latest-windows.zip" -OutFile $adbZip
Add-Type -AssemblyName System.IO.Compression
$archive = [System.IO.Compression.ZipFile]::OpenRead($adbZip)
try {
  if (-not ($archive.Entries | Where-Object { $_.FullName -eq "platform-tools/adb.exe" })) { throw "Google Platform Tools archive does not contain adb.exe" }
} finally { $archive.Dispose() }
$publish = Join-Path $PSScriptRoot "dist/win-x64"
dotnet publish $project -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:EnableCompressionInSingleFile=true -o $publish
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed" }
$exe = Join-Path $publish "AndroidCleanerPro.exe"
if (-not (Test-Path $exe)) { throw "Portable executable missing after publish" }
$zip = Join-Path $PSScriptRoot "dist/AndroidCleanerPro-Portable-win-x64.zip"
if (Test-Path $zip) { Remove-Item $zip -Force }
Compress-Archive -Path $exe -DestinationPath $zip -CompressionLevel Optimal
Write-Host "Built single EXE with embedded Google ADB: $exe"
Write-Host "Portable ZIP: $zip"
