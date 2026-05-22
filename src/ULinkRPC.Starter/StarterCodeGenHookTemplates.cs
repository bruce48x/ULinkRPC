namespace ULinkRPC.Starter;

internal static class StarterCodeGenHookTemplates
{
    public static string RenderServerTargets() =>
        Render(
            mode: "server",
            contractsPath: "../../Shared",
            generatedCompilePath: "Generated",
            expectedOutput: "Generated/AllServicesBinder.cs",
            commandArguments: "--contracts &quot;$(ULinkRPCContractsPath)&quot; --mode server --server-output &quot;$(ULinkRPCServerOutput)&quot; --server-namespace &quot;$(ULinkRPCServerNamespace)&quot;",
            extraProperties: "    <ULinkRPCServerOutput>Generated</ULinkRPCServerOutput>\n    <ULinkRPCServerNamespace>Server.Generated</ULinkRPCServerNamespace>");

    public static string RenderClientTargets(string mode) =>
        Render(
            mode: mode,
            contractsPath: "../Shared",
            generatedCompilePath: "Scripts/Rpc/Generated",
            expectedOutput: "Scripts/Rpc/Generated/RpcApi.cs",
            commandArguments: "--contracts &quot;$(ULinkRPCContractsPath)&quot; --mode $(ULinkRPCCodeGenMode) --output &quot;$(ULinkRPCOutputPath)&quot; --namespace &quot;$(ULinkRPCGeneratedNamespace)&quot;",
            extraProperties: "    <ULinkRPCOutputPath>Scripts/Rpc/Generated</ULinkRPCOutputPath>\n    <ULinkRPCGeneratedNamespace>Rpc.Generated</ULinkRPCGeneratedNamespace>");

    public static string RenderStrideClientTargets() =>
        Render(
            mode: "stride3d",
            contractsPath: "../../Shared",
            generatedCompilePath: "Scripts/Rpc/Generated",
            expectedOutput: "Scripts/Rpc/Generated/RpcApi.cs",
            commandArguments: "--contracts &quot;$(ULinkRPCContractsPath)&quot; --mode $(ULinkRPCCodeGenMode) --output &quot;$(ULinkRPCOutputPath)&quot; --namespace &quot;$(ULinkRPCGeneratedNamespace)&quot;",
            extraProperties: "    <ULinkRPCOutputPath>Scripts/Rpc/Generated</ULinkRPCOutputPath>\n    <ULinkRPCGeneratedNamespace>Rpc.Generated</ULinkRPCGeneratedNamespace>");

    private static string Render(
        string mode,
        string contractsPath,
        string generatedCompilePath,
        string expectedOutput,
        string commandArguments,
        string extraProperties) =>
        $$"""
  <PropertyGroup>
    <ULinkRPCCodeGenEnabled Condition="'$(ULinkRPCCodeGenEnabled)' == ''">true</ULinkRPCCodeGenEnabled>
    <ULinkRPCContractsPath>{{contractsPath}}</ULinkRPCContractsPath>
    <ULinkRPCCodeGenMode>{{mode}}</ULinkRPCCodeGenMode>
    <ULinkRPCGeneratedCompilePath>{{generatedCompilePath}}</ULinkRPCGeneratedCompilePath>
    <ULinkRPCCodeGenExpectedOutput>{{expectedOutput}}</ULinkRPCCodeGenExpectedOutput>
    <ULinkRPCCodeGenStampFile>$(IntermediateOutputPath)ULinkRPCCodeGen.stamp</ULinkRPCCodeGenStampFile>
    <ULinkRPCCodeGenCommand Condition="'$(ULINKRPC_STARTER_LOCAL_CODEGEN_PROJECT)' != ''">dotnet run --no-restore --no-build --project &quot;$(ULINKRPC_STARTER_LOCAL_CODEGEN_PROJECT)&quot; --</ULinkRPCCodeGenCommand>
    <ULinkRPCCodeGenCommand Condition="'$(ULinkRPCCodeGenCommand)' == ''">dotnet tool run ulinkrpc-codegen --</ULinkRPCCodeGenCommand>
{{extraProperties}}
  </PropertyGroup>

  <ItemGroup Condition="'$(ULinkRPCCodeGenEnabled)' == 'true'">
    <Compile Remove="$(ULinkRPCGeneratedCompilePath)/**/*.cs" />
    <ULinkRPCCodeGenInput Include="$(ULinkRPCContractsPath)/**/*.cs" />
  </ItemGroup>

  <Target Name="ULinkRPCGenerateCode"
          BeforeTargets="ULinkRPCIncludeGeneratedCode"
          Condition="'$(ULinkRPCCodeGenEnabled)' == 'true'"
          Inputs="@(ULinkRPCCodeGenInput)"
          Outputs="$(ULinkRPCCodeGenStampFile);$(ULinkRPCCodeGenExpectedOutput)">
    <Message Importance="high" Text="Running ULinkRPC.CodeGen ($(ULinkRPCCodeGenMode))" />
    <Exec Command="$(ULinkRPCCodeGenCommand) {{commandArguments}}" />
    <MakeDir Directories="$(IntermediateOutputPath)" />
    <Touch Files="$(ULinkRPCCodeGenStampFile)" AlwaysCreate="true" />
  </Target>

  <Target Name="ULinkRPCIncludeGeneratedCode"
          BeforeTargets="CoreCompile"
          DependsOnTargets="ULinkRPCGenerateCode"
          Condition="'$(ULinkRPCCodeGenEnabled)' == 'true'">
    <ItemGroup>
      <Compile Include="$(ULinkRPCGeneratedCompilePath)/**/*.cs" />
    </ItemGroup>
  </Target>
""";
}
