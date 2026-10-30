#!/usr/bin/env pwsh
<#
.SYNOPSIS
  List custom (non-catalog) agents from recommendation, manifest, and .agents/agents/.
.PARAMETER Target
  Repo root (default: cwd).
#>
param(
    [string]$Target = (Get-Location).Path
)

$ErrorActionPreference = 'Stop'
$core = @(
    'coordinator', 'ba-analyst', 'architect', 'team-lead', 'developer', 'tester',
    'security-reviewer', 'devops', 'tech-writer', 'stack-selector'
)
$coreSet = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
foreach ($c in $core) { [void]$coreSet.Add($c) }

$rows = [System.Collections.Generic.List[object]]::new()
$seen = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)

function Register-Custom([string]$name, [string]$source) {
    if ([string]::IsNullOrWhiteSpace($name)) { return }
    $n = $name.Trim()
    if ($coreSet.Contains($n)) { return }
    if (-not $seen.Add($n)) { return }
    $path = Join-Path $Target ".agents/agents/$n.md"
    $rows.Add([pscustomobject]@{
            Name   = $n
            Source = $source
            File   = $(if (Test-Path -LiteralPath $path) { $path } else { '(missing — create manually or extract instructions)' })
        }) | Out-Null
}

$recPath = Join-Path $Target '.agents/pipeline.recommendation.json'
if (Test-Path -LiteralPath $recPath) {
    $rec = Get-Content -LiteralPath $recPath -Raw | ConvertFrom-Json
    foreach ($a in @($rec.agents)) { Register-Custom ([string]$a.name) 'recommendation' }
}

$manPath = Join-Path $Target 'pipeline.manifest.json'
if (Test-Path -LiteralPath $manPath) {
    $man = Get-Content -LiteralPath $manPath -Raw | ConvertFrom-Json
    foreach ($a in @($man.agents)) { Register-Custom ([string]$a) 'manifest.agents' }
    foreach ($a in @($man.agents_extra)) { Register-Custom ([string]$a) 'manifest.agents_extra' }
}

$agentsDir = Join-Path $Target '.agents/agents'
if (Test-Path -LiteralPath $agentsDir) {
    Get-ChildItem -LiteralPath $agentsDir -Filter '*.md' -File | ForEach-Object {
        Register-Custom $_.BaseName ("file:{0}" -f $_.Name)
    }
}

if ($rows.Count -eq 0) {
    Write-Host 'OK no custom agents found'
    exit 0
}

Write-Host "Custom agents ($($rows.Count)) — run /agent-reconcile to map replacements or extract instructions:"
$rows | Sort-Object Name | Format-Table -AutoSize Name, Source, File
exit 0
