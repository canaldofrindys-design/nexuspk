<#
.SYNOPSIS
  Faz o site do portal atender os endereços que o jogo e o launcher chamam no site antigo.

.DESCRIPTION
  O jogo e o launcher falam com o site, não só com o banco:
    ServerMain\*.asp     clã dentro do jogo (criar, convidar, sair, líder, vice, informações, SoD)
    Clan\verificarcl.asp criação do clã
    ClanContent\*.bmp    ícones dos clãs
    updater\             version.txt, Updates.xml, zips e patch.json do launcher
    att, rankAPI, include  níveis, rankings e status usados fora do site

  Este script não copia nem reescreve nada disso. Ele cria, dentro do site NexusPortal do IIS, uma aplicação
  para cada pasta que existe no site antigo, apontando para a MESMA pasta e rodando no MESMO pool do site antigo.
  Os arquivos, as configurações deles e a conta que os executa continuam os mesmos; só o nome do site muda.
  Assim, quando o domínio principal passar para o portal, o jogo continua chamando os mesmos endereços.

  Também copia do site antigo o usuário anônimo e as configurações do ASP (página de código, idioma, caminhos
  pai), e gera de novo o web.config da versão no ar com o PHP do site antigo para essas pastas.

  O site antigo não é alterado. Rodar de novo atualiza as aplicações. -Remove desfaz.

.EXAMPLE
  .\Connect-LegacyPaths.ps1
  .\Connect-LegacyPaths.ps1 -LegacyRoot D:\site -Paths ServerMain, ClanContent, updater
  .\Connect-LegacyPaths.ps1 -Remove
#>
[CmdletBinding()]
param(
    [string]$LegacyRoot = 'C:\wwwroot',
    [string[]]$Paths,
    [string]$LegacyAppPool,
    [string]$Root = 'C:\NexusPortal',
    [switch]$Remove
)

. (Join-Path $PSScriptRoot 'Common.ps1')
Assert-Admin
Import-Module WebAdministration
$portalPaths = Get-PortalPaths $Root
if (-not (Get-Website -Name $script:SiteName)) { throw "Site $script:SiteName não existe no IIS. Rode o Install-Portal.ps1 primeiro." }
if (-not $Paths) { $Paths = $script:LegacyDefaultPaths }

function Update-PortalWebConfig {
    $current = Get-CurrentRelease $portalPaths
    if (-not $current) { return }
    Write-WebConfig $portalPaths $current
    Write-Ok 'web.config da versão no ar atualizado'
}

if ($Remove) {
    Write-Step 'Desligando as pastas do site antigo do portal'
    foreach ($name in Get-LegacyApps) {
        Remove-WebApplication -Site $script:SiteName -Name $name
        Clear-WebConfiguration -Filter 'system.webServer/asp' -PSPath 'MACHINE/WEBROOT/APPHOST' -Location "$script:SiteName/$name" -ErrorAction SilentlyContinue
        Clear-WebConfiguration -Filter 'system.webServer/security/authentication/anonymousAuthentication' -PSPath 'MACHINE/WEBROOT/APPHOST' -Location "$script:SiteName/$name" -ErrorAction SilentlyContinue
        Write-Ok "/$name removido do portal (continua no site antigo)"
    }
    Update-PortalWebConfig
    return
}

$LegacyRoot = $LegacyRoot.TrimEnd('\')
if (-not (Test-Path $LegacyRoot -PathType Container)) { throw "Pasta do site antigo não encontrada: $LegacyRoot" }

Write-Step 'Site antigo'
$legacySite = @(Get-Website | Where-Object {
        $_.name -ne $script:SiteName -and
        [Environment]::ExpandEnvironmentVariables($_.physicalPath).TrimEnd('\') -ieq $LegacyRoot
    }) | Select-Object -First 1
if (-not $LegacyAppPool) {
    if (-not $legacySite) { throw "Nenhum site do IIS usa a pasta $LegacyRoot. Informe o pool com -LegacyAppPool." }
    $LegacyAppPool = $legacySite.applicationPool
}
if (-not (Test-Path "IIS:\AppPools\$LegacyAppPool")) { throw "Pool $LegacyAppPool não existe no IIS." }
if ($LegacyAppPool -eq $script:AppPool) { throw 'O pool do site antigo não pode ser o do portal.' }
if ($legacySite) { Write-Ok "site $($legacySite.name), pool $LegacyAppPool" } else { Write-Ok "pool $LegacyAppPool" }

$aspHandler = Get-WebConfiguration -Filter "system.webServer/handlers/add[@name='ASPClassic']" -PSPath 'MACHINE/WEBROOT/APPHOST'
if (-not $aspHandler) { Write-Warn 'o ASP clássico não está instalado no IIS: as páginas .asp não vão rodar.' }

# Configurações do site antigo que valem para as páginas dele (ficam no applicationHost.config, por site).
$copied = @()
if ($legacySite) {
    $from = "IIS:\Sites\$($legacySite.name)"
    $asp = Get-WebConfiguration -Filter 'system.webServer/asp' -PSPath $from
    $anonymous = Get-WebConfiguration -Filter 'system.webServer/security/authentication/anonymousAuthentication' -PSPath $from
    $copied = @(
        @{ Filter = 'system.webServer/asp'; Name = 'codePage'; Value = $asp.codePage },
        @{ Filter = 'system.webServer/asp'; Name = 'lcid'; Value = $asp.lcid },
        @{ Filter = 'system.webServer/asp'; Name = 'enableParentPaths'; Value = $asp.enableParentPaths },
        @{ Filter = 'system.webServer/security/authentication/anonymousAuthentication'; Name = 'userName'; Value = $anonymous.userName }
    )
}

Write-Step "Ligando as pastas de $LegacyRoot ao portal"
$connected = @()
foreach ($name in $Paths) {
    if ($name -notmatch '^[A-Za-z0-9_-]+$') { throw "Nome de pasta inválido: $name" }
    $folder = Join-Path $LegacyRoot $name
    if (-not (Test-Path $folder -PathType Container)) { Write-Warn "$folder não existe, pulei"; continue }
    $existing = Get-WebApplication -Site $script:SiteName -Name $name
    if ($existing) {
        Set-ItemProperty "IIS:\Sites\$script:SiteName\$name" -Name physicalPath -Value $folder
        Set-ItemProperty "IIS:\Sites\$script:SiteName\$name" -Name applicationPool -Value $LegacyAppPool
    }
    else {
        New-WebApplication -Site $script:SiteName -Name $name -PhysicalPath $folder -ApplicationPool $LegacyAppPool | Out-Null
    }
    foreach ($setting in $copied) {
        if ($null -eq $setting.Value) { continue }
        Set-WebConfigurationProperty -PSPath 'MACHINE/WEBROOT/APPHOST' -Location "$script:SiteName/$name" -Filter $setting.Filter -Name $setting.Name -Value $setting.Value
    }
    $connected += $name
    Write-Ok "/$name -> $folder"
}
if ($connected.Count -eq 0) { Write-Warn 'nenhuma pasta ligada'; return }

Update-PortalWebConfig

Write-Step 'Conferindo'
$site = Get-SiteHost
foreach ($check in @('updater/version.txt', 'updater/Updates.xml')) {
    if (-not (Test-Path (Join-Path $LegacyRoot $check))) { continue }
    $status = Invoke-LocalSite -HostName $site.HostName -Path "/$check"
    if ($status -eq 200) { Write-Ok "http://$($site.HostName)/$check responde 200" }
    else { Write-Warn "http://$($site.HostName)/$check respondeu $status" }
}
Write-Host ''
Write-Host "Pronto. O jogo e o launcher podem chamar http://$($site.HostName)/ServerMain/..., /ClanContent/... e /updater/..." -ForegroundColor Green
Write-Host 'O site antigo continua igual. Para desfazer: .\Connect-LegacyPaths.ps1 -Remove'
