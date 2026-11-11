<#
.SYNOPSIS
  Backup completo do PortalDB (banco do portal). O banco do jogo não entra aqui.

.DESCRIPTION
  Grava PortalDB_<data>[_<marca>].bak em <Root>\backups com CHECKSUM, apaga os mais velhos que
  -RetentionDays e, com -CopyTo, copia o arquivo para outro disco ou compartilhamento.
  Roda todo dia pela tarefa "NexusPortal - backup do PortalDB" (conta SYSTEM).
  O SQL Server Express não tem SQL Agent nem compressão de backup.

.EXAMPLE
  .\Backup-PortalDb.ps1 -Verify -CopyTo \\outro-servidor\backups\portal
#>
[CmdletBinding()]
param(
    [string]$Root = 'C:\NexusPortal',
    [string]$SqlInstance = '.\SQLEXPRESS',
    [pscredential]$SqlCredential,
    [int]$RetentionDays = 14,
    [string]$Tag,
    [string]$CopyTo,
    [switch]$Verify
)

. (Join-Path $PSScriptRoot 'Common.ps1')
$paths = Get-PortalPaths $Root
$log = Join-Path $paths.Logs 'backup.log'

function Write-Log([string]$Message) {
    $line = '{0:yyyy-MM-dd HH:mm:ss} {1}' -f (Get-Date), $Message
    Add-Content -Path $log -Value $line -Encoding UTF8
    Write-Host "    $Message"
}

try {
    if ($Tag -and $Tag -notmatch '^[A-Za-z0-9._-]{1,80}$') { throw 'Use só letras, números, ponto, hífen e sublinhado na marca.' }
    $name = 'PortalDB_' + (Get-Date -Format 'yyyyMMdd_HHmmss')
    if ($Tag) { $name += "_$Tag" }
    $file = Join-Path $paths.Backups "$name.bak"
    $escaped = $file.Replace("'", "''")

    Invoke-Sql -SqlInstance $SqlInstance -Credential $SqlCredential -Query "BACKUP DATABASE [$script:PortalDatabase] TO DISK = N'$escaped' WITH INIT, CHECKSUM, NAME = N'$name'" | Out-Null
    $size = [math]::Round((Get-Item $file).Length / 1MB, 1)
    Write-Log "backup gravado: $file ($size MB)"

    if ($Verify) {
        Invoke-Sql -SqlInstance $SqlInstance -Credential $SqlCredential -Query "RESTORE VERIFYONLY FROM DISK = N'$escaped' WITH CHECKSUM" | Out-Null
        Write-Log 'backup conferido (RESTORE VERIFYONLY)'
    }

    if ($CopyTo) {
        Copy-Item -Path $file -Destination $CopyTo
        Write-Log "cópia em $CopyTo"
    }

    $limit = (Get-Date).AddDays(-$RetentionDays)
    Get-ChildItem -Path $paths.Backups -Filter 'PortalDB_*.bak' | Where-Object { $_.LastWriteTime -lt $limit } | ForEach-Object {
        Remove-Item $_.FullName
        Write-Log "apagado por ter mais de $RetentionDays dias: $($_.Name)"
    }
}
catch {
    Write-Log "FALHA: $($_.Exception.Message)"
    exit 1
}
