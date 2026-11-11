<#
.SYNOPSIS
  Gera o pacote de publicação para Windows: nexusportal-<versão>.zip e o .sha256.

.DESCRIPTION
  Roda no GitHub Actions (windows-latest) ou numa máquina Windows de desenvolvimento, com PowerShell 7,
  .NET 10 SDK e Node.js 22. O Next precisa ser compilado no Windows: o pacote leva bibliotecas nativas.

  Conteúdo do zip:
    VERSION                   versão do pacote
    api\  worker\  migrator\  .NET publicados para win-x64 (usam o runtime do Hosting Bundle)
    web\                      Next (saída standalone) + start.js
    deploy\                   scripts de instalação, publicação, backup e volta de versão
    deploy\sql\               permissoes-portal.sql
  Nenhum segredo vai no pacote: connection strings e senhas ficam em C:\NexusPortal\config no servidor.
#>
[CmdletBinding()]
param(
    [string]$Version,
    [string]$Output = (Join-Path $PSScriptRoot '..\artifacts'),
    [switch]$SkipNpmInstall
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$repo = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path

function Invoke-Native([string]$Exe, [string[]]$Arguments, [string]$WorkingDirectory = $repo) {
    Push-Location $WorkingDirectory
    try {
        & $Exe @Arguments
        if ($LASTEXITCODE -ne 0) { throw "$Exe $($Arguments -join ' ') falhou ($LASTEXITCODE)." }
    }
    finally { Pop-Location }
}

if (-not $Version) {
    $sha = (& git -C $repo rev-parse --short=7 HEAD).Trim()
    $Version = '{0:yyyy.MM.dd}-{1}' -f (Get-Date).ToUniversalTime(), $sha
}
if ($Version -notmatch '^[A-Za-z0-9._-]{1,64}$') { throw "Versão inválida: $Version" }

$Output = [IO.Path]::GetFullPath($Output)
$stage = Join-Path $Output "nexusportal-$Version"
if (Test-Path $stage) { Remove-Item -Recurse -Force $stage }
New-Item -ItemType Directory -Force -Path $stage | Out-Null

Write-Host "==> .NET (win-x64)"
$projects = @{ api = 'Priston.Api'; worker = 'Priston.Worker'; migrator = 'Priston.DbMigrator' }
foreach ($key in $projects.Keys) {
    $proj = Join-Path $repo "backend/src/$($projects[$key])/$($projects[$key]).csproj"
    Invoke-Native dotnet @('publish', $proj, '-c', 'Release', '-r', 'win-x64', '--self-contained', 'false',
        '-o', (Join-Path $stage $key), "-p:Version=$($Version -replace '^(\d+)\.(\d+)\.(\d+).*$', '$1.$2.$3')", "-p:InformationalVersion=$Version", '--nologo', '-v', 'quiet')
    Get-ChildItem (Join-Path $stage $key) -Filter 'appsettings.Development.json' | Remove-Item
}

Write-Host "==> Next.js"
$frontend = Join-Path $repo 'frontend'
if (-not $SkipNpmInstall) { Invoke-Native npm @('ci', '--no-audit', '--no-fund') $frontend }
$env:NEXT_TELEMETRY_DISABLED = '1'
Invoke-Native npm @('run', 'build') $frontend
$web = Join-Path $stage 'web'
Copy-Item -Recurse -Path (Join-Path $frontend '.next/standalone') -Destination $web
Copy-Item -Recurse -Path (Join-Path $frontend '.next/static') -Destination (Join-Path $web '.next/static')
if (Test-Path (Join-Path $frontend 'public')) { Copy-Item -Recurse -Path (Join-Path $frontend 'public') -Destination (Join-Path $web 'public') }
Copy-Item -Path (Join-Path $PSScriptRoot 'windows/web/start.js') -Destination $web
# Arquivos de ambiente locais nunca vão para o servidor.
Get-ChildItem -Path $web -Filter '.env*' -File -Force -ErrorAction SilentlyContinue | Remove-Item -Force

Write-Host "==> Scripts"
$deploy = Join-Path $stage 'deploy'
New-Item -ItemType Directory -Force -Path (Join-Path $deploy 'sql'), (Join-Path $deploy 'web') | Out-Null
Copy-Item -Path (Join-Path $PSScriptRoot 'windows/*.ps1') -Destination $deploy
Copy-Item -Path (Join-Path $PSScriptRoot 'windows/web/web.config.template') -Destination (Join-Path $deploy 'web')
Copy-Item -Path (Join-Path $repo 'database/scripts/permissoes-portal.sql') -Destination (Join-Path $deploy 'sql')
Set-Content -Path (Join-Path $stage 'VERSION') -Value $Version -NoNewline -Encoding ascii

$zip = Join-Path $Output "nexusportal-$Version.zip"
if (Test-Path $zip) { Remove-Item $zip }
# ZipFile inclui pastas que começam com ponto (.next), que o Compress-Archive pode pular.
Add-Type -AssemblyName System.IO.Compression.FileSystem
[IO.Compression.ZipFile]::CreateFromDirectory($stage, $zip, [IO.Compression.CompressionLevel]::Optimal, $false)
$hash = (Get-FileHash -Algorithm SHA256 -Path $zip).Hash.ToLowerInvariant()
Set-Content -Path "$zip.sha256" -Value "$hash  nexusportal-$Version.zip" -Encoding ascii
Remove-Item -Recurse -Force $stage

$mb = [math]::Round((Get-Item $zip).Length / 1MB, 1)
Write-Host "==> $zip ($mb MB)"
Write-Host "    sha256 $hash"
if ($env:GITHUB_OUTPUT) {
    Add-Content -Path $env:GITHUB_OUTPUT -Value "zip=$zip"
    Add-Content -Path $env:GITHUB_OUTPUT -Value "version=$Version"
}
