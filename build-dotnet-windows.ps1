$ErrorActionPreference = "Stop"
dotnet publish .\src\AndroidCleanerPro\AndroidCleanerPro.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:EnableCompressionInSingleFile=true -o .\dist\win-x64
Write-Host "Built: dist\win-x64\AndroidCleanerPro.exe"
Write-Host "ADB is still required: add platform-tools to PATH or copy platform-tools beside the EXE."