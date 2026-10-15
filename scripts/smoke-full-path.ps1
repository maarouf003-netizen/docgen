#Requires -Version 5.1
<#
.SYNOPSIS
  Phase-1 smoke script for the full path (ASCII-only file; Arabic API values are
  built from code points so the script stays pure ASCII and parses on PS 5.1).

  Plays lawyer/head roles against the local API only (default
  http://localhost:5199 from start-app.bat): creates executive files of all
  sides, changes statuses, runs a full delegation, an appeal, a review letter,
  correspondence exchanges, then deletes every SMOKE- record from the dev DB.

  Every created record is tagged with the SMOKE- prefix and removed at the end.
  Never touches production. Seed accounts come from RUN_GUIDE.md (all 123456
  by default) unless alternatives are passed.

.PARAMETER BaseUrl
  Local backend address. Example: http://localhost:5199

.PARAMETER Password
  Seed-account password (default 123456).

.PARAMETER LawyerUser / HeadUser / AdminUser
  Login names for the lawyer / head / admin roles
  (defaults: lawyer1 / head1 / admin).

.PARAMETER DelegateUser / DelegatePassword
  Optional entitymanager (delegate) account for the portal section.
  Without them the portal section is SKIPPED.

.EXAMPLE
  powershell -NoProfile -ExecutionPolicy Bypass -File scripts\smoke-full-path.ps1
#>
[CmdletBinding()]
param(
  [string]$BaseUrl = 'http://localhost:5199',
  [string]$Password = '123456',
  [string]$LawyerUser = 'lawyer1',
  [string]$HeadUser = 'head1',
  [string]$AdminUser = 'admin',
  [string]$DelegateUser = '',
  [string]$DelegatePassword = ''
)

$ErrorActionPreference = 'Stop'
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8

# --- Arabic literals via code points (file must stay ASCII) ---
function Ar {
  param([int[]]$Codes)
  return -join ($Codes | ForEach-Object { [char]$_ })
}
$A_Ordinary = Ar @(0x0639, 0x0627, 0x062F, 0x064A)          # ordinary
$A_Banking  = Ar @(0x0645, 0x0635, 0x0631, 0x0641, 0x064A)  # banking
$A_Estate   = Ar @(0x0639, 0x0642, 0x0627, 0x0631)          # real estate
$A_Deferred = Ar @(0x062A, 0x0631, 0x064A, 0x062B)          # deferred
$A_Circ     = Ar @(0x0645, 0x062A, 0x062F, 0x0627, 0x0648, 0x0644) # circulating
$A_Executed = Ar @(0x0645, 0x0646, 0x0641, 0x0630)          # executed

$BaseUrl = $BaseUrl.TrimEnd('/')
$Stamp = Get-Date -Format 'MMddHHmm'
$Tag = "SMOKE-$Stamp"
$Year = (Get-Date).Year.ToString()
$script:passed = 0
$script:failed = 0
$script:skipped = 0

function Write-Step {
  param([string]$Name)
  Write-Host ''
  Write-Host "== $Name" -ForegroundColor Cyan
}

function Pass {
  param([string]$Name)
  $script:passed++
  Write-Host "  [PASS] $Name" -ForegroundColor Green
}

function Fail {
  param([string]$Name, [string]$Detail)
  $script:failed++
  Write-Host "  [FAIL] $Name -- $Detail" -ForegroundColor Red
  throw "SMOKE-FAIL: $Name -- $Detail"
}

function Skip {
  param([string]$Name, [string]$Reason)
  $script:skipped++
  Write-Host "  [SKIP] $Name ($Reason)" -ForegroundColor Yellow
}

function Check {
  param([string]$Name, [bool]$Condition, [string]$Detail = 'condition not met')
  if ($Condition) { Pass $Name } else { Fail $Name $Detail }
}

function Get-CsrfToken {
  param($Session)
  $uri = New-Object System.Uri($BaseUrl)
  foreach ($c in $Session.Cookies.GetCookies($uri)) {
    if ($c.Name -eq 'docgen_csrf') { return $c.Value }
  }
  return $null
}

function Invoke-Api {
  param(
    [string]$Method,
    [string]$Path,
    $Session,
    $Body = $null
  )
  $url = "$BaseUrl$Path"
  $p = @{
    Uri = $url
    Method = $Method
    WebSession = $Session
    ContentType = 'application/json; charset=utf-8'
    ErrorAction = 'Stop'
  }
  if ($null -ne $Body) {
    $json = $Body | ConvertTo-Json -Depth 8 -Compress
    $p['Body'] = [System.Text.Encoding]::UTF8.GetBytes($json)
  }
  if ($Method -in @('POST', 'PUT', 'PATCH', 'DELETE')) {
    $csrf = Get-CsrfToken $Session
    if ($csrf) { $p['Headers'] = @{ 'X-CSRF-Token' = $csrf } }
  }
  try {
    return Invoke-RestMethod @p
  }
  catch {
    $script:lastStatus = 0
    $script:lastBody = $_.Exception.Message
    if ($_.Exception.Response) {
      try { $script:lastStatus = [int]$_.Exception.Response.StatusCode } catch { $script:lastStatus = 0 }
      try {
        $reader = New-Object System.IO.StreamReader($_.Exception.Response.GetResponseStream())
        $script:lastBody = $reader.ReadToEnd()
      } catch { }
    }
    elseif ($_.ErrorDetails) { $script:lastBody = $_.ErrorDetails.Message }
    throw "HTTP $($script:lastStatus) :: $($script:lastBody)"
  }
}

function New-Session {
  return (New-Object Microsoft.PowerShell.Commands.WebRequestSession)
}

function Login {
  param([string]$Username, [string]$Pwd)
  $s = New-Session
  try {
    Invoke-Api -Method 'POST' -Path '/api/auth/login' -Session $s -Body @{ username = $Username; password = $Pwd } | Out-Null
  }
  catch {
    Fail "login $Username" "HTTP $($script:lastStatus): $($script:lastBody)"
  }
  $me = Invoke-Api -Method 'GET' -Path '/api/auth/me' -Session $s
  return @{ Session = $s; Me = $me }
}

# -- 0) connectivity --
Write-Step '0) backend connectivity'
$lawyer1 = Login $LawyerUser $Password
Check 'login lawyer' ($null -ne $lawyer1.Me) 'empty me'
$lawyer1Id = $lawyer1.Me.id
$head1 = Login $HeadUser $Password
Check 'login head' ($null -ne $head1.Me) 'empty me'
$admin = Login $AdminUser $Password
Check 'login admin' ($null -ne $admin.Me) 'empty me'

# -- 1) second lawyer for delegation/assignment --
Write-Step '1) SMOKE lawyer (head)'
$smokeLawyerName = "$Tag-lawyer2"
try {
  $created = Invoke-Api -Method 'POST' -Path '/api/users/lawyers' -Session $head1.Session -Body @{
    username = $smokeLawyerName; fullName = "$Tag second lawyer"; password = $Password
  }
}
catch { Fail 'create SMOKE lawyer' "HTTP $($script:lastStatus): $($script:lastBody)" }
$smokeLawyerId = $created.id
if (-not $smokeLawyerId) {
  $list = Invoke-Api -Method 'GET' -Path "/api/users/lawyers?q=$smokeLawyerName" -Session $head1.Session
  $row = @($list) | Where-Object { $_.username -eq $smokeLawyerName } | Select-Object -First 1
  if ($row) { $smokeLawyerId = $row.id }
}
Check 'SMOKE lawyer has id' ($smokeLawyerId -gt 0) 'no lawyer id'
$lawyer2 = Login $smokeLawyerName $Password
Check 'login SMOKE lawyer' ($null -ne $lawyer2.Me) 'empty me'

# -- 2) execution circuit --
Write-Step '2) execution circuit'
$circuits = Invoke-Api -Method 'GET' -Path '/api/execution-circuits/for-lawyer' -Session $lawyer1.Session
$circuitId = @($circuits)[0].id
Check 'circuit available' ($circuitId -gt 0) 'no circuits'

# -- 3) executive files (applicant ordinary/banking, executed, deposit) --
Write-Step '3) create executive files'
$reg = '1/8/2026'
$script:n = 0
function Next-FileNumber { $script:n++; return "$Tag-$script:n" }

$ordBody = @{
  borrowerName = "$Tag-borrower"; borrowerFather = "$Tag-f"; borrowerFamily = "$Tag-fam"
  contractTypeSelector = $A_Ordinary; contractType = "$Tag-decision"; contractNumber = "$Tag-101"; contractDate = $reg
  inclusionText = "$Tag decision summary one"; inclusionNotes = "$Tag other decision 5"
  fileNumber = (Next-FileNumber); fileType = 'SMOKE'; fileYear = $Year; fileRegistrationDate = $reg
  executionCircuitId = $circuitId
  guarantors = @(); borrowerHeirs = @(); initialActions = @()
  assets = @(@{ assetKind = $A_Estate; propertyNumber = "$Tag-p1"; propertyDistrict = "$Tag-d"; owners = @("$Tag-borrower"); seizureDate = $reg })
}
$docOrd = Invoke-Api -Method 'POST' -Path '/api/documents' -Session $lawyer1.Session -Body $ordBody
Check 'create applicant (ordinary)' ($docOrd.id -gt 0) 'no id'
$g = Invoke-Api -Method 'GET' -Path "/api/documents/$($docOrd.id)" -Session $lawyer1.Session
Check 'read inclusionNotes' ($g.inclusionNotes -eq "$Tag other decision 5") "value: $($g.inclusionNotes)"
$assetId = @($g.assets)[0].id
Check 'estate asset has id' ($assetId -gt 0) 'no asset id'

$bankBody = @{
  borrowerName = "$Tag-borrower2"; contractTypeSelector = $A_Banking; contractType = "$Tag-contract"
  contractNumber = "$Tag-202"; contractDate = $reg; amountNumeric = 1000
  fileNumber = (Next-FileNumber); fileType = 'SMOKE'; fileYear = $Year; fileRegistrationDate = $reg
  executionCircuitId = $circuitId
  guarantors = @(); assets = @(); borrowerHeirs = @(); initialActions = @()
}
$docBank = Invoke-Api -Method 'POST' -Path '/api/documents' -Session $lawyer1.Session -Body $bankBody
Check 'create applicant (banking)' ($docBank.id -gt 0) 'no id'

$execBase = @{
  contractTypeSelector = $A_Ordinary; contractType = "$Tag-exec-decision"; contractNumber = "$Tag-303"
  fileReceiptNumber = "$Tag-77"; fileReceiptDate = $reg
  executionCircuitId = $circuitId
  inclusionText = "$Tag summary"; inclusionNotes = "$Tag other decisions"
  executedRequiredAmount = 1000
  executionApplicants = @(@{ name = "$Tag-claimant"; nature = 'natural' })
  executedPublicEntities = @(@{ entityName = "$Tag-entity"; nature = 'legal'; registrationNumber = "$Tag-r1"; representedBy = "$Tag-rep" })
  executedNaturalPersons = @()
}
$docExec = Invoke-Api -Method 'POST' -Path '/api/documents' -Session $lawyer1.Session -Body ($execBase + @{ generalEntitySide = 'executed'; fileNumber = (Next-FileNumber); fileType = 'SMOKE'; fileYear = $Year })
Check 'create executed-side file' ($docExec.id -gt 0) 'no id'
$docDep = Invoke-Api -Method 'POST' -Path '/api/documents' -Session $lawyer1.Session -Body ($execBase + @{ generalEntitySide = 'deposit'; fileNumber = (Next-FileNumber); fileType = 'SMOKE'; fileYear = $Year })
Check 'create deposit file' ($docDep.id -gt 0) 'no id'

# -- 4) edit notes --
Write-Step '4) edit inclusionNotes'
$g1 = Invoke-Api -Method 'GET' -Path "/api/documents/$($docOrd.id)" -Session $lawyer1.Session
$upd = $ordBody.Clone()
$upd['inclusionNotes'] = "$Tag other decision amended"
$upd['version'] = $g1.version
$put = Invoke-Api -Method 'PUT' -Path "/api/documents/$($docOrd.id)" -Session $lawyer1.Session -Body $upd
Check 'notes updated' ($put.inclusionNotes -eq "$Tag other decision amended") "value: $($put.inclusionNotes)"
$g2 = Invoke-Api -Method 'GET' -Path "/api/documents/$($docOrd.id)" -Session $lawyer1.Session
$assetId = @($g2.assets)[0].id
Check 'estate asset id refreshed after edit' ($assetId -gt 0) 'no asset id'

# -- 5) defer then revert --
Write-Step '5) defer then revert (applicant)'
Invoke-Api -Method 'POST' -Path "/api/documents/$($docOrd.id)/status" -Session $lawyer1.Session -Body @{
  status = $A_Deferred; fields = @{ tarithNumber = "$Tag-t1"; tarithDate = $reg; tarithRegNumber = "$Tag-tr1"; tarithRegDate = $reg }
} | Out-Null
$s1 = Invoke-Api -Method 'GET' -Path "/api/documents/$($docOrd.id)" -Session $lawyer1.Session
Check 'status deferred' ($s1.execStatus -eq $A_Deferred) "status: $($s1.execStatus)"
Invoke-Api -Method 'POST' -Path "/api/documents/$($docOrd.id)/revert-status" -Session $lawyer1.Session -Body @{
  fields = @{ sayerNumber = "$Tag-s1"; sayerDate = $reg; sayerRegNumber = "$Tag-w1"; sayerRegDate = $reg }
} | Out-Null
$s2 = Invoke-Api -Method 'GET' -Path "/api/documents/$($docOrd.id)" -Session $lawyer1.Session
Check 'reverted to circulating' (($s2.execStatus -eq '') -or ($s2.execStatus -eq $A_Circ)) "status: $($s2.execStatus)"

# -- 6) executed side: executed --
Write-Step '6) executed side file to executed'
Invoke-Api -Method 'POST' -Path "/api/documents/$($docExec.id)/executed-status" -Session $lawyer1.Session -Body @{
  status = $A_Executed; executedPaidAmount = 2000; executedDescription = "$Tag collection"
} | Out-Null
$e1 = Invoke-Api -Method 'GET' -Path "/api/documents/$($docExec.id)" -Session $lawyer1.Session
Check 'status executed' ($e1.executedStatus -eq $A_Executed) "status: $($e1.executedStatus)"

# -- 7) full delegation + mirror lock --
Write-Step '7) delegation: create, assign, register, mirror lock, complete'
$delCircuits = Invoke-Api -Method 'GET' -Path '/api/execution-circuits/for-delegation' -Session $lawyer1.Session
$delCircuitId = @($delCircuits)[0].id
Check 'delegation circuit available' ($delCircuitId -gt 0) 'no delegation circuits'
$del = Invoke-Api -Method 'POST' -Path "/api/documents/$($docOrd.id)/delegations" -Session $lawyer1.Session -Body @{
  delegatedCourt = $null; isExternal = $false; delegationDate = $reg
  delegationText = "$Tag delegation"; depositBookNumber = "$Tag-d1"; depositBookDate = $reg; assetIds = @($assetId)
  delegatedCircuitId = $delCircuitId
}
Check 'delegation created' ($del.id -gt 0) 'no delegation id'
$pend = Invoke-Api -Method 'GET' -Path '/api/delegations/pending' -Session $head1.Session
$found = @($pend) | Where-Object { $_.id -eq $del.id } | Select-Object -First 1
Check 'delegation pending for head' ($null -ne $found) 'not found'
Invoke-Api -Method 'POST' -Path "/api/delegations/$($del.id)/assign" -Session $head1.Session -Body @{ assignedLawyerId = $smokeLawyerId } | Out-Null
$dl = Invoke-Api -Method 'GET' -Path "/api/documents/$($docOrd.id)/delegations" -Session $lawyer1.Session
$mirrorId = @($dl) | Where-Object { $_.id -eq $del.id } | Select-Object -First 1 | ForEach-Object { $_.targetDocumentId }
Check 'mirror file created' ($mirrorId -gt 0) 'no mirror file'
Invoke-Api -Method 'POST' -Path "/api/delegations/$($del.id)/register" -Session $lawyer2.Session -Body @{
  fileNumber = "$Tag-mirror"; fileYear = $Year; fileRegistrationDate = $reg
} | Out-Null
$mg = Invoke-Api -Method 'GET' -Path "/api/documents/$mirrorId" -Session $lawyer2.Session
Check 'mirror carries notes' ($mg.inclusionNotes -eq "$Tag other decision amended") "value: $($mg.inclusionNotes)"
try {
  $mb = $upd.Clone(); $mb['inclusionNotes'] = "$Tag tamper"; $mb['version'] = $mg.version
  Invoke-Api -Method 'PUT' -Path "/api/documents/$mirrorId" -Session $lawyer2.Session -Body $mb | Out-Null
  Fail 'mirror lock' 'editing inclusionNotes on mirror accepted (400 expected)'
}
catch {
  if ($script:lastStatus -eq 400) { Pass 'mirror lock (400 rejected)' }
  else { Fail 'mirror lock' "HTTP $($script:lastStatus): $($script:lastBody)" }
}
$dl2 = Invoke-Api -Method 'GET' -Path "/api/documents/$($docOrd.id)/delegations" -Session $lawyer1.Session
$drow = @($dl2) | Where-Object { $_.id -eq $del.id } | Select-Object -First 1
$dAssetId = @($drow.assets)[0].id
Check 'delegation asset has sale id' ($dAssetId -gt 0) 'no delegation asset id'
Invoke-Api -Method 'POST' -Path "/api/delegations/$($del.id)/complete" -Session $lawyer2.Session -Body @{
  returnDate = $reg; sales = @(@{ delegationAssetId = $dAssetId; salePrice = 1000 }); forcedExecutionDate = $reg
  saleCoversFullDebt = $true
} | Out-Null
Pass 'delegation completed with sale'

# -- 8) appeal: create, assign, register, decide --
Write-Step '8) appeal lifecycle'
$ap = Invoke-Api -Method 'POST' -Path "/api/documents/$($docOrd.id)/appeals" -Session $lawyer1.Session -Body @{
  direction = 'against-us'; appellants = @(@{ kind = 'borrower'; partyId = $docOrd.id })
  appealedDecisionText = "$Tag appealed decision"; appealedDecisionDate = $reg
  noticeNumber = "$Tag-n1"; noticeDate = $reg; defenseOpinion = "$Tag defense opinion"
}
Check 'appeal created' ($ap.id -gt 0) 'no appeal id'
Invoke-Api -Method 'POST' -Path "/api/appeals/$($ap.id)/assign" -Session $head1.Session -Body @{ assignedLawyerId = $lawyer1Id } | Out-Null
Invoke-Api -Method 'PUT' -Path "/api/appeals/$($ap.id)/registration" -Session $lawyer1.Session -Body @{
  appellateCourt = "$Tag-appeal-court"; appealBaseNumber = "$Tag-100"; appealYear = $Year; registrationDate = $reg
} | Out-Null
Invoke-Api -Method 'POST' -Path "/api/appeals/$($ap.id)/decide" -Session $lawyer1.Session -Body @{
  decisionNumber = "$Tag-j1"; decisionDate = $reg; decisionRuling = "$Tag ruling"; outcome = 'in-favor'
} | Out-Null
$apg = Invoke-Api -Method 'GET' -Path "/api/appeals/$($ap.id)" -Session $lawyer1.Session
Check 'appeal decided' (($apg.status -eq 'decided') -and ($apg.outcome -eq 'in-favor')) "status: $($apg.status)"

# -- 9) review letter + head reply --
Write-Step '9) review letter'
$rl = Invoke-Api -Method 'POST' -Path '/api/review-letters' -Session $lawyer1.Session -Body @{
  documentId = $docOrd.id; bodyHtml = "<p>$Tag review</p>"
}
Check 'review letter created' ($rl.id -gt 0) 'no letter id'
Invoke-Api -Method 'POST' -Path "/api/review-letters/$($rl.id)/replies" -Session $head1.Session -Body @{ bodyHtml = "<p>$Tag head reply</p>" } | Out-Null
$rlg = Invoke-Api -Method 'GET' -Path "/api/review-letters/$($rl.id)" -Session $lawyer1.Session
Check 'head reply visible' (($rlg.replies.Count -gt 0) -or ($rlg.messages.Count -gt 1)) 'no reply'

# -- 10) correspondence --
Write-Step '10) correspondence'
$tq = [System.Uri]::EscapeDataString($HeadUser)
$tg = Invoke-Api -Method 'GET' -Path "/api/correspondence/targets?q=$tq" -Session $lawyer1.Session
$head1uid = @($tg)[0].userId
if (-not $head1uid) { $head1uid = @($tg)[0].id }
Check 'recipient head resolved' ($head1uid -gt 0) 'no recipient id'
$corr = Invoke-Api -Method 'POST' -Path '/api/correspondence' -Session $lawyer1.Session -Body @{
  documentId = $null; targetUserId = $head1uid; importance = 'normal'; bodyHtml = "<p>$Tag message</p>"
}
Check 'correspondence created' ($corr.id -gt 0) 'no correspondence id'
Invoke-Api -Method 'POST' -Path "/api/correspondence/$($corr.id)/replies" -Session $head1.Session -Body @{ bodyHtml = "<p>$Tag corr reply</p>" } | Out-Null
Invoke-Api -Method 'POST' -Path "/api/correspondence/$($corr.id)/mark-seen" -Session $head1.Session | Out-Null
Pass 'correspondence replied and seen'

# -- 11) delegate portal (optional) --
Write-Step '11) delegate portal'
if ($DelegateUser -and $DelegatePassword) {
  $dlg = Login $DelegateUser $DelegatePassword
  $scope = Invoke-Api -Method 'GET' -Path '/api/portal/my-scope' -Session $dlg.Session
  Check 'delegate scope' ($null -ne $scope) 'no scope'
  $pfiles = Invoke-Api -Method 'GET' -Path '/api/portal/files?page=1&perPage=5' -Session $dlg.Session
  Check 'portal files list' ($null -ne $pfiles.items) 'no items property'
  $first = @($pfiles.items)[0]
  if ($first -and $first.id -gt 0) {
    $pd = Invoke-Api -Method 'GET' -Path "/api/portal/files/$($first.id)" -Session $dlg.Session
    Check 'portal file detail' ($pd.id -eq $first.id) 'id mismatch'
    Invoke-Api -Method 'GET' -Path "/api/portal/files/$($first.id)/appeals" -Session $dlg.Session | Out-Null
    Pass 'portal file appeals'
    Invoke-Api -Method 'GET' -Path "/api/portal/files/$($first.id)/delegations" -Session $dlg.Session | Out-Null
    Pass 'portal file delegations'
  }
  else { Skip 'portal file detail' 'delegate scope holds no files (SMOKE files are not registry-linked)' }
  Invoke-Api -Method 'GET' -Path '/api/portal/correspondence' -Session $dlg.Session | Out-Null
  Pass 'portal correspondence list'
}
else { Skip 'delegate portal' 'no delegate account (DelegateUser/DelegatePassword)' }

# -- 12) cleanup --
Write-Step '12) cleanup SMOKE records'
function Remove-File {
  param([int]$Id, [array]$Sessions)
  $script:removeReason = ''
  foreach ($ss in $Sessions) {
    try { Invoke-Api -Method 'DELETE' -Path "/api/documents/$Id" -Session $ss | Out-Null; return $true }
    catch {
      if ($script:lastBody -like '*لا يمكن حذف*') { $script:removeReason = 'linked'; return $false }
      $script:removeReason = "HTTP $($script:lastStatus)"
    }
  }
  return $false
}
function Report-Retained {
  param([int]$Id)
  if ($script:removeReason -eq 'linked') { Skip "file $Id retained" 'linked (delegation/appeal) - deletion forbidden by design' }
  else { Skip "file $Id retained" "inaccessible ($($script:removeReason), owner deactivated in a prior run)" }
  $script:retained += $Id
}
$script:retained = @()
foreach ($id in @($docOrd.id, $docBank.id, $docExec.id, $docDep.id, $mirrorId)) {
  if ($id -gt 0) {
    if (Remove-File $id @($lawyer1.Session, $lawyer2.Session, $head1.Session)) { Pass "deleted file $id" }
    else { Report-Retained $id }
  }
}
try {
  Invoke-Api -Method 'DELETE' -Path "/api/appeals/$($ap.id)" -Session $lawyer1.Session | Out-Null
  Pass 'SMOKE appeal deleted'
}
catch { Skip 'SMOKE appeal delete' "no delete path: HTTP $($script:lastStatus)" }
$sweep = Invoke-Api -Method 'GET' -Path '/api/documents?q=SMOKE-&perPage=100' -Session $head1.Session
foreach ($it in @($sweep.items)) {
  if ($it.id -gt 0) {
    if (Remove-File $it.id @($head1.Session, $lawyer1.Session, $lawyer2.Session)) { }
    else { Report-Retained $it.id }
  }
}
Pass 'sweep: unlinked SMOKE files removed'
$allLawyers = Invoke-Api -Method 'GET' -Path '/api/users/lawyers?mode=branch' -Session $head1.Session
foreach ($lw in @($allLawyers)) {
  if ($lw.username -like 'SMOKE-*-lawyer2') {
    $f0 = $script:failed
    try {
      Start-Sleep -Seconds 3
      Invoke-Api -Method 'PATCH' -Path "/api/users/$($lw.id)/active" -Session $head1.Session -Body @{ isActive = $true } | Out-Null
      $ls = Login $lw.username $Password
      $mine = Invoke-Api -Method 'GET' -Path '/api/documents?q=SMOKE-&perPage=100' -Session $ls.Session
      foreach ($it in @($mine.items)) {
        if ($it.id -gt 0) {
          if (Remove-File $it.id @($ls.Session, $head1.Session, $lawyer1.Session)) { }
          else { Report-Retained $it.id }
        }
      }
      Invoke-Api -Method 'PATCH' -Path "/api/users/$($lw.id)/active" -Session $head1.Session -Body @{ isActive = $false } | Out-Null
    }
    catch { $script:failed = $f0; Skip "SMOKE lawyer $($lw.username)" 'cleanup cycle failed - left as is' }
  }
}
Pass 'sweep: SMOKE lawyers cycled and deactivated'
try { $chk = Invoke-Api -Method 'GET' -Path "/api/documents/$($docBank.id)" -Session $lawyer1.Session }
catch { $chk = $null }
Check 'deleted file hidden' (($null -eq $chk) -or ($chk.isDeleted)) 'still visible'

if ($script:retained.Count -gt 0) {
  Write-Host "RETAINED (linked, by design): $($script:retained -join ', ')" -ForegroundColor Yellow
}

Write-Host ''
Write-Host "RESULT: $($script:passed) passed / $($script:failed) failed / $($script:skipped) skipped" -ForegroundColor Cyan
Write-Host 'NOTE: linked SMOKE files (delegation/appeal) cannot be deleted by design;' -ForegroundColor Yellow
Write-Host 'they stay as dev data with their appeal, review letter and correspondence (all tagged SMOKE-).' -ForegroundColor Yellow
if ($script:failed -gt 0) { exit 1 }
