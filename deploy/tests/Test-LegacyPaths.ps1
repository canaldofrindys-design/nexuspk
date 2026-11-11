# Pastas do site antigo atendidas pelo site do portal (Connect-LegacyPaths.ps1), no Windows do GitHub.
# Monta um "site antigo" de laboratório em C:\legado com uma página ASP no formato das do clã,
# um ícone de clã e o version.txt do launcher. Com -CheckOnly, só confere (depois de atualizar e voltar versão).
param(
    [Parameter(Mandatory)] [string]$HostName,
    [switch]$CheckOnly
)
. (Join-Path $PSScriptRoot 'TestHelpers.ps1')
Import-Module WebAdministration
$legacy = 'C:\legado'

# Início do corpo, sem as tags, para o erro do IIS aparecer no log.
function Get-Short($Text) {
    $plain = ("$Text" -replace '<[^>]+>', ' ' -replace '\s+', ' ').Trim()
    if ($plain.Length -gt 600) { $plain = $plain.Substring(0, 600) }
    return $plain
}

if (-not $CheckOnly) {
    New-Item -ItemType Directory -Force -Path "$legacy\ServerMain", "$legacy\ClanContent", "$legacy\updater" | Out-Null
    # Mesmo formato de resposta das páginas do clã: Code=<n> seguido de CR.
    Set-Content -Path "$legacy\ServerMain\isCheckClanName.asp" -Encoding ASCII -Value @'
<%
Dim clname
clname = Trim(Request("ClName"))
If clname = "" Then
  Response.Write("Code=100" & Chr(13))
Else
  Response.Write("Code=0" & Chr(13))
End If
%>
'@
    [IO.File]::WriteAllBytes("$legacy\ClanContent\1000000001.bmp", [byte[]](0x42, 0x4D, 0, 0, 0, 0))
    Set-Content -Path "$legacy\updater\version.txt" -Encoding ASCII -NoNewline -Value '1012'
    Set-Content -Path "$legacy\index.html" -Encoding ASCII -Value 'site antigo'
    & icacls.exe $legacy /grant '*S-1-5-32-568:(OI)(CI)RX' | Out-Null   # IIS_IUSRS

    if (-not (Test-Path 'IIS:\AppPools\Legado')) { New-WebAppPool -Name 'Legado' | Out-Null }
    if (-not (Get-Website -Name 'Legado')) { New-Website -Name 'Legado' -PhysicalPath $legacy -ApplicationPool 'Legado' -Port 8081 | Out-Null }
    Set-WebConfigurationProperty -PSPath 'MACHINE/WEBROOT/APPHOST' -Location 'Legado' -Filter 'system.webServer/asp' -Name codePage -Value 1252

    $direct = (& curl.exe -s --max-time 60 'http://127.0.0.1:8081/ServerMain/isCheckClanName.asp?ClName=teste') -join ''
    Assert-That 'site antigo de laboratório responde direto' ($direct -match '^Code=0') "('$(Get-Short $direct)')"

    & (Join-Path $PSScriptRoot '..\windows\Connect-LegacyPaths.ps1') -LegacyRoot $legacy
}

$apps = @(Get-WebApplication -Site 'NexusPortal')
Assert-That 'ServerMain, ClanContent e updater ligados ao portal' ((@($apps | ForEach-Object { $_.path }) -join ',') -match '/ServerMain' -and (@($apps).Count -eq 3)) "($(@($apps | ForEach-Object { $_.path }) -join ','))"
Assert-That 'no pool do site antigo' (@($apps | Where-Object { $_.applicationPool -ne 'Legado' }).Count -eq 0)
$codePage = (Get-WebConfigurationProperty -PSPath 'MACHINE/WEBROOT/APPHOST' -Location 'NexusPortal/ServerMain' -Filter 'system.webServer/asp' -Name codePage).Value
Assert-That 'página de código do ASP copiada do site antigo' ($codePage -eq 1252) "($codePage)"

foreach ($http in $false, $true) {
    $label = if ($http) { 'http' } else { 'https' }
    $r = Get-SiteResponse $HostName '/ServerMain/isCheckClanName.asp?ClName=teste&gserver=1' -Http:$http
    Assert-That "ASP do clã responde pelo portal ($label)" ($r.Status -eq 200 -and $r.Body -eq "Code=0`r") "($($r.Status) '$(Get-Short $r.Body)')"
    Assert-That "ASP do clã sem os cabeçalhos do portal ($label)" ([bool]("$($r.Headers)" -notmatch 'Content-Security-Policy')) ''
}
$r = Get-SiteResponse $HostName '/ServerMain/isCheckClanName.asp' -Http
Assert-That 'ASP sem parâmetro responde Code=100' ($r.Body -eq "Code=100`r") "($($r.Status) '$(Get-Short $r.Body)')"
$r = Get-SiteResponse $HostName '/ClanContent/1000000001.bmp' -Http
Assert-That 'ícone do clã' ($r.Status -eq 200 -and $r.Headers -match 'Content-Type: image/bmp') "($($r.Status))"
$r = Get-SiteResponse $HostName '/updater/version.txt' -Http
Assert-That 'version.txt do launcher' ($r.Status -eq 200 -and $r.Body -eq '1012') "($($r.Status) '$(Get-Short $r.Body)')"
$r = Get-SiteResponse $HostName '/'
Assert-That 'página inicial continua sendo o portal' ($r.Status -eq 200 -and $r.Headers -match 'Content-Security-Policy' -and $r.Body -notmatch 'site antigo') "($($r.Status))"
$r = Get-SiteResponse $HostName '/noticias'
Assert-That 'outras páginas continuam no portal' ($r.Status -eq 200) "($($r.Status))"
$r = Get-SiteResponse $HostName '/ServerMain/web.config' -Http
Assert-That 'web.config do site antigo não é servido' ($r.Status -eq 404) "($($r.Status))"

Complete-Test
