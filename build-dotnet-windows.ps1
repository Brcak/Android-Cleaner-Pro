$ErrorActionPreference = "Stop"
$project = Join-Path $PSScriptRoot "src/AndroidCleanerPro/AndroidCleanerPro.csproj"
$output = Join-Path $PSScriptRoot "dist/Windows"
dotnet publish $project -c Release --no-self-contained -o $output
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed" }
if (-not (Test-Path (Join-Path $output "AndroidCleanerPro.exe"))) { throw "Executable missing" }
Write-Host "Windows application built at $output"
Write-Host "Requires .NET 10 Desktop Runtime and Android SDK Platform Tools."
