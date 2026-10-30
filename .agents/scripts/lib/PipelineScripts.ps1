# Shared helpers for pipeline PowerShell scripts (pwsh 7+).
# Dot-source: . (Join-Path $PSScriptRoot '..\lib\PipelineScripts.ps1')

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Get-RepoRootFromScript {
    param([int]$LevelsUp = 4)
    $dir = $PSScriptRoot
    for ($i = 0; $i -lt $LevelsUp; $i++) {
        $dir = Split-Path $dir -Parent
    }
    return (Resolve-Path $dir).Path
}

function Read-ManifestValue {
    param(
        [string]$ManifestPath = 'pipeline.manifest.json',
        [string[]]$Keys
    )
    if (-not (Test-Path $ManifestPath)) { return '' }
    $json = Get-Content $ManifestPath -Raw | ConvertFrom-Json
    $cur = $json
    foreach ($k in $Keys) {
        if ($null -eq $cur) { return '' }
        $cur = $cur.$k
    }
    if ($null -eq $cur) { return '' }
    return [string]$cur
}

function Resolve-GateScriptPath {
    param([string]$Path)
    if (Test-Path $Path) { return (Resolve-Path $Path).Path }
    return $Path
}

function Test-GateScriptReady {
    param([string]$Path)
    $resolved = Resolve-GateScriptPath $Path
    if (-not (Test-Path $resolved)) { return $false }
    return $true
}

function Copy-Tree {
    param(
        [string]$Source,
        [string]$Destination,
        [string[]]$ExcludeDirNames = @('available', 'archive')
    )
    if (-not (Test-Path $Source)) { return }
    New-Item -ItemType Directory -Force -Path $Destination | Out-Null

    # Merge copy: update/overwrite paths present in Source; NEVER delete destination-only
    # children (preserves host-only user skills/agents/hooks). Do not use robocopy /MIR.
    if (Get-Command robocopy -ErrorAction SilentlyContinue) {
        $xd = @()
        foreach ($name in $ExcludeDirNames) { $xd += '/XD'; $xd += $name }
        # /E copy subdirs including empty; /XO optional — use plain /E without /MIR
        & robocopy $Source $Destination /E /NFL /NDL /NJH /NJS /NC /NS /XO @xd | Out-Null
        if ($LASTEXITCODE -ge 8) { throw "robocopy failed with exit $LASTEXITCODE" }
        return
    }

    Get-ChildItem -Path $Source -Force -ErrorAction SilentlyContinue | ForEach-Object {
        if ($_.PSIsContainer -and ($_.Name -in $ExcludeDirNames)) { return }
        $destPath = Join-Path $Destination $_.Name
        if ($_.PSIsContainer) {
            Copy-Tree -Source $_.FullName -Destination $destPath -ExcludeDirNames $ExcludeDirNames
        }
        else {
            Copy-Item -Path $_.FullName -Destination $destPath -Force
        }
    }
}

function Get-TreeHash {
    # Content hash of a file or directory (relative paths + bytes); used to detect host edits.
    param([string]$Path)
    $sha = [System.Security.Cryptography.SHA256]::Create()
    try {
        if (Test-Path -LiteralPath $Path -PathType Leaf) {
            return [Convert]::ToHexString($sha.ComputeHash([IO.File]::ReadAllBytes($Path)))
        }
        $root = (Resolve-Path -LiteralPath $Path).Path
        $files = Get-ChildItem -LiteralPath $root -Recurse -File -Force | Sort-Object { $_.FullName.Substring($root.Length).Replace('\', '/') }
        $ms = [IO.MemoryStream]::new()
        foreach ($f in $files) {
            $rel = [Text.Encoding]::UTF8.GetBytes($f.FullName.Substring($root.Length).Replace('\', '/'))
            $ms.Write($rel, 0, $rel.Length)
            $bytes = [IO.File]::ReadAllBytes($f.FullName)
            $ms.Write($bytes, 0, $bytes.Length)
        }
        $ms.Position = 0
        return [Convert]::ToHexString($sha.ComputeHash($ms))
    }
    finally { $sha.Dispose() }
}

function Test-IsLink {
    param([string]$Path)
    $item = Get-Item -LiteralPath $Path -Force -ErrorAction SilentlyContinue
    return ($null -ne $item -and $null -ne $item.LinkType -and $item.LinkType -ne '')
}

function Get-LinkTargetFull {
    # Absolute, normalized target of a symlink/junction (relative targets resolved from the link's folder).
    param([string]$Path)
    $item = Get-Item -LiteralPath $Path -Force
    $t = [string]$item.Target
    if (-not [IO.Path]::IsPathRooted($t)) { $t = Join-Path (Split-Path $Path -Parent) $t }
    return [IO.Path]::GetFullPath($t)
}

function New-RelativeLink {
    # Create Link -> relative path to Source. Falls back to junction (dirs, Windows w/o symlink
    # privilege) or copy (files). Returns 'link' | 'junction' | 'copy'.
    param([string]$Link, [string]$Source)
    $parent = Split-Path $Link -Parent
    New-Item -ItemType Directory -Force -Path $parent | Out-Null
    $relative = [IO.Path]::GetRelativePath($parent, $Source)
    $isDir = Test-Path -LiteralPath $Source -PathType Container
    try {
        if ($isDir) { [IO.Directory]::CreateSymbolicLink($Link, $relative) | Out-Null }
        else { [IO.File]::CreateSymbolicLink($Link, $relative) | Out-Null }
        return 'link'
    }
    catch {
        if ($isDir -and $IsWindows) {
            New-Item -ItemType Junction -Path $Link -Target $Source | Out-Null
            return 'junction'
        }
        if ($isDir) { Copy-Item -LiteralPath $Source -Destination $Link -Recurse -Force }
        else { Copy-Item -LiteralPath $Source -Destination $Link -Force }
        return 'copy'
    }
}

function Sync-LinkedEntries {
    <#
      Project every entry of SourceDir (a .agents/ subtree) into DestDir as one relative link
      per entry. Never deletes host-only entries (user/Spec Kit skills, hooks, ...).
        - missing                         -> create link
        - link into .agents (any target)  -> re-point if needed
        - stale link into .agents         -> removed (its source is gone)
        - real copy identical to source   -> replaced by link (dedupe legacy copies)
        - real copy that differs          -> KEPT (host-local edit) + warning
        - link pointing outside .agents   -> KEPT (user link) + warning
      RenameExtension maps e.g. '.mdc' -> '.md' (Claude/Windsurf rules).
    #>
    param(
        [string]$SourceDir,
        [string]$DestDir,
        [string]$AgentsRoot,
        [string]$FromExtension = '',
        [string]$ToExtension = '',
        [string[]]$Exclude = @('available', 'archive', '.overlay-stash', '.backup', '.hand-added')
    )
    $stats = @{ linked = 0; unchanged = 0; replaced = 0; kept = 0; removed = 0; fallback = 0 }
    if (-not (Test-Path -LiteralPath $SourceDir)) { return $stats }
    $agentsFull = [IO.Path]::GetFullPath($AgentsRoot).TrimEnd([IO.Path]::DirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar

    # Legacy: whole destination folder was a link to .agents/<kind> — turn into a real folder.
    if ((Test-Path -LiteralPath $DestDir) -and (Test-IsLink $DestDir)) {
        $target = Get-LinkTargetFull $DestDir
        if (($target + [IO.Path]::DirectorySeparatorChar).StartsWith($agentsFull)) {
            (Get-Item -LiteralPath $DestDir -Force).Delete()
        }
    }
    New-Item -ItemType Directory -Force -Path $DestDir | Out-Null

    $expected = @{}
    foreach ($entry in Get-ChildItem -LiteralPath $SourceDir -Force) {
        if ($entry.Name -in $Exclude -or $entry.Name.EndsWith('.hand-added')) { continue }
        $name = $entry.Name
        if ($FromExtension -and -not $entry.PSIsContainer -and $name.EndsWith($FromExtension)) {
            $name = $name.Substring(0, $name.Length - $FromExtension.Length) + $ToExtension
        }
        $expected[$name] = $true
        $dest = Join-Path $DestDir $name
        $source = $entry.FullName

        if (Test-IsLink $dest) {
            $current = Get-LinkTargetFull $dest
            if ($current -eq [IO.Path]::GetFullPath($source)) { $stats.unchanged++; continue }
            if ($current.StartsWith($agentsFull)) {
                (Get-Item -LiteralPath $dest -Force).Delete()
            }
            else {
                Write-Warning "kept user link $dest -> $current"
                $stats.kept++; continue
            }
        }
        elseif (Test-Path -LiteralPath $dest) {
            if ((Get-TreeHash $dest) -eq (Get-TreeHash $source)) {
                Remove-Item -LiteralPath $dest -Recurse -Force
                $stats.replaced++
            }
            else {
                Write-Warning "kept host copy $dest (differs from $source; move edits into .agents or a '## local overlay' to relink)"
                $stats.kept++; continue
            }
        }
        $mode = New-RelativeLink -Link $dest -Source $source
        if ($mode -eq 'link') { $stats.linked++ } else { $stats.fallback++ }
    }

    # Remove only OUR stale links (into .agents, source gone). Never touch anything else.
    foreach ($item in Get-ChildItem -LiteralPath $DestDir -Force) {
        if ($expected.ContainsKey($item.Name) -or -not (Test-IsLink $item.FullName)) { continue }
        $t = Get-LinkTargetFull $item.FullName
        if ($t.StartsWith($agentsFull) -and -not (Test-Path -LiteralPath $t)) {
            $item.Delete()
            $stats.removed++
        }
    }
    return $stats
}

function Invoke-ManifestCommand {
    param([string]$Command)
    if ([string]::IsNullOrWhiteSpace($Command)) { return $true }
    Write-Host "+ $Command"
    Invoke-Expression $Command
    if ($LASTEXITCODE -and $LASTEXITCODE -ne 0) { return $false }
    return $true
}

function Invoke-AgenticTool {
    param([string[]]$ToolArgs)

    if (Get-Command agentic-tool -ErrorAction SilentlyContinue) {
        & agentic-tool @ToolArgs
        return $LASTEXITCODE
    }

    $root = Get-Location
    $home = $env:AI_PIPELINE_HOME
    if ([string]::IsNullOrWhiteSpace($home) -and (Test-Path (Join-Path $root '.agents/pipeline.home'))) {
        $home = (Get-Content (Join-Path $root '.agents/pipeline.home') -Raw).Trim()
    }

    $proj = Join-Path $home 'src/Agentic.Tool/Agentic.Tool.fsproj'
    if (Test-Path $proj) {
        $env:AI_PIPELINE_HOME = $home
        dotnet run --project $proj -v q -- @ToolArgs
        return $LASTEXITCODE
    }

    $psWrapper = Join-Path $home 'bin/agentic-tool.ps1'
    if (Test-Path $psWrapper) {
        $env:AI_PIPELINE_HOME = $home
        & pwsh -NoProfile -File $psWrapper @ToolArgs
        return $LASTEXITCODE
    }

    Write-Error 'agentic-tool not found (PATH, AI_PIPELINE_HOME, or .agents/pipeline.home).'
    return 1
}
