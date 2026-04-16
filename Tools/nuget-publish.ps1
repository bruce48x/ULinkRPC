$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $PSScriptRoot
$artifacts = Join-Path $root "artifacts"
$release = Join-Path $artifacts "release"
$apiKey = $env:NUGET_API_KEY

if ([string]::IsNullOrWhiteSpace($apiKey)) {
    throw "NUGET_API_KEY is not set."
}

if (!(Test-Path $release)) {
    throw "Release folder not found. Run Tools/nuget-pack.ps1 first."
}

$patterns = @(
    "ULinkRPC.Core*.nupkg",
    "ULinkRPC.Client*.nupkg",
    "ULinkRPC.Server*.nupkg",
    "ULinkRPC.Transport.Tcp*.nupkg",
    "ULinkRPC.Transport.WebSocket*.nupkg",
    "ULinkRPC.Transport.Kcp*.nupkg",
    "ULinkRPC.Transport.Loopback*.nupkg",
    "ULinkRPC.Serializer.MemoryPack*.nupkg",
    "ULinkRPC.Serializer.Json*.nupkg",
    "ULinkRPC.CodeGen*.nupkg",
    "ULinkRPC.Starter*.nupkg"
)

$packages = @()
foreach ($pattern in $patterns) {
    $packages += Get-ChildItem -Path $release -Filter $pattern
}

if ($packages.Count -eq 0) {
    throw "No ULinkRPC packages found in artifacts."
}

foreach ($pkg in $packages) {
    dotnet nuget push $pkg.FullName --api-key $apiKey --source "https://api.nuget.org/v3/index.json" --skip-duplicate
}
