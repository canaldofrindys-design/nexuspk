<#
.SYNOPSIS
  Primeira instalação do portal novo no Windows Server, ao lado do site atual.

.DESCRIPTION
  Cria tudo do portal sem mexer no site antigo:
    - pastas em C:\NexusPortal com permissões fechadas;
    - serviços Windows NexusPortalApi e NexusPortalWorker (contas virtuais, sem senha);
    - pool e site "NexusPortal" no IIS, só com o nome de host informado;
    - banco PortalDB e as migrações;
    - logins SQL de permissão mínima (portal_game_read, portal_game_write, portal_app) com senhas
      geradas aqui mesmo, gravadas só em C:\NexusPortal\config\portal.json;
    - tarefa diária de backup do PortalDB.
  Depois publica a versão do pacote e confere se responde.

  O banco do jogo não tem nenhuma tabela alterada: o script só cria os logins e dá SELECT nas tabelas
  que o portal lê, e INSERT/UPDATE(Passwd) nas tabelas de conta (database/scripts/permissoes-portal.sql).

  Pré-requisitos: IIS, .NET 10 Hosting Bundle, Node.js 22 LTS, sqlcmd e o SQL Server com
  autenticação mista. Rode como administrador, numa conta que seja sysadmin no SQL Server
  (ou informe -SqlCredential).

.EXAMPLE
  .\Install-Portal.ps1 -Package C:\NexusPortal-pacotes\nexusportal-2026.11.11-abc1234.zip -HostName portal.seudominio.com -CertificateThumbprint 0123ABCD...
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)] [string]$Package,
    [Parameter(Mandatory)] [string]$HostName,
    [string]$Root = 'C:\NexusPortal',
    [string]$SqlInstance = '.\SQLEXPRESS',
    [pscredential]$SqlCredential,
    [string]$CertificateThumbprint,
    [ValidateRange(1024, 65535)] [int]$ApiPort = 5080,
    [string]$BackupTime = '04:00',
    [switch]$SkipSqlLogins,
    [switch]$SkipBackupTask
)

. (Join-Path $PSScriptRoot 'Common.ps1')
Assert-Admin
$paths = Get-PortalPaths $Root
$HostName = $HostName.Trim().ToLowerInvariant()

# ---------------------------------------------------------------------------------------------
Write-Step 'Conferindo pré-requisitos'

if ($HostName -notmatch '^(?=.{4,253}$)([a-z0-9]([a-z0-9-]{0,61}[a-z0-9])?\.)+[a-z]{2,63}$') { throw "Nome de host inválido: $HostName" }
if ($Root -match '\s') { throw 'Use uma pasta sem espaços em -Root (ex.: C:\NexusPortal).' }

if (-not (Get-Service -Name W3SVC -ErrorAction SilentlyContinue)) {
    throw 'IIS não instalado. No Gerenciador do Servidor, adicione a função "Servidor Web (IIS)".'
}
Import-Module WebAdministration
Write-Ok 'IIS'

if (-not (Test-Path (Join-Path $env:ProgramFiles 'IIS\Asp.Net Core Module\V2\aspnetcorev2.dll'))) {
    throw 'Falta o módulo ASP.NET Core do IIS. Instale o ".NET 10 Hosting Bundle" (https://dotnet.microsoft.com/download/dotnet/10.0) e rode "iisreset".'
}
$runtimes = & dotnet --list-runtimes 2>$null
if (-not ($runtimes | Where-Object { $_ -match '^Microsoft\.AspNetCore\.App 10\.' })) {
    throw 'Falta o runtime ASP.NET Core 10. Instale o ".NET 10 Hosting Bundle".'
}
Write-Ok '.NET 10 Hosting Bundle'

$node = Get-NodePath
$nodeVersion = (& $node --version).TrimStart('v')
if ([version]$nodeVersion -lt [version]'20.9.0') { throw "Node.js $nodeVersion é antigo. Instale o Node.js 22 LTS." }
Write-Ok "Node.js $nodeVersion"

if (-not (Get-Command sqlcmd -ErrorAction SilentlyContinue)) { throw 'sqlcmd não encontrado. Instale as "Microsoft Command Line Utilities for SQL Server".' }
if (-not (Get-Command curl.exe -ErrorAction SilentlyContinue)) { throw 'curl.exe não encontrado (vem com o Windows Server 2019 ou mais novo).' }

$sqlInfo = @(Invoke-Sql -SqlInstance $SqlInstance -Credential $SqlCredential -Query "SELECT CONCAT(IS_SRVROLEMEMBER('sysadmin'), '|', CAST(SERVERPROPERTY('IsIntegratedSecurityOnly') AS int))")
$sysadmin, $windowsOnly = ($sqlInfo[0].Trim() -split '\|')
if ($sysadmin -ne '1') { throw 'A conta usada não é sysadmin no SQL Server. Use uma que seja ou informe -SqlCredential.' }
if ($windowsOnly -eq '1' -and -not $SkipSqlLogins) {
    throw 'O SQL Server aceita só autenticação do Windows. O portal usa logins SQL próprios: ative o modo misto (SQL Server e Windows) e reinicie o serviço do SQL.'
}
Write-Ok "SQL Server $SqlInstance"

$https = [bool]$CertificateThumbprint
if ($https) {
    $CertificateThumbprint = $CertificateThumbprint.Replace(' ', '').ToUpperInvariant()
    if (-not (Test-Path "Cert:\LocalMachine\My\$CertificateThumbprint")) { throw "Certificado $CertificateThumbprint não está em Computador Local > Pessoal." }
    if (-not (Test-UrlRewrite)) { Write-Warn 'URL Rewrite não instalado: o http não vai redirecionar para https (instale pelo site da Microsoft).' }
}
else {
    Write-Warn 'sem certificado: o site sobe só em http. Os cookies de sessão exigem https; use só para teste.'
}

foreach ($binding in @(Get-WebBinding)) {
    $parts = $binding.bindingInformation -split ':'
    $owner = $binding.ItemXPath -replace ".*name='([^']+)'.*", '$1'
    if ($parts[2] -eq $HostName -and $owner -ne $script:SiteName) {
        throw "O host $HostName já está no site '$owner' do IIS. Escolha outro nome para o portal."
    }
}
Write-Ok "host $HostName livre no IIS"

# ---------------------------------------------------------------------------------------------
Write-Step 'Serviços Windows'
$services = @(
    @{ Name = $script:ApiService; Exe = 'api\Priston.Api.exe'; Display = 'NexusPK Portal - API'; Log = 'api-.log'
       Env = @("ASPNETCORE_URLS=http://127.0.0.1:$ApiPort", 'ASPNETCORE_ENVIRONMENT=Production') },
    @{ Name = $script:WorkerService; Exe = 'worker\Priston.Worker.exe'; Display = 'NexusPK Portal - tarefas'; Log = 'worker-.log'
       Env = @() }
)
foreach ($svc in $services) {
    $bin = Join-Path $paths.Current $svc.Exe
    if (-not (Get-Service -Name $svc.Name -ErrorAction SilentlyContinue)) {
        & sc.exe create $svc.Name binPath= $bin start= delayed-auto obj= (Get-ServiceAccount $svc.Name) DisplayName= $svc.Display | Out-Null
        if ($LASTEXITCODE -ne 0) { throw "Não consegui criar o serviço $($svc.Name)." }
        & sc.exe description $svc.Name 'Portal NexusPK. Instalado pelo Install-Portal.ps1.' | Out-Null
        Write-Ok "serviço $($svc.Name) criado"
    }
    else {
        Write-Ok "serviço $($svc.Name) já existe"
    }
    # Reinicia sozinho se cair.
    & sc.exe failure $svc.Name reset= 86400 actions= restart/5000/restart/10000/restart/60000 | Out-Null
    $envValues = @("NEXUSPORTAL_CONFIG=$($paths.ConfigFile)", 'DOTNET_ENVIRONMENT=Production',
        'Serilog__WriteTo__1__Name=File',
        "Serilog__WriteTo__1__Args__path=$(Join-Path $paths.Logs $svc.Log)",
        'Serilog__WriteTo__1__Args__rollingInterval=Day',
        'Serilog__WriteTo__1__Args__retainedFileCountLimit=30') + $svc.Env
    Set-ServiceEnvironment $svc.Name $envValues
}

# ---------------------------------------------------------------------------------------------
Write-Step 'IIS: pool e site'
if (-not (Test-Path "IIS:\AppPools\$script:AppPool")) { New-WebAppPool -Name $script:AppPool | Out-Null }
$poolPath = "IIS:\AppPools\$script:AppPool"
Set-ItemProperty $poolPath -Name managedRuntimeVersion -Value ''
Set-ItemProperty $poolPath -Name startMode -Value 'AlwaysRunning'
Set-ItemProperty $poolPath -Name processModel.idleTimeout -Value ([TimeSpan]::Zero)
Write-Ok "pool $script:AppPool (sem .NET gerenciado, sempre ligado)"

$sitePath = Join-Path $paths.Current 'web'
if (-not (Get-Website -Name $script:SiteName)) {
    # O IIS exige que a pasta exista ao criar o site; a junção definitiva vem na publicação.
    $placeholder = Join-Path $paths.Releases '_vazio'
    New-Item -ItemType Directory -Force -Path (Join-Path $placeholder 'web') | Out-Null
    if (-not (Test-Path $paths.Current)) { New-Item -ItemType Junction -Path $paths.Current -Target $placeholder | Out-Null }
    New-Website -Name $script:SiteName -PhysicalPath $sitePath -ApplicationPool $script:AppPool -HostHeader $HostName -Port 80 | Out-Null
    Stop-Website -Name $script:SiteName
    Write-Ok "site $script:SiteName criado (http://$HostName)"
}
# Log do IIS sem a query string: os links de e-mail levam o código de confirmação e de nova senha na URL.
Set-ItemProperty "IIS:\Sites\$script:SiteName" -Name logFile.logExtFileFlags -Value 'Date,Time,ClientIP,UserName,ServerIP,Method,UriStem,HttpStatus,Win32Status,TimeTaken,ServerPort,UserAgent,Referer,HttpSubStatus'
Write-Ok 'log do IIS sem query string'
if ($https -and -not (Get-WebBinding -Name $script:SiteName -Protocol https)) {
    New-WebBinding -Name $script:SiteName -Protocol https -Port 443 -HostHeader $HostName -SslFlags 1
    (Get-WebBinding -Name $script:SiteName -Protocol https).AddSslCertificate($CertificateThumbprint, 'My')
    Write-Ok "https://$HostName com o certificado $CertificateThumbprint"
}

# ---------------------------------------------------------------------------------------------
Write-Step 'Pastas e permissões'
$apiAccount = Get-ServiceAccount $script:ApiService
$workerAccount = Get-ServiceAccount $script:WorkerService
$poolAccount = Get-AppPoolAccount
# Leitura na raiz para atravessar a junção "current"; config, logs e backups têm permissões próprias.
Set-FolderAcl $paths.Root @{ $apiAccount = 'ReadAndExecute'; $workerAccount = 'ReadAndExecute'; $poolAccount = 'ReadAndExecute' }
Set-FolderAcl $paths.Config @{ $apiAccount = 'ReadAndExecute'; $workerAccount = 'ReadAndExecute' }
Set-FolderAcl $paths.Releases @{ $apiAccount = 'ReadAndExecute'; $workerAccount = 'ReadAndExecute'; $poolAccount = 'ReadAndExecute' }
Set-FolderAcl $paths.Logs @{ $apiAccount = 'Modify'; $workerAccount = 'Modify'; $poolAccount = 'Modify' }
New-Item -ItemType Directory -Force -Path (Join-Path $paths.Logs 'web') | Out-Null
Set-FolderAcl $paths.Backups @{ (Get-SqlServiceAccount $SqlInstance) = 'Modify' }
Write-Ok "config: só administradores e os dois serviços; o site do IIS não lê os segredos"

# ---------------------------------------------------------------------------------------------
Write-Step 'Banco do portal (PortalDB)'
$staging = Expand-PortalRelease $paths $Package
try {
    Invoke-PortalMigrations $staging.Folder $SqlInstance $SqlCredential -CreateDatabase
    Write-Ok 'PortalDB criado e migrado'

    if ((Test-Path $paths.ConfigFile) -or $SkipSqlLogins) {
        Write-Ok "$($paths.ConfigFile) já existe ou -SkipSqlLogins: logins e senhas mantidos"
    }
    else {
        Write-Step 'Logins SQL de permissão mínima'
        $pw = @{ read = New-RandomPassword; write = New-RandomPassword; app = New-RandomPassword }
        $sql = Get-Content -Raw -Encoding UTF8 (Join-Path $staging.Folder 'deploy\sql\permissoes-portal.sql')
        $sql = $sql.Replace('<SENHA-LEITURA>', $pw.read).Replace('<SENHA-ESCRITA>', $pw.write).Replace('<SENHA-PORTAL>', $pw.app)
        # Se os logins já existiam de uma tentativa anterior, a senha passa a ser a nova.
        $sql += "`r`nGO`r`nALTER LOGIN portal_game_read WITH PASSWORD = '$($pw.read)';`r`nALTER LOGIN portal_game_write WITH PASSWORD = '$($pw.write)';`r`nALTER LOGIN portal_app WITH PASSWORD = '$($pw.app)';`r`nGO`r`n"
        # Arquivo temporário dentro de config (pasta fechada) e apagado logo depois.
        $tmp = Join-Path $paths.Config ('_logins_' + [guid]::NewGuid().ToString('N') + '.sql')
        try {
            [IO.File]::WriteAllText($tmp, $sql, (New-Object System.Text.UTF8Encoding($false)))
            Invoke-Sql -SqlInstance $SqlInstance -Credential $SqlCredential -InputFile $tmp | Out-Null
        }
        finally {
            Remove-Item $tmp -Force -ErrorAction SilentlyContinue
        }
        Write-Ok 'portal_game_read, portal_game_write e portal_app com senhas geradas aqui'

        $cs = { param($db, $user, $pass) "Server=$SqlInstance;Database=$db;User Id=$user;Password=$pass;TrustServerCertificate=True;Application Name=NexusPortal" }
        $scheme = 'http'
        if ($https) { $scheme = 'https' }
        $config = [ordered]@{
            PortalDatabase = [ordered]@{ ConnectionString = (& $cs $script:PortalDatabase 'portal_app' $pw.app) }
            GameDatabase   = [ordered]@{
                ReadConnectionString  = (& $cs 'accountdb' 'portal_game_read' $pw.read)
                WriteConnectionString = (& $cs 'accountdb' 'portal_game_write' $pw.write)
            }
            Accounts       = [ordered]@{ PublicBaseUrl = "${scheme}://$HostName" }
            Smtp           = [ordered]@{ Host = ''; Port = 587; EnableSsl = $true; UserName = ''; Password = ''; FromAddress = ''; FromName = 'NexusPK' }
            Captcha        = [ordered]@{ TurnstileSecret = '' }
            GameServer     = [ordered]@{ TimeoutMilliseconds = 2000; Targets = @() }
        }
        $json = $config | ConvertTo-Json -Depth 6
        [IO.File]::WriteAllText($paths.ConfigFile, $json, (New-Object System.Text.UTF8Encoding($false)))
        Write-Ok "segredos gravados em $($paths.ConfigFile)"
    }

    # A tarefa de backup roda como SYSTEM: só precisa de db_backupoperator no PortalDB.
    $system = Get-SystemAccountName
    $backupGrant = @"
IF SUSER_ID(N'$system') IS NULL CREATE LOGIN [$system] FROM WINDOWS;
USE [$script:PortalDatabase];
IF USER_ID(N'$system') IS NULL AND IS_SRVROLEMEMBER('sysadmin', N'$system') = 0
    CREATE USER [$system] FOR LOGIN [$system];
IF USER_ID(N'$system') IS NOT NULL ALTER ROLE db_backupoperator ADD MEMBER [$system];
"@
    Invoke-Sql -SqlInstance $SqlInstance -Credential $SqlCredential -Query $backupGrant | Out-Null
    Write-Ok 'backup do PortalDB liberado para a tarefa agendada'
}
finally {
    Remove-Item -Recurse -Force $staging.Folder -ErrorAction SilentlyContinue
}

# ---------------------------------------------------------------------------------------------
Write-Step 'Publicando a versão do pacote'
& (Join-Path $PSScriptRoot 'Deploy-Portal.ps1') -Package $Package -Root $Root -SqlInstance $SqlInstance -SqlCredential $SqlCredential
Remove-Item -Recurse -Force (Join-Path $paths.Releases '_vazio') -ErrorAction SilentlyContinue

if (-not $SkipBackupTask) {
    Write-Step 'Tarefa diária de backup'
    $script = Join-Path $paths.Current 'deploy\Backup-PortalDb.ps1'
    $action = New-ScheduledTaskAction -Execute 'powershell.exe' -Argument "-NoProfile -ExecutionPolicy Bypass -File `"$script`" -Root `"$Root`" -SqlInstance `"$SqlInstance`""
    $trigger = New-ScheduledTaskTrigger -Daily -At $BackupTime
    $principal = New-ScheduledTaskPrincipal -UserId (Get-SystemAccountName) -LogonType ServiceAccount -RunLevel Highest
    Register-ScheduledTask -TaskName 'NexusPortal - backup do PortalDB' -Action $action -Trigger $trigger -Principal $principal -Force | Out-Null
    Write-Ok "todo dia às $BackupTime, 14 dias guardados em $($paths.Backups)"
}

Write-Step 'Instalação concluída'
Write-Host @"

  Site:      http$(if ($https) { 's' })://$HostName
  Segredos:  $($paths.ConfigFile)  (abra o Bloco de Notas como administrador)
             Preencha Smtp (e-mail), Captcha:TurnstileSecret e GameServer:Targets (portas do jogo)
             e rode: Restart-Service $script:ApiService, $script:WorkerService
  Logs:      $($paths.Logs)
  Backups:   $($paths.Backups)

  Primeiro superadministrador do painel da equipe:
    $(Join-Path $paths.Current 'deploy\New-PortalAdmin.ps1') -UserName <usuario>

  Próximas versões:
    $(Join-Path $paths.Current 'deploy\Deploy-Portal.ps1') -Package <pacote.zip>

"@
