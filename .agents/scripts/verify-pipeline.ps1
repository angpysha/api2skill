# verify-pipeline.ps1 — sanity-check an installed pipeline (pwsh).
# Usage: pwsh -File verify-pipeline.ps1 [TARGET_DIR]
param([string]$Target = (Get-Location).Path)

. (Join-Path $PSScriptRoot 'lib/PipelineScripts.ps1')

$Target = (Resolve-Path $Target).Path
Set-Location $Target

$Errors = 0
$Warnings = 0

function Test-Check {
    param([string]$Label, [string]$Path)
    if (Test-Path $Path) { Write-Host "OK   $Label" }
    else { Write-Host "MISS $Label ($Path)"; $script:Errors++ }
}

function Write-Warn {
    param([string]$Message)
    Write-Host "WARN $Message"
    $script:Warnings++
}

function Get-PipelineHomePath {
    if (Test-Path '.agents/pipeline.home') {
        return (Get-Content '.agents/pipeline.home' -Raw).Trim()
    }
    return ''
}

function Get-PackExpectedSkills {
    param(
        [string]$PipelineHome,
        [string]$PackName,
        [string[]]$Features
    )
    $packJson = Join-Path $PipelineHome "packs/$PackName/pack.json"
    if (-not (Test-Path $packJson)) { return @() }
    $pack = Get-Content $packJson -Raw | ConvertFrom-Json
    $skills = [System.Collections.Generic.List[string]]::new()
    $props = $pack.PSObject.Properties.Name
    if ($props -contains 'skills' -and $pack.skills) {
        foreach ($s in @($pack.skills)) {
            if (-not [string]::IsNullOrWhiteSpace($s)) { $skills.Add([string]$s) }
        }
    }
    if ($props -contains 'optional_features' -and $pack.optional_features -and $Features) {
        foreach ($feature in $Features) {
            $fp = $pack.optional_features.PSObject.Properties[$feature]
            $feat = if ($fp) { $fp.Value } else { $null }
            if ($feat -and $feat.PSObject.Properties.Name -contains 'skills' -and $feat.skills) {
                foreach ($s in @($feat.skills)) {
                    if (-not [string]::IsNullOrWhiteSpace($s) -and -not $skills.Contains([string]$s)) {
                        $skills.Add([string]$s)
                    }
                }
            }
        }
    }
    return @($skills)
}

function Get-RecommendationPackFeatures {
    param([string]$PackName)
    $recPath = '.agents/pipeline.recommendation.json'
    if (-not (Test-Path $recPath)) { return @() }
    $rec = Get-Content $recPath -Raw | ConvertFrom-Json
    if (-not $rec.packs) { return @() }
    foreach ($item in @($rec.packs)) {
        if ($item.name -eq $PackName -and $item.features) {
            return @($item.features | ForEach-Object { [string]$_ })
        }
    }
    return @()
}

Write-Host "=== Verify pipeline @ $Target ==="
Test-Check 'adapt skill (/pipeline-adapt)' '.agents/skills/pipeline-adapt/SKILL.md'
Test-Check 'project-intake skill' '.agents/skills/project-intake/SKILL.md'
Test-Check 'spec-kit skill' '.agents/skills/spec-kit/SKILL.md'
Test-Check 'beads rule (br only)' '.agents/rules/beads.mdc'
Test-Check 'pipeline.home' '.agents/pipeline.home'
Test-Check 'manifest' 'pipeline.manifest.json'
Test-Check 'checkpoint dir' '_code_agent'
Test-Check 'sdlc docs dir' 'docs/sdlc'

$status = '?'
if (Test-Path 'pipeline.manifest.json') {
    $status = Read-ManifestValue -Keys @('status')
    if ([string]::IsNullOrWhiteSpace($status)) { $status = '?' }
    Write-Host "INFO manifest status: $status"
}

if ($status -eq 'needs_adapt') {
    Write-Warn "run '/pipeline-adapt mode=init' in your IDE to reach status: ready"
    if (-not (Test-Path 'PROJECT.md')) {
        Write-Warn 'PROJECT.md missing — adapt will run project-intake (vision + stack) before stack detection'
    }
    if (Test-Path '.agents/pipeline.recommendation.json') {
        Write-Host 'INFO recommendation present (apply pending?)'
    }
}
elseif ($status -eq 'ready') {
    Test-Check 'orchestrator skill' '.agents/skills/sdlc-orchestrator/SKILL.md'
    Test-Check 'coordinator agent' '.agents/agents/coordinator.md'
    Test-Check 'pack registry' '.agents/packs/registry.json'

    if (Test-Path '.agents/agents/coordinator.md') {
        $coord = Get-Content '.agents/agents/coordinator.md' -Raw
        if ($coord -match '(?i)\bbr\b' -and $coord -match '(?i)never[^\n]{0,40}\bbd\b') {
            Write-Host 'OK   coordinator states br-only (never bd)'
        }
        else {
            Write-Host 'MISS coordinator missing br-only / never bd hard lock'
            $Errors++
        }
    }

    if (Test-Path 'pipeline.manifest.json') {
        $manifest = Get-Content 'pipeline.manifest.json' -Raw | ConvertFrom-Json
        foreach ($prop in $manifest.gates.PSObject.Properties) {
            $gatePath = Resolve-GateScriptPath $prop.Value
            if (Test-Path $gatePath) {
                Write-Host "OK   gate script: $gatePath"
            }
            else {
                Write-Host "MISS gate script: $($prop.Value)"
                $Errors++
            }
        }

        # Named pack skill presence checks (pack + skill)
        $pipelineHome = Get-PipelineHomePath
        if (-not [string]::IsNullOrWhiteSpace($pipelineHome) -and (Test-Path $pipelineHome)) {
            $packNames = @()
            if ($manifest.packs) { $packNames = @($manifest.packs | ForEach-Object { [string]$_ }) }
            foreach ($packName in $packNames) {
                if ([string]::IsNullOrWhiteSpace($packName)) { continue }
                $features = Get-RecommendationPackFeatures -PackName $packName
                $expectedSkills = Get-PackExpectedSkills -PipelineHome $pipelineHome -PackName $packName -Features $features
                foreach ($skill in $expectedSkills) {
                    $skillDir = Join-Path '.agents/skills' $skill
                    if (Test-Path $skillDir) {
                        Write-Host "OK   pack skill: $packName/$skill"
                    }
                    else {
                        Write-Host "MISS pack skill: $packName/$skill (.agents/skills/$skill)"
                        $Errors++
                    }
                }
            }
        }
        else {
            Write-Warn 'cannot resolve pipeline.home for pack skill checks'
        }

        # Pack lock drift (slice 2) — when present, disk skills must match lock
        if (Test-Path '.agents/pack.lock.json') {
            Write-Host 'OK   pack.lock.json present'
            try {
                $packLock = Get-Content '.agents/pack.lock.json' -Raw | ConvertFrom-Json
                if ($packLock.packs) {
                    foreach ($lockPack in @($packLock.packs)) {
                        $lockPackName = [string]$lockPack.name
                        if ([string]::IsNullOrWhiteSpace($lockPackName)) { continue }
                        foreach ($skill in @($lockPack.skills)) {
                            $skillName = [string]$skill
                            if ([string]::IsNullOrWhiteSpace($skillName)) { continue }
                            $skillDir = Join-Path '.agents/skills' $skillName
                            if (Test-Path $skillDir) {
                                Write-Host "OK   pack.lock skill: $lockPackName/$skillName"
                            }
                            else {
                                Write-Host "MISS pack.lock skill: $lockPackName/$skillName (.agents/skills/$skillName)"
                                $Errors++
                            }
                        }
                    }
                }
                $lockCatalog = [string]$packLock.catalog_version
                if (-not [string]::IsNullOrWhiteSpace($lockCatalog)) {
                    $toolCatalog = ''
                    if (-not [string]::IsNullOrWhiteSpace($pipelineHome) -and (Test-Path $pipelineHome)) {
                        $versionFile = Join-Path $pipelineHome 'VERSION'
                        if (Test-Path $versionFile) {
                            $toolCatalog = (Get-Content $versionFile -Raw).Trim()
                        }
                    }
                    if (-not [string]::IsNullOrWhiteSpace($toolCatalog) -and $lockCatalog -ne $toolCatalog) {
                        Write-Warn "pack.lock catalog_version drift — lock=$lockCatalog tool=$toolCatalog. Re-run: agentic-tool apply"
                    }
                    else {
                        Write-Host "OK   pack.lock catalog_version: $lockCatalog"
                    }
                }
            }
            catch {
                Write-Warn "pack.lock.json unreadable: $($_.Exception.Message)"
            }
        }
        elseif ($status -eq 'ready') {
            Write-Host 'INFO pack.lock.json missing — optional until apply after catalog with lock writer'
        }

        # Host projections: every manifest host must link (relative) into .agents/, no broken links
        $hostList = @()
        if ($manifest.PSObject.Properties.Name -contains 'hosts' -and $manifest.hosts) { $hostList = @($manifest.hosts) }
        elseif ($manifest.host) { $hostList = @([string]$manifest.host) }
        $agentsFull = [IO.Path]::GetFullPath((Join-Path $Target '.agents')) + [IO.Path]::DirectorySeparatorChar
        foreach ($hostName in $hostList) {
            $hostRoot = switch ($hostName.ToLowerInvariant()) {
                'cursor' { '.cursor' }
                'claude' { '.claude' }
                'windsurf' { '.windsurf' }
                default { $null }
            }
            if (-not $hostRoot) { Write-Host "INFO host $hostName — no folder projection"; continue }
            $probe = Join-Path $hostRoot 'skills/sdlc-orchestrator/SKILL.md'
            if (-not (Test-Path $probe)) {
                Write-Host "MISS host projection: $probe (host=$hostName) — run agentic-tool sync --host $hostName"
                $Errors++
                continue
            }
            $links = 0; $broken = 0; $absolute = 0; $copies = 0
            foreach ($kind in @('skills', 'agents', 'rules')) {
                $dir = Join-Path $hostRoot $kind
                if (-not (Test-Path $dir)) { continue }
                foreach ($item in Get-ChildItem -LiteralPath $dir -Force) {
                    if (Test-IsLink $item.FullName) {
                        $links++
                        $raw = [string]$item.Target
                        if ([IO.Path]::IsPathRooted($raw)) { $absolute++ }
                        $full = Get-LinkTargetFull $item.FullName
                        if ($full.StartsWith($agentsFull) -and -not (Test-Path -LiteralPath $full)) {
                            Write-Host "MISS broken link: $($item.FullName) -> $raw"
                            $broken++
                        }
                    }
                    elseif (Test-Path -LiteralPath (Join-Path (Join-Path '.agents' $kind) $item.Name)) { $copies++ }
                }
            }
            $Errors += $broken
            Write-Host ("OK   host {0}: {1} link(s) into .agents ({2} absolute), {3} host copy(ies) kept" -f $hostName, $links, $absolute, $copies)
            if ($copies -gt 0) { Write-Warn "$hostName has $copies host-local copies that differ from .agents (see sync warnings)" }
        }
    }
}

$csPrimary = Read-ManifestValue -Keys @('code_search', 'primary')
if ([string]::IsNullOrWhiteSpace($csPrimary)) { $csPrimary = 'serena' }
Write-Host "INFO code-search: $csPrimary (primary) → alternates → default tools"
if ($csPrimary -eq 'serena' -and -not (Get-Command serena -ErrorAction SilentlyContinue)) {
    Write-Warn "serena not on PATH — run: pwsh .agents/skills/serena-install/scripts/install-serena.ps1 -ConfigureMcp"
}


# Spec Kit readiness
$specifyCmd = Get-Command specify -ErrorAction SilentlyContinue
$specKitEnabled = $true
if (Test-Path 'pipeline.manifest.json') {
    try {
        $m = Get-Content 'pipeline.manifest.json' -Raw | ConvertFrom-Json
        if ($null -ne $m.spec_kit -and $null -ne $m.spec_kit.enabled) { $specKitEnabled = [bool]$m.spec_kit.enabled }
    } catch {}
}
if (-not $specKitEnabled) {
    Write-Host 'INFO Spec Kit readiness: degraded_disabled'
}
elseif (-not $specifyCmd) {
    Write-Host 'MISS Spec Kit readiness: cli_missing — install specify-cli (uv tool install specify-cli --from git+https://github.com/github/spec-kit.git)'
    $Errors++
}
elseif (-not (Test-Path '.specify')) {
    Write-Host 'MISS Spec Kit readiness: not_initialized — run: agentic-tool update'
    $Errors++
}
else {
    Write-Host 'OK   Spec Kit readiness: ok'
    $laneIds = @('pipeline-spike', 'pipeline-micro', 'pipeline-short', 'pipeline-fast', 'pipeline-full')
    $regPath = Join-Path '.specify' 'workflows/workflow-registry.json'
    if (Test-Path $regPath) {
        try {
            $reg = Get-Content $regPath -Raw | ConvertFrom-Json
            $missingLanes = @()
            foreach ($id in $laneIds) {
                $present = $false
                if ($reg.workflows -and $reg.workflows.PSObject.Properties.Name -contains $id) { $present = $true }
                elseif (Test-Path (Join-Path '.specify/workflows' $id)) { $present = $true }
                if (-not $present) { $missingLanes += $id }
            }
            if ($missingLanes.Count -eq 0) { Write-Host 'OK   lane workflows registered (pipeline-*)' }
            else {
                Write-Warn ("lane workflows missing: {0} — re-run apply with specify on PATH" -f ($missingLanes -join ', '))
            }
        } catch {
            Write-Warn 'workflow-registry.json unreadable — re-run apply'
        }
    }
    else {
        $copied = @($laneIds | Where-Object { Test-Path (Join-Path '.specify/workflows' $_) })
        if ($copied.Count -eq 5) {
            Write-Warn 'pipeline-* YAML present but workflow-registry.json missing — run: specify workflow add <path> for each (or re-apply with specify on PATH)'
        }
        elseif ($status -eq 'ready') {
            Write-Warn 'lane workflows not installed — re-run agentic-tool apply'
        }
    }
}

if (Test-Path 'pipeline.manifest.json') {
    $cs = Read-ManifestValue -Keys @('code_search', 'codebase_name')
    if (-not [string]::IsNullOrWhiteSpace($cs)) { Write-Host "INFO code_search.codebase_name: $cs" }

    $installedCatalogVersion = Read-ManifestValue -Keys @('catalog_version')
    if ([string]::IsNullOrWhiteSpace($installedCatalogVersion)) {
        $installedCatalogVersion = Read-ManifestValue -Keys @('adapt', 'catalog_version')
    }
    if (-not [string]::IsNullOrWhiteSpace($installedCatalogVersion)) {
        Write-Host "INFO catalog_version (installed): $installedCatalogVersion"
    }

    $catalogVersion = ''
    if (Test-Path '.agents/pipeline.home') {
        $homePath = (Get-Content '.agents/pipeline.home' -Raw).Trim()
        $versionFile = Join-Path $homePath 'VERSION'
        if (Test-Path $versionFile) {
            $catalogVersion = (Get-Content $versionFile -Raw).Trim()
        }
    }
    if (-not [string]::IsNullOrWhiteSpace($catalogVersion)) {
        Write-Host "INFO catalog_version (tool package): $catalogVersion"
        if ($installedCatalogVersion -and $installedCatalogVersion -ne $catalogVersion) {
            Write-Warn "catalog stale — installed $installedCatalogVersion, tool has $catalogVersion. Run: agentic-tool update"
        }
    }
    elseif ($status -eq 'ready') {
        Write-Warn 'cannot read catalog VERSION — run agentic-tool apply to refresh skills/agents/rules'
    }

    $specKitEnabled = Read-ManifestValue -Keys @('spec_kit', 'enabled')
    if ($specKitEnabled -eq 'True' -or $specKitEnabled -eq 'true') {
        if (Test-Path '.specify') { Write-Host 'OK   Spec Kit initialized (.specify present)' }
        else { Write-Warn "spec_kit.enabled but .specify missing — run 'agentic-tool update'" }
    }
    elseif (-not [string]::IsNullOrWhiteSpace($specKitEnabled)) {
        Write-Host 'INFO spec_kit.enabled=false — running in SPEC-KIT DEGRADED mode'
    }
    elseif ($status -eq 'ready') {
        Write-Warn 'spec_kit missing from ready manifest — re-run apply after upgrade (manifest-merge)'
    }
}

Write-Host '==============================='
if ($Errors -gt 0) {
    Write-Host "FAIL: $Errors problem(s), $Warnings warning(s)"
    exit 1
}
Write-Host "PASS: pipeline structure OK ($Warnings warning(s))"
