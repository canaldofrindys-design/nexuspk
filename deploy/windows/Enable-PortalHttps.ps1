<#
.SYNOPSIS
  Liga o https do portal no IIS: gera (ou usa) o certificado, cria a associação na porta 443,
  libera a porta no firewall e atualiza o web.config.

.DESCRIPTION
  Sem domínio (site aberto pelo IP): gera um certificado autoassinado válido por 3 anos com os IPs
  da máquina. O navegador mostra um aviso uma vez ("Avançado > Continuar"), mas o login do painel
  e do jogador passa a funcionar. Nesse modo o http continua aberto (sem redirecionar), para os
  jogadores não verem o aviso ao abrir o site.

  Com domínio: passe -HostName e, se já tiver um certificado (win-acme), -CertificateThumbprint.
  Com um certificado de verdade, quem abrir por http é levado para o https.

  Pode rodar de novo quantas vezes quiser: reaproveita o certificado e a associação que já existem.

.EXAMPLE
  .\Enable-PortalHttps.ps1
  Sem domínio: certificado autoassinado, acesso por https://IP-DA-VPS

.EXAMPLE
  .\Enable-PortalHttps.ps1 -HostName nexuspk.203.0.113.10.sslip.io
  Sem domínio, com o site antigo na porta 80/443 do IP: nome provisório gratuito do sslip.io
  (aponta para o IP que está no nome). Certificado autoassinado.

.EXAMPLE
  .\Enable-PortalHttps.ps1 -HostName portal.seudominio.com -CertificateThumbprint 0123ABCD...
  Com domínio e certificado do win-acme.
#>
[CmdletBinding()]
param(
    [string]$HostName = '',
    [string]$CertificateThumbprint = '',
    [int]$Years = 3,
    [string]$Root = 'C:\NexusPortal'
)

. (Join-Path $PSScriptRoot 'Common.ps1')
Assert-Admin
Import-Module WebAdministration
$paths = Get-PortalPaths $Root
$friendly = 'NexusPortal (autoassinado)'
$HostName = $HostName.Trim().ToLowerInvariant()

if (-not (Get-Website -Name $script:SiteName)) { throw "Site $script:SiteName não encontrado no IIS. Instale o portal antes." }

# Outro site (ex.: o site antigo) já usando a mesma porta e nome: não mexe nele.
function Get-OtherOwner([string]$Protocol, [string]$Info) {
    foreach ($s in @(Get-Website | Where-Object { $_.Name -ne $script:SiteName })) {
        if (@(Get-WebBinding -Name $s.Name -Protocol $Protocol | Where-Object { $_.bindingInformation -eq $Info }).Count -gt 0) { return $s.Name }
    }
    return $null
}
$owner = Get-OtherOwner 'https' "*:443:$HostName"
if ($owner) {
    if ($HostName) { throw "O site '$owner' já usa https://$HostName no IIS." }
    throw "O site '$owner' já usa a porta 443 sem nome de host. Rode com -HostName (ex.: -HostName nexuspk.SEU-IP.sslip.io) para o portal ficar ao lado dele."
}

# Com nome de host, o portal também responde por http nesse nome (o site antigo continua com o resto da porta 80).
if ($HostName) {
    $owner = Get-OtherOwner 'http' "*:80:$HostName"
    if ($owner) { throw "O host $HostName já está no site '$owner' do IIS." }
    if (@(Get-WebBinding -Name $script:SiteName -Protocol http | Where-Object { $_.bindingInformation -eq "*:80:$HostName" }).Count -eq 0) {
        New-WebBinding -Name $script:SiteName -Protocol http -Port 80 -HostHeader $HostName
        Write-Ok "http://$HostName ligado ao portal"
    }
}

# 1. Certificado
Write-Step 'Certificado'
$selfSigned = $false
if ($CertificateThumbprint) {
    $CertificateThumbprint = $CertificateThumbprint.Replace(' ', '').ToUpperInvariant()
    if (-not (Test-Path "Cert:\LocalMachine\My\$CertificateThumbprint")) { throw "Certificado $CertificateThumbprint não está em Computador Local > Pessoal." }
    Write-Ok "usando o certificado $CertificateThumbprint"
}
else {
    $selfSigned = $true
    $existing = Get-ChildItem Cert:\LocalMachine\My |
        Where-Object { $_.FriendlyName -eq $friendly -and $_.NotAfter -gt (Get-Date).AddDays(30) } |
        Where-Object { -not $HostName -or @($_.DnsNameList | ForEach-Object { $_.Unicode }) -contains $HostName } |
        Sort-Object NotAfter -Descending | Select-Object -First 1
    if ($existing) {
        $CertificateThumbprint = $existing.Thumbprint
        Write-Ok "reaproveitando o certificado autoassinado (vale até $($existing.NotAfter.ToString('dd/MM/yyyy')))"
    }
    else {
        $names = @('nexuspk', $env:COMPUTERNAME.ToLowerInvariant())
        if ($HostName) { $names = @($HostName) + $names }
        $ips = @(Get-NetIPAddress -AddressFamily IPv4 -ErrorAction SilentlyContinue |
            Where-Object { $_.IPAddress -notlike '127.*' -and $_.IPAddress -notlike '169.254.*' } |
            ForEach-Object { $_.IPAddress })
        $san = (@($names | ForEach-Object { "DNS=$_" }) + @($ips | ForEach-Object { "IPAddress=$_" })) -join '&'
        $cert = New-SelfSignedCertificate -Subject "CN=$($names[0])" -FriendlyName $friendly `
            -TextExtension @("2.5.29.17={text}$san") -CertStoreLocation Cert:\LocalMachine\My `
            -KeyExportPolicy NonExportable -KeyAlgorithm RSA -KeyLength 2048 -HashAlgorithm SHA256 `
            -NotAfter (Get-Date).AddYears($Years)
        $CertificateThumbprint = $cert.Thumbprint
        Write-Ok "certificado autoassinado criado para $($names -join ', ') $(if ($ips) { "e $($ips -join ', ')" }) (vale $Years anos)"
    }
}

# 2. Associação https na porta 443
Write-Step 'Associação https no IIS'
# Filtro explícito: "-HostHeader ''" no Get-WebBinding devolveria também as associações com nome de host.
function Find-HttpsBinding { @(Get-WebBinding -Name $script:SiteName -Protocol https | Where-Object { $_.bindingInformation -eq "*:443:$HostName" }) | Select-Object -First 1 }
$binding = Find-HttpsBinding
if (-not $binding) {
    $flags = 0
    if ($HostName) { $flags = 1 }  # SNI só com nome de host
    New-WebBinding -Name $script:SiteName -Protocol https -Port 443 -HostHeader $HostName -SslFlags $flags
    $binding = Find-HttpsBinding
    Write-Ok "associação https criada$(if ($HostName) { " para $HostName" } else { ' (todos os endereços)' })"
}
else {
    try { $binding.RemoveSslCertificate() } catch { }
}
$binding.AddSslCertificate($CertificateThumbprint, 'My')
Write-Ok "certificado $CertificateThumbprint ligado à porta 443"

# 3. Firewall
Write-Step 'Firewall'
$rule = 'NexusPortal HTTPS (443)'
if (-not (Get-NetFirewallRule -DisplayName $rule -ErrorAction SilentlyContinue)) {
    New-NetFirewallRule -DisplayName $rule -Direction Inbound -Protocol TCP -LocalPort 443 -Action Allow | Out-Null
    Write-Ok 'porta 443 liberada'
}
else { Write-Ok 'porta 443 já liberada' }

# 4. Redirecionamento do http e web.config da versão atual
Write-Step 'Redirecionamento http -> https'
$flag = Join-Path $paths.Config 'https-sem-redirecionamento'
if ($selfSigned) {
    if (-not (Test-Path $paths.Config)) { New-Item -ItemType Directory -Path $paths.Config | Out-Null }
    Set-Content -Path $flag -Value 'Certificado autoassinado: o http continua aberto. Apague este arquivo para redirecionar tudo para o https.' -Encoding UTF8
    Write-Ok 'http continua aberto (certificado autoassinado mostraria aviso a todos os jogadores)'
}
elseif (Test-Path $flag) {
    Remove-Item $flag -Force
    Write-Ok 'quem abrir por http será levado ao https'
}
$current = Get-CurrentRelease $paths
if ($current) {
    Write-WebConfig $paths $current
    Write-Ok 'web.config atualizado'
}

# 5. Conferência
Write-Step 'Conferência'
$probeHost = $HostName
if (-not $probeHost) { $probeHost = 'localhost' }
$ok = Wait-Http { Invoke-LocalSite -HostName $probeHost -Path '/' -Https } -Seconds 90
if (-not $ok) { throw "o site não respondeu em https://$probeHost (veja $($paths.Logs)\web)" }
Write-Ok "https respondendo"

$address = $HostName
if (-not $address) {
    $ip = @(Get-NetIPAddress -AddressFamily IPv4 -ErrorAction SilentlyContinue | Where-Object { $_.IPAddress -notlike '127.*' -and $_.IPAddress -notlike '169.254.*' } | Select-Object -First 1)
    $address = if ($ip) { $ip[0].IPAddress } else { 'IP-DA-VPS' }
}
Write-Host ''
Write-Host "  Pronto. Painel da equipe: https://$address/admin/entrar" -ForegroundColor Green
if ($selfSigned) {
    Write-Host '  O navegador vai avisar que o certificado não é confiável: clique em Avançado > Continuar.' -ForegroundColor Yellow
    Write-Host '  Com um domínio, rode de novo com -HostName (e -CertificateThumbprint do win-acme) para tirar o aviso.' -ForegroundColor Yellow
}
