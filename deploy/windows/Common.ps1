# Funções comuns dos scripts de instalação e publicação do portal.
# Compatível com o Windows PowerShell 5.1 do Windows Server. Não rode este arquivo sozinho.

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$script:ApiService = 'NexusPortalApi'
$script:WorkerService = 'NexusPortalWorker'
$script:SiteName = 'NexusPortal'
$script:AppPool = 'NexusPortal'
$script:PortalDatabase = 'PortalDB'

function Write-Step([string]$Message) {
    Write-Host ''
    Write-Host "==> $Message" -ForegroundColor Cyan
}

function Write-Ok([string]$Message) { Write-Host "    ok: $Message" -ForegroundColor Green }

function Write-Warn([string]$Message) { Write-Host "    atenção: $Message" -ForegroundColor Yellow }

function Assert-Admin {
    $principal = New-Object Security.Principal.WindowsPrincipal([Security.Principal.WindowsIdentity]::GetCurrent())
    if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
        throw 'Abra o PowerShell como administrador.'
    }
}

function Get-PortalPaths([string]$Root) {
    return @{
        Root       = $Root
        Config     = Join-Path $Root 'config'
        ConfigFile = Join-Path $Root 'config\portal.json'
        Releases   = Join-Path $Root 'releases'
        Current    = Join-Path $Root 'current'
        Previous   = Join-Path $Root 'config\previous-release.txt'
        Logs       = Join-Path $Root 'logs'
        Backups    = Join-Path $Root 'backups'
    }
}

function Get-ServiceAccount([string]$ServiceName) { return "NT SERVICE\$ServiceName" }

function Get-AppPoolAccount { return "IIS AppPool\$script:AppPool" }

# Nome da conta SYSTEM no idioma do Windows ("NT AUTHORITY\SYSTEM", "AUTORIDADE NT\SISTEMA"...).
function Get-SystemAccountName {
    $sid = New-Object System.Security.Principal.SecurityIdentifier('S-1-5-18')
    return $sid.Translate([System.Security.Principal.NTAccount]).Value
}

# Conta do serviço do SQL Server (grava os arquivos de backup).
function Get-SqlServiceAccount([string]$SqlInstance) {
    $name = ($SqlInstance -split '\\')[-1]
    if ($SqlInstance -notmatch '\\' -or $name -eq 'MSSQLSERVER') { return 'NT SERVICE\MSSQLSERVER' }
    return "NT SERVICE\MSSQL`$$name"
}

# Troca as permissões da pasta: só quem for listado acessa (sem herdar "Usuários" do C:\).
function Set-FolderAcl([string]$Path, [hashtable]$Rights) {
    if (-not (Test-Path $Path)) { New-Item -ItemType Directory -Path $Path | Out-Null }
    $acl = New-Object System.Security.AccessControl.DirectorySecurity
    $acl.SetAccessRuleProtection($true, $false)
    $inherit = [System.Security.AccessControl.InheritanceFlags]'ContainerInherit, ObjectInherit'
    $propagate = [System.Security.AccessControl.PropagationFlags]::None
    # Administradores e SYSTEM pelo SID: no Windows em português os nomes são outros.
    foreach ($sid in 'S-1-5-32-544', 'S-1-5-18') {
        $id = New-Object System.Security.Principal.SecurityIdentifier($sid)
        $acl.AddAccessRule((New-Object System.Security.AccessControl.FileSystemAccessRule($id, 'FullControl', $inherit, $propagate, 'Allow')))
    }
    foreach ($key in $Rights.Keys) {
        $acl.AddAccessRule((New-Object System.Security.AccessControl.FileSystemAccessRule($key, $Rights[$key], $inherit, $propagate, 'Allow')))
    }
    Set-Acl -Path $Path -AclObject $acl
}

# sqlcmd com a conta do Windows (-E) ou com o login informado (senha pela variável SQLCMDPASSWORD,
# nunca na linha de comando).
function Invoke-Sql {
    param(
        [Parameter(Mandatory)] [string]$SqlInstance,
        [string]$Query,
        [string]$InputFile,
        [string]$Database = 'master',
        [pscredential]$Credential
    )
    $sqlArgs = @('-S', $SqlInstance, '-d', $Database, '-b', '-C', '-h', '-1', '-W', '-f', '65001')
    if ($Credential) {
        $sqlArgs += @('-U', $Credential.UserName)
        $env:SQLCMDPASSWORD = $Credential.GetNetworkCredential().Password
    }
    else {
        $sqlArgs += '-E'
    }
    if ($InputFile) { $sqlArgs += @('-i', $InputFile) } else { $sqlArgs += @('-Q', "SET NOCOUNT ON; SET QUOTED_IDENTIFIER ON; $Query") }
    try {
        $output = & sqlcmd @sqlArgs 2>&1
        if ($LASTEXITCODE -ne 0) { throw "sqlcmd falhou: $($output -join ' ')" }
        return $output
    }
    finally {
        Remove-Item Env:\SQLCMDPASSWORD -ErrorAction SilentlyContinue
    }
}

# Connection string para o migrador (login administrativo só durante a publicação).
function Get-DeployConnectionString([string]$SqlInstance, [pscredential]$Credential) {
    $cs = "Server=$SqlInstance;Database=$script:PortalDatabase;TrustServerCertificate=True;"
    if ($Credential) {
        return $cs + "User Id=$($Credential.UserName);Password=$($Credential.GetNetworkCredential().Password)"
    }
    return $cs + 'Integrated Security=True'
}

# Senha forte só com letras e números: segura dentro de connection string e de literal SQL.
function New-RandomPassword([int]$Length = 32) {
    $chars = 'abcdefghijkmnopqrstuvwxyzABCDEFGHJKLMNPQRSTUVWXYZ23456789'.ToCharArray()
    $bytes = New-Object byte[] $Length
    $rng = [System.Security.Cryptography.RandomNumberGenerator]::Create()
    try { $rng.GetBytes($bytes) } finally { $rng.Dispose() }
    $body = -join ($bytes | ForEach-Object { $chars[$_ % $chars.Length] })
    # Garante maiúscula, minúscula e número para a política de senha do Windows.
    return 'Px7' + $body.Substring(3)
}

# Variáveis de ambiente de um serviço Windows (valor "Environment" no registro do serviço).
function Set-ServiceEnvironment([string]$ServiceName, [string[]]$Values) {
    $key = "HKLM:\SYSTEM\CurrentControlSet\Services\$ServiceName"
    New-ItemProperty -Path $key -Name 'Environment' -PropertyType MultiString -Value $Values -Force | Out-Null
}

function Get-NodePath {
    $cmd = Get-Command node.exe -ErrorAction SilentlyContinue
    if (-not $cmd) { throw 'Node.js não encontrado. Instale o Node.js 22 LTS (https://nodejs.org) e abra um novo PowerShell.' }
    return $cmd.Source
}

function Test-UrlRewrite { return Test-Path (Join-Path $env:windir 'System32\inetsrv\rewrite.dll') }

# Lê a versão do pacote (arquivo VERSION na raiz da pasta extraída).
function Get-ReleaseVersion([string]$Folder) {
    $file = Join-Path $Folder 'VERSION'
    if (-not (Test-Path $file)) { throw "Pacote inválido: falta o arquivo VERSION em $Folder." }
    $version = (Get-Content $file -Raw).Trim()
    if ($version -notmatch '^[A-Za-z0-9._-]{1,64}$') { throw "Versão inválida no pacote: '$version'." }
    return $version
}

# Requisição local ao site pelo nome do host, sem depender do DNS.
function Invoke-LocalSite([string]$HostName, [string]$Path = '/', [switch]$Https) {
    $scheme = 'http'; $port = 80
    if ($Https) { $scheme = 'https'; $port = 443 }
    $url = "${scheme}://${HostName}${Path}"
    # -k: a conferência aqui é do portal, não do certificado (a conexão é local, em 127.0.0.1).
    $out = & curl.exe -s -k -o NUL -w '%{http_code}' --max-time 60 --resolve "${HostName}:${port}:127.0.0.1" $url 2>$null
    return [int]$out
}

function Wait-Http([scriptblock]$Probe, [int]$Expected = 200, [int]$Seconds = 180) {
    $deadline = (Get-Date).AddSeconds($Seconds)
    $last = 0
    while ((Get-Date) -lt $deadline) {
        try { $last = & $Probe } catch { $last = 0 }
        if ($last -eq $Expected) { return $true }
        Start-Sleep -Seconds 3
    }
    Write-Warn "última resposta: $last"
    return $false
}

function Get-ApiPort {
    $value = (Get-ItemProperty "HKLM:\SYSTEM\CurrentControlSet\Services\$script:ApiService" -Name Environment -ErrorAction SilentlyContinue)
    if ($value) {
        foreach ($line in $value.Environment) {
            if ($line -match '^ASPNETCORE_URLS=http://127\.0\.0\.1:(\d+)$') { return [int]$Matches[1] }
        }
    }
    return 5080
}

# Dá um direito a mais numa pasta, sem tirar os que ela já herda.
function Add-FolderRight([string]$Path, [string]$Identity, [string]$Rights) {
    if (-not (Test-Path $Path)) { New-Item -ItemType Directory -Path $Path | Out-Null }
    $acl = Get-Acl $Path
    $inherit = [System.Security.AccessControl.InheritanceFlags]'ContainerInherit, ObjectInherit'
    $rule = New-Object System.Security.AccessControl.FileSystemAccessRule($Identity, $Rights, $inherit, 'None', 'Allow')
    $acl.AddAccessRule($rule)
    Set-Acl -Path $Path -AclObject $acl
}

# Extrai o pacote numa pasta nova em releases\<versão>_<data>. Nunca sobrescreve uma versão existente.
function Expand-PortalRelease([hashtable]$Paths, [string]$Package) {
    if (-not (Test-Path $Package)) { throw "Pacote não encontrado: $Package" }
    $hashFile = "$Package.sha256"
    if (Test-Path $hashFile) {
        $expected = ((Get-Content $hashFile -Raw).Trim() -split '\s+')[0].ToLowerInvariant()
        $actual = (Get-FileHash -Algorithm SHA256 -Path $Package).Hash.ToLowerInvariant()
        if ($expected -ne $actual) { throw 'O SHA-256 do pacote não confere com o arquivo .sha256. Baixe de novo.' }
        Write-Ok 'SHA-256 do pacote confere'
    }
    else {
        Write-Warn "sem $([IO.Path]::GetFileName($hashFile)) ao lado do pacote; integridade não conferida"
    }

    $temp = Join-Path $Paths.Releases ('_extraindo_' + [guid]::NewGuid().ToString('N'))
    Expand-Archive -Path $Package -DestinationPath $temp
    try {
        $version = Get-ReleaseVersion $temp
        foreach ($required in 'api\Priston.Api.exe', 'worker\Priston.Worker.exe', 'migrator\Priston.DbMigrator.exe', 'web\server.js', 'web\start.js', 'deploy\Deploy-Portal.ps1') {
            if (-not (Test-Path (Join-Path $temp $required))) { throw "Pacote incompleto: falta $required." }
        }
        $target = Join-Path $Paths.Releases ($version + '_' + (Get-Date -Format 'yyyyMMddHHmmss'))
        Rename-Item -Path $temp -NewName ([IO.Path]::GetFileName($target))
        return @{ Version = $version; Folder = $target }
    }
    catch {
        Remove-Item -Recurse -Force $temp -ErrorAction SilentlyContinue
        throw
    }
}

# Roda o migrador da versão: aplica as migrações novas no PortalDB (login administrativo, só agora).
function Invoke-PortalMigrations([string]$ReleaseFolder, [string]$SqlInstance, [pscredential]$Credential, [switch]$CreateDatabase) {
    $exe = Join-Path $ReleaseFolder 'migrator\Priston.DbMigrator.exe'
    $env:PRISTON_ConnectionStrings__PortalDbDeploy = Get-DeployConnectionString $SqlInstance $Credential
    try {
        $migratorArgs = @()
        if ($CreateDatabase) { $migratorArgs += '--create-database' }
        $output = & $exe @migratorArgs 2>&1
        $code = $LASTEXITCODE
        $output | Where-Object { $_ -match 'Executing|Migrações|Falha|error' } | ForEach-Object { Write-Host "    $_" }
        if ($code -ne 0) { throw "Migração falhou (código $code). Saída: $($output -join ' | ')" }
    }
    finally {
        Remove-Item Env:\PRISTON_ConnectionStrings__PortalDbDeploy -ErrorAction SilentlyContinue
    }
}

function Get-CurrentRelease([hashtable]$Paths) {
    if (-not (Test-Path $Paths.Current)) { return $null }
    $item = Get-Item $Paths.Current -Force
    if ($item.LinkType -ne 'Junction') { throw "$($Paths.Current) existe e não é uma junção. Remova ou renomeie antes." }
    return @($item.Target)[0]
}

function Stop-Portal([hashtable]$Paths) {
    $current = Get-CurrentRelease $Paths
    if ($current) {
        # O módulo do IIS encerra o Node e mostra esta página enquanto a pasta é trocada.
        Set-Content -Path (Join-Path $current 'web\app_offline.htm') -Encoding UTF8 -Value '<!doctype html><html lang="pt-BR"><meta charset="utf-8"><title>Atualizando</title><body style="background:#0e0b08;color:#e8dcc4;font-family:sans-serif;padding:3rem">O portal está sendo atualizado. Volte em alguns minutos.</body></html>'
        Start-Sleep -Seconds 3
    }
    foreach ($name in $script:WorkerService, $script:ApiService) {
        $svc = Get-Service -Name $name -ErrorAction SilentlyContinue
        if ($svc -and $svc.Status -ne 'Stopped') { Stop-Service -Name $name -Force; $svc.WaitForStatus('Stopped', [TimeSpan]::FromSeconds(60)) }
    }
}

function Start-Portal([hashtable]$Paths) {
    $current = Get-CurrentRelease $Paths
    Remove-Item (Join-Path $current 'web\app_offline.htm') -ErrorAction SilentlyContinue
    foreach ($name in $script:ApiService, $script:WorkerService) { Start-Service -Name $name }
    Import-Module WebAdministration
    if ((Get-WebAppPoolState -Name $script:AppPool).Value -eq 'Started') { Restart-WebAppPool -Name $script:AppPool } else { Start-WebAppPool -Name $script:AppPool }
    if ((Get-WebsiteState -Name $script:SiteName).Value -ne 'Started') { Start-Website -Name $script:SiteName }
}

# Aponta "current" para outra versão. Os serviços precisam estar parados.
function Set-CurrentRelease([hashtable]$Paths, [string]$Folder) {
    $old = Get-CurrentRelease $Paths
    if ($old) {
        Set-Content -Path $Paths.Previous -Value $old -Encoding UTF8
        & cmd.exe /c rmdir "$($Paths.Current)" | Out-Null
        Remove-Item (Join-Path $old 'web\app_offline.htm') -ErrorAction SilentlyContinue
    }
    New-Item -ItemType Junction -Path $Paths.Current -Target $Folder | Out-Null
}

function Get-SiteHost {
    Import-Module WebAdministration
    $bindings = @(Get-WebBinding -Name $script:SiteName)
    $https = @($bindings | Where-Object { $_.protocol -eq 'https' })
    $chosen = $bindings[0]
    if ($https.Count -gt 0) { $chosen = $https[0] }
    return @{ HostName = ($chosen.bindingInformation -split ':')[2]; Https = ($chosen.protocol -eq 'https') }
}

function Test-PortalHealth([hashtable]$Paths) {
    $port = Get-ApiPort
    $site = Get-SiteHost
    $apiOk = Wait-Http { [int](& curl.exe -s -o NUL -w '%{http_code}' --max-time 20 "http://127.0.0.1:$port/health/ready") }
    if (-not $apiOk) { Write-Warn "a API não respondeu em http://127.0.0.1:$port/health/ready (veja $($Paths.Logs)\api-*.log)"; return $false }
    Write-Ok 'API e bancos respondendo'
    $siteOk = Wait-Http { Invoke-LocalSite -HostName $site.HostName -Path '/' -Https:$site.Https }
    if (-not $siteOk) { Write-Warn "o site não respondeu 200 em $($site.HostName) (veja $($Paths.Logs)\web)"; return $false }
    Write-Ok "site respondendo em $($site.HostName)"
    return $true
}

# Pastas do site antigo que o jogo e o launcher chamam (clã, ícones de clã, atualização, status).
$script:LegacyDefaultPaths = @('ServerMain', 'Clan', 'ClanContent', 'updater', 'att', 'rankAPI', 'include')

# Aplicações do site antigo ligadas ao site do portal (Connect-LegacyPaths.ps1), sem a barra inicial.
function Get-LegacyApps {
    Import-Module WebAdministration
    return @(Get-WebApplication -Site $script:SiteName | ForEach-Object { $_.path.TrimStart('/') } | Where-Object { $_ })
}

# php-cgi.exe registrado no FastCGI do IIS (o mesmo que o site antigo usa), ou $null.
function Get-PhpCgiPath {
    Import-Module WebAdministration
    $apps = @(Get-WebConfiguration -Filter 'system.webServer/fastCgi/application' -PSPath 'MACHINE/WEBROOT/APPHOST')
    $php = @($apps | Where-Object { $_.fullPath -like '*php-cgi.exe' })
    if ($php.Count -eq 0) { return $null }
    return $php[0].fullPath
}

# Blocos <location> das aplicações do site antigo: o PHP do site antigo vale só nelas
# (no site antigo ele vem do web.config da raiz, que as aplicações daqui não leem).
function Get-LegacyWebConfig {
    $apps = @(Get-LegacyApps)
    if ($apps.Count -eq 0) { return '' }
    $php = Get-PhpCgiPath
    if (-not $php) { Write-Warn 'nenhum php-cgi.exe no FastCGI do IIS: as páginas .php do site antigo não vão rodar no portal.'; return '' }
    $phpXml = [Security.SecurityElement]::Escape($php)
    $blocks = foreach ($app in $apps) {
        $pathXml = [Security.SecurityElement]::Escape($app)
        @"
  <location path="$pathXml">
    <system.webServer>
      <handlers>
        <remove name="PHP-legado" />
        <add name="PHP-legado" path="*.php" verb="*" modules="FastCgiModule" scriptProcessor="$phpXml" resourceType="File" requireAccess="Script" />
      </handlers>
    </system.webServer>
  </location>
"@
    }
    return ($blocks -join "`r`n")
}

# web.config da versão: caminho do Node, porta da API, pasta de logs e, com HTTPS, o redirecionamento.
# Modelo do web.config que veio junto com este script. Ao voltar para uma versão anterior a esta, o modelo dela
# não separa as pastas do site antigo; aí vale este, que é igual no resto.
$script:OwnWebConfigTemplate = $null
$ownTemplateFile = Join-Path $PSScriptRoot 'web\web.config.template'
if (Test-Path $ownTemplateFile) { $script:OwnWebConfigTemplate = Get-Content -Raw -Path $ownTemplateFile }

function Write-WebConfig([hashtable]$Paths, [string]$ReleaseFolder) {
    $template = Get-Content -Raw -Path (Join-Path $ReleaseFolder 'deploy\web\web.config.template')
    if ($template -notmatch '__LEGACY__' -and $script:OwnWebConfigTemplate -match '__LEGACY__') { $template = $script:OwnWebConfigTemplate }
    $site = Get-SiteHost
    $rewrite = ''
    if ($site.Https) {
        if (Test-UrlRewrite) {
            $rewrite = @'
    <rewrite>
      <rules>
        <rule name="Redirecionar para HTTPS" stopProcessing="true">
          <match url="(.*)" />
          <conditions><add input="{HTTPS}" pattern="off" /></conditions>
          <action type="Redirect" url="https://{HTTP_HOST}/{R:1}" redirectType="Permanent" />
        </rule>
      </rules>
    </rewrite>
'@
        }
        else {
            Write-Warn 'URL Rewrite não instalado: quem abrir por http não é levado ao https.'
        }
    }
    $xml = $template.Replace('__NODE__', (Get-NodePath)).Replace('__LOGS__', $Paths.Logs).Replace('__API__', "http://127.0.0.1:$(Get-ApiPort)").Replace('__REWRITE__', $rewrite).Replace('__LEGACY__', (Get-LegacyWebConfig))
    $utf8 = New-Object System.Text.UTF8Encoding($false)
    $file = Join-Path $ReleaseFolder 'web\web.config'
    [IO.File]::WriteAllText($file, $xml, $utf8)
    # O IIS lê o web.config do site com a conta do pool de cada aplicação. As pastas do site antigo
    # rodam no pool dele, que precisa ler só este arquivo (a pasta do portal continua fechada).
    Import-Module WebAdministration
    $pools = @(Get-WebApplication -Site $script:SiteName | ForEach-Object { $_.applicationPool } | Where-Object { $_ -and $_ -ne $script:AppPool } | Sort-Object -Unique)
    if ($pools.Count -gt 0) {
        $acl = Get-Acl $file
        foreach ($pool in $pools) { $acl.AddAccessRule((New-Object System.Security.AccessControl.FileSystemAccessRule("IIS AppPool\$pool", 'Read', 'Allow'))) }
        Set-Acl -Path $file -AclObject $acl
    }
}

# Mantém a versão atual, a anterior e as N mais novas.
function Remove-OldReleases([hashtable]$Paths, [int]$Keep) {
    $current = Get-CurrentRelease $Paths
    $previous = $null
    if (Test-Path $Paths.Previous) { $previous = (Get-Content $Paths.Previous -Raw).Trim() }
    $all = @(Get-ChildItem -Path $Paths.Releases -Directory | Sort-Object Name -Descending)
    $all | Where-Object { $_.Name -like '_extraindo_*' } | Remove-Item -Recurse -Force -ErrorAction SilentlyContinue
    $releases = @($all | Where-Object { $_.Name -notlike '_extraindo_*' })
    $kept = 0
    foreach ($dir in $releases) {
        if ($dir.FullName -eq $current -or $dir.FullName -eq $previous) { continue }
        $kept++
        if ($kept -gt $Keep) { Remove-Item -Recurse -Force $dir.FullName; Write-Ok "versão antiga removida: $($dir.Name)" }
    }
}
