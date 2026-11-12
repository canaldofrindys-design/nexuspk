<#
.SYNOPSIS
  Atualiza o portal sozinho: baixa o pacote mais novo do GitHub e publica com o Deploy-Portal.ps1.

.DESCRIPTION
  Cada merge na main gera, no GitHub, uma release "build-<versão>" com o pacote já testado num
  Windows com IIS e SQL Server. Este script:
  1. consulta as releases do repositório (público, sem token);
  2. se houver versão mais nova que a do ar, baixa o zip e o .sha256 para C:\NexusPortal-pacotes;
  3. roda o Deploy-Portal.ps1 (backup, migrações, troca de versão e volta automática se falhar).
  Cada versão é instalada sozinha uma vez só: se falhar ou for desfeita com o Rollback-Portal.ps1,
  só volta com -Force.
  Registro em C:\NexusPortal\logs\update.log.

  Com -InstallTask, cria a tarefa agendada "NexusPortal - atualização automática", que roda a
  cada 10 minutos como SYSTEM, e já faz a primeira atualização.

.EXAMPLE
  # Uma vez, num PowerShell como administrador (conta sysadmin no SQL Server):
  .\Update-Portal.ps1 -InstallTask

.EXAMPLE
  # Atualizar agora, sem esperar a tarefa:
  C:\NexusPortal\tools\Update-Portal.ps1

.EXAMPLE
  # Desligar a atualização automática:
  C:\NexusPortal\tools\Update-Portal.ps1 -RemoveTask
#>
[CmdletBinding()]
param(
    [string]$Repo = 'canaldofrindys-design/nexuspk',
    [string]$Root = 'C:\NexusPortal',
    [string]$SqlInstance = '.\SQLEXPRESS',
    [string]$PackagesDir = 'C:\NexusPortal-pacotes',
    # Só versões numeradas (tags v*), ignorando os builds de cada merge na main.
    [switch]$OnlyStable,
    [switch]$Force,
    [switch]$InstallTask,
    [int]$EveryMinutes = 10,
    [switch]$RemoveTask
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'
[Net.ServicePointManager]::SecurityProtocol = [Net.ServicePointManager]::SecurityProtocol -bor [Net.SecurityProtocolType]::Tls12

$TaskName = 'NexusPortal - atualização automática'
$logDir = Join-Path $Root 'logs'
$log = Join-Path $logDir 'update.log'
$stateFile = Join-Path $Root 'config\auto-update.txt'
$toolsDir = Join-Path $Root 'tools'

function Write-Log([string]$Message, [string]$Color = 'Gray') {
    if (-not (Test-Path $logDir)) { New-Item -ItemType Directory -Path $logDir | Out-Null }
    Add-Content -Path $log -Value ('{0:yyyy-MM-dd HH:mm:ss} {1}' -f (Get-Date), $Message) -Encoding UTF8
    Write-Host "    $Message" -ForegroundColor $Color
}

$principal = New-Object Security.Principal.WindowsPrincipal([Security.Principal.WindowsIdentity]::GetCurrent())
if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) { throw 'Abra o PowerShell como administrador.' }
if (-not (Test-Path (Join-Path $Root 'config\portal.json'))) { throw "Portal não instalado em $Root. Rode o Install-Portal.ps1 primeiro." }

$systemAccount = (New-Object System.Security.Principal.SecurityIdentifier('S-1-5-18')).Translate([System.Security.Principal.NTAccount]).Value

if ($RemoveTask) {
    Unregister-ScheduledTask -TaskName $TaskName -Confirm:$false -ErrorAction SilentlyContinue
    Write-Log 'atualização automática desligada' 'Yellow'
    return
}

if ($InstallTask) {
    Write-Host ''
    Write-Host '==> Instalando a atualização automática' -ForegroundColor Cyan
    if (-not (Test-Path $toolsDir)) { New-Item -ItemType Directory -Path $toolsDir | Out-Null }
    $installed = Join-Path $toolsDir 'Update-Portal.ps1'
    if ($PSCommandPath -ne $installed) { Copy-Item -Path $PSCommandPath -Destination $installed -Force }

    # A tarefa roda como SYSTEM: precisa aplicar migrações e conferir backups do PortalDB.
    $sql = @"
SET NOCOUNT ON;
IF SUSER_ID(N'$systemAccount') IS NULL CREATE LOGIN [$systemAccount] FROM WINDOWS;
IF IS_SRVROLEMEMBER('sysadmin', N'$systemAccount') = 0 ALTER SERVER ROLE dbcreator ADD MEMBER [$systemAccount];
USE [PortalDB];
IF IS_SRVROLEMEMBER('sysadmin', N'$systemAccount') = 0
BEGIN
    IF USER_ID(N'$systemAccount') IS NULL CREATE USER [$systemAccount] FOR LOGIN [$systemAccount];
    ALTER ROLE db_owner ADD MEMBER [$systemAccount];
END
"@
    $out = & sqlcmd -S $SqlInstance -E -b -C -Q $sql 2>&1
    if ($LASTEXITCODE -ne 0) { throw "Não consegui dar permissão no PortalDB para $systemAccount (use uma conta sysadmin no SQL Server): $($out -join ' ')" }
    Write-Host "    ok: $systemAccount pode atualizar o PortalDB" -ForegroundColor Green

    $taskArgs = "-NoProfile -ExecutionPolicy Bypass -File `"$installed`" -Root `"$Root`" -SqlInstance `"$SqlInstance`" -PackagesDir `"$PackagesDir`" -Repo `"$Repo`""
    if ($OnlyStable) { $taskArgs += ' -OnlyStable' }
    $action = New-ScheduledTaskAction -Execute 'powershell.exe' -Argument $taskArgs
    $trigger = New-ScheduledTaskTrigger -Once -At (Get-Date).AddMinutes(1) -RepetitionInterval (New-TimeSpan -Minutes $EveryMinutes)
    $taskPrincipal = New-ScheduledTaskPrincipal -UserId $systemAccount -LogonType ServiceAccount -RunLevel Highest
    $settings = New-ScheduledTaskSettingsSet -MultipleInstances IgnoreNew -ExecutionTimeLimit (New-TimeSpan -Hours 1) -StartWhenAvailable
    Register-ScheduledTask -TaskName $TaskName -Action $action -Trigger $trigger -Principal $taskPrincipal -Settings $settings -Force | Out-Null
    Write-Host "    ok: tarefa '$TaskName' a cada $EveryMinutes minutos" -ForegroundColor Green
    Write-Host ''
    Write-Host '==> Primeira verificação' -ForegroundColor Cyan
}

# Uma atualização por vez (a tarefa e alguém rodando à mão).
$mutex = New-Object System.Threading.Mutex($false, 'Global\NexusPortalUpdate')
if (-not $mutex.WaitOne(0)) { Write-Log 'outra atualização já está rodando'; return }
try {
    $headers = @{ 'User-Agent' = 'NexusPortal-Update'; 'Accept' = 'application/vnd.github+json' }
    try { $releases = Invoke-RestMethod -Uri "https://api.github.com/repos/$Repo/releases?per_page=20" -Headers $headers -TimeoutSec 60 }
    catch { Write-Log "não consegui consultar o GitHub: $($_.Exception.Message)" 'Yellow'; exit 1 }

    $candidates = foreach ($r in @($releases)) {
        if ($r.draft) { continue }
        if ($OnlyStable -and $r.tag_name -notlike 'v*') { continue }
        $zip = @($r.assets) | Where-Object { $_.name -match '^nexusportal-([A-Za-z0-9._-]{1,64})\.zip$' } | Select-Object -First 1
        if (-not $zip) { continue }
        $version = [regex]::Match($zip.name, '^nexusportal-(.+)\.zip$').Groups[1].Value
        $hash = @($r.assets) | Where-Object { $_.name -eq "$($zip.name).sha256" } | Select-Object -First 1
        if (-not $hash) { continue }
        [pscustomobject]@{ Tag = $r.tag_name; Version = $version; Published = [datetime]$r.published_at; Zip = $zip; Hash = $hash }
    }
    $latest = @($candidates) | Sort-Object Published -Descending | Select-Object -First 1
    if (-not $latest) { Write-Log 'nenhum pacote publicado no GitHub ainda'; return }

    $currentVersion = ''
    $versionFile = Join-Path $Root 'current\VERSION'
    if (Test-Path $versionFile) { $currentVersion = (Get-Content $versionFile -Raw).Trim() }
    if ($latest.Version -eq $currentVersion -and -not $Force) { Write-Log "já está na versão mais nova ($currentVersion)"; return }

    $lastTried = ''
    if (Test-Path $stateFile) { $lastTried = (Get-Content $stateFile -Raw).Trim() }
    if ($lastTried -eq $latest.Version -and -not $Force) {
        Write-Log "a versão $($latest.Version) já foi instalada antes (falhou ou foi desfeita com o Rollback). Rode com -Force para instalar de novo."
        return
    }

    Write-Log "versão nova: $($latest.Version) (no ar: $(if ($currentVersion) { $currentVersion } else { 'nenhuma' }))" 'Cyan'
    if (-not (Test-Path $PackagesDir)) { New-Item -ItemType Directory -Path $PackagesDir | Out-Null }
    $zipPath = Join-Path $PackagesDir $latest.Zip.name
    $hashPath = "$zipPath.sha256"
    Invoke-WebRequest -Uri $latest.Hash.browser_download_url -OutFile $hashPath -UseBasicParsing -Headers @{ 'User-Agent' = 'NexusPortal-Update' } -TimeoutSec 120
    Invoke-WebRequest -Uri $latest.Zip.browser_download_url -OutFile $zipPath -UseBasicParsing -Headers @{ 'User-Agent' = 'NexusPortal-Update' } -TimeoutSec 1800
    Unblock-File $zipPath
    Write-Log "baixado $($latest.Zip.name) ($([math]::Round((Get-Item $zipPath).Length / 1MB, 1)) MB)"

    Set-Content -Path $stateFile -Value $latest.Version -Encoding ascii
    $deploy = Join-Path $Root 'current\deploy\Deploy-Portal.ps1'
    $transcript = Join-Path $logDir ('update-{0:yyyyMMdd_HHmmss}.log' -f (Get-Date))
    Start-Transcript -Path $transcript | Out-Null
    try {
        & $deploy -Package $zipPath -Root $Root -SqlInstance $SqlInstance
        $ok = $true
    }
    catch {
        $ok = $false
        Write-Host $_.Exception.Message -ForegroundColor Red
    }
    finally { Stop-Transcript | Out-Null }

    if (-not $ok) { Write-Log "a publicação de $($latest.Version) falhou; detalhes em $transcript" 'Red'; exit 1 }
    Write-Log "versão $($latest.Version) no ar" 'Green'

    # Mantém este script igual ao da versão publicada e guarda só os 3 pacotes mais novos.
    $fresh = Join-Path $Root 'current\deploy\Update-Portal.ps1'
    $installed = Join-Path $toolsDir 'Update-Portal.ps1'
    if ((Test-Path $fresh) -and (Test-Path $installed)) { Copy-Item -Path $fresh -Destination $installed -Force }
    Get-ChildItem $PackagesDir -Filter 'nexusportal-*.zip' | Sort-Object LastWriteTime -Descending | Select-Object -Skip 3 |
        ForEach-Object { Remove-Item $_.FullName, "$($_.FullName).sha256" -Force -ErrorAction SilentlyContinue }
    Get-ChildItem $logDir -Filter 'update-*.log' | Where-Object LastWriteTime -lt (Get-Date).AddDays(-30) | Remove-Item -Force -ErrorAction SilentlyContinue
}
finally {
    $mutex.ReleaseMutex()
    $mutex.Dispose()
}
