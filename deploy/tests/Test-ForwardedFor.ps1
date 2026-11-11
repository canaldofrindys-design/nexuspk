# Confere o que o módulo do IIS repassa ao Node em X-Forwarded-For. O portal usa a ÚLTIMA entrada
# como IP do jogador: ela precisa ser a que o IIS acrescentou, nunca um valor enviado pelo navegador.
. (Join-Path $PSScriptRoot 'TestHelpers.ps1')
Import-Module WebAdministration
$dir = 'C:\xfftest'
New-Item -ItemType Directory -Force -Path $dir | Out-Null
Set-Content -Path "$dir\echo.js" -Encoding ascii -Value @'
require("http").createServer((q, s) => {
  s.setHeader("content-type", "application/json");
  s.end(JSON.stringify({ xff: q.headers["x-forwarded-for"] || "", proto: q.headers["x-forwarded-proto"] || "", host: q.headers.host || "" }));
}).listen(Number(process.env.ASPNETCORE_PORT), "127.0.0.1");
'@
$config = (Get-Content -Raw (Join-Path $PSScriptRoot '..\windows\web\web.config.template')).Replace('__NODE__', (Get-NodePath)).Replace('__LOGS__', $dir).Replace('__API__', 'http://127.0.0.1:1').Replace('__REWRITE__', '').Replace('arguments="start.js"', 'arguments="echo.js"')
Set-Content -Path "$dir\web.config" -Value $config -Encoding UTF8
New-Item -ItemType Directory -Force -Path "$dir\web" | Out-Null
if (-not (Test-Path IIS:\AppPools\xfftest)) { New-WebAppPool -Name xfftest | Out-Null }
Set-ItemProperty IIS:\AppPools\xfftest -Name managedRuntimeVersion -Value ''
New-Website -Name xfftest -PhysicalPath $dir -ApplicationPool xfftest -Port 8088 -IPAddress '127.0.0.1' | Out-Null
try {
    Start-Sleep -Seconds 2
    $raw = & curl.exe -s --max-time 60 -H 'X-Forwarded-For: 6.6.6.6' 'http://127.0.0.1:8088/'
    Write-Host "resposta: $raw"
    $r = $raw | ConvertFrom-Json
    # O módulo escreve "ip:porta"; o portal tira a porta (frontend/features/auth/forwardedIp.ts).
    $last = ($r.xff -split ',')[-1].Trim()
    Assert-That 'formato ip:porta, como o portal espera' ($last -match '^(\d{1,3}(\.\d{1,3}){3}|\[[0-9a-fA-F:.]+\]):\d+$') "($last)"
    $last = $last -replace '^\[(.+)\]:\d+$', '$1' -replace '^(\d{1,3}(\.\d{1,3}){3}):\d+$', '$1'
    Assert-That 'IIS acrescenta o IP real em X-Forwarded-For' ($r.xff -ne '' -and $last -ne '6.6.6.6') "(xff='$($r.xff)')"
    Assert-That 'última entrada é o IP de quem conectou' ($last -in '127.0.0.1', '::1') "($last)"
    Assert-That 'Host original preservado' ($r.host -eq '127.0.0.1:8088') "($($r.host))"
    $raw2 = & curl.exe -s --max-time 60 'http://127.0.0.1:8088/'
    $r2 = $raw2 | ConvertFrom-Json
    Assert-That 'sem cabeçalho do cliente, X-Forwarded-For só com o IP real' ($r2.xff.Trim() -match '^(127\.0\.0\.1|\[::1\]):\d+$') "($($r2.xff))"
}
finally {
    Remove-Website -Name xfftest
    Remove-WebAppPool -Name xfftest
}
Complete-Test
