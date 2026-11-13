param([switch]$BundleAdb)
$ErrorActionPreference = "Stop"
$publish = Join-Path $PSScriptRoot "dist/win-x64"
dotnet publish (Join-Path $PSScriptRoot "src/AndroidCleanerPro/AndroidCleanerPro.csproj") -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:EnableCompressionInSingleFile=true -o $publish
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed" }
if ($BundleAdb) {
  $url = "https://dl.google.com/android/repository/platform-tools-latest-windows.zip"
  $download = Join-Path $env:TEMP "android-cleaner-platform-tools.zip"
  Invoke-WebRequest -Uri $url -OutFile $download
  Expand-Archive -LiteralPath $download -DestinationPath $publish -Force
  Remove-Item $download -Force
  if (-not (Test-Path (Join-Path $publish "platform-tools/adb.exe"))) { throw "ADB bundle missing" }
}
$zip = Join-Path $PSScriptRoot "dist/AndroidCleanerPro-Portable-win-x64.zip"
if (Test-Path $zip) { Remove-Item $zip -Force }
Compress-Archive -Path (Join-Path $publish "*") -DestinationPath $zip -CompressionLevel Optimal
Write-Host "Portable ZIP: $zip"
Write-Host "Extract the ZIP before launching AndroidCleanerPro.exe."
if (-not $BundleAdb) { Write-Warning "ADB not bundled; pass -BundleAdb or install Android Platform Tools separately." }
