<#
.SYNOPSIS
  Troca o domínio principal do site antigo para o portal, numa tacada só, com backup e volta automática.

.DESCRIPTION
  Os dois sites estão no mesmo IIS. A troca é passar as associações do domínio (e do www) do site antigo
  para o site NexusPortal. O DNS não muda.

  Modo normal (troca):
    1. confere o portal e acha o site antigo (o que tem o domínio nas associações);
    2. backup: configuração do IIS (appcmd), zip da pasta do site antigo e cópia do portal.json;
    3. tira o domínio e o www do site antigo e põe no portal (http 80 e https 443 com SNI);
    4. certificado: usa -CertificateThumbprint, ou um que já cubra os dois nomes, ou pede um novo ao
       Let's Encrypt pelo win-acme já instalado (renova sozinho e atualiza o IIS);
    5. troca o Accounts.PublicBaseUrl do portal.json (links dos e-mails), liga o http -> https e reinicia;
    6. confere página inicial, www e as pastas do jogo pelo domínio. Se falhar, volta tudo sozinho.

  O site antigo continua ligado, atendendo pelo IP. As pastas do jogo (ServerMain, Clan, ClanContent,
  updater...) continuam onde estão: o portal já as atende pelo Connect-LegacyPaths.ps1.

  -StopOldSite (dias depois): confere nos registros do IIS se alguém (jogo, launcher) ainda chamou o
  site antigo depois da troca. Se ninguém chamou, para o site antigo. Não apaga nada e não para o pool
  dele (as pastas do jogo no portal rodam nesse pool).

  -Rollback: volta as associações do IIS, o portal.json e liga o site antigo de novo.

.EXAMPLE
  .\Switch-PortalDomain.ps1 -Domain nexuspk.com.br
  Troca o domínio (e o www) para o portal.

.EXAMPLE
  .\Switch-PortalDomain.ps1 -Domain nexuspk.com.br -WhatIf
  Só mostra o que faria, sem mudar nada.

.EXAMPLE
  .\Switch-PortalDomain.ps1 -StopOldSite
  Depois de uns dias: para o site antigo se ninguém mais o chamou.

.EXAMPLE
  .\Switch-PortalDomain.ps1 -Rollback
  Desfaz a troca.
#>
[CmdletBinding(DefaultParameterSetName = 'Switch', SupportsShouldProcess)]
param(
    [Parameter(Mandatory, ParameterSetName = 'Switch')] [string]$Domain,
    [Parameter(ParameterSetName = 'Switch')] [switch]$NoWww,
    [Parameter(ParameterSetName = 'Switch')] [string]$OldSite,
    [Parameter(ParameterSetName = 'Switch')] [string]$CertificateThumbprint,
    [Parameter(ParameterSetName = 'Switch')] [string]$WacsPath,
    [Parameter(ParameterSetName = 'Switch')] [switch]$SkipWinAcme,
    [Parameter(ParameterSetName = 'Switch')] [switch]$AcceptLetsEncryptTerms,
    [Parameter(ParameterSetName = 'Switch')] [string]$Email,
    [Parameter(ParameterSetName = 'Switch')] [switch]$SkipFileBackup,
    [Parameter(ParameterSetName = 'Switch')] [switch]$NoAutoRollback,
    [Parameter(ParameterSetName = 'Switch')] [string]$BackupDir = 'C:\Backups',

    [Parameter(Mandatory, ParameterSetName = 'Stop')] [switch]$StopOldSite,
    [Parameter(ParameterSetName = 'Stop')] [switch]$Force,

    [Parameter(Mandatory, ParameterSetName = 'Rollback')] [switch]$Rollback,

    [string]$Root = 'C:\NexusPortal'
)

. (Join-Path $PSScriptRoot 'Common.ps1')
Assert-Admin
Import-Module WebAdministration
$paths = Get-PortalPaths $Root
$stateFile = Join-Path $paths.Config 'troca-dominio.json'
$appcmd = Join-Path $env:windir 'system32\inetsrv\appcmd.exe'
$utf8 = New-Object System.Text.UTF8Encoding($false)

function Save-State($State) { [IO.File]::WriteAllText($stateFile, ($State | ConvertTo-Json -Depth 5), $utf8) }
function Read-State {
    if (-not (Test-Path $stateFile)) { throw "Nenhuma troca registrada ($stateFile não existe)." }
    return Get-Content -Raw $stateFile | ConvertFrom-Json
}

function Get-PublicBaseUrl {
    if (-not (Test-Path $paths.ConfigFile)) { return $null }
    $m = [regex]::Match((Get-Content -Raw $paths.ConfigFile), '"PublicBaseUrl"\s*:\s*"([^"]*)"')
    if ($m.Success) { return $m.Groups[1].Value }
    return $null
}
function Set-PublicBaseUrl([string]$Url) {
    $text = Get-Content -Raw $paths.ConfigFile
    $pattern = '("PublicBaseUrl"\s*:\s*")[^"]*(")'
    if ($text -notmatch $pattern) { Write-Warn "PublicBaseUrl não encontrado em $($paths.ConfigFile); links dos e-mails não foram trocados."; return }
    $text = [regex]::Replace($text, $pattern, { param($m) $m.Groups[1].Value + $Url + $m.Groups[2].Value })
    [IO.File]::WriteAllText($paths.ConfigFile, $text, $utf8)
}

function Restart-PortalServices {
    foreach ($name in $script:ApiService, $script:WorkerService) {
        if (Get-Service -Name $name -ErrorAction SilentlyContinue) { Restart-Service -Name $name -Force }
    }
}

function Update-WebConfig([switch]$RealCertificate) {
    $flag = Join-Path $paths.Config 'https-sem-redirecionamento'
    if ($RealCertificate -and (Test-Path $flag)) { Remove-Item $flag -Force; Write-Ok 'quem abrir por http será levado ao https' }
    $current = Get-CurrentRelease $paths
    if ($current) { Write-WebConfig $paths $current; Write-Ok 'web.config do portal atualizado' }
}

# Nomes que um certificado cobre (SAN), em minúsculas.
# Propriedade que pode não existir no objeto (o Set-StrictMode do Common.ps1 daria erro), como texto.
function Get-OptionalProperty($Object, [string]$Name) {
    $p = $Object.PSObject.Properties[$Name]
    if ($p -and $null -ne $p.Value) { return "$($p.Value)" }
    return ''
}

# Lê a extensão SAN direto: o DnsNameList do PowerShell não existe em todo objeto de certificado.
function Get-CertNames($Cert) {
    $names = @()
    $san = $Cert.Extensions | Where-Object { $_.Oid.Value -eq '2.5.29.17' } | Select-Object -First 1
    if ($san) {
        foreach ($part in ($san.Format($false) -split ',\s*')) {
            if ($part -match '^[^=]*DNS[^=]*=\s*(.+)$') { $names += $Matches[1].Trim().ToLowerInvariant() }
        }
    }
    if ($names.Count -eq 0) {
        $cn = $Cert.GetNameInfo([System.Security.Cryptography.X509Certificates.X509NameType]::DnsName, $false)
        if ($cn) { $names += $cn.ToLowerInvariant() }
    }
    return $names
}

function Test-CertCovers($Cert, [string[]]$Names) {
    $have = Get-CertNames $Cert
    foreach ($n in $Names) {
        $wild = '*.' + ($n -split '\.', 2)[1]
        if ($have -notcontains $n -and $have -notcontains $wild) { return $false }
    }
    return $true
}

# Certificado válido (não autoassinado de teste, com chave, dentro do prazo) que cubra os nomes.
function Find-Certificate([string[]]$Names, [switch]$Any) {
    $all = foreach ($store in 'My', 'WebHosting') {
        if (Test-Path "Cert:\LocalMachine\$store") {
            Get-ChildItem "Cert:\LocalMachine\$store" | Where-Object { $_.HasPrivateKey -and $_.NotAfter -gt (Get-Date).AddDays(7) } |
                ForEach-Object { [pscustomobject]@{ Cert = $_; Store = $store } }
        }
    }
    $match = @($all | Where-Object { if ($Any) { Test-CertCovers $_.Cert @($Names[0]) } else { Test-CertCovers $_.Cert $Names } } |
        Sort-Object { $_.Cert.NotAfter } -Descending)
    if ($match.Count -eq 0) { return $null }
    return $match[0]
}

function Find-Wacs {
    if ($WacsPath) { if (Test-Path $WacsPath) { return $WacsPath }; throw "win-acme não encontrado em $WacsPath." }
    # A tarefa de renovação do win-acme aponta para o wacs.exe instalado.
    foreach ($task in @(Get-ScheduledTask -ErrorAction SilentlyContinue | Where-Object { $_.TaskName -like 'win-acme*' })) {
        foreach ($action in @($task.Actions)) {
            $exe = Get-OptionalProperty $action 'Execute'
            if ($exe -and $exe -like '*wacs.exe' -and (Test-Path $exe)) { return $exe }
        }
    }
    foreach ($dir in 'C:\win-acme', "$env:ProgramFiles\win-acme", 'C:\tools\win-acme', 'C:\wacs', "$env:ProgramData\win-acme") {
        $exe = Join-Path $dir 'wacs.exe'
        if (Test-Path $exe) { return $exe }
    }
    return $null
}

function Get-SiteLogFolder([string]$SiteName) {
    $site = Get-Website -Name $SiteName
    $dir = [Environment]::ExpandEnvironmentVariables($site.logFile.directory)
    return Join-Path $dir "W3SVC$($site.id)"
}

function Invoke-Rollback($State) {
    Write-Step 'Voltando a troca'
    & $appcmd restore backup $State.IisBackup | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "appcmd não conseguiu restaurar o backup '$($State.IisBackup)' do IIS." }
    Write-Ok "associações do IIS como antes ($($State.IisBackup))"
    # O certificado de cada nome fica no http.sys, fora do backup do IIS: põe de volta o que o site antigo usava.
    foreach ($o in @($State.OldHttps)) {
        if (-not $o) { continue }
        $b = @(Get-WebBinding -Name $State.OldSite -Protocol https | Where-Object { $_.bindingInformation -eq $o.Info }) | Select-Object -First 1
        if (-not $b) { continue }
        try { $b.RemoveSslCertificate() } catch { }
        $store = if ($o.CertStore) { $o.CertStore } else { 'My' }
        try { $b.AddSslCertificate($o.CertHash, $store); Write-Ok "certificado do site antigo de volta em $($o.Info)" }
        catch { Write-Warn "não consegui pôr de volta o certificado em $($o.Info): $_" }
    }
    if ($State.PortalJsonBackup -and (Test-Path $State.PortalJsonBackup)) {
        Copy-Item $State.PortalJsonBackup $paths.ConfigFile -Force
        Write-Ok 'portal.json como antes'
    }
    if ($State.RedirectFlagExisted) {
        Set-Content -Path (Join-Path $paths.Config 'https-sem-redirecionamento') -Encoding UTF8 -Value 'Certificado autoassinado: o http continua aberto. Apague este arquivo para redirecionar tudo para o https.'
    }
    Update-WebConfig
    if ((Get-WebsiteState -Name $State.OldSite).Value -ne 'Started') { Start-Website -Name $State.OldSite }
    Write-Ok "site '$($State.OldSite)' ligado"
    Restart-PortalServices
    Write-Ok 'serviços do portal reiniciados'
    Remove-Item $stateFile -Force -ErrorAction SilentlyContinue
}

# ---------------------------------------------------------------- -Rollback
if ($PSCmdlet.ParameterSetName -eq 'Rollback') {
    $state = Read-State
    if ($PSCmdlet.ShouldProcess($state.Domain, 'voltar o domínio para o site antigo')) {
        Invoke-Rollback $state
        Write-Host ''
        Write-Host "  Pronto. $($state.Domain) voltou para o site '$($state.OldSite)'." -ForegroundColor Green
    }
    return
}

# ---------------------------------------------------------------- -StopOldSite
if ($PSCmdlet.ParameterSetName -eq 'Stop') {
    $state = Read-State
    $raw = $state.SwitchedAtUtc
    if ($raw -is [datetime]) { $since = $raw.ToUniversalTime() }
    else { $since = [datetime]::Parse($raw, [Globalization.CultureInfo]::InvariantCulture, [Globalization.DateTimeStyles]::AdjustToUniversal) }
    Write-Step "Quem chamou o site '$($state.OldSite)' desde a troca ($($since.ToLocalTime().ToString('dd/MM/yyyy HH:mm')))"
    $folder = Get-SiteLogFolder $state.OldSite
    $hits = @()
    if (Test-Path $folder) {
        foreach ($file in @(Get-ChildItem $folder -Filter '*.log' | Where-Object { $_.LastWriteTimeUtc -ge $since })) {
            $fields = @()
            foreach ($line in [IO.File]::ReadLines($file.FullName)) {
                if ($line.StartsWith('#Fields:')) { $fields = $line.Substring(9).Trim() -split ' '; continue }
                if ($line.StartsWith('#') -or $fields.Count -eq 0) { continue }
                $v = $line -split ' '
                if ($v.Count -ne $fields.Count) { continue }
                # Os registros do IIS ficam em UTC.
                $when = [datetime]::ParseExact("$($v[[array]::IndexOf($fields,'date')]) $($v[[array]::IndexOf($fields,'time')])", 'yyyy-MM-dd HH:mm:ss', [Globalization.CultureInfo]::InvariantCulture)
                if ($when -lt $since) { continue }
                $hits += [pscustomobject]@{ Uri = $v[[array]::IndexOf($fields, 'cs-uri-stem')]; Ip = $v[[array]::IndexOf($fields, 'c-ip')] }
            }
        }
    }
    else {
        Write-Warn "sem registros do IIS em ${folder}: não dá para saber se o jogo ainda chama o site antigo"
        if (-not $Force) { Write-Host '  O site antigo NÃO foi parado. Para parar mesmo assim, use -Force.' -ForegroundColor Yellow; return }
    }
    if ($hits.Count -gt 0) {
        Write-Warn "$($hits.Count) chamadas ao site antigo depois da troca. As mais comuns:"
        $hits | Group-Object Uri | Sort-Object Count -Descending | Select-Object -First 15 | ForEach-Object { Write-Host ("      {0,6}  {1}" -f $_.Count, $_.Name) }
        if (-not $Force) {
            Write-Host ''
            Write-Host '  O site antigo NÃO foi parado. Se aparecem ServerMain, Clan ou updater, o jogo ou o launcher' -ForegroundColor Yellow
            Write-Host '  chamam pelo IP: troque o endereço neles para o domínio antes. Para parar mesmo assim, use -Force.' -ForegroundColor Yellow
            return
        }
    }
    else { Write-Ok 'ninguém chamou o site antigo desde a troca' }
    if ($PSCmdlet.ShouldProcess($state.OldSite, 'parar o site antigo')) {
        Stop-Website -Name $state.OldSite
        Set-ItemProperty "IIS:\Sites\$($state.OldSite)" -Name serverAutoStart -Value $false
        Write-Ok "site '$($state.OldSite)' parado (não liga sozinho). Pasta, bancos e pool continuam como estão."
        Write-Host ''
        Write-Host ("  Para ligar de novo: Start-Website -Name '{0}'" -f $state.OldSite) -ForegroundColor Green
    }
    return
}

# ---------------------------------------------------------------- troca
$Domain = $Domain.Trim().ToLowerInvariant() -replace '^https?://', '' -replace '/.*$', ''
if ($Domain -like 'www.*') { $Domain = $Domain.Substring(4) }
$hosts = @($Domain)
if (-not $NoWww) { $hosts += "www.$Domain" }

if (Test-Path $stateFile) { throw "Já existe uma troca registrada em $stateFile. Use -Rollback antes de trocar de novo." }
if (-not (Get-Website -Name $script:SiteName)) { throw "Site $script:SiteName não encontrado no IIS." }

Write-Step 'Conferindo antes de mexer'
if (-not (Test-PortalHealth $paths)) { throw 'O portal não está saudável. Nada foi alterado.' }

# Associações do domínio que estão em outros sites.
$toMove = @()
foreach ($s in @(Get-Website | Where-Object { $_.Name -ne $script:SiteName })) {
    foreach ($b in @(Get-WebBinding -Name $s.Name)) {
        $hostHeader = ("$($b.bindingInformation)" -split ':')[-1].ToLowerInvariant()
        if ($hosts -contains $hostHeader) {
            $hash = Get-OptionalProperty $b 'certificateHash'; $store = Get-OptionalProperty $b 'certificateStoreName'
            if ($b.protocol -eq 'https' -and -not $hash) {
                # Associação com SNI: o certificado fica no http.sys, por nome e porta.
                $port = ("$($b.bindingInformation)" -split ':')[1]
                $ssl = Get-Item "IIS:\SslBindings\!$port!$hostHeader" -ErrorAction SilentlyContinue
                if ($ssl) { $hash = Get-OptionalProperty $ssl 'Thumbprint'; $store = Get-OptionalProperty $ssl 'Store' }
            }
            $toMove += [pscustomobject]@{ Site = $s.Name; Protocol = $b.protocol; Info = $b.bindingInformation; CertHash = $hash; CertStore = $store }
        }
    }
}
$owners = @($toMove | Select-Object -ExpandProperty Site -Unique)
if ($OldSite) {
    if (-not (Get-Website -Name $OldSite)) { throw "Site '$OldSite' não existe no IIS." }
    $extra = @($owners | Where-Object { $_ -ne $OldSite })
    if ($extra.Count -gt 0) { throw "O domínio também está no site '$($extra -join "', '")'. Confira as associações no IIS." }
}
else {
    if ($owners.Count -gt 1) { throw "O domínio está em mais de um site ($($owners -join ', ')). Rode com -OldSite <nome>." }
    if ($owners.Count -eq 0) { throw "Nenhum site do IIS tem $($hosts -join ' ou ') nas associações. Rode com -OldSite <nome> se o site antigo responde só pelo IP." }
    $OldSite = $owners[0]
}
$oldInfo = Get-Website -Name $OldSite
Write-Ok "site antigo: '$OldSite' (Id $($oldInfo.id), pasta $($oldInfo.physicalPath))"
foreach ($m in $toMove) { Write-Ok "vai passar para o portal: $($m.Protocol) $($m.Info)" }
$keep = @(Get-WebBinding -Name $OldSite | Where-Object { $hosts -notcontains ("$($_.bindingInformation)" -split ':')[-1].ToLowerInvariant() })
foreach ($k in $keep) { Write-Ok "continua no site antigo: $($k.protocol) $($k.bindingInformation)" }
$legacyApps = @(Get-LegacyApps)
if ($legacyApps.Count -eq 0) { Write-Warn 'nenhuma pasta do jogo ligada ao portal (Connect-LegacyPaths.ps1). Clã e launcher que chamam pelo domínio vão parar.' }
else { Write-Ok "pastas do jogo já atendidas pelo portal: $($legacyApps -join ', ')" }

# Certificado
$cert = $null
if ($CertificateThumbprint) {
    $tp = $CertificateThumbprint.Replace(' ', '').ToUpperInvariant()
    foreach ($store in 'My', 'WebHosting') {
        if (Test-Path "Cert:\LocalMachine\$store\$tp") { $cert = [pscustomobject]@{ Cert = Get-Item "Cert:\LocalMachine\$store\$tp"; Store = $store } }
    }
    if (-not $cert) { throw "Certificado $tp não está em Computador Local > Pessoal nem Hospedagem Web." }
}
else { $cert = Find-Certificate $hosts }
$wacs = $null
$needWacs = -not $cert
if ($needWacs) {
    $cert = Find-Certificate $hosts -Any   # cobre ao menos o domínio sem www (o do site antigo)
    if (-not $SkipWinAcme) { $wacs = Find-Wacs }
    if (-not $wacs -and -not $cert) { throw "Nenhum certificado para $Domain e win-acme não encontrado. Passe -CertificateThumbprint ou -WacsPath." }
    if ($wacs) { Write-Ok "certificado novo para $($hosts -join ' e ') será pedido pelo win-acme ($wacs)" }
    else { Write-Warn "nenhum certificado cobre $($hosts -join ' e '); usando o de $Domain até haver um novo (o www mostra aviso)" }
}
if ($cert) { Write-Ok "certificado: $((Get-CertNames $cert.Cert) -join ', ') (vale até $($cert.Cert.NotAfter.ToString('dd/MM/yyyy')))" }

if (-not $PSCmdlet.ShouldProcess($Domain, "passar do site '$OldSite' para o portal")) { return }

# Backup
Write-Step 'Backup'
$stamp = Get-Date -Format 'yyyyMMdd-HHmmss'
$iisBackup = "antes-da-troca-$stamp"
& $appcmd add backup $iisBackup | Out-Null
if ($LASTEXITCODE -ne 0) { throw 'appcmd não conseguiu guardar a configuração do IIS. Nada foi alterado.' }
Write-Ok "configuração do IIS guardada ($iisBackup)"
if (-not (Test-Path $BackupDir)) { New-Item -ItemType Directory -Path $BackupDir | Out-Null }
$jsonBackup = $null
if (Test-Path $paths.ConfigFile) {
    $jsonBackup = Join-Path $paths.Config "portal.json.antes-da-troca-$stamp"
    Copy-Item $paths.ConfigFile $jsonBackup
    Write-Ok "portal.json copiado ($jsonBackup)"
}
if (-not $SkipFileBackup) {
    $zip = Join-Path $BackupDir "site-antigo-$stamp.zip"
    $physical = [Environment]::ExpandEnvironmentVariables($oldInfo.physicalPath)
    Write-Host "    compactando $physical (pode levar alguns minutos)..."
    Compress-Archive -Path (Join-Path $physical '*') -DestinationPath $zip -CompressionLevel Optimal
    Write-Ok "pasta do site antigo em $zip ($([math]::Round((Get-Item $zip).Length / 1MB)) MB)"
}

$state = [ordered]@{
    Domain = $Domain; Hosts = $hosts; OldSite = $OldSite; IisBackup = $iisBackup
    OldHttps = @($toMove | Where-Object { $_.Protocol -eq 'https' -and $_.CertHash } | ForEach-Object { [ordered]@{ Info = $_.Info; CertHash = $_.CertHash; CertStore = $_.CertStore } })
    PortalJsonBackup = $jsonBackup; PublicBaseUrlBefore = (Get-PublicBaseUrl)
    RedirectFlagExisted = (Test-Path (Join-Path $paths.Config 'https-sem-redirecionamento'))
    SwitchedAtUtc = (Get-Date).ToUniversalTime().ToString('yyyy-MM-ddTHH:mm:ssZ', [Globalization.CultureInfo]::InvariantCulture)
}
Save-State $state

try {
    Write-Step 'Passando o domínio para o portal'
    foreach ($m in $toMove) {
        $info = $m.Info
        Get-WebBinding -Name $m.Site -Protocol $m.Protocol | Where-Object { $_.bindingInformation -eq $info } | Remove-WebBinding
        Write-Ok "removido do site antigo: $($m.Protocol) $($m.Info)"
    }
    foreach ($h in $hosts) {
        if (@(Get-WebBinding -Name $script:SiteName -Protocol http | Where-Object { $_.bindingInformation -eq "*:80:$h" }).Count -eq 0) {
            New-WebBinding -Name $script:SiteName -Protocol http -Port 80 -HostHeader $h
        }
        Write-Ok "http://$h -> portal"
        if ($cert) {
            $b = @(Get-WebBinding -Name $script:SiteName -Protocol https | Where-Object { $_.bindingInformation -eq "*:443:$h" }) | Select-Object -First 1
            if (-not $b) {
                New-WebBinding -Name $script:SiteName -Protocol https -Port 443 -HostHeader $h -SslFlags 1
                $b = @(Get-WebBinding -Name $script:SiteName -Protocol https | Where-Object { $_.bindingInformation -eq "*:443:$h" }) | Select-Object -First 1
            }
            # O certificado do nome fica no http.sys, não no site: troca o que o site antigo usava.
            try { $b.RemoveSslCertificate() } catch { }
            $b.AddSslCertificate($cert.Cert.Thumbprint, $cert.Store)
            Write-Ok "https://$h -> portal"
        }
    }

    if ($wacs) {
        Write-Step "Certificado do Let's Encrypt para $($hosts -join ' e ') (win-acme)"
        $portalId = (Get-Website -Name $script:SiteName).id
        $wacsArgs = @('--source', 'iis', '--siteid', $portalId, '--host', ($hosts -join ','), '--validation', 'selfhosting', '--installation', 'iis', '--closeonfinish')
        # Aceitar os termos do Let's Encrypt é decisão de quem roda: só com -AcceptLetsEncryptTerms.
        if ($AcceptLetsEncryptTerms) { $wacsArgs += '--accepttos' }
        if ($Email) { $wacsArgs += @('--emailaddress', $Email) }
        & $wacs @wacsArgs
        $wacsCode = $LASTEXITCODE
        $installed = Find-Certificate $hosts
        if ($wacsCode -eq 0 -and $installed) {
            Write-Ok "certificado novo até $($installed.Cert.NotAfter.ToString('dd/MM/yyyy')); o win-acme renova e atualiza o IIS sozinho"
            $cert = $installed
        }
        elseif ($cert) {
            # O domínio sem www já funciona com o certificado antigo: não desfaz a troca por causa do www.
            Write-Warn "o win-acme não emitiu o certificado (código $wacsCode). O site segue com o certificado de $Domain; o www mostra aviso."
            Write-Warn "Rode depois: & '$wacs' $($wacsArgs -join ' ') --accepttos"
        }
        else { throw "o win-acme não emitiu o certificado (código $wacsCode)" }
    }

    Write-Step 'Portal no domínio novo'
    if (Test-Path $paths.ConfigFile) {
        Set-PublicBaseUrl "https://$Domain"
        Write-Ok "PublicBaseUrl = https://$Domain (links dos e-mails)"
    }
    Update-WebConfig -RealCertificate:([bool]$cert)
    Restart-PortalServices
    Write-Ok 'serviços do portal reiniciados'

    Write-Step 'Conferência pelo domínio'
    $problems = @()
    if (-not (Test-PortalHealth $paths)) { $problems += 'API ou site do portal' }
    $front = Invoke-LocalSite -HostName $Domain -Path '/' -Https
    if ($front -ne 200) { $problems += "https://$Domain/ respondeu $front" } else { Write-Ok "https://$Domain/ responde" }
    $plain = Invoke-LocalSite -HostName $Domain -Path '/'
    if ($plain -notin 200, 301, 308) { $problems += "http://$Domain/ respondeu $plain" } else { Write-Ok "http://$Domain/ responde ($plain)" }
    if (-not $NoWww) {
        $www = Invoke-LocalSite -HostName "www.$Domain" -Path '/' -Https
        if ($www -notin 200, 301, 308) { $problems += "https://www.$Domain/ respondeu $www" } else { Write-Ok "https://www.$Domain/ responde ($www)" }
    }
    $physicalOld = [Environment]::ExpandEnvironmentVariables($oldInfo.physicalPath)
    if ($legacyApps -contains 'updater' -and (Test-Path (Join-Path $physicalOld 'updater\version.txt'))) {
        $upd = Invoke-LocalSite -HostName $Domain -Path '/updater/version.txt'
        if ($upd -ne 200) { $problems += "http://$Domain/updater/version.txt respondeu $upd" } else { Write-Ok 'launcher: updater/version.txt responde pelo domínio' }
    }
    if ($legacyApps -contains 'ServerMain') {
        $clan = Invoke-LocalSite -HostName $Domain -Path '/ServerMain/isCheckClanName.asp'
        if ($clan -ne 200) { $problems += "http://$Domain/ServerMain/isCheckClanName.asp respondeu $clan" } else { Write-Ok 'clã: ServerMain responde pelo domínio' }
    }
    if ($problems.Count -gt 0) { throw "conferência falhou: $($problems -join '; ')" }
}
catch {
    Write-Warn "$_"
    if ($NoAutoRollback) {
        Write-Host '  -NoAutoRollback: nada foi desfeito. Para voltar: .\Switch-PortalDomain.ps1 -Rollback' -ForegroundColor Yellow
        throw
    }
    Invoke-Rollback $state
    throw "A troca falhou e foi desfeita: $_"
}

Write-Host ''
Write-Host "  Pronto. https://$Domain agora é o portal." -ForegroundColor Green
Write-Host "  O site '$OldSite' continua ligado pelo IP. Backup do IIS: $iisBackup" -ForegroundColor Green
Write-Host '  Falta conferir no navegador (janela anônima): login, cadastro e um e-mail de recuperação.' -ForegroundColor Green
Write-Host '  Se o cadastro usa o captcha da Cloudflare, ponha o domínio nos Hostnames do widget.' -ForegroundColor Yellow
Write-Host '  Para desfazer:                     .\Switch-PortalDomain.ps1 -Rollback' -ForegroundColor Green
Write-Host '  Daqui a uns dias, desligar o antigo: .\Switch-PortalDomain.ps1 -StopOldSite' -ForegroundColor Green
