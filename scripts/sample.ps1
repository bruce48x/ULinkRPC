[CmdletBinding()]
param(
    [ValidateSet("RpcCall.MemoryPack", "RpcCall.Json")]
    [string]$Sample = "RpcCall.MemoryPack",

    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Debug",

    [ValidateSet("quiet", "minimal", "normal", "detailed", "diagnostic")]
    [string]$Verbosity = "minimal",

    [ValidateSet("Unity", "Server", "All")]
    [string]$GenMode = "All",

    [int]$Port,
    [switch]$Run,
    [switch]$SkipGen,
    [switch]$SkipBuild,
    [switch]$NoRestore,
    [switch]$NoBuildTool,
    [switch]$AllowParallel,
    [switch]$DisableBuildServer,
    [switch]$IgnoreFailedSources,
    [switch]$CleanGenerated,

    [Parameter(ValueFromRemainingArguments = $true)]
    [string[]]$ServerArgs = @()
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$toolProject = Join-Path $repoRoot "src/ULinkRPC.CodeGen/ULinkRPC.CodeGen.csproj"

$sampleConfig = @{
    "RpcCall.MemoryPack" = @{
        Project = "samples/RpcCall.MemoryPack/RpcCall.MemoryPack.Server/RpcCall.MemoryPack.Server/RpcCall.MemoryPack.Server.csproj"
        Contracts = "samples/RpcCall.MemoryPack/RpcCall.MemoryPack.Unity/Packages/com.samples.contracts"
        UnityOutput = "samples/RpcCall.MemoryPack/RpcCall.MemoryPack.Unity/Assets/Scripts/Rpc/RpcGenerated"
        ServerOutput = "samples/RpcCall.MemoryPack/RpcCall.MemoryPack.Server/RpcCall.MemoryPack.Server/Generated"
    }
    "RpcCall.Json" = @{
        Project = "samples/RpcCall.Json/RpcCall.Json.Server/RpcCall.Json.Server/RpcCall.Json.Server.csproj"
        Contracts = "samples/RpcCall.Json/RpcCall.Json.Unity/Packages/com.samples.contracts"
        UnityOutput = "samples/RpcCall.Json/RpcCall.Json.Unity/Assets/Scripts/Rpc/RpcGenerated"
        ServerOutput = "samples/RpcCall.Json/RpcCall.Json.Server/RpcCall.Json.Server/Generated"
    }
}

$config = $sampleConfig[$Sample]
if ($null -eq $config) {
    throw "Unsupported sample: $Sample"
}

$projectPath = Join-Path $repoRoot $config.Project
$contractsPath = Join-Path $repoRoot $config.Contracts
$unityOutputPath = Join-Path $repoRoot $config.UnityOutput
$serverOutputPath = Join-Path $repoRoot $config.ServerOutput

foreach ($path in @($projectPath, $contractsPath)) {
    if (-not (Test-Path $path)) {
        throw "Required path not found: $path"
    }
}

$env:DOTNET_CLI_HOME = Join-Path $repoRoot ".dotnet"
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = "1"
$env:NUGET_PACKAGES = Join-Path $repoRoot ".nuget\packages"
$env:MSBUILDDISABLENODEREUSE = "1"

foreach ($path in @($env:DOTNET_CLI_HOME, $env:NUGET_PACKAGES)) {
    if (-not (Test-Path $path)) {
        New-Item -ItemType Directory -Path $path | Out-Null
    }
}

function Invoke-DotNet {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Command,

        [string[]]$Arguments = @()
    )

    Write-Host "==> dotnet $Command $($Arguments -join ' ')" -ForegroundColor Cyan
    & dotnet $Command @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet $Command failed with exit code $LASTEXITCODE"
    }
}

function Get-MsBuildArgs {
    if ($AllowParallel) {
        return @("-m")
    }

    return @("-m:1", "/nr:false")
}

function Remove-GeneratedFiles {
    param([string]$Path)

    if (-not (Test-Path $Path)) {
        return
    }

    Get-ChildItem -Path $Path -File | Remove-Item
}

if (-not $SkipGen) {
    if (-not $NoBuildTool) {
        $toolBuildArgs = @($toolProject, "-c", $Configuration, "-v", $Verbosity) + (Get-MsBuildArgs)
        if ($DisableBuildServer) {
            $toolBuildArgs += "--disable-build-servers"
        }
        if ($NoRestore) {
            $toolBuildArgs += "--no-restore"
        }

        Invoke-DotNet -Command "build" -Arguments $toolBuildArgs
    }

    if ($CleanGenerated) {
        if ($GenMode -in @("Unity", "All")) {
            Remove-GeneratedFiles -Path $unityOutputPath
        }
        if ($GenMode -in @("Server", "All")) {
            Remove-GeneratedFiles -Path $serverOutputPath
        }
    }

    $toolRunBaseArgs = @("run", "--project", $toolProject, "-c", $Configuration)
    if ($NoBuildTool) {
        $toolRunBaseArgs += "--no-build"
    }
    if ($NoRestore) {
        $toolRunBaseArgs += "--no-restore"
    }
    $toolRunBaseArgs += "--"

    if ($GenMode -in @("Unity", "All")) {
        $unityArgs = @() + $toolRunBaseArgs + @(
            "--contracts", $contractsPath,
            "--mode", "unity",
            "--output", $unityOutputPath
        )
        Write-Host "==> dotnet $($unityArgs -join ' ')" -ForegroundColor Cyan
        & dotnet @unityArgs
        if ($LASTEXITCODE -ne 0) {
            throw "Unity codegen failed with exit code $LASTEXITCODE"
        }
    }

    if ($GenMode -in @("Server", "All")) {
        $serverGenArgs = @() + $toolRunBaseArgs + @(
            "--contracts", $contractsPath,
            "--mode", "server",
            "--server-output", $serverOutputPath
        )
        Write-Host "==> dotnet $($serverGenArgs -join ' ')" -ForegroundColor Cyan
        & dotnet @serverGenArgs
        if ($LASTEXITCODE -ne 0) {
            throw "Server codegen failed with exit code $LASTEXITCODE"
        }
    }
}

if (-not $SkipBuild) {
    $buildArgs = @($projectPath, "-c", $Configuration, "-v", $Verbosity) + (Get-MsBuildArgs)
    if ($DisableBuildServer) {
        $buildArgs += "--disable-build-servers"
    }
    if ($NoRestore) {
        $buildArgs += "--no-restore"
    } else {
        if (-not $AllowParallel) {
            $buildArgs += "/p:RestoreDisableParallel=true"
        }
        if ($IgnoreFailedSources) {
            $buildArgs += "--ignore-failed-sources"
        }
    }

    Invoke-DotNet -Command "build" -Arguments $buildArgs
}

if ($Run) {
    $runArgs = @("run", "--project", $projectPath, "-c", $Configuration, "--no-launch-profile")
    if (-not $SkipBuild) {
        $runArgs += "--no-build"
    }
    if ($NoRestore) {
        $runArgs += "--no-restore"
    }
    $runArgs += "--"

    if ($PSBoundParameters.ContainsKey("Port")) {
        $runArgs += $Port.ToString()
    }

    if ($ServerArgs.Count -gt 0) {
        $runArgs += $ServerArgs
    }

    Write-Host "==> dotnet $($runArgs -join ' ')" -ForegroundColor Cyan
    & dotnet @runArgs
    exit $LASTEXITCODE
}
