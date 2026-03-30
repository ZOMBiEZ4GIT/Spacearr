$ErrorActionPreference = 'Stop'
$installDir = 'C:\dotnet8'
Write-Host 'Downloading install script...'
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
Invoke-WebRequest -Uri 'https://dot.net/v1/dotnet-install.ps1' -OutFile "$env:TEMP\dotnet-install.ps1"
Write-Host 'Running installer...'
& "$env:TEMP\dotnet-install.ps1" -Channel 8.0 -InstallDir $installDir
Write-Host 'Done. Checking version...'
& "$installDir\dotnet.exe" --version
