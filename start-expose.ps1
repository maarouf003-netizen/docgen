# DocGen - start backend + web app + a public quick tunnel, then print the public URL.
# Usage: .\start-expose.ps1 [-NoOpen]
param(
    [switch]$NoOpen
)

$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$env:PATH = "C:\Program Files\dotnet;C:\Program Files\nodejs;$env:PATH"

$cloudflared = Join-Path $env:LOCALAPPDATA 'DocGen\cloudflared.exe'
$tunnelLog = Join-Path $env:TEMP 'docgen-tunnel.log'
$tunnelErrLog = Join-Path $env:TEMP 'docgen-tunnel.err.log'
$apiBase = 'http://localhost:5199'
$webBase = 'http://localhost:5173'

function Get-HttpCode {
    param([string[]]$CurlArgs)

    $raw = & curl.exe -s -o NUL -w '%{http_code}' @CurlArgs
    $text = ''
    if ($null -ne $raw) { $text = ("$raw").Trim() }
    $code = 0
    if ([int]::TryParse($text, [ref]$code)) { return $code }
    return 0
}

function Get-Cloudflared {
    if (Test-Path -LiteralPath $cloudflared) {
        & $cloudflared version | Out-Null
        if ($LASTEXITCODE -eq 0) { return $true }
        Remove-Item -LiteralPath $cloudflared -Force -ErrorAction SilentlyContinue
    }

    $dir = Split-Path -Parent $cloudflared
    New-Item -ItemType Directory -Force -Path $dir | Out-Null

    $url = 'https://github.com/cloudflare/cloudflared/releases/latest/download/cloudflared-windows-amd64.exe'
    Write-Host '  Downloading cloudflared (one time, about 55 MB)...'
    for ($attempt = 1; $attempt -le 5; $attempt++) {
        if ($attempt -eq 1) {
            & curl.exe -L -o $cloudflared --max-time 900 $url
        } else {
            & curl.exe -L -C - -o $cloudflared --max-time 900 $url
        }
        if ($LASTEXITCODE -eq 0) { break }
        Start-Sleep -Seconds 3
    }

    if (-not (Test-Path -LiteralPath $cloudflared)) { return $false }
    & $cloudflared version | Out-Null
    return ($LASTEXITCODE -eq 0)
}

function Stop-PortListener {
    param([int]$Port)

    $lines = & netstat -ano
    foreach ($line in $lines) {
        if ("$line" -match ('(?i):' + $Port + '\s+\S+\s+LISTENING\s+(\d+)$')) {
            & taskkill /PID $Matches[1] /T /F | Out-Null
        }
    }
}

Write-Host '=========================================================='
Write-Host '  DocGen - Public link setup (API + web app + tunnel)'
Write-Host '=========================================================='

if (-not (Test-Path -LiteralPath (Join-Path $root 'backend\DocGenerator.sln'))) {
    Write-Host 'ERROR: backend project not found. Run this script from the project root.'
    exit 1
}

Write-Host '[1/5] Preparing tunnel tool (cloudflared)...'
if (-not (Get-Cloudflared)) {
    Write-Host 'ERROR: cloudflared is not available (download failed).'
    exit 1
}

Write-Host '[2/5] Starting a fresh tunnel...'
Get-Process -Name 'cloudflared' -ErrorAction SilentlyContinue | ForEach-Object {
    Stop-Process -Id $_.Id -Force -ErrorAction SilentlyContinue
    Write-Host ('     stopped old tunnel PID ' + $_.Id)
}
Start-Sleep -Milliseconds 800
Remove-Item -LiteralPath $tunnelLog, $tunnelErrLog -Force -ErrorAction SilentlyContinue

Start-Process -FilePath $cloudflared `
    -ArgumentList @('tunnel', '--url', $webBase, '--no-autoupdate') `
    -WindowStyle Hidden `
    -RedirectStandardOutput $tunnelLog `
    -RedirectStandardError $tunnelErrLog

$publicUrl = ''
for ($i = 1; $i -le 60 -and $publicUrl -eq ''; $i++) {
    Start-Sleep -Seconds 1
    foreach ($file in @($tunnelErrLog, $tunnelLog)) {
        if (-not (Test-Path -LiteralPath $file)) { continue }
        $content = Get-Content -LiteralPath $file -Raw -ErrorAction SilentlyContinue
        if (-not $content) { continue }
        $found = [regex]::Matches($content, 'https://[a-z0-9\-]+\.trycloudflare\.com')
        if ($found.Count -gt 0) {
            $publicUrl = $found[$found.Count - 1].Value
            break
        }
    }
}

if ($publicUrl -eq '') {
    Write-Host 'ERROR: the tunnel did not report a URL. Log tail:'
    if (Test-Path -LiteralPath $tunnelErrLog) {
        Get-Content -LiteralPath $tunnelErrLog -Tail 15 | ForEach-Object { Write-Host ('   ' + $_) }
    }
    exit 1
}
$tunnelHost = ([Uri]$publicUrl).Host
Write-Host ('     tunnel ready: ' + $publicUrl)

$apiUp = Get-HttpCode @('--max-time', '5', "$apiBase/api/auth/login")
if ($apiUp -eq 0) {
    Write-Host '[3/5] Backend (API) is not running - starting it on http://localhost:5199 ...'
    Start-Process -FilePath 'cmd.exe' `
        -ArgumentList '/k', 'dotnet run --project src/DocGenerator.Api --urls http://localhost:5199' `
        -WorkingDirectory (Join-Path $root 'backend') `
        -WindowStyle Normal
    $waited = 0
    while ($waited -lt 150) {
        Start-Sleep -Seconds 3
        $waited += 3
        if ((Get-HttpCode @('--max-time', '5', "$apiBase/api/auth/login")) -ne 0) { break }
    }
    if ((Get-HttpCode @('--max-time', '5', "$apiBase/api/auth/login")) -eq 0) {
        Write-Host 'ERROR: backend did not come up. Check the "DocGen API" window.'
        exit 1
    }
} else {
    Write-Host ('[3/5] Backend already running (HTTP ' + $apiUp + ').')
}

$probeArgs = @('--max-time', '8', '-H', ('Host: ' + $tunnelHost), $webBase)
$webPlain = Get-HttpCode @('--max-time', '5', $webBase)
$webCode = 0
if ($webPlain -ne 0) { $webCode = Get-HttpCode $probeArgs }

if ($webCode -ne 200) {
    if ($webPlain -ne 0) {
        Write-Host '[4/5] Web app is running but does not allow this tunnel host - restarting it...'
        Stop-PortListener -Port 5173
        Start-Sleep -Seconds 1
    } else {
        Write-Host '[4/5] Web app is not running - starting it...'
    }
    $env:VITE_ALLOWED_HOSTS = $tunnelHost
    Start-Process -FilePath 'cmd.exe' `
        -ArgumentList '/k', 'npm run dev' `
        -WorkingDirectory (Join-Path $root 'frontend') `
        -WindowStyle Normal
    $waited = 0
    $webCode = 0
    while ($waited -lt 90) {
        Start-Sleep -Seconds 3
        $waited += 3
        $webCode = Get-HttpCode $probeArgs
        if ($webCode -eq 200) { break }
    }
    if ($webCode -ne 200) {
        Write-Host 'ERROR: web app did not become reachable with the tunnel host.'
        exit 1
    }
} else {
    Write-Host '[4/5] Web app already running and accepting this tunnel host.'
}

Write-Host '[5/5] Verifying through the public link...'
$pubCode = Get-HttpCode @('--max-time', '30', $publicUrl)
$apiTunnel = Get-HttpCode @('--max-time', '30', ($publicUrl + '/api/auth/login'))
$problems = @()
if ($pubCode -ne 200) { $problems += ('web page -> HTTP ' + $pubCode) }
if ($apiTunnel -in 0, 403, 502, 503, 504) { $problems += ('API proxy -> HTTP ' + $apiTunnel) }

if ($problems.Count -gt 0) {
    Write-Host ('ERROR: verification failed: ' + ($problems -join ', '))
    exit 1
}

Write-Host ''
Write-Host '=========================================================='
Write-Host '  READY - open this link on your phone:'
Write-Host ('    ' + $publicUrl)
Write-Host ''
Write-Host '  Note: the link changes every time you run this script.'
Write-Host '  Stop the tunnel only : stop-expose.bat'
Write-Host '  Stop everything      : stop-app.bat'
Write-Host '=========================================================='

if (-not $NoOpen) { Start-Process $publicUrl }
exit 0
