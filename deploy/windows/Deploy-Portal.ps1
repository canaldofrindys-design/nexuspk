<#
.SYNOPSIS
  Publica uma versão nova do portal (ou a primeira, chamado pelo Install-Portal.ps1).

.DESCRIPTION
  1. Confere e extrai o pacote numa pasta nova (releases\<versão>_<data>).
  2. Faz backup do PortalDB.
  3. Para o portal (página "atualizando" no IIS), aplica as migrações novas do PortalDB.
  4. Troca a junção "current" para a versão nova e sobe tudo de novo.
  5. Confere a API, os bancos e a página inicial. Se falhar, volta sozinho para a versão anterior
     (só os arquivos; o banco fica como está, porque as migrações só acrescentam).
  O banco do jogo não é alterado em nenhum passo.

.EXAMPLE
  .\Deploy-Portal.ps1 -Package C:\NexusPortal\pacotes\nexusportal-2026.11.11-abc1234.zip
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)] [string]$Package,
    [string]$Root = 'C:\NexusPortal',
    [string]$SqlInstance = '.\SQLEXPRESS',
    [pscredential]$SqlCredential,
    [switch]$SkipBackup,
    [switch]$CreateDatabase,
    [int]$KeepReleases = 3
)

. (Join-Path $PSScriptRoot 'Common.ps1')
Assert-Admin
Import-Module WebAdministration
$paths = Get-PortalPaths $Root

if (-not (Test-Path $paths.ConfigFile)) { throw "Falta $($paths.ConfigFile). Rode o Install-Portal.ps1 primeiro." }
foreach ($name in $script:ApiService, $script:WorkerService) {
    if (-not (Get-Service -Name $name -ErrorAction SilentlyContinue)) { throw "Serviço $name não existe. Rode o Install-Portal.ps1 primeiro." }
}
if (-not (Get-Website -Name $script:SiteName)) { throw "Site $script:SiteName não existe no IIS. Rode o Install-Portal.ps1 primeiro." }

Write-Step 'Extraindo o pacote'
$release = Expand-PortalRelease $paths $Package
Write-Ok "versão $($release.Version) em $($release.Folder)"
Write-WebConfig $paths $release.Folder
# O Next grava o cache das páginas dentro de .next.
Add-FolderRight (Join-Path $release.Folder 'web\.next') (Get-AppPoolAccount) 'Modify'

$databaseExists = $false
try { $databaseExists = ((Invoke-Sql -SqlInstance $SqlInstance -Credential $SqlCredential -Query "SELECT COUNT(*) FROM sys.databases WHERE name = N'$script:PortalDatabase'") | Select-Object -First 1).Trim() -eq '1' }
catch { throw "Não consegui conectar no SQL Server $SqlInstance. $($_.Exception.Message)" }

if ($databaseExists -and -not $SkipBackup) {
    Write-Step 'Backup do PortalDB antes da atualização'
    & (Join-Path $release.Folder 'deploy\Backup-PortalDb.ps1') -Root $Root -SqlInstance $SqlInstance -SqlCredential $SqlCredential -Tag ('antes-' + $release.Version) -Verify
    if ($LASTEXITCODE -ne 0) { throw "O backup falhou; nada foi alterado. Veja $($paths.Logs)\backup.log." }
}

$previous = Get-CurrentRelease $paths

Write-Step 'Parando o portal'
Stop-Portal $paths
Write-Ok 'serviços parados; o IIS mostra a página de atualização'

Write-Step 'Migrações do PortalDB'
try {
    Invoke-PortalMigrations $release.Folder $SqlInstance $SqlCredential -CreateDatabase:($CreateDatabase -or -not $databaseExists)
    Write-Ok 'banco do portal atualizado'
}
catch {
    Write-Warn 'a migração falhou; religando a versão que estava no ar'
    if ($previous) { Start-Portal $paths }
    throw
}

Write-Step 'Trocando para a versão nova'
Set-CurrentRelease $paths $release.Folder
Start-Portal $paths

Write-Step 'Conferindo'
if (-not (Test-PortalHealth $paths)) {
    if ($previous) {
        Write-Warn "voltando para $previous"
        Stop-Portal $paths
        Set-CurrentRelease $paths $previous
        Start-Portal $paths
        if (Test-PortalHealth $paths) { Write-Warn 'a versão anterior voltou ao ar' }
    }
    throw "A versão $($release.Version) não passou na conferência. Logs em $($paths.Logs)."
}

Remove-OldReleases $paths $KeepReleases
Write-Step "Versão $($release.Version) publicada"
