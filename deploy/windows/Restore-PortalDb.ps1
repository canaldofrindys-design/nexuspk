<#
.SYNOPSIS
  Restaura o PortalDB a partir de um backup. Apaga o que foi gravado no portal depois do backup.

.DESCRIPTION
  Para o portal, faz um backup de segurança do estado atual, restaura o arquivo escolhido
  com REPLACE e liga o portal de novo. Pede confirmação. O banco do jogo não é tocado.

.EXAMPLE
  .\Restore-PortalDb.ps1 -BackupFile C:\NexusPortal\backups\PortalDB_20261111_040000.bak
#>
[CmdletBinding(SupportsShouldProcess, ConfirmImpact = 'High')]
param(
    [Parameter(Mandatory)] [string]$BackupFile,
    [string]$Root = 'C:\NexusPortal',
    [string]$SqlInstance = '.\SQLEXPRESS',
    [pscredential]$SqlCredential
)

. (Join-Path $PSScriptRoot 'Common.ps1')
Assert-Admin
$paths = Get-PortalPaths $Root
if (-not (Test-Path $BackupFile)) { throw "Arquivo não encontrado: $BackupFile" }
$escaped = (Resolve-Path $BackupFile).Path.Replace("'", "''")

Invoke-Sql -SqlInstance $SqlInstance -Credential $SqlCredential -Query "RESTORE VERIFYONLY FROM DISK = N'$escaped' WITH CHECKSUM" | Out-Null
Write-Ok 'arquivo de backup conferido'

if (-not $PSCmdlet.ShouldProcess($script:PortalDatabase, "substituir pelo backup $BackupFile")) { return }

Write-Step 'Backup de segurança do estado atual'
& (Join-Path $PSScriptRoot 'Backup-PortalDb.ps1') -Root $Root -SqlInstance $SqlInstance -SqlCredential $SqlCredential -Tag 'antes-de-restaurar'
if ($LASTEXITCODE -ne 0) { throw 'O backup de segurança falhou; nada foi restaurado.' }

Write-Step 'Parando o portal e restaurando'
Stop-Portal $paths
try {
    Invoke-Sql -SqlInstance $SqlInstance -Credential $SqlCredential -Query "ALTER DATABASE [$script:PortalDatabase] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; RESTORE DATABASE [$script:PortalDatabase] FROM DISK = N'$escaped' WITH REPLACE, CHECKSUM; ALTER DATABASE [$script:PortalDatabase] SET MULTI_USER;" | Out-Null
    Write-Ok 'PortalDB restaurado'
}
finally {
    try { Invoke-Sql -SqlInstance $SqlInstance -Credential $SqlCredential -Query "IF DB_ID(N'$script:PortalDatabase') IS NOT NULL ALTER DATABASE [$script:PortalDatabase] SET MULTI_USER;" | Out-Null } catch { Write-Warn $_.Exception.Message }
    Start-Portal $paths
}
if (-not (Test-PortalHealth $paths)) { throw 'O portal não respondeu depois da restauração. Veja os logs.' }
Write-Step 'Restauração concluída'
