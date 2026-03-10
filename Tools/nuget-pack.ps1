$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $PSScriptRoot
$artifacts = Join-Path $root "artifacts"

$projects = @(
    (Join-Path $root "src\ULinkRPC.Core\ULinkRPC.Core.csproj"),
    (Join-Path $root "src\ULinkRPC.Client\ULinkRPC.Client.csproj"),
    (Join-Path $root "src\ULinkRPC.Client.Unity\ULinkRPC.Client.Unity.csproj"),
    (Join-Path $root "src\ULinkRPC.Server\ULinkRPC.Server.csproj"),
    (Join-Path $root "src\ULinkRPC.Transport.Tcp\ULinkRPC.Transport.Tcp.csproj"),
    (Join-Path $root "src\ULinkRPC.Transport.WebSocket\ULinkRPC.Transport.WebSocket.csproj"),
    (Join-Path $root "src\ULinkRPC.Transport.Kcp\ULinkRPC.Transport.Kcp.csproj"),
    (Join-Path $root "src\ULinkRPC.Transport.Loopback\ULinkRPC.Transport.Loopback.csproj"),
    (Join-Path $root "src\ULinkRPC.Serializer.MemoryPack\ULinkRPC.Serializer.MemoryPack.csproj"),
    (Join-Path $root "src\ULinkRPC.Serializer.Json\ULinkRPC.Serializer.Json.csproj"),
    (Join-Path $root "src\ULinkRPC.CodeGen\ULinkRPC.CodeGen.csproj")
)

if (!(Test-Path $artifacts)) {
    New-Item -ItemType Directory -Path $artifacts | Out-Null
}

foreach ($project in $projects) {
    dotnet pack $project -c Release -o $artifacts
}
