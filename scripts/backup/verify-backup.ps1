#Requires -Version 5.1
<#
.SYNOPSIS
  P5-06 - Verifies a PostgreSQL backup WITHOUT restoring it: SHA-256 checksum,
  pg_restore archive readability, and the migration head recorded in
  __EFMigrationsHistory (extracted from archive data).

.DESCRIPTION
  No database connection is used. Encrypted artifacts are decrypted to a temporary
  file that is overwritten and removed in a finally block.

  -ChecksumOnly verifies the hash and stops. -ExpectedHead fails on migration drift.
  -SelfTest runs offline checksum/parser tests.

.EXAMPLE
  powershell -NoProfile -File scripts\backup\verify-backup.ps1 -File D:\backups\docgen\docgen-...dump

.EXAMPLE
  powershell -NoProfile -File scripts\backup\verify-backup.ps1 -File ... -ExpectedHead 20261121034750_P3_02_PortalSearchIndex
#>
[CmdletBinding()]
param(
  [string]$File,
  [string]$ChecksumFile = '',
  [string]$ExpectedHead = '',
  [switch]$ChecksumOnly,
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
  $tempDir = Join-Path ([System.IO.Path]::GetTempPath()) ('verify-backup-selftest-' + [guid]::NewGuid().ToString('N'))
  try {
    New-Item -ItemType Directory -Path $tempDir -Force | Out-Null

    $sample = Join-Path $tempDir 'sample.dump'
    $sums = $sample + '.sha256'
    [System.IO.File]::WriteAllText($sample, 'archive-bytes')
    [void](Write-ChecksumFile -File $sample -ChecksumPath $sums)
    if (-not (Test-Checksum -File $sample -ChecksumPath $sums)) {
      Write-Host 'SELF-TEST FAIL: valid checksum rejected' -ForegroundColor Red
      $script:SelfTestFailures += 1
    }
    [System.IO.File]::AppendAllText($sample, 'x')
    if (Test-Checksum -File $sample -ChecksumPath $sums) {
      Write-Host 'SELF-TEST FAIL: corrupted artifact passed checksum verification' -ForegroundColor Red
      $script:SelfTestFailures += 1
    }
    $other = Join-Path $tempDir 'other.dump'
    [System.IO.File]::WriteAllText($other, 'archive-bytes')
    if (Test-Checksum -File $other -ChecksumPath $sums) {
      Write-Host 'SELF-TEST FAIL: checksum for a different artifact was accepted' -ForegroundColor Red
      $script:SelfTestFailures += 1
    }
    if (Test-Checksum -File $sample -ChecksumPath (Join-Path $tempDir 'missing.sha256')) {
      Write-Host 'SELF-TEST FAIL: missing checksum file was accepted' -ForegroundColor Red
      $script:SelfTestFailures += 1
    }

    $copyLines = @(
      'SET statement_timeout = 0;',
      'COPY "public"."__EFMigrationsHistory" ("MigrationId", "ProductVersion") FROM stdin;',
      "20261114101737_InitialPostgresBaseline`t10.0.10",
      "20261114140029_AddIdempotencyResponseMetadata`t10.0.10",
      "20261121034750_P3_02_PortalSearchIndex`t10.0.10",
      '\.'
    )
    $rows = @(ConvertFrom-CopyRows -Lines $copyLines)
    Assert-SelfTestEqual 'COPY rows' '3' ([string]$rows.Count)
    Assert-SelfTestEqual 'COPY head' '20261121034750_P3_02_PortalSearchIndex' (Get-MigrationHead -Rows $rows)

    Assert-SelfTestEqual 'gpg detection' 'gpg' (Get-ArchiveEncryptionMode -Path 'x.dump.gpg')
    Assert-SelfTestEqual 'openssl detection' 'openssl' (Get-ArchiveEncryptionMode -Path 'x.dump.enc')
    Assert-SelfTestEqual 'plain detection' 'none' (Get-ArchiveEncryptionMode -Path 'x.dump')

    if ($script:SelfTestFailures -ne 0) {
      Write-Host ("verify-backup.ps1 self-test: FAILED ({0})" -f $script:SelfTestFailures) -ForegroundColor Red
      return 1
    }
    Write-Host 'verify-backup.ps1 self-test: OK'
    return 0
  }
  finally {
    Remove-Item -LiteralPath $tempDir -Recurse -Force -ErrorAction SilentlyContinue
  }
}

if ($SelfTest) {
  exit (Test-ScriptSelfTest)
}

$plainArchive = $null
try {
  if ([string]::IsNullOrWhiteSpace($File)) {
    throw '-File is required.'
  }
  if (-not (Test-Path -LiteralPath $File -PathType Leaf)) {
    throw "Backup artifact not found: $File"
  }
  if ([string]::IsNullOrWhiteSpace($ChecksumFile)) {
    $ChecksumFile = $File + '.sha256'
  }
  Require-Sha256Tool
  if (-not (Test-Checksum -File $File -ChecksumPath $ChecksumFile)) {
    throw 'Checksum verification failed.'
  }
  if ($ChecksumOnly) {
    Write-BackupLog 'Checksum-only verification passed.'
    return
  }

  Require-PgTools -Tools @('pg_restore')
  $encryptionMode = Get-ArchiveEncryptionMode -Path $File
  if ($encryptionMode -ne 'none' -and [string]::IsNullOrEmpty($env:BACKUP_ENCRYPTION_KEY)) {
    throw "Artifact is encrypted ($encryptionMode) but BACKUP_ENCRYPTION_KEY is not set; cannot verify archive readability."
  }
  $plainArchive = Expand-BackupArchiveToTemp -Archive $File -Mode $encryptionMode

  $toc = & pg_restore --list $plainArchive
  if ($LASTEXITCODE -ne 0) {
    throw 'pg_restore cannot read the archive (corrupt, wrong key, or not a custom-format dump).'
  }
  $entries = @($toc | Where-Object { $_ -and $_ -notmatch '^;' })
  if ($entries.Count -eq 0) {
    throw 'Archive table of contents has no restorable entries.'
  }
  $archiveDb = ''
  foreach ($line in @($toc)) {
    if ($line -match '^;\s*dbname:\s*(.+)$') {
      $archiveDb = $Matches[1].Trim()
      break
    }
  }
  Write-BackupLog ("Archive readable: entries={0} dbname={1} encryption={2}" -f $entries.Count, $(if ($archiveDb) { $archiveDb } else { 'unknown' }), $encryptionMode)

  $migrationRows = @(Get-MigrationRowsFromArchive -ArchivePath $plainArchive)
  $migrationHead = Get-MigrationHead -Rows $migrationRows
  $migrationCount = $migrationRows.Count
  if ($migrationCount -eq 0) {
    throw 'No migrations found in __EFMigrationsHistory inside the archive; this is not a usable DocGen backup.'
  }
  Write-BackupLog ("Migrations in archive: count={0} head={1}" -f $migrationCount, $migrationHead)
  if ($ExpectedHead -and $migrationHead -ne $ExpectedHead) {
    throw ("Migration head drift: archive has '{0}' but '{1}' was expected." -f $migrationHead, $ExpectedHead)
  }
  Write-BackupLog 'Backup verification passed (no restore performed).'
}
catch {
  Write-BackupLog ("ERROR: " + $_.Exception.Message)
  exit 1
}
finally {
  if ($plainArchive -and $plainArchive -ne $File) {
    Remove-SensitiveFile -Path $plainArchive
  }
}
