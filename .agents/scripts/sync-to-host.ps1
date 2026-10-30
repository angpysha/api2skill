# sync-to-host.ps1 — project .agents/ into one or more agent hosts (pwsh).
# Every skill/agent/rule lives ONCE in .agents/; hosts get one RELATIVE symlink per entry
# (e.g. .claude/skills/spec-kit -> ../../.agents/skills/spec-kit). Host-only entries
# (user skills, Spec Kit commands, hooks, settings) are never modified or deleted.
#
# Usage: pwsh -File sync-to-host.ps1 [-Target DIR] [-AgentHost auto|all|manual|cursor,claude,...]
param(
    [string]$Target = (Get-Location).Path,
    [string]$AgentHost = 'auto'
)

. (Join-Path $PSScriptRoot 'lib/PipelineScripts.ps1')

$Target = (Resolve-Path $Target).Path
$Src = Join-Path $Target '.agents'

if (-not (Test-Path $Src)) {
    Write-Error "$Src not found — run 'agentic-tool install' first"
    exit 1
}

$Known = @('cursor', 'claude', 'windsurf', 'codex', 'agents-md', 'manual')

function Get-DetectedHosts {
    $hosts = [System.Collections.Generic.List[string]]::new()
    if (Test-Path (Join-Path $Target '.cursor')) { $hosts.Add('cursor') }
    if ((Test-Path (Join-Path $Target '.claude')) -or (Test-Path (Join-Path $Target 'CLAUDE.md'))) { $hosts.Add('claude') }
    if (Test-Path (Join-Path $Target '.windsurf')) { $hosts.Add('windsurf') }
    if ($hosts.Count -eq 0) { $hosts.Add('cursor') }
    return $hosts
}

function Get-ManifestHosts {
    $path = Join-Path $Target 'pipeline.manifest.json'
    if (-not (Test-Path $path)) { return @() }
    try {
        $m = Get-Content $path -Raw | ConvertFrom-Json
        if ($m.PSObject.Properties.Name -contains 'hosts' -and $m.hosts) { return @($m.hosts) }
        if ($m.PSObject.Properties.Name -contains 'host' -and $m.host) { return @($m.host) }
    }
    catch { }
    return @()
}

# Host layout: kind -> (dest subdir, extension mapping for rules)
$Layouts = @{
    cursor   = @{ root = '.cursor';   kinds = @(@('skills', '', ''), @('agents', '', ''), @('rules', '', '')) }
    claude   = @{ root = '.claude';   kinds = @(@('skills', '', ''), @('agents', '', ''), @('rules', '.mdc', '.md')) }
    windsurf = @{ root = '.windsurf'; kinds = @(@('skills', '', ''), @('agents', '', ''), @('rules', '.mdc', '.md')) }
}

function Invoke-HostSync {
    param([string]$Name)
    if (-not $Layouts.ContainsKey($Name)) {
        Write-Host "INFO $Name — no folder projection (reads AGENTS.md / .agents/ directly)"
        return
    }
    $layout = $Layouts[$Name]
    foreach ($kind in $layout.kinds) {
        $sub, $fromExt, $toExt = $kind
        $dest = Join-Path (Join-Path $Target $layout.root) $sub
        $s = Sync-LinkedEntries -SourceDir (Join-Path $Src $sub) -DestDir $dest -AgentsRoot $Src -FromExtension $fromExt -ToExtension $toExt
        $rel = [IO.Path]::GetRelativePath($Target, $dest)
        Write-Host ("sync -> {0,-18} linked={1} unchanged={2} deduped={3} kept={4} stale-removed={5} fallback={6}" -f `
                $rel, $s.linked, $s.unchanged, $s.replaced, $s.kept, $s.removed, $s.fallback)
    }
}

$hosts = switch ($AgentHost) {
    'auto' {
        $m = @(Get-ManifestHosts)
        if ($m.Count -gt 0) { $m } else { Get-DetectedHosts }
    }
    'all' { @('cursor', 'claude', 'windsurf') }
    default { $AgentHost.Split(',', [StringSplitOptions]::RemoveEmptyEntries) | ForEach-Object { $_.Trim().ToLowerInvariant() } }
}

foreach ($h in $hosts) {
    if ($h -notin $Known) { Write-Error "unknown host: $h"; exit 1 }
}
if ($hosts -contains 'manual') {
    Write-Host 'INFO manual host — no projection (.agents/ only)'
    exit 0
}

foreach ($h in ($hosts | Select-Object -Unique)) { Invoke-HostSync $h }
Write-Host "OK sync complete ($((@($hosts) | Select-Object -Unique) -join ','))"
