$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$rootDir = (Resolve-Path (Join-Path $PSScriptRoot "../..")).Path
$workDir = Join-Path $rootDir ".tmp/starter-stride-daily"
$generatedRoot = Join-Path $workDir "generated"
$transport = if ($env:STARTER_TRANSPORT) { $env:STARTER_TRANSPORT } else { "websocket" }
$serializer = if ($env:STARTER_SERIALIZER) { $env:STARTER_SERIALIZER } else { "json" }
$transportLabel = (Get-Culture).TextInfo.ToTitleCase($transport)
$serializerLabel = (Get-Culture).TextInfo.ToTitleCase($serializer)
$projectName = "StarterStride$transportLabel$serializerLabel"
$projectDir = Join-Path $generatedRoot $projectName
$serverProject = Join-Path $projectDir "Server/Server/Server.csproj"
$clientProject = Join-Path $projectDir "Client/Client.csproj"
$localFeed = Join-Path $rootDir "artifacts/ci-nuget"
$ciNuGetConfig = Join-Path $generatedRoot "NuGet.config"
$nuGetPackages = Join-Path $workDir "nuget-packages"

if ($transport -ne "websocket") {
    throw "Unsupported STARTER_TRANSPORT for Stride starter verification: $transport"
}

if ($serializer -ne "json") {
    throw "Unsupported STARTER_SERIALIZER for Stride starter verification: $serializer"
}

function Invoke-LoggedCommand {
    param(
        [Parameter(Mandatory = $true)]
        [string] $FilePath,

        [Parameter(Mandatory = $true)]
        [string[]] $Arguments
    )

    Write-Host ">> $FilePath $($Arguments -join ' ')"
    & $FilePath @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "Command failed with exit code ${LASTEXITCODE}: $FilePath $($Arguments -join ' ')"
    }
}

function Pack-LocalPackage {
    param(
        [Parameter(Mandatory = $true)]
        [string] $ProjectPath
    )

    Invoke-LoggedCommand -FilePath "dotnet" -Arguments @("pack", $ProjectPath, "-c", "Release", "-o", $localFeed, "--nologo")
}

Remove-Item -LiteralPath $workDir -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item -LiteralPath $localFeed -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Force -Path $generatedRoot, $localFeed, $nuGetPackages | Out-Null
$env:NUGET_PACKAGES = $nuGetPackages
$env:ULINKRPC_STARTER_LOCAL_CODEGEN_PROJECT = Join-Path $rootDir "src/ULinkRPC.CodeGen/ULinkRPC.CodeGen.csproj"

@"
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <clear />
    <add key="local" value="$localFeed" />
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
  </packageSources>
</configuration>
"@ | Set-Content -LiteralPath $ciNuGetConfig -Encoding UTF8

Write-Host "Packing local packages into $localFeed"
Pack-LocalPackage (Join-Path $rootDir "src/ULinkRPC.Core/ULinkRPC.Core.csproj")
Pack-LocalPackage (Join-Path $rootDir "src/ULinkRPC.Client/ULinkRPC.Client.csproj")
Pack-LocalPackage (Join-Path $rootDir "src/ULinkRPC.Server/ULinkRPC.Server.csproj")
Pack-LocalPackage (Join-Path $rootDir "src/ULinkRPC.Transport.WebSocket/ULinkRPC.Transport.WebSocket.csproj")
Pack-LocalPackage (Join-Path $rootDir "src/ULinkRPC.Serializer.Json/ULinkRPC.Serializer.Json.csproj")

Write-Host "Building local CodeGen tool for no-build starter runs"
Invoke-LoggedCommand -FilePath "dotnet" -Arguments @("build", $env:ULINKRPC_STARTER_LOCAL_CODEGEN_PROJECT, "-c", "Debug", "--nologo")

Write-Host "Generating starter project at $projectDir ($transport + $serializer)"
Invoke-LoggedCommand -FilePath "dotnet" -Arguments @(
    "run",
    "--project",
    (Join-Path $rootDir "src/ULinkRPC.Starter/ULinkRPC.Starter.csproj"),
    "--",
    "--name",
    $projectName,
    "--output",
    $generatedRoot,
    "--client-engine",
    "stride3d",
    "--transport",
    $transport,
    "--serializer",
    $serializer,
    "--no-next-steps")

Write-Host "Restoring and building generated server"
Invoke-LoggedCommand -FilePath "dotnet" -Arguments @("restore", $serverProject, "--configfile", $ciNuGetConfig)
Invoke-LoggedCommand -FilePath "dotnet" -Arguments @("build", $serverProject, "-c", "Release", "--no-restore")

Write-Host "Restoring and building generated Stride3D client"
Invoke-LoggedCommand -FilePath "dotnet" -Arguments @("restore", $clientProject, "--configfile", $ciNuGetConfig)
Invoke-LoggedCommand -FilePath "dotnet" -Arguments @("build", $clientProject, "-c", "Release", "--no-restore")

$clientProjectText = Get-Content -Raw -LiteralPath $clientProject
if ($clientProjectText -notmatch 'PackageReference Include="Stride\.CommunityToolkit\.Windows"') {
    throw "Generated Stride3D client is missing Stride.CommunityToolkit.Windows."
}

if ($clientProjectText -notmatch 'PackageReference Include="ULinkRPC\.Transport\.WebSocket"') {
    throw "Generated Stride3D client is missing ULinkRPC.Transport.WebSocket."
}

if ($clientProjectText -notmatch 'PackageReference Include="ULinkRPC\.Serializer\.Json"') {
    throw "Generated Stride3D client is missing ULinkRPC.Serializer.Json."
}

if (-not (Test-Path -LiteralPath (Join-Path $projectDir "Client/Scripts/Rpc/Generated/RpcApi.cs"))) {
    throw "Generated Stride3D client is missing generated RpcApi.cs."
}

Write-Host "Starter Stride3D $transport + $serializer verification passed."
