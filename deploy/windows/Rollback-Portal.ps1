<#
.SYNOPSIS
  Volta o portal para a versão anterior (ou para uma pasta de releases escolhida).

.DESCRIPTION
  Troca só os arquivos. O PortalDB fica como está: as migrações só acrescentam tabelas e colunas,
  então a versão anterior continua funcionando. Para voltar também o banco, use o Restore-PortalDb.ps1
  com o backup "antes-<versão>" feito na publicação.

.EXAMPLE
  .\Rollback-Portal.ps1
  .\Rollback-Portal.ps1 -Release 2026.11.01-abc1234_20261101120000
#>
[CmdletBinding()]
param(
    [string]$Root = 'C:\NexusPortal',
    [string]$Release
)

. (Join-Path $PSScriptRoot 'Common.ps1')
Assert-Admin
$paths = Get-PortalPaths $Root

if ($Release) {
    $target = Join-Path $paths.Releases $Release
}
elseif (Test-Path $paths.Previous) {
    $target = (Get-Content $paths.Previous -Raw).Trim()
}
else {
    throw 'Não há versão anterior registrada. Informe -Release com o nome da pasta em releases.'
}
if (-not (Test-Path (Join-Path $target 'api\Priston.Api.exe'))) { throw "Versão não encontrada: $target" }
$current = Get-CurrentRelease $paths
if ($current -eq $target) { throw "$target já é a versão no ar." }

Write-Step "Voltando para $([IO.Path]::GetFileName($target))"
Stop-Portal $paths
Set-CurrentRelease $paths $target
Start-Portal $paths
if (-not (Test-PortalHealth $paths)) { throw "A versão $target não respondeu. Logs em $($paths.Logs)." }
Write-Step 'Versão anterior no ar'
