#Requires -Version 5.1
<#
.SYNOPSIS
  P5-06 - Restores a PostgreSQL custom-format backup (full or a single table) with
  explicit safety guards: checksum verification, -Yes confirmation, production refusal,
  empty-target preflight, single-transaction restore, and a post-restore smoke query.

.DESCRIPTION
  Connection comes ONLY from POSTGRES_CONNECTION (target host/credentials). The target
  database must be named explicitly with -TargetDb. The checksum is verified before any
  write, and archive readability is checked with pg_restore --list.

  A target whose database/host looks like production, or whose host+database equals the
  backup source recorded in the .meta.json sidecar, is refused unless -AllowProduction.

  -WhatIf validates everything and prints the plan without writing.
  -SelfTest runs offline guard/checksum tests (no database or PostgreSQL tools).

.EXAMPLE
  $env:POSTGRES_CONNECTION = 'postgresql://<user>@db.example.test:5432/postgres?sslmode=require'
  powershell -NoProfile -File scripts\backup\pg-restore.ps1 -File D:\backups\docgen.dump -TargetDb docgen_staging -Yes

.EXAMPLE
  powershell -NoProfile -File scripts\backup\pg-restore.ps1 -File D:\backups\docgen.dump -TargetDb docgen_staging -Table Documents -Yes
#>
[CmdletBinding()]
param(
  [string]$File,
  [string]$TargetDb,
  [string]$Table = '',
  [string]$ChecksumFile = '',
  [switch]$Yes,
  [switch]$AllowProduction,
  [switch]$AllowNonEmpty,
  [switch]$TruncateTable,
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

function Assert-TargetDatabaseName {
  param([string]$Name)
  if ([string]::IsNullOrWhiteSpace($Name)) {
    Fail-Backup '-TargetDb is required (an explicit database name).'
  }
  if ($Name.Length -gt 63) {
    Fail-Backup '-TargetDb is longer than 63 characters.'
  }
  if ($Name -notmatch '^[A-Za-z0-9_][A-Za-z0-9_.-]*$') {
    Fail-Backup '-TargetDb contains unsupported characters (allowed: letters, digits, _, ., -).'
  }
  if ($Name.ToLowerInvariant() -in @('template0', 'template1')) {
    Fail-Backup 'Refusing to target a template database.'
  }
}

function Assert-TableName {
  param([string]$Name)
  if ($Name -notmatch '^[A-Za-z_][A-Za-z0-9_]*(\.[A-Za-z_][A-Za-z0-9_]*)?$') {
    Fail-Backup '-Table must be a simple identifier or schema.identifier.'
  }
}

function Get-QuotedTableSql {
  param([string]$Name)
  if ($Name.Contains('.')) {
    $parts = $Name.Split('.', 2)
    return ('"' + $parts[0] + '"."' + $parts[1] + '"')
  }
  return ('"public"."' + $Name + '"')
}

function Get-MetadataValue {
  param([string]$Path, [string]$Property)
  if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
    return ''
  }
  try {
    $meta = Get-Content -LiteralPath $Path -Raw | ConvertFrom-Json
    $propertyObject = $meta.source
    if ($null -eq $propertyObject) {
      return ''
    }
    $value = $propertyObject.$Property
    if ($null -eq $value) {
      return ''
    }
    return [string]$value
  }
  catch {
    return ''
  }
}

function Invoke-PsqlQuery {
  param([string]$Sql)
  # SQL is piped on stdin (not argv) so quoted identifiers survive PowerShell 5.1
  # native argument quoting and any .cmd/cmd.exe wrapper.
  $result = $Sql | & psql --no-password --no-psqlrc --set=ON_ERROR_STOP=1 `
    --tuples-only --no-align --dbname=$TargetDb
  if ($LASTEXITCODE -ne 0) {
    throw "psql query failed: $Sql"
  }
  return (($result | Out-String).Trim())
}

function Invoke-FullRestoreSmoke {
  param([string]$ArchiveHead)
  $tables = Invoke-PsqlQuery "SELECT count(*) FROM information_schema.tables WHERE table_schema='public' AND table_type='BASE TABLE';"
  $tableCount = 0
  if (-not [int]::TryParse($tables, [ref]$tableCount) -or $tableCount -lt 1) {
    Fail-Backup 'Post-restore smoke check failed: no tables in public schema.'
  }

  $historyRaw = Invoke-PsqlQuery 'SELECT count(*) FROM public."__EFMigrationsHistory";'
  $history = 0
  if (-not [int]::TryParse($historyRaw, [ref]$history) -or $history -lt 1) {
    Fail-Backup 'Post-restore smoke check failed: __EFMigrationsHistory is missing or empty.'
  }

  $head = Invoke-PsqlQuery 'SELECT "MigrationId" FROM public."__EFMigrationsHistory" ORDER BY "MigrationId" DESC LIMIT 1;'
  if ($head -ne $ArchiveHead) {
    Fail-Backup ("Post-restore smoke check failed: migration head '{0}' != archive head '{1}'." -f $head, $ArchiveHead)
  }

  $documentsPresent = Invoke-PsqlQuery "SELECT to_regclass('public.""Documents""') IS NOT NULL;"
  if ($documentsPresent -ne 't') {
    Fail-Backup 'Post-restore smoke check failed: Documents table is missing.'
  }
  $documentsRows = Invoke-PsqlQuery 'SELECT count(*) FROM public."Documents";'

  Write-BackupLog ("Smoke OK: tables={0} history={1} head={2} documents={3}" -f $tableCount, $history, $head, $documentsRows)
}

function Invoke-PartialRestoreSmoke {
  param([string]$TableName)
  $quoted = Get-QuotedTableSql -Name $TableName
  $exists = Invoke-PsqlQuery ("SELECT to_regclass('{0}') IS NOT NULL;" -f $quoted)
  if ($exists -ne 't') {
    Fail-Backup ("Post-restore smoke check failed: table '{0}' is missing." -f $TableName)
  }
  $rows = Invoke-PsqlQuery ("SELECT count(*) FROM {0};" -f $quoted)

  $historyRaw = Invoke-PsqlQuery 'SELECT count(*) FROM public."__EFMigrationsHistory";'
  $history = 0
  if (-not [int]::TryParse($historyRaw, [ref]$history) -or $history -lt 1) {
    Fail-Backup 'Partial restore target must be an initialized DocGen database (empty __EFMigrationsHistory).'
  }
  $head = Invoke-PsqlQuery 'SELECT "MigrationId" FROM public."__EFMigrationsHistory" ORDER BY "MigrationId" DESC LIMIT 1;'
  Write-BackupLog ("Partial smoke OK: table={0} rows={1} history={2} head={3}" -f $TableName, $rows, $history, $head)
}

function Test-ScriptSelfTest {
  $tempDir = Join-Path ([System.IO.Path]::GetTempPath()) ('pg-restore-selftest-' + [guid]::NewGuid().ToString('N'))
  $savedConnection = $env:POSTGRES_CONNECTION
  try {
    New-Item -ItemType Directory -Path $tempDir -Force | Out-Null
    Remove-Item Env:POSTGRES_CONNECTION -ErrorAction SilentlyContinue

    Assert-SelfTestEqual 'prod db' 'True' ([string](Test-ProductionLike -HostName 'db.example.test' -DatabaseName 'docgen_prod'))
    Assert-SelfTestEqual 'postgres default db' 'True' ([string](Test-ProductionLike -HostName 'db.example.test' -DatabaseName 'postgres'))
    Assert-SelfTestEqual 'prod host' 'True' ([string](Test-ProductionLike -HostName 'db.prod.internal' -DatabaseName 'docgen'))
    Assert-SelfTestEqual 'staging db' 'False' ([string](Test-ProductionLike -HostName 'staging.example.test' -DatabaseName 'docgen_staging'))

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

    Assert-TargetDatabaseName 'docgen_staging'
    foreach ($invalid in @('bad name;drop', 'template0', '')) {
      $rejected = $false
      try {
        Assert-TargetDatabaseName $invalid
      }
      catch {
        $rejected = $true
      }
      if (-not $rejected) {
        Write-Host ("SELF-TEST FAIL: invalid target database accepted: '{0}'" -f $invalid) -ForegroundColor Red
        $script:SelfTestFailures += 1
      }
    }
    $tableRejected = $false
    try {
      Assert-TableName 'Documents; DROP TABLE x'
    }
    catch {
      $tableRejected = $true
    }
    if (-not $tableRejected) {
      Write-Host 'SELF-TEST FAIL: unsafe table name accepted' -ForegroundColor Red
      $script:SelfTestFailures += 1
    }
    Assert-SelfTestEqual 'quoted public table' '"public"."Documents"' (Get-QuotedTableSql -Name 'Documents')
    Assert-SelfTestEqual 'quoted schema table' '"audit"."Log"' (Get-QuotedTableSql -Name 'audit.Log')

    $metaPath = Join-Path $tempDir 'meta.json'
    $meta = [ordered]@{ source = [ordered]@{ host = 'db.example.test'; database = 'docgen' } }
    [System.IO.File]::WriteAllText($metaPath, ($meta | ConvertTo-Json -Depth 4))
    Assert-SelfTestEqual 'metadata host' 'db.example.test' (Get-MetadataValue -Path $metaPath -Property 'host')
    Assert-SelfTestEqual 'metadata database' 'docgen' (Get-MetadataValue -Path $metaPath -Property 'database')
    Assert-SelfTestEqual 'metadata missing key' '' (Get-MetadataValue -Path $metaPath -Property 'absent')

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

    Assert-SelfTestEqual 'gpg detection' 'gpg' (Get-ArchiveEncryptionMode -Path 'x.dump.gpg')
    Assert-SelfTestEqual 'openssl detection' 'openssl' (Get-ArchiveEncryptionMode -Path 'x.dump.enc')
    Assert-SelfTestEqual 'plain detection' 'none' (Get-ArchiveEncryptionMode -Path 'x.dump')

    if ($script:SelfTestFailures -ne 0) {
      Write-Host ("pg-restore.ps1 self-test: FAILED ({0})" -f $script:SelfTestFailures) -ForegroundColor Red
      return 1
    }
    Write-Host 'pg-restore.ps1 self-test: OK'
    return 0
  }
  finally {
    $env:POSTGRES_CONNECTION = $savedConnection
    Remove-Item -LiteralPath $tempDir -Recurse -Force -ErrorAction SilentlyContinue
  }
}

if ($SelfTest) {
  exit (Test-ScriptSelfTest)
}

$plainArchive = $null
try {
  if ([string]::IsNullOrWhiteSpace($File)) {
    Fail-Backup '-File is required.'
  }
  Assert-TargetDatabaseName -Name $TargetDb
  if ($Table) {
    Assert-TableName -Name $Table
  }
  if ($TruncateTable -and -not $Table) {
    Fail-Backup '-TruncateTable requires -Table.'
  }
  if (-not (Test-Path -LiteralPath $File -PathType Leaf)) {
    Fail-Backup "Backup artifact not found: $File"
  }
  if ([string]::IsNullOrWhiteSpace($ChecksumFile)) {
    $ChecksumFile = $File + '.sha256'
  }
  if (-not (Test-Path -LiteralPath $ChecksumFile -PathType Leaf)) {
    Fail-Backup "Checksum file not found: $ChecksumFile (refusing to restore an unverified backup)."
  }

  Resolve-PgConnection
  $script:PgDatabase = $TargetDb
  $env:PGDATABASE = $TargetDb
  Write-BackupLog ("Restore target: " + (Get-RedactedTarget))
  Write-BackupLog ("Archive: " + [System.IO.Path]::GetFileName($File))
  Require-PgTools -Tools @('pg_restore', 'psql')
  Require-Sha256Tool

  if (-not (Test-Checksum -File $File -ChecksumPath $ChecksumFile)) {
    Fail-Backup 'Checksum verification failed; refusing to restore.'
  }

  $encryptionMode = Get-ArchiveEncryptionMode -Path $File
  if ($encryptionMode -ne 'none' -and [string]::IsNullOrEmpty($env:BACKUP_ENCRYPTION_KEY)) {
    Fail-Backup "Artifact is encrypted ($encryptionMode) but BACKUP_ENCRYPTION_KEY is not set."
  }
  $plainArchive = Expand-BackupArchiveToTemp -Archive $File -Mode $encryptionMode
  & pg_restore --list $plainArchive > $null
  if ($LASTEXITCODE -ne 0) {
    Fail-Backup 'Archive is not readable by pg_restore (corrupt, wrong key, or not a custom-format dump).'
  }

  $migrationRows = @(Get-MigrationRowsFromArchive -ArchivePath $plainArchive)
  $archiveMigrationHead = Get-MigrationHead -Rows $migrationRows
  $archiveMigrationCount = $migrationRows.Count
  if ($archiveMigrationCount -eq 0) {
    Write-BackupWarning 'Archive contains no __EFMigrationsHistory rows; a full-restore smoke check will fail.'
  }
  else {
    Write-BackupLog ("Archive migrations: count={0} head={1}" -f $archiveMigrationCount, $archiveMigrationHead)
  }

  Assert-SafeTarget -HostName $script:PgHost -DatabaseName $TargetDb -AllowProduction:$AllowProduction
  $metadataPath = $File + '.meta.json'
  if (Test-Path -LiteralPath $metadataPath -PathType Leaf) {
    $sourceHost = (Get-MetadataValue -Path $metadataPath -Property 'host').ToLowerInvariant()
    $sourceDb = Get-MetadataValue -Path $metadataPath -Property 'database'
    if ($sourceHost -and $sourceHost -eq $script:PgHost.ToLowerInvariant() -and $sourceDb -eq $TargetDb) {
      if (-not $AllowProduction) {
        Fail-Backup ("Target is the same host+database the backup came from ({0}@{1}). Refusing to overwrite the source without -AllowProduction." -f $TargetDb, $script:PgHost)
      }
      Write-BackupWarning ("Restoring over the backup source ({0}@{1}) because -AllowProduction was supplied." -f $TargetDb, $script:PgHost)
    }
  }
  else {
    Write-BackupWarning ("Metadata file not found ({0}); source-overwrite detection is limited to name/host heuristics." -f $metadataPath)
  }

  $currentDb = Invoke-PsqlQuery 'SELECT current_database();'
  if ($currentDb -ne $TargetDb) {
    Fail-Backup ("Connected database '{0}' is not the requested target '{1}'." -f $currentDb, $TargetDb)
  }
  $targetTablesRaw = Invoke-PsqlQuery "SELECT count(*) FROM information_schema.tables WHERE table_schema='public' AND table_type='BASE TABLE';"
  $targetTables = 0
  if (-not [int]::TryParse($targetTablesRaw, [ref]$targetTables)) {
    Fail-Backup 'Target preflight query returned an unexpected table count.'
  }

  if ($Table) {
    $quotedTable = Get-QuotedTableSql -Name $Table
    $tablePresent = Invoke-PsqlQuery ("SELECT to_regclass('{0}') IS NOT NULL;" -f $quotedTable)
    if ($tablePresent -ne 't') {
      Fail-Backup ("Partial-restore target table '{0}' does not exist (create the schema first or use a full restore)." -f $Table)
    }
    Write-BackupLog ("Partial restore: table={0} target_tables={1} truncate={2}" -f $Table, $targetTables, $TruncateTable)
  }
  else {
    if ($targetTables -gt 0 -and -not $AllowNonEmpty) {
      Fail-Backup ("Target database '{0}' is not empty ({1} tables). Full restore requires an empty database or -AllowNonEmpty." -f $TargetDb, $targetTables)
    }
    Write-BackupLog ("Full restore: target_tables={0} allow_nonempty={1}" -f $targetTables, $AllowNonEmpty)
  }

  if (-not $WhatIf) {
    Assert-RestoreConfirmation -Confirmed $Yes.IsPresent
  }

  $operation = 'Full restore into {0}@{1}' -f $TargetDb, $script:PgHost
  if ($Table) {
    $operation = 'Partial data-only restore of {0} into {1}@{2}' -f $Table, $TargetDb, $script:PgHost
  }
  if ($WhatIf) {
    Write-BackupLog 'DRY-RUN: no restore will be performed.'
    Write-BackupLog ("Planned action: " + $operation)
    if ($TruncateTable) {
      Write-BackupLog ("Planned pre-step: TRUNCATE " + (Get-QuotedTableSql -Name $Table))
    }
    Write-BackupLog 'Checksum and archive readability verified; dry-run exiting 0.'
    return
  }

  $restoreArguments = @('--exit-on-error', '--single-transaction', '--no-owner', '--no-privileges', "--dbname=$TargetDb")
  if ($Table) {
    $restoreArguments += @('--data-only', "--table=$Table")
  }

  if ($TruncateTable) {
    Write-BackupLog ("Truncating target table {0} as explicitly requested..." -f (Get-QuotedTableSql -Name $Table))
    Invoke-PsqlQuery ("TRUNCATE TABLE {0};" -f (Get-QuotedTableSql -Name $Table)) | Out-Null
  }

  Write-BackupLog 'Restoring (single transaction, no owner/privilege replay)...'
  & pg_restore @restoreArguments $plainArchive
  if ($LASTEXITCODE -ne 0) {
    Fail-Backup 'pg_restore failed; the single transaction was rolled back (no partial restore).'
  }

  if ($Table) {
    Invoke-PartialRestoreSmoke -TableName $Table
  }
  else {
    Invoke-FullRestoreSmoke -ArchiveHead $archiveMigrationHead
  }

  Write-BackupLog 'Restore completed successfully.'
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
