# Maps StS2 install release_info.json "version" (e.g. v0.101.0) to GameVersions / publish folder names (e.g. v0_101_0).
param(
    [Parameter(Mandatory = $true)]
    [string] $Sts2Path
)

$releaseInfo = Join-Path $Sts2Path "release_info.json"
if (-not (Test-Path -LiteralPath $releaseInfo)) {
    Write-Error "release_info.json not found at: $releaseInfo (set Sts2Path or DeployGameVersionFolder manually)."
    exit 2
}

$raw = Get-Content -LiteralPath $releaseInfo -Raw -Encoding UTF8
$j = $raw | ConvertFrom-Json
if (-not $j.version) {
    Write-Error "release_info.json has no 'version' property: $releaseInfo"
    exit 3
}

$ver = [string]$j.version.Trim()
if ($ver.StartsWith("v", [System.StringComparison]::OrdinalIgnoreCase)) {
    $ver = $ver.Substring(1)
}
$folder = "v" + ($ver -replace "\.", "_")
Write-Output $folder.Trim()
