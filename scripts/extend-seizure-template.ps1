#Requires -Version 5.1
<#
.SYNOPSIS
  Expands the seizure table template (Seizure.docx, template 004) from 5
  debtors to 50 debtors (borrower + up to 49 guarantors) by cloning the
  guarantor_4 row with renumbered tokens. (ASCII-only file per repo rule;
  see smoke-full-path.ps1 header.)

.DESCRIPTION
  Template layout: one w:tbl whose rows are header + borrower + guarantor_1..4,
  each debtor row carrying seizure_date{n} + court_with_prefix{n} cells.
  The script clones the guarantor_4 row 45 times (guarantor_5..49 with
  seizure_date6..50) using fresh paragraph ids, so the table flows across
  multiple printed pages (owner-approved). Idempotent: exits unchanged when
  guarantor_5_name is already present.

  Single source of the cap: PartyLimitsCatalog (backend) + MAX_GUARANTORS
  (frontend documentFormConstants.ts).

.EXAMPLE
  powershell -ExecutionPolicy Bypass -File scripts/extend-seizure-template.ps1
#>
[CmdletBinding()]
param()

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$docxPath = Join-Path $repoRoot "backend/src/DocGenerator.Api/WordTemplates/Seizure.docx"
if (!(Test-Path -LiteralPath $docxPath)) { throw "Template not found: $docxPath" }

Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem

$readZip = [System.IO.Compression.ZipFile]::Open($docxPath, [System.IO.Compression.ZipArchiveMode]::Read)
try {
    $entry = $readZip.Entries | Where-Object { $_.FullName -eq "word/document.xml" } | Select-Object -First 1
    if ($null -eq $entry) { throw "word/document.xml missing inside template" }
    $reader = New-Object System.IO.StreamReader($entry.Open(), [System.Text.Encoding]::UTF8)
    try { $documentXml = $reader.ReadToEnd() }
    finally { $reader.Close() }
    $otherEntries = @{}
    foreach ($e in $readZip.Entries) {
        $ms = New-Object System.IO.MemoryStream
        $s = $e.Open()
        try { $s.CopyTo($ms) }
        finally { $s.Close() }
        $otherEntries[$e.FullName] = $ms.ToArray()
        $ms.Close()
    }
}
finally { $readZip.Dispose() }

if ($documentXml.Contains("guarantor_5_name")) {
    Write-Output "Template already expanded (guarantor_5_name present) - no change."
    exit 0
}

$xml = New-Object xml
$xml.PreserveWhitespace = $true
$xml.LoadXml($documentXml)
$ns = New-Object System.Xml.XmlNamespaceManager($xml.NameTable)
$ns.AddNamespace("w", "http://schemas.openxmlformats.org/wordprocessingml/2006/main")

$rows = $xml.SelectNodes("//w:tr", $ns)
$templateRow = $null
foreach ($row in $rows) {
    if ($row.InnerText.Contains("{{ guarantor_4_name }}")) { $templateRow = $row; break }
}
if ($null -eq $templateRow) { throw "Template row guarantor_4 not found" }
$table = $templateRow.ParentNode

# Namespace declarations from the document root for re-parsing cloned fragments.
$nsDecls = ""
foreach ($attr in $xml.DocumentElement.Attributes) {
    if ($attr.Prefix -eq "xmlns" -or $attr.Name -eq "xmlns") { $nsDecls += " $($attr.Name)=`"$($attr.Value)`"" }
}

$rng = New-Object System.Random
function New-HexId {
    $bytes = New-Object byte[] 4
    $rng.NextBytes($bytes)
    return (($bytes | ForEach-Object { $_.ToString("X2") }) -join "")
}

# Sequential insert: each clone right after its predecessor keeps order 5..49.
$anchor = $templateRow
for ($k = 5; $k -le 49; $k++) {
    $n = $k + 1  # visible debtor number (borrower = 1)
    $frag = $templateRow.OuterXml
    $frag = $frag.Replace("guarantor_4_", "guarantor_$($k)_")
    $frag = $frag.Replace("court_with_prefix5", "court_with_prefix$n")
    # NOTE: the {{seizure_date5}} token is split across runs ("{{seizure_date"+"5"+"}}"),
    # so the row holds exactly two bare "<w:t>5</w:t>" runs (visible index + token
    # middle) and both take the same new number.
    $indexCount = ([regex]::Matches($frag, "<w:t>5</w:t>")).Count
    if ($indexCount -ne 2) { throw "Bare-5 runs changed in template row (k=$k, count=$indexCount)" }
    $frag = $frag.Replace("<w:t>5</w:t>", "<w:t>$n</w:t>")
    $frag = [regex]::Replace($frag, 'w14:paraId="[0-9A-Fa-f]+"', { param($m) 'w14:paraId="' + (New-HexId) + '"' })
    $frag = [regex]::Replace($frag, 'w14:textId="[0-9A-Fa-f]+"', { param($m) 'w14:textId="' + (New-HexId) + '"' })

    $fragDoc = New-Object xml
    $fragDoc.LoadXml("<frag$nsDecls>$frag</frag>")
    $newRow = $xml.ImportNode($fragDoc.DocumentElement.FirstChild, $true)
    $anchor = $table.InsertAfter($newRow, $anchor)
}

$tmpPath = "$docxPath.tmp"
if (Test-Path -LiteralPath $tmpPath) { Remove-Item -LiteralPath $tmpPath -Force }
$writeZip = [System.IO.Compression.ZipFile]::Open($tmpPath, [System.IO.Compression.ZipArchiveMode]::Create)
try {
    foreach ($name in $otherEntries.Keys) {
        $ne = $writeZip.CreateEntry($name, [System.IO.Compression.CompressionLevel]::Optimal)
        $ds = $ne.Open()
        try {
            $payload = $otherEntries[$name]
            if ($name -eq "word/document.xml") {
                $payload = [System.Text.Encoding]::UTF8.GetBytes($xml.OuterXml)
            }
            $ds.Write($payload, 0, $payload.Length)
        }
        finally { $ds.Close() }
    }
}
finally { $writeZip.Dispose() }
Move-Item -LiteralPath $tmpPath -Destination $docxPath -Force

$rowCount = $table.SelectNodes("w:tr", $ns).Count
Write-Output "Expanded: table rows = $rowCount (expected 51)."
if ($rowCount -ne 51) { throw "Unexpected row count: $rowCount" }
