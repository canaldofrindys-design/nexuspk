# Atualização, volta de versão, backup agendado e restauração (laboratório do GitHub Actions).
param([Parameter(Mandatory)] [string]$HostName, [string]$Root = 'C:\NexusPortal', [string]$SqlInstance = '.\SQLEXPRESS')
. (Join-Path $PSScriptRoot 'TestHelpers.ps1')
$paths = Get-PortalPaths $Root
$zip = (Get-ChildItem C:\pacotes -Filter 'nexusportal-*.zip').FullName

$first = Get-CurrentRelease $paths
Write-Step 'Publicando de novo o mesmo pacote (atualização)'
& (Join-Path $paths.Current 'deploy\Deploy-Portal.ps1') -Package $zip -Root $Root -SqlInstance $SqlInstance
$second = Get-CurrentRelease $paths
Assert-That 'atualização troca a pasta da versão' ($second -ne $first) "($first -> $second)"
Assert-That 'site no ar depois da atualização' ((Get-SiteResponse $HostName '/').Status -eq 200)
Assert-That 'backup feito antes da atualização' (@(Get-ChildItem $paths.Backups -Filter 'PortalDB_*_antes-*.bak').Count -ge 1)

Write-Step 'Voltando para a versão anterior'
& (Join-Path $paths.Current 'deploy\Rollback-Portal.ps1') -Root $Root
Assert-That 'volta de versão aponta para a primeira pasta' ((Get-CurrentRelease $paths) -eq $first)
Assert-That 'site no ar depois da volta' ((Get-SiteResponse $HostName '/').Status -eq 200)
Assert-That 'login continua funcionando' ((Invoke-Api POST '/api/v1/auth/login' '{"login":"smoke01","password":"senha123"}').Status -eq 200)

Write-Step 'Backup pela tarefa agendada (conta SYSTEM)'
$before = Get-Date
Start-ScheduledTask -TaskName 'NexusPortal - backup do PortalDB'
$deadline = (Get-Date).AddMinutes(3)
do { Start-Sleep -Seconds 3; $state = (Get-ScheduledTask -TaskName 'NexusPortal - backup do PortalDB').State } while ($state -eq 'Running' -and (Get-Date) -lt $deadline)
$info = Get-ScheduledTaskInfo -TaskName 'NexusPortal - backup do PortalDB'
Assert-That 'tarefa de backup terminou sem erro' ($info.LastTaskResult -eq 0) "(resultado $($info.LastTaskResult))"
$bak = Get-ChildItem $paths.Backups -Filter 'PortalDB_*.bak' | Where-Object { $_.LastWriteTime -ge $before.AddSeconds(-2) -and $_.Name -notlike '*_antes-*' } | Sort-Object LastWriteTime -Descending | Select-Object -First 1
Assert-That 'arquivo de backup novo' ($null -ne $bak)
Write-Host (Get-Content (Join-Path $paths.Logs 'backup.log') -Tail 5 | Out-String)

if ($bak) {
    Write-Step 'Restauração'
    Invoke-Sql -SqlInstance $SqlInstance -Database PortalDB -Query "INSERT INTO dbo.AuditLog (ActorType, ActorId, Action, TargetType, TargetId) VALUES ('system', 'teste', 'teste.marcador', 'teste', 'depois-do-backup')" | Out-Null
    & (Join-Path $paths.Current 'deploy\Restore-PortalDb.ps1') -BackupFile $bak.FullName -Root $Root -SqlInstance $SqlInstance -Confirm:$false
    $count = (Invoke-Sql -SqlInstance $SqlInstance -Database PortalDB -Query "SELECT COUNT(*) FROM dbo.AuditLog WHERE Action = 'teste.marcador'" | Select-Object -First 1).Trim()
    Assert-That 'restauração volta o banco ao ponto do backup' ($count -eq '0') "($count)"
    Assert-That 'site no ar depois da restauração' ((Get-SiteResponse $HostName '/').Status -eq 200)
    Assert-That 'login depois da restauração' ((Invoke-Api POST '/api/v1/auth/login' '{"login":"smoke01","password":"senha123"}').Status -eq 200)
}

Complete-Test
