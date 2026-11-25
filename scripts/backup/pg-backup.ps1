#Requires -Version 5.1
<#
.SYNOPSIS
  P5-06 - Creates a PostgreSQL custom-format backup with SHA-256 checksum, optional
  client-side encryption, metadata, retention pruning, and -WhatIf plan mode.

.DESCRIPTION
  Connection comes ONLY from the POSTGRES_CONNECTION environment variable (postgres://
  URI or key=value string). The password is moved to PGPASSWORD for child processes and
  is never written to this script or its logs.

  Optional encryption: set BACKUP_ENCRYPTION_KEY and have gpg (preferred, AES256) or
  openssl (AES-256-CBC/PBKDF2, 200000 iterations) available.

  -WhatIf validates the connection/tools and prints the plan without writing anything.
  -SelfTest runs offline parser/checksum/retention tests (no database or PostgreSQL tools).

.EXAMPLE
  $env:POSTGRES_CONNECTION = 'postgresql://<user>:<password>@db.example.test:5432/docgen?sslmode=require'
  powershell -NoProfile -File scripts\backup\pg-backup.ps1 -WhatIf

.EXAMPLE
  powershell -NoProfile -File scripts\backup\pg-backup.ps1 -OutputDir D:\backups\docgen -RetentionDays 30
#>
[CmdletBinding()]
param(
  [string]$OutputDir = $(if ($env:BACKUP_OUTPUT_DIR) { $env:BACKUP_OUTPUT_DIR } else { Join-Path $HOME 'docgen-backups' }),
  [int]$RetentionDays = $(if ($env:BACKUP_RETENTION_DAYS) { [int]$env:BACKUP_RETENTION_DAYS } else { 30 }),
  [ValidateRange(0, 9)][int]$CompressLevel = $(if ($env:BACKUP_COMPRESS_LEVEL) { [int]$env:BACKUP_COMPRESS_LEVEL } else { 6 }),
  [switch]$WhatIf,
  [switch]$SelfTest
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
. (Join-Path $scriptDir 'backup-common.ps1')

$script:SelfTestFailures = 0

function Assert-SelfTestEqual {
  param([string]$Label, [string]$Expected, [string]$Actual)
  if ($Expected -ne $Actual) {
    Write-Host ("SELF-TEST FAIL: {0} (expected '{1}', got '{2}')" -f $Label, $Expected, $Actual) -ForegroundColor Red
    $script:SelfTestFailures += 1
  }
}

function Test-ScriptSelfTest {
  $savedConnection = $env:POSTGRES_CONNECTION
  $savedKey = $env:BACKUP_ENCRYPTION_KEY
  $tempDir = Join-Path ([System.IO.Path]::GetTempPath()) ('pg-backup-selftest-' + [guid]::NewGuid().ToString('N'))
  try {
    New-Item -ItemType Directory -Path $tempDir -Force | Out-Null
    Remove-Item Env:POSTGRES_CONNECTION -ErrorAction SilentlyContinue
    Remove-Item Env:BACKUP_ENCRYPTION_KEY -ErrorAction SilentlyContinue

    Assert-SelfTestEqual 'artifact name' 'docgen-20260102T030405Z.dump' (Get-ArtifactName -Timestamp '20260102T030405Z')
    Assert-SelfTestEqual 'gpg suffix' '.gpg' (Get-ArtifactSuffix -Mode 'gpg')
    Assert-SelfTestEqual 'openssl suffix' '.enc' (Get-ArtifactSuffix -Mode 'openssl')
    Assert-SelfTestEqual 'plain suffix' '' (Get-ArtifactSuffix -Mode 'none')

    $env:POSTGRES_CONNECTION = 'postgresql://backup%40user:p%40ss%3Aword@db.example.test:6543/docgen?sslmode=require&application_name=x'
    Resolve-PgConnection
    Assert-SelfTestEqual 'URI host' 'db.example.test' $script:PgHost
    Assert-SelfTestEqual 'URI port' '6543' $script:PgPort
    Assert-SelfTestEqual 'URI database' 'docgen' $script:PgDatabase
    Assert-SelfTestEqual 'URI user' 'backup@user' $script:PgUser
    Assert-SelfTestEqual 'URI password decoding' 'p@ss:word' $env:PGPASSWORD
    Assert-SelfTestEqual 'URI sslmode' 'require' $script:PgSslMode
    $description = Get-RedactedTarget
    if ($description -match 'p@ss|s3mi') {
      Write-Host 'SELF-TEST FAIL: redacted target leaked a password' -ForegroundColor Red
      $script:SelfTestFailures += 1
    }
    if ($description -notmatch 'password=\*\*\*') {
      Write-Host 'SELF-TEST FAIL: redacted target is missing the password marker' -ForegroundColor Red
      $script:SelfTestFailures += 1
    }

    $env:POSTGRES_CONNECTION = 'Host=db.example.test;Port=5432;Database=docgen;Username=docgen_app;Password="s3mi;colon";SSL Mode=Require'
    Resolve-PgConnection
    Assert-SelfTestEqual 'Npgsql password with semicolon' 's3mi;colon' $env:PGPASSWORD
    Assert-SelfTestEqual 'Npgsql ssl mode' 'require' $script:PgSslMode
    Assert-SelfTestEqual 'Npgsql default port' '5432' $script:PgPort

    $env:POSTGRES_CONNECTION = "host=db.example.test port=5433 dbname=docgen user=docgen_app password='a b' sslmode=verify-full"
    Resolve-PgConnection
    Assert-SelfTestEqual 'libpq password with space' 'a b' $env:PGPASSWORD
    Assert-SelfTestEqual 'libpq port' '5433' $script:PgPort
    Assert-SelfTestEqual 'libpq verify-full' 'verify-full' $script:PgSslMode

    Remove-Item Env:POSTGRES_CONNECTION -ErrorAction SilentlyContinue
    $missingRejected = $false
    try {
      Resolve-PgConnection
    }
    catch {
      $missingRejected = $true
    }
    if (-not $missingRejected) {
      Write-Host 'SELF-TEST FAIL: missing POSTGRES_CONNECTION was accepted' -ForegroundColor Red
      $script:SelfTestFailures += 1
    }

    Assert-SelfTestEqual 'prod db' 'True' ([string](Test-ProductionLike -HostName 'db.example.test' -DatabaseName 'docgen_prod'))
    Assert-SelfTestEqual 'postgres default db' 'True' ([string](Test-ProductionLike -HostName 'db.example.test' -DatabaseName 'postgres'))
    Assert-SelfTestEqual 'prod host' 'True' ([string](Test-ProductionLike -HostName 'db.prod.internal' -DatabaseName 'docgen'))
    Assert-SelfTestEqual 'staging db' 'False' ([string](Test-ProductionLike -HostName 'staging.example.test' -DatabaseName 'docgen_staging'))
    Assert-SelfTestEqual 'mylive db' 'False' ([string](Test-ProductionLike -HostName 'db.example.test' -DatabaseName 'mylive'))

    $confirmationRejected = $false
    try {
      Assert-RestoreConfirmation -Confirmed $false
    }
    catch {
      $confirmationRejected = $true
    }
    if (-not $confirmationRejected) {
      Write-Host 'SELF-TEST FAIL: restore proceeded without confirmation' -ForegroundColor Red
      $script:SelfTestFailures += 1
    }
    Assert-RestoreConfirmation -Confirmed $true

    $sample = Join-Path $tempDir 'sample.dump'
    $sums = $sample + '.sha256'
    [System.IO.File]::WriteAllText($sample, 'archive-bytes')
    [void](Write-ChecksumFile -File $sample -ChecksumPath $sums)
    if (-not (Test-Checksum -File $sample -ChecksumPath $sums)) {
      Write-Host 'SELF-TEST FAIL: valid checksum rejected' -ForegroundColor Red
      $script:SelfTestFailures += 1
    }
    [System.IO.File]::AppendAllText($sample, 'corrupt')
    if (Test-Checksum -File $sample -ChecksumPath $sums) {
      Write-Host 'SELF-TEST FAIL: corrupted artifact passed checksum verification' -ForegroundColor Red
      $script:SelfTestFailures += 1
    }
    if (Test-Checksum -File $sample -ChecksumPath (Join-Path $tempDir 'missing.sha256')) {
      Write-Host 'SELF-TEST FAIL: missing checksum file was accepted' -ForegroundColor Red
      $script:SelfTestFailures += 1
    }

    $old = Join-Path $tempDir 'docgen-old.dump'
    $new = Join-Path $tempDir 'docgen-new.dump'
    [System.IO.File]::WriteAllText($old, 'old')
    [System.IO.File]::WriteAllText($new, 'new')
    (Get-Item -LiteralPath $old).LastWriteTimeUtc = [datetime]::SpecifyKind([datetime]'2020-01-01', [System.DateTimeKind]::Utc)
    $candidates = @(Get-RetentionCandidates -Directory $tempDir -Days 30)
    if (-not ($candidates | Where-Object { $_.Name -eq 'docgen-old.dump' })) {
      Write-Host 'SELF-TEST FAIL: old artifact not selected by retention' -ForegroundColor Red
      $script:SelfTestFailures += 1
    }
    if ($candidates | Where-Object { $_.Name -eq 'docgen-new.dump' }) {
      Write-Host 'SELF-TEST FAIL: fresh artifact selected by retention' -ForegroundColor Red
      $script:SelfTestFailures += 1
    }

    if ($script:SelfTestFailures -ne 0) {
      Write-Host ("pg-backup.ps1 self-test: FAILED ({0})" -f $script:SelfTestFailures) -ForegroundColor Red
      return 1
    }
    Write-Host 'pg-backup.ps1 self-test: OK'
    return 0
  }
  finally {
    $env:POSTGRES_CONNECTION = $savedConnection
    $env:BACKUP_ENCRYPTION_KEY = $savedKey
    Remove-Item -LiteralPath $tempDir -Recurse -Force -ErrorAction SilentlyContinue
  }
}

if ($SelfTest) {
  exit (Test-ScriptSelfTest)
}

$tempPlain = $null
$tempMetadata = $null
try {
  if ([string]::IsNullOrWhiteSpace($OutputDir)) {
    throw 'OutputDir must not be empty.'
  }
  Resolve-PgConnection
  Write-BackupLog ("Backup source: " + (Get-RedactedTarget))
  Require-PgTools -Tools @('pg_dump', 'pg_restore')
  Require-Sha256Tool
  $encryptionMode = Get-EncryptionMode
  if ($encryptionMode -ne 'none') {
    Write-BackupLog ("Encryption: {0} (key supplied via environment; never logged)." -f $encryptionMode)
  }
  else {
    Write-BackupLog 'Encryption: disabled (set BACKUP_ENCRYPTION_KEY to enable).'
  }

  $pgDumpVersion = (& pg_dump --version | Select-Object -First 1)
  $timestamp = (Get-Date).ToUniversalTime().ToString('yyyyMMddTHHmmssZ')
  $artifactName = (Get-ArtifactName -Timestamp $timestamp) + (Get-ArtifactSuffix -Mode $encryptionMode)
  $artifact = Join-Path $OutputDir $artifactName
  $checksum = $artifact + '.sha256'
  $metadata = $artifact + '.meta.json'

  $expired = @(Get-RetentionCandidates -Directory $OutputDir -Days $RetentionDays)
  if ($WhatIf) {
    Write-BackupLog 'DRY-RUN: no backup will be written.'
    Write-BackupLog ("Planned artifact: " + $artifact)
    Write-BackupLog ("Planned checksum: " + $checksum)
    Write-BackupLog ("Planned metadata: " + $metadata)
    Write-BackupLog ("Compression level: {0} (custom format)" -f $CompressLevel)
    Write-BackupLog ("Client: " + $pgDumpVersion)
    Write-BackupLog ("Retention: would delete {0} expired artifact(s) older than {1} day(s)." -f $expired.Count, $RetentionDays)
    return
  }

  if (-not (Test-Path -LiteralPath $OutputDir -PathType Container)) {
    New-Item -ItemType Directory -Path $OutputDir -Force | Out-Null
  }
  if (Test-Path -LiteralPath $artifact) {
    throw "Artifact already exists, refusing to overwrite: $artifact"
  }

  $tempPlain = Join-Path $OutputDir ('.' + $artifactName + '.partial')
  $tempMetadata = $metadata + '.tmp'
  Write-BackupLog ("Dumping database '{0}' to a temporary artifact..." -f $script:PgDatabase)
  & pg_dump --format=custom --compress=$CompressLevel --no-password --file=$tempPlain
  if ($LASTEXITCODE -ne 0) {
    throw 'pg_dump failed; no artifact was published.'
  }
  & pg_restore --list $tempPlain > $null
  if ($LASTEXITCODE -ne 0) {
    throw 'Dumped archive is not readable by pg_restore; no artifact was published.'
  }

  $migrationRows = @(Get-MigrationRowsFromArchive -ArchivePath $tempPlain)
  $migrationHead = Get-MigrationHead -Rows $migrationRows
  $migrationCount = $migrationRows.Count
  if ($migrationCount -eq 0) {
    Write-BackupWarning 'No rows found in __EFMigrationsHistory inside the archive; verify-backup.ps1 will reject it as unexpected for DocGen.'
  }

  if ($encryptionMode -eq 'none') {
    Move-Item -LiteralPath $tempPlain -Destination $artifact -Force
  }
  else {
    Write-BackupLog ("Encrypting artifact with {0}..." -f $encryptionMode)
    Protect-BackupFile -Source $tempPlain -Destination $artifact -Mode $encryptionMode
    $verificationCopy = $null
    try {
      $verificationCopy = Expand-BackupArchiveToTemp -Archive $artifact -Mode $encryptionMode
      & pg_restore --list $verificationCopy > $null
      if ($LASTEXITCODE -ne 0) {
        throw 'Encrypted artifact failed its decrypt/readability round-trip.'
      }
    }
    catch {
      Remove-Item -LiteralPath $artifact -Force -ErrorAction SilentlyContinue
      throw
    }
    finally {
      if ($verificationCopy -and $verificationCopy -ne $artifact) {
        Remove-SensitiveFile -Path $verificationCopy
      }
    }
    Remove-SensitiveFile -Path $tempPlain
    $tempPlain = $null
  }

  $artifactHash = Write-ChecksumFile -File $artifact -ChecksumPath $checksum
  $artifactBytes = (Get-Item -LiteralPath $artifact).Length
  $metadataObject = [ordered]@{
    schemaVersion    = 1
    createdAtUtc     = (Get-Date).ToUniversalTime().ToString('yyyy-MM-ddTHH:mm:ssZ')
    artifact         = $artifactName
    bytes            = [long]$artifactBytes
    sha256           = $artifactHash
    compressionLevel = $CompressLevel
    encryption       = $encryptionMode
    pgDumpVersion    = [string]$pgDumpVersion
    source           = [ordered]@{
      host     = $script:PgHost
      port     = [int]$script:PgPort
      database = $script:PgDatabase
      user     = $script:PgUser
    }
    migrations       = [ordered]@{
      count = [int]$migrationCount
      head  = [string]$migrationHead
    }
  }
  $json = ($metadataObject | ConvertTo-Json -Depth 5)
  [System.IO.File]::WriteAllText($tempMetadata, $json + "`n", (New-Object System.Text.UTF8Encoding -ArgumentList $false))
  Move-Item -LiteralPath $tempMetadata -Destination $metadata -Force
  $tempMetadata = $null

  Write-BackupLog ("Backup written: {0} ({1} bytes)" -f $artifactName, $artifactBytes)
  Write-BackupLog ("SHA-256: " + $artifactHash)
  Write-BackupLog ("Migrations recorded in archive: count={0} head={1}" -f $migrationCount, $migrationHead)
  Invoke-BackupPrune -Directory $OutputDir -Days $RetentionDays -KeepArtifact $artifact
  Write-BackupLog 'Backup completed successfully.'
}
catch {
  Write-BackupLog ("ERROR: " + $_.Exception.Message)
  exit 1
}
finally {
  if ($tempPlain) {
    Remove-SensitiveFile -Path $tempPlain
  }
  if ($tempMetadata) {
    Remove-Item -LiteralPath $tempMetadata -Force -ErrorAction SilentlyContinue
  }
}
