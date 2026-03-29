# Updates README.md compatibility line(s) from Sts2SupportedVersions.json and mod $(Version).
# Called from MSBuild when UpdateReadmeFromSts2Versions=true.
# Before changing Sts2SupportedVersions.json, use MCP fetch_latest_game_version (installed game) and
# list_available_versions (decompiled_cache folders) so displayLabel and mcpDecompiledFolderName stay accurate.
param(
    [Parameter(Mandatory = $true)]
    [string] $RepoRoot,
    [Parameter(Mandatory = $true)]
    [string] $ModVersion
)

$ErrorActionPreference = "Stop"
$jsonPath = Join-Path $RepoRoot "Sts2SupportedVersions.json"
$readmePath = Join-Path $RepoRoot "README.md"

if (-not (Test-Path $jsonPath)) { throw "Missing $jsonPath" }
if (-not (Test-Path $readmePath)) { throw "Missing $readmePath" }

$doc = Get-Content -LiteralPath $jsonPath -Raw -Encoding UTF8 | ConvertFrom-Json
if (-not $doc.supportedGameBuilds -or $doc.supportedGameBuilds.Count -eq 0) {
    throw "Sts2SupportedVersions.json: supportedGameBuilds is empty."
}

$labels = @($doc.supportedGameBuilds | ForEach-Object { $_.displayLabel.Trim() } | Sort-Object)
$compatLine = "**Slay the Spire 2 Compatibility:** " + ($labels -join ", ") + "  "

$content = Get-Content -LiteralPath $readmePath -Raw -Encoding UTF8

$content = [regex]::Replace(
    $content,
    '(?m)^(\*\*Mod version:\*\*\s*)v[\d.]+',
    "`${1}v$ModVersion")

$content = [regex]::Replace(
    $content,
    '(?m)^\*\*Slay the Spire 2 Compatibility:\*\*\s*.*$',
    $compatLine,
    1)

Set-Content -LiteralPath $readmePath -Value $content -Encoding UTF8
Write-Host "Updated README mod version and StS2 compatibility from Sts2SupportedVersions.json."
