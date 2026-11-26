# P5-06 - Shared helpers for PostgreSQL backup/restore/verify scripts (PowerShell 5.1+).
#
# Dot-sourced by pg-backup.ps1 / pg-restore.ps1 / verify-backup.ps1. ASCII-only file
# (keeps Windows PowerShell 5.1 parsing predictable). Never writes secrets to logs:
# the password is moved from POSTGRES_CONNECTION into the child-process environment.

$script:PgBackupMinClientMajor = 16
$script:PgBackupAppName = 'docgen-p5-06-backup'

$script:PgHost = ''
$script:PgPort = '5432'
$script:PgDatabase = ''
$script:PgUser = ''
$script:PgPassword = ''
$script:PgSslMode = ''

function Write-BackupLog {
  param([string]$Message)
  Write-Host ("[{0}] {1}" -f (Get-Date).ToUniversalTime().ToString('yyyy-MM-ddTHH:mm:ssZ'), $Message)
}

function Write-BackupWarning {
  param([string]$Message)
  Write-BackupLog ("WARN: " + $Message)
}

function Fail-Backup {
  param([string]$Message)
  throw $Message
}

function Split-QuotedText {
  param([string]$Text, [char]$Separator)
  $parts = New-Object 'System.Collections.Generic.List[string]'
  $builder = New-Object System.Text.StringBuilder
  $inQuotes = $false
  foreach ($ch in $Text.ToCharArray()) {
    if ($ch -eq [char]'"') {
      $inQuotes = -not $inQuotes
      [void]$builder.Append($ch)
      continue
    }
    if ($ch -eq $Separator -and -not $inQuotes) {
      $parts.Add($builder.ToString())
      [void]$builder.Clear()
      continue
    }
    [void]$builder.Append($ch)
  }
  $parts.Add($builder.ToString())
  return $parts.ToArray()
}

function Split-LibpqTokens {
  param([string]$Text)
  $tokens = New-Object 'System.Collections.Generic.List[string]'
  $builder = New-Object System.Text.StringBuilder
  $inQuotes = $false
  $escape = $false
  foreach ($ch in $Text.ToCharArray()) {
    if ($escape) {
      [void]$builder.Append($ch)
      $escape = $false
      continue
    }
    if ($ch -eq [char]'\') {
      $escape = $true
      continue
    }
    if ($ch -eq [char]"'") {
      $inQuotes = -not $inQuotes
      continue
    }
    if ([char]::IsWhiteSpace($ch) -and -not $inQuotes) {
      if ($builder.Length -gt 0) {
        $tokens.Add($builder.ToString())
        [void]$builder.Clear()
      }
      continue
    }
    [void]$builder.Append($ch)
  }
  if ($builder.Length -gt 0) {
    $tokens.Add($builder.ToString())
  }
  return $tokens.ToArray()
}

function Set-PgField {
  param([string]$Key, [string]$Value)
  $norm = ($Key -replace '[ _-]', '').ToLowerInvariant()
  if ($norm -in @('host', 'server', 'address', 'datasource')) {
    $script:PgHost = $Value
  }
  elseif ($norm -eq 'port') {
    $script:PgPort = $Value
  }
  elseif ($norm -in @('database', 'initialcatalog', 'dbname')) {
    $script:PgDatabase = $Value
  }
  elseif ($norm -in @('username', 'userid', 'uid', 'user')) {
    $script:PgUser = $Value
  }
  elseif ($norm -in @('password', 'pwd')) {
    $script:PgPassword = $Value
  }
  elseif ($norm -eq 'sslmode') {
    $script:PgSslMode = $Value
  }
  # Other keys (Pooling/Timeout/Trust Server Certificate/...) do not apply to pg_dump.
}

function Parse-PgUri {
  param([string]$Raw)
  try {
    $uri = [System.Uri]$Raw
  }
  catch {
    Fail-Backup 'POSTGRES_CONNECTION URI is invalid (the value is not echoed).'
  }
  $userInfo = $uri.UserInfo
  if ($userInfo) {
    $index = $userInfo.IndexOf(':')
    if ($index -ge 0) {
      $script:PgUser = [uri]::UnescapeDataString($userInfo.Substring(0, $index))
      $script:PgPassword = [uri]::UnescapeDataString($userInfo.Substring($index + 1))
    }
    else {
      $script:PgUser = [uri]::UnescapeDataString($userInfo)
    }
  }
  $script:PgHost = [uri]::UnescapeDataString($uri.Host)
  if ($uri.Port -gt 0) {
    $script:PgPort = [string]$uri.Port
  }
  $path = $uri.AbsolutePath
  if ($path -and $path -ne '/') {
    $script:PgDatabase = [uri]::UnescapeDataString($path.TrimStart('/'))
  }
  $query = $uri.Query
  if ($query) {
    foreach ($pair in (Split-QuotedText -Text $query.TrimStart('?') -Separator '&')) {
      if (-not $pair) {
        continue
      }
      $index = $pair.IndexOf('=')
      if ($index -lt 0) {
        continue
      }
      $key = [uri]::UnescapeDataString($pair.Substring(0, $index)).ToLowerInvariant()
      $value = [uri]::UnescapeDataString($pair.Substring($index + 1))
      if ($key -in @('dbname', 'database')) {
        $script:PgDatabase = $value
      }
      elseif ($key -eq 'host') {
        $script:PgHost = $value
      }
      elseif ($key -eq 'port') {
        $script:PgPort = $value
      }
      elseif ($key -eq 'user') {
        $script:PgUser = $value
      }
      elseif ($key -eq 'password') {
        $script:PgPassword = $value
      }
      elseif ($key -eq 'sslmode') {
        $script:PgSslMode = $value
      }
    }
  }
}

function Parse-PgKeyValue {
  param([string]$Raw)
  foreach ($item in (Split-QuotedText -Text $Raw -Separator ';')) {
    $entry = $item.Trim()
    if (-not $entry) {
      continue
    }
    $index = $entry.IndexOf('=')
    if ($index -lt 1) {
      Fail-Backup 'Malformed POSTGRES_CONNECTION entry (expected key=value; value is not echoed).'
    }
    $key = $entry.Substring(0, $index).Trim()
    $value = $entry.Substring($index + 1).Trim()
    if ($value.Length -ge 2 -and $value.StartsWith('"') -and $value.EndsWith('"')) {
      $value = $value.Substring(1, $value.Length - 2)
    }
    Set-PgField -Key $key -Value $value
  }
}

function Parse-PgLibpq {
  param([string]$Raw)
  foreach ($token in (Split-LibpqTokens -Text $Raw)) {
    if (-not $token) {
      continue
    }
    $index = $token.IndexOf('=')
    if ($index -lt 1) {
      Fail-Backup 'Malformed POSTGRES_CONNECTION entry (expected key=value; value is not echoed).'
    }
    Set-PgField -Key $token.Substring(0, $index).Trim() -Value $token.Substring($index + 1)
  }
}

function Resolve-PgConnection {
  $raw = $env:POSTGRES_CONNECTION
  if ([string]::IsNullOrWhiteSpace($raw)) {
    Fail-Backup 'POSTGRES_CONNECTION is not set. Export a PostgreSQL URI (postgres://...) or a libpq/Npgsql connection string; never hardcode it.'
  }
  $script:PgHost = ''
  $script:PgPort = '5432'
  $script:PgDatabase = ''
  $script:PgUser = ''
  $script:PgPassword = ''
  $script:PgSslMode = ''

  if ($raw -match '^(?i)(postgres|postgresql)://') {
    Parse-PgUri -Raw $raw
  }
  elseif ($raw.Contains(';')) {
    Parse-PgKeyValue -Raw $raw
  }
  elseif ($raw.Contains('=')) {
    Parse-PgLibpq -Raw $raw
  }
  else {
    Fail-Backup 'POSTGRES_CONNECTION format is not recognized (expected postgres:// URI or key=value string; value is not echoed).'
  }

  if (-not $script:PgHost) {
    Fail-Backup 'POSTGRES_CONNECTION must include a host (or Host=).'
  }
  if (-not $script:PgDatabase) {
    Fail-Backup 'POSTGRES_CONNECTION must include a database name (or Database=).'
  }
  if (-not $script:PgUser) {
    Fail-Backup 'POSTGRES_CONNECTION must include a username (or Username=).'
  }
  $portNumber = 0
  if (-not [int]::TryParse($script:PgPort, [ref]$portNumber) -or $portNumber -lt 1 -or $portNumber -gt 65535) {
    Fail-Backup 'POSTGRES_CONNECTION has an invalid port.'
  }
  switch ($script:PgSslMode.ToLowerInvariant()) {
    'verifyca' { $script:PgSslMode = 'verify-ca' }
    'verifyfull' { $script:PgSslMode = 'verify-full' }
    default { $script:PgSslMode = $script:PgSslMode.ToLowerInvariant() }
  }

  $env:PGHOST = $script:PgHost
  $env:PGPORT = $script:PgPort
  $env:PGDATABASE = $script:PgDatabase
  $env:PGUSER = $script:PgUser
  if (-not $env:PGCONNECT_TIMEOUT) {
    $env:PGCONNECT_TIMEOUT = '15'
  }
  $env:PGAPPNAME = $script:PgBackupAppName
  if ($script:PgPassword) {
    $env:PGPASSWORD = $script:PgPassword
  }
  else {
    Remove-Item Env:PGPASSWORD -ErrorAction SilentlyContinue
  }
  if ($script:PgSslMode) {
    $env:PGSSLMODE = $script:PgSslMode
  }
  else {
    Remove-Item Env:PGSSLMODE -ErrorAction SilentlyContinue
  }
  $script:PgPassword = ''
  Remove-Item Env:POSTGRES_CONNECTION -ErrorAction SilentlyContinue
}

function Get-RedactedTarget {
  $ssl = 'default'
  if ($script:PgSslMode) {
    $ssl = $script:PgSslMode
  }
  return ("host={0} port={1} dbname={2} user={3} sslmode={4} password=***" -f `
      $script:PgHost, $script:PgPort, $script:PgDatabase, $script:PgUser, $ssl)
}

function Require-BackupCommand {
  param([string]$Name)
  if (-not (Get-Command $Name -ErrorAction SilentlyContinue)) {
    Fail-Backup "Required tool '$Name' not found in PATH."
  }
}

function Require-Sha256Tool {
  # Get-FileHash is built into PowerShell 5.1; no external tool is required.
  if (-not (Get-Command Get-FileHash -ErrorAction SilentlyContinue)) {
    Fail-Backup 'Get-FileHash is unavailable; PowerShell 5.1+ is required.'
  }
}

function Get-Sha256 {
  param([string]$Path)
  return (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant()
}

function Write-ChecksumFile {
  param([string]$File, [string]$ChecksumPath)
  $hash = Get-Sha256 -Path $File
  $name = [System.IO.Path]::GetFileName($File)
  $encoding = New-Object System.Text.UTF8Encoding -ArgumentList $false
  [System.IO.File]::WriteAllText($ChecksumPath, ($hash + '  ' + $name + "`n"), $encoding)
  return $hash
}

function Test-Checksum {
  param([string]$File, [string]$ChecksumPath)
  if (-not (Test-Path -LiteralPath $File -PathType Leaf)) {
    Write-BackupLog "ERROR: artifact not found: $File"
    return $false
  }
  if (-not (Test-Path -LiteralPath $ChecksumPath -PathType Leaf)) {
    Write-BackupLog "ERROR: checksum file missing: $ChecksumPath (refusing to continue)"
    return $false
  }
  $line = Get-Content -LiteralPath $ChecksumPath -TotalCount 1
  if (-not $line -or $line -notmatch '^([0-9A-Fa-f]{64})\s+\*?(.+)$') {
    Write-BackupLog "ERROR: checksum file has an invalid format: $ChecksumPath"
    return $false
  }
  $expected = $Matches[1].ToLowerInvariant()
  $listed = [System.IO.Path]::GetFileName($Matches[2].Trim())
  if ($listed -and $listed -ne [System.IO.Path]::GetFileName($File)) {
    Write-BackupLog "ERROR: checksum file names a different artifact ($listed); refusing to verify $([System.IO.Path]::GetFileName($File))."
    return $false
  }
  $actual = Get-Sha256 -Path $File
  if ($expected -ne $actual) {
    Write-BackupLog "ERROR: checksum mismatch for $([System.IO.Path]::GetFileName($File)) (expected $expected, got $actual)"
    return $false
  }
  Write-BackupLog "Checksum OK (sha256 $actual)"
  return $true
}

function Get-EncryptionMode {
  if ([string]::IsNullOrEmpty($env:BACKUP_ENCRYPTION_KEY)) {
    return 'none'
  }
  if ($env:BACKUP_ENCRYPTION_KEY -match "[`r`n]") {
    Fail-Backup 'BACKUP_ENCRYPTION_KEY must not contain newlines.'
  }
  if (Get-Command gpg -ErrorAction SilentlyContinue) {
    return 'gpg'
  }
  if (Get-Command openssl -ErrorAction SilentlyContinue) {
    return 'openssl'
  }
  Fail-Backup 'BACKUP_ENCRYPTION_KEY is set but neither gpg nor openssl is available.'
}

function Get-ArtifactName {
  param([string]$Timestamp)
  return ('docgen-' + $Timestamp + '.dump')
}

function Get-ArtifactSuffix {
  param([string]$Mode)
  switch ($Mode) {
    'gpg' { return '.gpg' }
    'openssl' { return '.enc' }
    'none' { return '' }
    default { Fail-Backup "internal: unknown encryption mode '$Mode'" }
  }
}

function Get-ArchiveEncryptionMode {
  param([string]$Path)
  if ($Path.EndsWith('.gpg')) { return 'gpg' }
  if ($Path.EndsWith('.enc')) { return 'openssl' }
  return 'none'
}

function Protect-BackupFile {
  param([string]$Source, [string]$Destination, [string]$Mode)
  switch ($Mode) {
    'gpg' {
      $key = $env:BACKUP_ENCRYPTION_KEY
      $args = @('--batch', '--yes', '--quiet', '--pinentry-mode', 'loopback', '--passphrase-fd', '0',
        '--symmetric', '--cipher-algo', 'AES256', '--output', $Destination, $Source)
      ($key + "`n") | & gpg @args
      if ($LASTEXITCODE -ne 0) {
        Remove-Item -LiteralPath $Destination -Force -ErrorAction SilentlyContinue
        Fail-Backup 'gpg encryption failed (artifact not kept).'
      }
    }
    'openssl' {
      & openssl enc -aes-256-cbc -salt -pbkdf2 -iter 200000 -in $Source -out $Destination -pass env:BACKUP_ENCRYPTION_KEY
      if ($LASTEXITCODE -ne 0) {
        Remove-Item -LiteralPath $Destination -Force -ErrorAction SilentlyContinue
        Fail-Backup 'openssl encryption failed (artifact not kept).'
      }
    }
    default {
      Fail-Backup "internal: unsupported encryption mode '$Mode'"
    }
  }
}

function Expand-BackupArchiveToTemp {
  param([string]$Archive, [string]$Mode)
  if ($Mode -eq 'none') {
    return $Archive
  }
  if ([string]::IsNullOrEmpty($env:BACKUP_ENCRYPTION_KEY)) {
    Fail-Backup "Artifact is encrypted ($Mode) but BACKUP_ENCRYPTION_KEY is not set."
  }
  $temp = [System.IO.Path]::Combine([System.IO.Path]::GetTempPath(),
    ('docgen-verify-' + [guid]::NewGuid().ToString('N') + '.dump'))
  switch ($Mode) {
    'gpg' {
      $key = $env:BACKUP_ENCRYPTION_KEY
      $args = @('--batch', '--quiet', '--yes', '--pinentry-mode', 'loopback', '--passphrase-fd', '0',
        '--decrypt', '--output', $temp, $Archive)
      ($key + "`n") | & gpg @args
    }
    'openssl' {
      & openssl enc -d -aes-256-cbc -pbkdf2 -iter 200000 -in $Archive -out $temp -pass env:BACKUP_ENCRYPTION_KEY
    }
    default {
      Fail-Backup "internal: unsupported encryption mode '$Mode'"
    }
  }
  if ($LASTEXITCODE -ne 0 -or -not (Test-Path -LiteralPath $temp -PathType Leaf)) {
    Remove-SensitiveFile -Path $temp
    Fail-Backup 'Decryption failed (wrong key or corrupt artifact).'
  }
  return $temp
}

function Remove-SensitiveFile {
  param([string]$Path)
  if (-not $Path) {
    return
  }
  if (Test-Path -LiteralPath $Path -PathType Leaf) {
    try {
      $length = (Get-Item -LiteralPath $Path).Length
      if ($length -gt 0) {
        $stream = [System.IO.File]::Open($Path, [System.IO.FileMode]::Open,
          [System.IO.FileAccess]::Write, [System.IO.FileShare]::None)
        try {
          $buffer = New-Object byte[] 8192
          $remaining = [long]$length
          while ($remaining -gt 0) {
            $count = [int][Math]::Min([long]$buffer.Length, $remaining)
            $stream.Write($buffer, 0, $count)
            $remaining -= $count
          }
          $stream.Flush($true)
        }
        finally {
          $stream.Dispose()
        }
      }
    }
    catch {
      # Best-effort overwrite; deletion below still runs.
    }
    Remove-Item -LiteralPath $Path -Force -ErrorAction SilentlyContinue
  }
}

function Get-PgClientMajor {
  param([string]$Tool)
  $output = & $Tool --version 2>$null
  if ($output -and ($output -match '([0-9]+)\.[0-9]+')) {
    return [int]$Matches[1]
  }
  return 0
}

function Require-PgTools {
  param([string[]]$Tools)
  foreach ($tool in $Tools) {
    Require-BackupCommand -Name $tool
  }
  foreach ($tool in $Tools) {
    $major = Get-PgClientMajor -Tool $tool
    if ($major -lt $script:PgBackupMinClientMajor) {
      Fail-Backup ("'{0}' major version {1} is older than the PostgreSQL server (requires >= {2})." -f `
          $tool, $major, $script:PgBackupMinClientMajor)
    }
  }
}

function Test-ProductionLike {
  param([string]$HostName, [string]$DatabaseName)
  $h = $HostName.ToLowerInvariant()
  $d = $DatabaseName.ToLowerInvariant()
  # The "postgres" maintenance/default database (Supabase and others) is treated as
  # production-like: restoring over it by accident is a common, catastrophic mistake.
  if ($d -eq 'postgres') {
    return $true
  }
  if ($d -match '(^|[-_])(prod|production|prd|live)([-_]|$)') {
    return $true
  }
  if ($h -match '(^|[.-])(prod|production|prd|live)([.-]|$)') {
    return $true
  }
  $deny = $env:BACKUP_PRODUCTION_DENYLIST
  if ($deny) {
    foreach ($item in $deny.Split(',')) {
      $candidate = $item.Trim().ToLowerInvariant()
      if ($candidate -and $d -eq $candidate) {
        return $true
      }
    }
  }
  return $false
}

function Assert-SafeTarget {
  param([string]$HostName, [string]$DatabaseName, [switch]$AllowProduction)
  if ($DatabaseName.ToLowerInvariant() -in @('template0', 'template1')) {
    Fail-Backup "Refusing to restore into template database '$DatabaseName'."
  }
  if (Test-ProductionLike -HostName $HostName -DatabaseName $DatabaseName) {
    if (-not $AllowProduction) {
      Fail-Backup "Target database '$DatabaseName' on host '$HostName' looks like PRODUCTION. Refusing without -AllowProduction and an approved change window."
    }
    Write-BackupWarning "PRODUCTION override accepted (-AllowProduction) for db '$DatabaseName' on host '$HostName'."
  }
}

function Assert-RestoreConfirmation {
  param([bool]$Confirmed)
  if (-not $Confirmed) {
    Fail-Backup 'Refusing to restore without -Yes. Re-run with -Yes after verifying the target and the backup checksum.'
  }
}

function ConvertFrom-CopyRows {
  param([string[]]$Lines)
  $rows = New-Object 'System.Collections.Generic.List[string]'
  $inside = $false
  foreach ($line in @($Lines)) {
    if ($line -match '^COPY .* FROM stdin;$') {
      $inside = $true
      continue
    }
    if ($inside -and $line -eq '\.') {
      $inside = $false
      continue
    }
    if ($inside) {
      $fields = $line -split "`t"
      if ($fields.Count -ge 1 -and $fields[0] -and $fields[0] -ne '\N') {
        $rows.Add($fields[0])
      }
    }
  }
  return $rows.ToArray()
}

function Get-MigrationRowsFromArchive {
  param([string]$ArchivePath)
  $output = & pg_restore --data-only --table=__EFMigrationsHistory --file=- $ArchivePath 2>$null
  $exitCode = $LASTEXITCODE
  if ($exitCode -ne 0) {
    return @()
  }
  return (ConvertFrom-CopyRows -Lines @($output))
}

function Get-MigrationHead {
  param([string[]]$Rows)
  $head = ''
  foreach ($row in @($Rows)) {
    if ($row -and [string]::CompareOrdinal($row, $head) -gt 0) {
      $head = $row
    }
  }
  return $head
}

function Get-RetentionCandidates {
  param([string]$Directory, [int]$Days)
  if ($Days -le 0 -or -not (Test-Path -LiteralPath $Directory -PathType Container)) {
    return @()
  }
  $cutoff = (Get-Date).ToUniversalTime().AddDays(-$Days)
  return @(Get-ChildItem -LiteralPath $Directory -File |
      Where-Object { $_.Name -match '^docgen-.*\.dump(\.gpg|\.enc)?$' -and $_.LastWriteTimeUtc -lt $cutoff })
}

function Invoke-BackupPrune {
  param([string]$Directory, [int]$Days, [string]$KeepArtifact)
  if ($Days -le 0) {
    Write-BackupLog 'Retention disabled (RetentionDays=0 keeps every artifact).'
    return
  }
  $count = 0
  foreach ($file in (Get-RetentionCandidates -Directory $Directory -Days $Days)) {
    if ($file.FullName -eq $KeepArtifact) {
      continue
    }
    Remove-Item -LiteralPath $file.FullName -Force
    foreach ($sidecar in @($file.FullName + '.sha256', $file.FullName + '.meta.json')) {
      Remove-Item -LiteralPath $sidecar -Force -ErrorAction SilentlyContinue
    }
    Write-BackupLog ("Pruned expired backup: " + $file.Name)
    $count += 1
  }
  Write-BackupLog ("Retention: removed {0} expired backup(s) older than {1} day(s)." -f $count, $Days)
}
