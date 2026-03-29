# Compares declared Sts2SupportedVersions.json entries to folders on disk (MCP decompiled cache).
# When the MCP adds a new game version folder under the cache root, this script fails unless you
# add a matching supportedGameBuilds entry (and verify the mod still works).
# Pair with MCP fetch_latest_game_version for the user's installed StS2 build vs list_available_versions for cache coverage.
#
# Usage:
#   .\build\Test-Sts2McpVersionCoverage.ps1 -RepoRoot . -McpDecompiledCacheRoot "D:\path\to\decompiled_cache"
#
# Exit codes: 0 = OK, 1 = mismatch or error
param(
    [Parameter(Mandatory = $true)]
    [string] $RepoRoot,
    [Parameter(Mandatory = $false)]
    [string] $McpDecompiledCacheRoot = "",
    [Parameter(Mandatory = $false)]
    [switch] $TreatMissingDeclaredFoldersAsError
)

$ErrorActionPreference = "Stop"
$jsonPath = Join-Path $RepoRoot "Sts2SupportedVersions.json"
if (-not (Test-Path $jsonPath)) {
    Write-Error "Missing $jsonPath"
    exit 1
}

$doc = Get-Content -LiteralPath $jsonPath -Raw -Encoding UTF8 | ConvertFrom-Json
$declared = @{}
foreach ($b in $doc.supportedGameBuilds) {
    $name = $b.mcpDecompiledFolderName
    if ([string]::IsNullOrWhiteSpace($name)) { continue }
    $declared[$name.Trim()] = $true
}

if ([string]::IsNullOrWhiteSpace($McpDecompiledCacheRoot)) {
    Write-Host "Test-Sts2McpVersionCoverage: skipped (McpDecompiledCacheRoot not set)."
    exit 0
}

if (-not (Test-Path -LiteralPath $McpDecompiledCacheRoot)) {
    Write-Error "McpDecompiledCacheRoot does not exist: $McpDecompiledCacheRoot"
    exit 1
}

$onDisk = Get-ChildItem -LiteralPath $McpDecompiledCacheRoot -Directory -ErrorAction Stop | ForEach-Object { $_.Name }
$extra = @()
foreach ($d in $onDisk) {
    if (-not $declared.ContainsKey($d)) { $extra += $d }
}

$missing = @()
foreach ($k in $declared.Keys) {
    if ($k -notin $onDisk) { $missing += $k }
}

if ($extra.Count -gt 0) {
    Write-Error ("MCP cache has version folder(s) not listed in Sts2SupportedVersions.json: " + ($extra -join ", ") +
        ". Add supportedGameBuilds entries (displayLabel / mcpDecompiledFolderName) or point to a different cache.")
    exit 1
}

if ($missing.Count -gt 0 -and $TreatMissingDeclaredFoldersAsError) {
    Write-Error ("Sts2SupportedVersions.json lists mcpDecompiledFolderName not present under cache: " + ($missing -join ", "))
    exit 1
}

if ($missing.Count -gt 0) {
    Write-Warning ("Declared MCP folder name(s) not found under cache (update path or JSON): " + ($missing -join ", "))
}

Write-Host "Test-Sts2McpVersionCoverage: OK (declared folders match MCP cache root)."
exit 0
