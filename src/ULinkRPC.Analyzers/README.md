# ULinkRPC.Analyzers

Roslyn analyzers and source generators for ULinkRPC contract projects.

Generated starter server and SDK-style client projects reference this package as a private build dependency. It generates RPC client and server glue at compile time from interfaces annotated with `RpcService`, `RpcMethod`, `RpcNotificationContract`, and `RpcNotification`, and reports diagnostics for invalid or duplicate contract ids.

Unity-compatible client assemblies should opt in with `[assembly: ULinkRPCGenerateClient("Rpc.Generated")]` so only one Unity script assembly receives generated client glue.

Typical projects should add this package with:

```xml
<PackageReference Include="ULinkRPC.Analyzers" Version="0.1.8">
  <PrivateAssets>all</PrivateAssets>
  <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
</PackageReference>
```
