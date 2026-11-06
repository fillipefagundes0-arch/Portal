#Requires -RunAsAdministrator
[CmdletBinding()]
param(
 [Parameter(Mandatory)][string]$AdminKey,
 [Parameter(Mandatory)][string]$ConnectionString,
 [string]$InstallPath = 'C:\inetpub\PortalAutomacao',
 [string]$DataPath = 'C:\ProgramData\PortalAutomacao',
 [ValidateRange(1,65535)][int]$Port = 8080
)
$ErrorActionPreference = 'Stop'
if ($AdminKey -notmatch '^[^\\]+\\[^\\]+$') { throw 'Use DOMINIO\chave para AdminKey.' }
if (!(Test-Path "$PSScriptRoot\app\Portal.dll")) { throw 'Pacote incompleto: app\Portal.dll ausente.' }
if (!(Get-Module -ListAvailable WebAdministration)) { throw 'Habilite IIS, Windows Authentication e IIS Management Scripts and Tools antes de instalar.' }
Import-Module WebAdministration
if (!(Test-Path "$env:ProgramFiles\IIS\Asp.Net Core Module\V2\aspnetcorev2.dll")) { throw 'Instale o ASP.NET Core 10 Hosting Bundle oficial e reinicie o IIS.' }
if (!(Get-WindowsOptionalFeature -Online -FeatureName IIS-WindowsAuthentication).State.Equals('Enabled')) { throw 'Habilite o recurso IIS-WindowsAuthentication.' }
if ((Get-Website | Where-Object { $_.Name -ne 'PortalAutomacao' } | ForEach-Object { $_.Bindings.Collection.bindingInformation }) -match ":${Port}:") { throw 'A porta está em uso por outro site IIS.' }
New-Item -ItemType Directory -Force $InstallPath, $DataPath, "$DataPath\attachments" | Out-Null
if (Test-Path "$InstallPath\appsettings.Production.json") { throw 'Instalação existente detectada. Faça backup e siga o procedimento de atualização do README; este instalador não sobrescreve configurações existentes.' }
if (!(Test-Path IIS:\AppPools\PortalAutomacao)) { New-WebAppPool PortalAutomacao | Out-Null }
Set-ItemProperty IIS:\AppPools\PortalAutomacao managedRuntimeVersion ''
Copy-Item "$PSScriptRoot\app\*" $InstallPath -Recurse -Force
@{
 ConnectionStrings = @{ Portal = $ConnectionString }
 DatabaseProvider = 'SqlServer'
 StoragePath = $DataPath
 BootstrapAdmin = $AdminKey
 LocalDevelopmentIdentity = $false
} | ConvertTo-Json -Depth 4 | Set-Content "$InstallPath\appsettings.Production.json" -Encoding UTF8
if (!(Test-Path IIS:\Sites\PortalAutomacao)) { New-Website -Name PortalAutomacao -PhysicalPath $InstallPath -ApplicationPool PortalAutomacao -Port $Port | Out-Null }
Set-WebConfigurationProperty -Filter /system.webServer/security/authentication/anonymousAuthentication -PSPath IIS:\ -Location PortalAutomacao -Name enabled -Value $false
Set-WebConfigurationProperty -Filter /system.webServer/security/authentication/windowsAuthentication -PSPath IIS:\ -Location PortalAutomacao -Name enabled -Value $true
& icacls $InstallPath /grant 'IIS AppPool\PortalAutomacao:(OI)(CI)RX' | Out-Null
if ($LASTEXITCODE -ne 0) { throw 'Falha ao configurar permissão da aplicação.' }
& icacls $DataPath /inheritance:r /grant:r '*S-1-5-18:(OI)(CI)F' '*S-1-5-32-544:(OI)(CI)F' 'IIS AppPool\PortalAutomacao:(OI)(CI)M' | Out-Null
if ($LASTEXITCODE -ne 0) { throw 'Falha ao proteger os anexos.' }
if ((Get-WebAppPoolState PortalAutomacao).Value -ne 'Started') { Start-WebAppPool PortalAutomacao }
if ((Get-Website -Name PortalAutomacao).State -ne 'Started') { Start-Website PortalAutomacao }
Write-Host "Arquivos e IIS configurados. Valide acesso com a conta $AdminKey."
Write-Host 'A TI deve liberar a identidade do pool no SQL, validar o domínio e configurar certificado HTTPS/binding antes do uso corporativo.'
