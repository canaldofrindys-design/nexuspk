# Funções dos testes de instalação (GitHub Actions, Windows PowerShell 5.1). Só para laboratório.
. (Join-Path $PSScriptRoot '..\windows\Common.ps1')
$script:Failures = 0

function Assert-That([string]$Name, [bool]$Condition, [string]$Detail = '') {
    if ($Condition) { Write-Host "ok   - $Name" -ForegroundColor Green }
    else { Write-Host "FAIL - $Name $Detail" -ForegroundColor Red; $script:Failures++ }
}

function Invoke-Api([string]$Method, [string]$Path, [string]$Body) {
    $port = Get-ApiPort
    $tmp = [IO.Path]::GetTempFileName()
    try {
        $curlArgs = @('-s', '-o', $tmp, '-w', '%{http_code}', '-X', $Method, '-H', 'Content-Type: application/json')
        if ($Body) {
            $bodyFile = [IO.Path]::GetTempFileName()
            [IO.File]::WriteAllText($bodyFile, $Body)
            $curlArgs += @('--data-binary', "@$bodyFile")
        }
        $code = & curl.exe @curlArgs "http://127.0.0.1:$port$Path"
        return @{ Status = [int]$code; Body = (Get-Content $tmp -Raw) }
    }
    finally { Remove-Item $tmp -ErrorAction SilentlyContinue }
}

function Get-SiteResponse([string]$HostName, [string]$Path = '/', [switch]$Http) {
    $scheme = 'https'; $port = 443
    if ($Http) { $scheme = 'http'; $port = 80 }
    $headers = [IO.Path]::GetTempFileName(); $body = [IO.Path]::GetTempFileName()
    try {
        $code = & curl.exe -s -k -D $headers -o $body -w '%{http_code}' --max-time 60 --resolve "${HostName}:${port}:127.0.0.1" "${scheme}://${HostName}${Path}"
        return @{ Status = [int]$code; Headers = (Get-Content $headers -Raw); Body = (Get-Content $body -Raw -Encoding UTF8) }
    }
    finally { Remove-Item $headers, $body -ErrorAction SilentlyContinue }
}

function Complete-Test {
    if ($script:Failures -gt 0) { Write-Host "$script:Failures conferência(s) falharam" -ForegroundColor Red; exit 1 }
    Write-Host 'todas as conferências passaram' -ForegroundColor Green
}
