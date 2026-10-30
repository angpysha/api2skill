#!/usr/bin/env pwsh
# Create or checkout a branch: {type}/{WW}-{spec-name}-{YYYY}-{description}
# WW/YYYY = ISO 8601 week and week-based year; spec-name from .specify/feature.json.
# Same inputs (type, description, date, feature.json) always yield the same name.
[CmdletBinding()]
param(
    [Parameter(Mandatory = $false)]
    [string]$Type,

    [Parameter(Mandatory = $false)]
    [string]$Description,

    [Parameter(Mandatory = $false)]
    [string]$SpecName,

    [Parameter(Mandatory = $false)]
    [string]$Date,

    [switch]$DryRun,

    [switch]$AllowExisting,

    [switch]$Json,

    [switch]$Help
)

$ErrorActionPreference = 'Stop'

$AllowedTypes = @('feature', 'fix', 'chore', 'docs', 'refactor', 'test', 'spike')
$MaxBranchLength = 244   # GitHub limit (bytes; names are ASCII after slugging)

if ($Help) {
    Write-Host @"
Usage: ./create-spec-branch.ps1 -Type <type> -Description <text> [-SpecName <name>] [-Date yyyy-MM-dd] [-DryRun] [-AllowExisting] [-Json]

  Branch: {type}/{WW}-{spec-name}-{YYYY}-{description}

  -Type           One of: $($AllowedTypes -join ', ')
  -Description    Short description (normalized to slug)
  -SpecName       Override spec name (default: basename of feature_directory in .specify/feature.json)
  -Date           yyyy-MM-dd used for ISO week/year (default: today)
  -DryRun         Print BRANCH_NAME only; no git changes
  -AllowExisting  If local branch exists, checkout instead of erroring
  -Json           Emit a single JSON object instead of KEY: value lines

Example:
  pwsh ./create-spec-branch.ps1 -Type feature -Description 'add date filter' -Date 2026-10-12 -DryRun
  # BRANCH_NAME: feature/42-014-session-filters-2026-add-date-filter
"@
    exit 0
}

function Fail {
    param([string]$Message, [int]$Code = 1)
    [Console]::Error.WriteLine("ERROR: $Message")
    exit $Code
}

function ConvertTo-Slug {
    param([string]$Text, [string]$Field)
    $s = $Text.ToLowerInvariant() -replace '[^a-z0-9]+', '-' -replace '-{2,}', '-' -replace '^-', '' -replace '-$', ''
    if ([string]::IsNullOrWhiteSpace($s)) {
        Fail "$Field produced an empty slug after normalization."
    }
    return $s
}

function Get-RepoRoot {
    $root = & git rev-parse --show-toplevel 2>$null
    if ($LASTEXITCODE -eq 0 -and -not [string]::IsNullOrWhiteSpace($root)) {
        return $root.Trim()
    }
    return (Get-Location).Path
}

function Write-Result {
    param([System.Collections.Specialized.OrderedDictionary]$Result)
    if ($Json) {
        $Result | ConvertTo-Json -Compress
    }
    else {
        foreach ($key in $Result.Keys) {
            Write-Output "${key}: $($Result[$key])"
        }
    }
}

# --- Inputs -----------------------------------------------------------------

if ([string]::IsNullOrWhiteSpace($Type) -or [string]::IsNullOrWhiteSpace($Description)) {
    Fail "-Type and -Description are required (non-interactive). Ask the user if missing from context."
}

$normalizedType = $Type.Trim().ToLowerInvariant()
if ($AllowedTypes -notcontains $normalizedType) {
    Fail "Invalid -Type '$Type'. Allowed: $($AllowedTypes -join ', ')"
}

$when = Get-Date
if (-not [string]::IsNullOrWhiteSpace($Date)) {
    $parsed = [datetime]::MinValue
    if (-not [datetime]::TryParseExact($Date, 'yyyy-MM-dd', [System.Globalization.CultureInfo]::InvariantCulture, [System.Globalization.DateTimeStyles]::None, [ref]$parsed)) {
        Fail "-Date must be yyyy-MM-dd (got '$Date')"
    }
    $when = $parsed
}
# ISO week-based year keeps WW and YYYY consistent around New Year (e.g. 2026-12-28 -> 53 / 2026, 2027-01-01 -> 53 / 2026).
$weekDisplay = '{0:D2}' -f [System.Globalization.ISOWeek]::GetWeekOfYear($when)
$yearDisplay = '{0:D4}' -f [System.Globalization.ISOWeek]::GetYear($when)

$repoRoot = Get-RepoRoot
$featureDir = ''
if ([string]::IsNullOrWhiteSpace($SpecName)) {
    $featureJson = Join-Path $repoRoot '.specify/feature.json'
    if (-not (Test-Path -LiteralPath $featureJson)) {
        Fail ".specify/feature.json not found at '$featureJson'. Run /speckit.specify first or pass -SpecName."
    }
    try {
        $featureDir = (Get-Content -LiteralPath $featureJson -Raw | ConvertFrom-Json).feature_directory
    }
    catch {
        Fail "Could not parse '$featureJson': $($_.Exception.Message)"
    }
    if ([string]::IsNullOrWhiteSpace($featureDir)) {
        Fail "'feature_directory' is empty in '$featureJson'. Pass -SpecName."
    }
    $SpecName = Split-Path -Leaf ($featureDir.TrimEnd('/', '\'))
}

$specSlug = ConvertTo-Slug -Text $SpecName -Field 'Spec name'
$descSlug = ConvertTo-Slug -Text $Description -Field 'Description'

# --- Compose (truncate description only; prefix is fixed) --------------------

$prefix = "$normalizedType/$weekDisplay-$specSlug-$yearDisplay-"
$maxDesc = $MaxBranchLength - $prefix.Length
if ($maxDesc -lt 1) {
    Fail "Spec name '$specSlug' is too long to fit a $MaxBranchLength-byte branch name."
}
if ($descSlug.Length -gt $maxDesc) {
    $descSlug = $descSlug.Substring(0, $maxDesc) -replace '-+$', ''
}
$branchName = "$prefix$descSlug"

& git check-ref-format --branch $branchName *> $null
if ($LASTEXITCODE -ne 0) {
    Fail "Computed branch name '$branchName' is not a valid git ref."
}

$result = [ordered]@{
    BRANCH_NAME  = $branchName
    TYPE         = $normalizedType
    WEEK         = $weekDisplay
    SPEC_NAME    = $specSlug
    YEAR         = $yearDisplay
    DESCRIPTION  = $descSlug
    FEATURE_DIR  = $featureDir
}

if ($DryRun) {
    $result['DRY_RUN'] = $true
    Write-Result $result
    exit 0
}

# --- Git ---------------------------------------------------------------------

$inside = & git rev-parse --is-inside-work-tree 2>$null
if ($LASTEXITCODE -ne 0 -or $inside.Trim() -ne 'true') {
    Fail "not inside a git work tree"
}

$current = (& git rev-parse --abbrev-ref HEAD 2>$null)
if ($current -and $current.Trim() -eq $branchName) {
    $result['ACTION'] = 'already_on'
    Write-Result $result
    exit 0
}

& git show-ref --verify --quiet "refs/heads/$branchName" 2>$null
if ($LASTEXITCODE -eq 0) {
    if (-not $AllowExisting) {
        Fail "local branch '$branchName' already exists. Re-run with -AllowExisting to checkout, or choose a different description."
    }
    & git checkout --quiet $branchName
    if ($LASTEXITCODE -ne 0) { Fail "failed to checkout existing branch '$branchName'" $LASTEXITCODE }
    $result['ACTION'] = 'checked_out'
    Write-Result $result
    exit 0
}

& git checkout --quiet -b $branchName
if ($LASTEXITCODE -ne 0) { Fail "failed to create branch '$branchName'" $LASTEXITCODE }
$result['ACTION'] = 'created'
Write-Result $result
exit 0
