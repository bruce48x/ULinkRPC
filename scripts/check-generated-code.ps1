[CmdletBinding()]
param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Debug",

    [ValidateSet("quiet", "minimal", "normal", "detailed", "diagnostic")]
    [string]$Verbosity = "minimal",

    [switch]$NoRestore,
    [switch]$SkipDiffCheck
)

$ErrorActionPreference = "Stop"

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$sampleScript = Join-Path $repoRoot "scripts/sample.ps1"
$samples = @(
    "RpcCall.MemoryPack",
    "RpcCall.Json",
    "RpcCall.Kcp",
    "Agar.MixedTransport"
)

foreach ($sample in $samples) {
    $arguments = @(
        "-NoProfile",
        "-File", $sampleScript,
        "-Sample", $sample,
        "-Configuration", $Configuration,
        "-Verbosity", $Verbosity,
        "-SkipBuild",
        "-DisableBuildServer"
    )

    if ($NoRestore) {
        $arguments += "-NoRestore"
    }

    Write-Host "==> checking generated code for $sample" -ForegroundColor Cyan
    & pwsh @arguments
    if ($LASTEXITCODE -ne 0) {
        throw "Code generation failed for $sample with exit code $LASTEXITCODE"
    }
}

if ($SkipDiffCheck) {
    Write-Host "Generated code completed. Skipping git diff check because -SkipDiffCheck was set."
    exit 0
}

$diffOutput = & git -C $repoRoot status --porcelain
if ($LASTEXITCODE -ne 0) {
    throw "git status failed with exit code $LASTEXITCODE"
}

if (-not [string]::IsNullOrWhiteSpace($diffOutput)) {
    Write-Error @"
Generated code is stale. Run this command locally and commit the result:

  pwsh -NoProfile -File scripts/check-generated-code.ps1

Changed files:
$diffOutput
"@
    exit 1
}

Write-Host "Generated code is up to date."
