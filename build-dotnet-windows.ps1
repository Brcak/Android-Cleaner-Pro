$ErrorActionPreference = "Stop"
$project = Join-Path $PSScriptRoot "src/AndroidCleanerPro/AndroidCleanerPro.csproj"
$output = Join-Path $PSScriptRoot "dist/Windows"
$version = if ($env:GITHUB_RUN_NUMBER) { "0.3.$env:GITHUB_RUN_NUMBER" } else { "0.3.0" }
dotnet publish $project -c Release --no-self-contained -p:Version=$version -o $output
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed" }
if (-not (Test-Path (Join-Path $output "AndroidCleanerPro.exe"))) { throw "Executable missing" }
Write-Host "Windows application built at $output"
Write-Host "Requires .NET 10 Desktop Runtime and Android SDK Platform Tools."
