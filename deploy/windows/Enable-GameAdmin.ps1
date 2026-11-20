<#
.SYNOPSIS
  Liga as funções do jogo no painel da equipe: contas, banimentos, coins, VIP, rankings, editores
  (Box Donate, lojas, drops, itens) e, opcionalmente, o "Ligar / reiniciar" do servidor.

.DESCRIPTION
  1. Cria (ou troca a senha do) login SQL portal_game_admin, com leitura e escrita de dados nos bancos
     do jogo que existirem (sem poder criar ou apagar tabelas). A senha é gerada aqui na VPS e não aparece na tela.
  2. Grava GameDatabase:AdminConnectionString no C:\NexusPortal\config\portal.json.
  3. Com -GameServerExe: cadastra os programas do servidor do jogo (na ordem de ligar) para a página
     "Ligar / reiniciar" e dá à conta do serviço da API permissão na pasta deles.
  4. Reinicia a API e o Worker para valer na hora.

  Pode rodar de novo quantas vezes quiser.

.EXAMPLE
  C:\NexusPortal\current\deploy\Enable-GameAdmin.ps1
  Só as funções do jogo (sem ligar/reiniciar).

.EXAMPLE
  C:\NexusPortal\current\deploy\Enable-GameAdmin.ps1 -GameServerExe 'C:\ServidorPT\LoginServer.exe','C:\ServidorPT\GameServer.exe'
  Também cadastra os programas do servidor, na ordem em que devem ligar (login primeiro).
#>
[CmdletBinding()]
param(
    [string]$SqlInstance = '.\SQLEXPRESS',
    [pscredential]$SqlCredential,
    [string[]]$GameServerExe = @(),
    [string]$Root = 'C:\NexusPortal'
)

. (Join-Path $PSScriptRoot 'Common.ps1')
Assert-Admin
$paths = Get-PortalPaths $Root
if (-not (Test-Path $paths.ConfigFile)) { throw "$($paths.ConfigFile) não encontrado. Instale o portal antes." }

$sqlFile = Join-Path $PSScriptRoot 'sql\permissoes-admin-jogo.sql'
if (-not (Test-Path $sqlFile)) { $sqlFile = Join-Path $PSScriptRoot '..\..\database\scripts\permissoes-admin-jogo.sql' }
if (-not (Test-Path $sqlFile)) { throw 'permissoes-admin-jogo.sql não encontrado ao lado do script.' }

foreach ($exe in $GameServerExe) {
    if (-not (Test-Path $exe -PathType Leaf)) { throw "Arquivo não encontrado: $exe" }
}

# ---------------------------------------------------------------------------------------------
Write-Step 'Login SQL do painel (portal_game_admin)'
$pw = New-RandomPassword
$sql = (Get-Content -Raw -Encoding UTF8 $sqlFile).Replace('<SENHA-ADMIN>', $pw)
$sql += "`r`nGO`r`nALTER LOGIN portal_game_admin WITH PASSWORD = '$pw';`r`nGO`r`n"
$tmp = Join-Path $paths.Config ('_admin_' + [guid]::NewGuid().ToString('N') + '.sql')
try {
    [IO.File]::WriteAllText($tmp, $sql, (New-Object System.Text.UTF8Encoding($false)))
    $out = Invoke-Sql -SqlInstance $SqlInstance -Credential $SqlCredential -InputFile $tmp
    foreach ($line in @($out | Where-Object { "$_" -like 'portal_game_admin liberado*' })) { Write-Ok "$line" }
}
finally {
    Remove-Item $tmp -Force -ErrorAction SilentlyContinue
}

# ---------------------------------------------------------------------------------------------
Write-Step 'Configuração do portal'
$config = Get-Content -Raw -Encoding UTF8 $paths.ConfigFile | ConvertFrom-Json
if (-not $config.PSObject.Properties['GameDatabase']) {
    $config | Add-Member -NotePropertyName GameDatabase -NotePropertyValue ([pscustomobject]@{})
}
$cs = "Server=$SqlInstance;Database=accountdb;User Id=portal_game_admin;Password=$pw;TrustServerCertificate=True;Application Name=NexusPortal"
if ($config.GameDatabase.PSObject.Properties['AdminConnectionString']) { $config.GameDatabase.AdminConnectionString = $cs }
else { $config.GameDatabase | Add-Member -NotePropertyName AdminConnectionString -NotePropertyValue $cs }
Write-Ok 'GameDatabase:AdminConnectionString gravada'

if ($GameServerExe.Count -gt 0) {
    $used = @{}
    $processes = @()
    foreach ($exe in $GameServerExe) {
        $full = (Resolve-Path $exe).Path
        $name = [IO.Path]::GetFileNameWithoutExtension($full)
        $key = ($name.ToLowerInvariant() -replace '[^a-z0-9]+', '-').Trim('-')
        if ($key.Length -eq 0) { $key = 'servidor' }
        if ($key.Length -gt 28) { $key = $key.Substring(0, 28) }
        $base = $key; $n = 2
        while ($used.ContainsKey($key)) { $key = "$base-$n"; $n++ }
        $used[$key] = $true
        $processes += [ordered]@{ Key = $key; Name = $name; ExecutablePath = $full; Arguments = ''; WorkingDirectory = ''; StartDelaySeconds = 10 }

        # A API (conta virtual do serviço) precisa abrir o programa e ele precisa gravar logs e dados na própria pasta.
        Add-FolderRight (Split-Path $full -Parent) (Get-ServiceAccount $script:ApiService) 'Modify'
        Write-Ok "$name ($key): permissão para $(Get-ServiceAccount $script:ApiService) em $(Split-Path $full -Parent)"
    }
    $control = [ordered]@{ Processes = $processes; StopTimeoutSeconds = 30; CooldownSeconds = 30 }
    if ($config.PSObject.Properties['GameServerControl']) { $config.GameServerControl = $control }
    else { $config | Add-Member -NotePropertyName GameServerControl -NotePropertyValue $control }
    Write-Ok "$($processes.Count) programa(s) cadastrados para Ligar / reiniciar"
    Write-Warn 'feche os programas do servidor abertos à mão e ligue pelo painel uma vez: o painel só consegue fechar o que ele mesmo abriu.'
}

[IO.File]::WriteAllText($paths.ConfigFile, ($config | ConvertTo-Json -Depth 8), (New-Object System.Text.UTF8Encoding($false)))
Write-Ok "salvo em $($paths.ConfigFile)"

# ---------------------------------------------------------------------------------------------
Write-Step 'Reiniciando a API e o Worker'
foreach ($name in $script:ApiService, $script:WorkerService) {
    if (Get-Service -Name $name -ErrorAction SilentlyContinue) { Restart-Service -Name $name -Force; Write-Ok $name }
}

Write-Host ''
Write-Host 'Pronto. No painel (/admin), o superadministrador já vê Contas, Coins/VIP, Rankings e Lojas, drops e itens.' -ForegroundColor Green
Write-Host 'As lojas, drops, Box Donate, catálogo e logs precisam ser ligados à tabela certa uma vez, em "Configurar".' -ForegroundColor Green
