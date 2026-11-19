# Troca do domínio principal (Switch-PortalDomain.ps1), no Windows do GitHub.
# Usa o site antigo de laboratório do Test-LegacyPaths.ps1 ("Legado", C:\legado): põe nele um domínio de teste
# com www (http e https), troca para o portal, confere, tenta parar o site antigo e desfaz.
. (Join-Path $PSScriptRoot 'TestHelpers.ps1')
Import-Module WebAdministration
$switchScript = Join-Path $PSScriptRoot '..\windows\Switch-PortalDomain.ps1'
$domain = 'troca.localtest.me'
$www = "www.$domain"

if (-not (Get-Website -Name 'Legado')) { throw 'rode o Test-LegacyPaths.ps1 antes (site Legado)' }
$cert = New-SelfSignedCertificate -DnsName $domain, $www -CertStoreLocation Cert:\LocalMachine\My
foreach ($h in $domain, $www) {
    New-WebBinding -Name 'Legado' -Protocol http -Port 80 -HostHeader $h
    New-WebBinding -Name 'Legado' -Protocol https -Port 443 -HostHeader $h -SslFlags 1
    (Get-WebBinding -Name 'Legado' -Protocol https | Where-Object { $_.bindingInformation -eq "*:443:$h" }).AddSslCertificate($cert.Thumbprint, 'My')
}
$r = Get-SiteResponse $domain '/'
Assert-That 'antes: o domínio abre o site antigo' ($r.Status -eq 200 -and $r.Body -match 'site antigo') "($($r.Status))"
$urlBefore = [regex]::Match((Get-Content -Raw C:\NexusPortal\config\portal.json), '"PublicBaseUrl"\s*:\s*"([^"]*)"').Groups[1].Value

# -WhatIf não muda nada.
& $switchScript -Domain $domain -WhatIf
Assert-That '-WhatIf não mexe nas associações' (@(Get-WebBinding -Name 'Legado' | Where-Object { $_.bindingInformation -like "*$domain" }).Count -eq 4)
Assert-That '-WhatIf não registra troca' (-not (Test-Path C:\NexusPortal\config\troca-dominio.json))

& $switchScript -Domain $domain -CertificateThumbprint $cert.Thumbprint -BackupDir C:\backups-teste
$r = Get-SiteResponse $domain '/'
Assert-That 'depois: o domínio abre o portal' ($r.Status -eq 200 -and $r.Headers -match 'Content-Security-Policy' -and $r.Body -notmatch 'site antigo') "($($r.Status))"
$r = Get-SiteResponse $www '/download'
Assert-That 'www vai para o domínio sem www' ($r.Status -eq 308 -and $r.Headers -match "Location: https://$([regex]::Escape($domain))/download") "($($r.Status))"
$r = Get-SiteResponse $domain '/painel/cadastrar'
Assert-That 'endereço antigo de cadastro redireciona' ($r.Status -eq 308 -and $r.Headers -match 'Location: /criar-conta') "($($r.Status))"
$r = Get-SiteResponse $domain '/ServerMain/isCheckClanName.asp?ClName=teste' -Http
Assert-That 'clã responde pelo domínio por http' ($r.Status -eq 200 -and $r.Body -eq "Code=0`r") "($($r.Status))"
$r = Get-SiteResponse $domain '/updater/version.txt' -Http
Assert-That 'launcher responde pelo domínio por http' ($r.Status -eq 200 -and $r.Body -eq '1012') "($($r.Status))"
Assert-That 'site antigo sem o domínio' (@(Get-WebBinding -Name 'Legado' | Where-Object { $_.bindingInformation -like "*$domain" }).Count -eq 0)
Assert-That 'site antigo continua pela porta própria' (@(Get-WebBinding -Name 'Legado' | Where-Object { $_.bindingInformation -eq '*:8081:' }).Count -eq 1)
$json = Get-Content -Raw C:\NexusPortal\config\portal.json
Assert-That 'PublicBaseUrl trocado' ($json -match "`"PublicBaseUrl`"\s*:\s*`"https://$([regex]::Escape($domain))`"")
Assert-That 'backup da pasta do site antigo' (@(Get-ChildItem C:\backups-teste -Filter 'site-antigo-*.zip').Count -eq 1)

# Segunda troca sem desfazer a primeira é recusada.
$refused = $false
try { & $switchScript -Domain $domain -CertificateThumbprint $cert.Thumbprint -SkipFileBackup } catch { $refused = $true }
Assert-That 'segunda troca recusada' $refused

# Alguém chamou o site antigo depois da troca: não para sem -Force.
Start-Sleep -Seconds 2
& curl.exe -s -o NUL --max-time 30 'http://127.0.0.1:8081/updater/version.txt' | Out-Null
& netsh http flush logbuffer | Out-Null
Start-Sleep -Seconds 2
& $switchScript -StopOldSite
Assert-That 'com chamadas depois da troca, o site antigo não para' ((Get-WebsiteState -Name 'Legado').Value -eq 'Started')
& $switchScript -StopOldSite -Force
Assert-That 'com -Force, o site antigo para' ((Get-WebsiteState -Name 'Legado').Value -eq 'Stopped')

& $switchScript -Rollback
Assert-That 'volta: site antigo ligado' ((Get-WebsiteState -Name 'Legado').Value -eq 'Started')
$r = Get-SiteResponse $domain '/'
Assert-That 'volta: o domínio abre o site antigo de novo' ($r.Status -eq 200 -and $r.Body -match 'site antigo') "($($r.Status))"
$urlAfter = [regex]::Match((Get-Content -Raw C:\NexusPortal\config\portal.json), '"PublicBaseUrl"\s*:\s*"([^"]*)"').Groups[1].Value
Assert-That 'volta: PublicBaseUrl como antes' ($urlAfter -eq $urlBefore) "($urlAfter)"
Assert-That 'volta: troca não fica registrada' (-not (Test-Path C:\NexusPortal\config\troca-dominio.json))
$r = Get-SiteResponse $env:PORTAL_HOST '/'
Assert-That 'volta: portal continua no endereço de teste' ($r.Status -eq 200) "($($r.Status))"

# Deixa o laboratório como estava para os próximos passos.
foreach ($h in $domain, $www) {
    foreach ($p in @(@('http', "*:80:$h"), @('https', "*:443:$h"))) { Remove-WebBinding -Name 'Legado' -Protocol $p[0] -BindingInformation $p[1] -ErrorAction SilentlyContinue }
}

Complete-Test
