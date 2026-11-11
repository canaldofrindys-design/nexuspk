<#
.SYNOPSIS
  Cria o primeiro superadministrador do painel da equipe.

.DESCRIPTION
  A senha é digitada aqui, no console do servidor, sem aparecer na tela. Ela não passa por
  linha de comando, chat ou log. Depois, os outros membros da equipe são criados pelo painel.

.EXAMPLE
  .\New-PortalAdmin.ps1 -UserName admin.portal
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)] [string]$UserName,
    [string]$Root = 'C:\NexusPortal',
    [string]$SqlInstance = '.\SQLEXPRESS',
    [pscredential]$SqlCredential
)

. (Join-Path $PSScriptRoot 'Common.ps1')
Assert-Admin
$paths = Get-PortalPaths $Root
$exe = Join-Path $paths.Current 'migrator\Priston.DbMigrator.exe'
$env:PRISTON_ConnectionStrings__PortalDbDeploy = Get-DeployConnectionString $SqlInstance $SqlCredential
try {
    & $exe --criar-superadmin $UserName
    if ($LASTEXITCODE -ne 0) { throw 'Não foi possível criar o superadministrador.' }
}
finally {
    Remove-Item Env:\PRISTON_ConnectionStrings__PortalDbDeploy -ErrorAction SilentlyContinue
}
