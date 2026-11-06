[CmdletBinding()]
param([string]$Output = "$PSScriptRoot\..\artifacts\PortalAutomacao")
$ErrorActionPreference = 'Stop'
New-Item -ItemType Directory -Force "$Output\app" | Out-Null
dotnet publish "$PSScriptRoot\..\Portal\Portal.csproj" -c Release -r win-x64 --self-contained true -o "$Output\app"
if ($LASTEXITCODE -ne 0) { throw 'Publicação falhou.' }
Copy-Item "$PSScriptRoot\Install-Portal.ps1" $Output -Force
Copy-Item "$PSScriptRoot\Instalar.cmd" $Output -Force
Copy-Item "$PSScriptRoot\..\README.md" $Output -Force
Compress-Archive -Path "$Output\*" -DestinationPath "$Output.zip" -Force
Write-Host "Instalador em $Output.zip"
