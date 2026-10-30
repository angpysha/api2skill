#!/usr/bin/env pwsh
# install-serena.ps1 — install Serena (oraios/serena) and wire it as the primary code-search MCP
# for one or more hosts. MCP config writes are ADD-ONLY: an existing 'serena' entry is kept.
[CmdletBinding()]
param(
    [string]$Target = (Get-Location).Path,
    [string]$Hosts = 'auto',          # auto | comma list of cursor,claude,windsurf
    [switch]$ConfigureMcp,
    [switch]$Index,                   # pre-index the project (serena project index)
    [switch]$Force,                   # reinstall even if 'serena' is on PATH
    [switch]$DryRun,
    [switch]$Help
)

$ErrorActionPreference = 'Stop'

if ($Help) {
    Write-Host @"
Usage: install-serena.ps1 [-Target DIR] [-Hosts auto|cursor,claude,windsurf] [-ConfigureMcp] [-Index] [-Force] [-DryRun]
Exit codes: 0 ok, 1 prerequisite missing (uv), 2 install/config failed.
"@
    exit 0
}

$Target = (Resolve-Path $Target).Path

# Per-host MCP file + launch args (Serena contexts: 'ide' for IDEs, 'claude-code' for Claude Code).
$McpSpecs = @{
    cursor   = @{ file = '.cursor/mcp.json';   args = @('start-mcp-server', '--context', 'ide', '--project', '${workspaceFolder}') }
    claude   = @{ file = '.mcp.json';          args = @('start-mcp-server', '--context', 'claude-code', '--project-from-cwd') }
    windsurf = @{ file = '.windsurf/mcp.json'; args = @('start-mcp-server', '--context', 'ide', '--project-from-cwd') }
}

function Resolve-Hosts {
    if ($Hosts -ne 'auto') {
        return @($Hosts.Split(',', [StringSplitOptions]::RemoveEmptyEntries) | ForEach-Object { $_.Trim().ToLowerInvariant() } | Where-Object { $McpSpecs.ContainsKey($_) })
    }
    $found = @()
    if (Test-Path (Join-Path $Target '.cursor')) { $found += 'cursor' }
    if ((Test-Path (Join-Path $Target '.claude')) -or (Test-Path (Join-Path $Target 'CLAUDE.md'))) { $found += 'claude' }
    if (Test-Path (Join-Path $Target '.windsurf')) { $found += 'windsurf' }
    if ($found.Count -eq 0) { $found = @('cursor') }
    return $found
}

function Add-SerenaMcp {
    param([string]$HostName)
    $spec = $McpSpecs[$HostName]
    $path = Join-Path $Target $spec.file
    $config = [ordered]@{}
    if (Test-Path $path) {
        $raw = Get-Content $path -Raw
        if (-not [string]::IsNullOrWhiteSpace($raw)) {
            $existing = $raw | ConvertFrom-Json -AsHashtable
            foreach ($k in $existing.Keys) { $config[$k] = $existing[$k] }
        }
    }
    if (-not $config.Contains('mcpServers') -or $null -eq $config['mcpServers']) { $config['mcpServers'] = [ordered]@{} }
    if ($config['mcpServers'].Contains('serena')) {
        Write-Host "MCP kept   ($HostName): $($spec.file) already has 'serena'"
        return 'kept'
    }
    $config['mcpServers']['serena'] = [ordered]@{ command = 'serena'; args = $spec.args }
    if ($DryRun) { Write-Host "DRY-RUN would add 'serena' to $($spec.file)"; return 'dry-run' }
    New-Item -ItemType Directory -Force -Path (Split-Path $path -Parent) | Out-Null
    $config | ConvertTo-Json -Depth 20 | Set-Content -Path $path -Encoding UTF8
    Write-Host "MCP added  ($HostName): $($spec.file)"
    return 'added'
}

$report = [ordered]@{ tool = 'serena'; installed = $false; version = ''; hosts = [ordered]@{}; indexed = $false; at = (Get-Date).ToUniversalTime().ToString('o') }

# 1. Install (uv tool) unless already present
$serena = Get-Command serena -ErrorAction SilentlyContinue
if ($serena -and -not $Force) {
    Write-Host "serena already installed: $($serena.Source)"
}
else {
    if (-not (Get-Command uv -ErrorAction SilentlyContinue)) {
        [Console]::Error.WriteLine('ERROR uv is required: https://docs.astral.sh/uv/ (then re-run). Alternates: codesearch-install skill, or mcp-codebase-search.')
        exit 1
    }
    $uvArgs = @('tool', 'install', '-p', '3.13', 'serena-agent')
    if ($Force) { $uvArgs += '--force' }
    if ($DryRun) { Write-Host "DRY-RUN uv $($uvArgs -join ' ')" }
    else {
        & uv @uvArgs
        if ($LASTEXITCODE -ne 0) { [Console]::Error.WriteLine("ERROR uv tool install serena-agent failed ($LASTEXITCODE)"); exit 2 }
    }
    $serena = Get-Command serena -ErrorAction SilentlyContinue
    if (-not $serena -and -not $DryRun) {
        [Console]::Error.WriteLine("ERROR 'serena' not on PATH after install — run 'uv tool update-shell' and restart the shell")
        exit 2
    }
}
$report.installed = [bool]$serena
if ($serena) { try { $report.version = (& serena --version 2>$null | Select-Object -First 1) } catch { } }

# 2. MCP wiring (add-only), one entry per host
if ($ConfigureMcp) {
    foreach ($h in Resolve-Hosts) { $report.hosts[$h] = Add-SerenaMcp $h }
}

# 3. Optional pre-index (speeds up first symbol lookups on large repos)
if ($Index -and $serena -and -not $DryRun) {
    Push-Location $Target
    try {
        & serena project index
        $report.indexed = ($LASTEXITCODE -eq 0)
    }
    finally { Pop-Location }
}

if (-not $DryRun) {
    $reportPath = Join-Path $Target '.agents/serena-install-report.json'
    New-Item -ItemType Directory -Force -Path (Split-Path $reportPath -Parent) | Out-Null
    $report | ConvertTo-Json -Depth 5 | Set-Content -Path $reportPath -Encoding UTF8
}
Write-Host 'OK serena ready'
exit 0
