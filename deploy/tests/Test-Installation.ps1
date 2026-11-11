# Confere a instalação feita pelo Install-Portal.ps1 (laboratório do GitHub Actions).
param([Parameter(Mandatory)] [string]$HostName, [string]$Root = 'C:\NexusPortal')
. (Join-Path $PSScriptRoot 'TestHelpers.ps1')
$paths = Get-PortalPaths $Root

foreach ($name in $script:ApiService, $script:WorkerService) {
    $svc = Get-Service $name
    Assert-That "serviço $name rodando" ($svc.Status -eq 'Running')
    $account = (Get-CimInstance Win32_Service -Filter "Name='$name'").StartName
    Assert-That "serviço $name com conta virtual" ($account -eq "NT SERVICE\$name") $account
}

foreach ($path in '/', '/status', '/ranking', '/noticias', '/eventos', '/login', '/cadastro') {
    $r = Get-SiteResponse $HostName $path
    Assert-That "https $path responde 200" ($r.Status -eq 200) "($($r.Status))"
}
$homePage = Get-SiteResponse $HostName '/'
Assert-That 'página inicial com o nome do servidor' ($homePage.Body -match 'NexusPK')
Assert-That 'sem cabeçalho Server' ($homePage.Headers -notmatch '(?im)^Server:')
Assert-That 'sem X-Powered-By' ($homePage.Headers -notmatch '(?im)^X-Powered-By:')
Assert-That 'CSP presente' ($homePage.Headers -match "(?im)^Content-Security-Policy:.*frame-ancestors 'none'")
Assert-That 'HSTS presente' ($homePage.Headers -match '(?im)^Strict-Transport-Security:')
$plain = Get-SiteResponse $HostName '/' -Http
Assert-That 'http também responde (sem URL Rewrite no laboratório)' ($plain.Status -in 200, 301) "($($plain.Status))"
$asset = [regex]::Match($homePage.Body, '/_next/static/[^"'' ]+\.js').Value
$static = Get-SiteResponse $HostName $asset
Assert-That 'arquivo estático com cache longo' ($static.Status -eq 200 -and $static.Headers -match '(?im)^Cache-Control:.*immutable') "($asset $($static.Status))"

$ready = Invoke-Api GET '/health/ready'
Assert-That 'API e bancos prontos' ($ready.Status -eq 200) "($($ready.Status) $($ready.Body))"
$listen = @(Get-NetTCPConnection -State Listen -LocalPort (Get-ApiPort) -ErrorAction SilentlyContinue)
Assert-That 'API escuta só em 127.0.0.1' ($listen.Count -gt 0 -and -not ($listen | Where-Object { $_.LocalAddress -ne '127.0.0.1' })) (($listen | ForEach-Object LocalAddress) -join ',')
$nodePids = @(Get-Process node -ErrorAction SilentlyContinue | ForEach-Object Id)
$nodeListen = @(Get-NetTCPConnection -State Listen -ErrorAction SilentlyContinue | Where-Object { $nodePids -contains $_.OwningProcess })
Assert-That 'Node escuta só em 127.0.0.1' ($nodeListen.Count -gt 0 -and -not ($nodeListen | Where-Object { $_.LocalAddress -ne '127.0.0.1' })) (($nodeListen | ForEach-Object { "$($_.LocalAddress):$($_.LocalPort)" }) -join ',')

$reg = Invoke-Api POST '/api/v1/auth/register' '{"login":"smoke01","password":"senha123","email":"smoke01@example.com"}'
Assert-That 'cadastro grava no banco do jogo com o login de escrita' ($reg.Status -eq 201) "($($reg.Status) $($reg.Body))"
$login = Invoke-Api POST '/api/v1/auth/login' '{"login":"smoke01","password":"senha123"}'
Assert-That 'login lê com o login de leitura' ($login.Status -eq 200) "($($login.Status))"

$users = New-Object System.Security.Principal.SecurityIdentifier('S-1-5-32-545')
$configIds = @((Get-Acl $paths.Config).Access | ForEach-Object { try { $_.IdentityReference.Translate([System.Security.Principal.SecurityIdentifier]).Value } catch { $_.IdentityReference.Value } })
Assert-That 'pasta config sem acesso de Usuários' (-not ($configIds -contains $users.Value))
$poolSid = (New-Object System.Security.Principal.NTAccount((Get-AppPoolAccount))).Translate([System.Security.Principal.SecurityIdentifier]).Value
Assert-That 'pasta config sem acesso do site do IIS' (-not ($configIds -contains $poolSid))

Start-Sleep -Seconds 5
$apiLogs = @(Get-ChildItem $paths.Logs -Filter 'api-*.log')
$workerLogs = @(Get-ChildItem $paths.Logs -Filter 'worker-*.log')
Assert-That 'log da API em C:\NexusPortal\logs' ($apiLogs.Count -gt 0)
Assert-That 'log das tarefas em C:\NexusPortal\logs' ($workerLogs.Count -gt 0)
$secrets = [regex]::Matches((Get-Content $paths.ConfigFile -Raw), 'Password=([^;"]+)') | ForEach-Object { $_.Groups[1].Value }
Assert-That 'três senhas SQL geradas' (@($secrets).Count -eq 3)
$leaks = @(Get-ChildItem $paths.Logs -Recurse -File | Where-Object { $content = Get-Content $_.FullName -Raw; $content -and (($secrets + 'senha123') | Where-Object { $content.Contains($_) }) })
Assert-That 'nenhuma senha nos logs' ($leaks.Count -eq 0) (($leaks | ForEach-Object Name) -join ',')

Import-Module WebAdministration
$flags = (Get-ItemProperty "IIS:\Sites\$script:SiteName" -Name logFile.logExtFileFlags)
Assert-That 'log do IIS sem query string' ("$flags" -notmatch 'UriQuery') "$flags"

$task = Get-ScheduledTask -TaskName 'NexusPortal - backup do PortalDB' -ErrorAction SilentlyContinue
Assert-That 'tarefa de backup agendada' ($null -ne $task)

Complete-Test
