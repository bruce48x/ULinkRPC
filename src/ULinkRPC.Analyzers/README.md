# ULinkRPC.Analyzers

Roslyn analyzers and source generators for ULinkRPC contract projects.

Generated starter server and SDK-style client projects reference this package as a private build dependency. It generates RPC client and server glue at compile time from interfaces annotated with `RpcService`, `RpcMethod`, `RpcCallback`, and `RpcPush`, and reports diagnostics for invalid or duplicate contract ids.

Typical projects should add this package with:

```xml
<PackageReference Include="ULinkRPC.Analyzers" Version="0.1.4">
  <PrivateAssets>all</PrivateAssets>
  <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
</PackageReference>
```
