using System.Text;

namespace ULinkRPC.Starter;

internal enum TransportKind
{
    Tcp,
    WebSocket,
    Kcp,
    Loopback
}

internal enum SerializerKind
{
    Json,
    MemoryPack
}

internal sealed record ResolvedVersions(string Server, string Client, string Transport, string Serializer);

internal sealed class StarterTemplateGenerator(Action<string, string> runDotNet, Action<string, string> runGit)
{
    public void GenerateTemplate(string rootPath, string projectName, TransportKind transport, SerializerKind serializer, ResolvedVersions versions)
    {
        const string sharedProjectName = "Shared";
        const string clientProjectName = "Client";

        var sharedPath = Path.Combine(rootPath, sharedProjectName);
        var serverPath = Path.Combine(rootPath, "Server");
        var serverAppPath = Path.Combine(serverPath, "Server");
        var clientPath = Path.Combine(rootPath, clientProjectName);

        Directory.CreateDirectory(sharedPath);
        Directory.CreateDirectory(serverPath);
        Directory.CreateDirectory(serverAppPath);
        Directory.CreateDirectory(clientPath);

        var companyId = MakeCompanyId(projectName);

        GenerateGitIgnore(rootPath);
        GenerateShared(sharedPath, companyId);
        GenerateServer(serverAppPath, transport, serializer, versions);
        GenerateSolution(serverPath);
        GenerateUnityClient(clientPath, sharedProjectName, companyId, transport, serializer, versions);
        InitializeGit(rootPath);
    }

    private void InitializeGit(string rootPath)
    {
        runGit(rootPath, "init");
    }

    private void GenerateSolution(string serverPath)
    {
        var solutionPath = Path.Combine(serverPath, "Server.slnx");
        runDotNet(serverPath, "new sln -n \"Server\"");
        runDotNet(serverPath, $"sln \"{solutionPath}\" add \"..{Path.DirectorySeparatorChar}Shared{Path.DirectorySeparatorChar}Shared.csproj\"");
        runDotNet(serverPath, $"sln \"{solutionPath}\" add \"Server{Path.DirectorySeparatorChar}Server.csproj\"");
    }

    private static string MakeCompanyId(string projectName)
    {
        var filtered = new string(projectName.Where(char.IsLetterOrDigit).ToArray());
        return string.IsNullOrWhiteSpace(filtered) ? "ulinkrpc.sample" : $"ulinkrpc.{filtered.ToLowerInvariant()}";
    }

    private static void GenerateShared(string sharedPath, string companyId)
    {
        var projectName = Path.GetFileName(sharedPath);

        // Shared code is consumed by Unity 2022, so generated source must stay within C# 9.0.
        var csproj = """
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFrameworks>netstandard2.1;net10.0</TargetFrameworks>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <LangVersion>9.0</LangVersion>
    <RootNamespace>Shared</RootNamespace>
  </PropertyGroup>
</Project>
""";

        var contracts = """
namespace Shared.Interfaces
{
    public sealed class PingRequest
    {
        public string Message { get; set; } = string.Empty;
    }

    public sealed class PingReply
    {
        public string Message { get; set; } = string.Empty;
        public DateTimeOffset ServerTime { get; set; }
    }
}
""";

        var interfacesDir = Path.Combine(sharedPath, "Interfaces");
        Directory.CreateDirectory(interfacesDir);

        var packageJson = $$"""
{
  "name": "com.{{companyId}}.shared",
  "version": "1.0.0",
  "displayName": "{{projectName}} Shared",
  "description": "Shared DTO and utility code",
  "unity": "2022.3",
  "author": {
    "name": "Shared"
  }
}
""";

        var asmdef = """
{
  "name": "Shared",
  "rootNamespace": "Shared",
  "references": [],
  "includePlatforms": [],
  "excludePlatforms": [],
  "allowUnsafeCode": false,
  "overrideReferences": false,
  "precompiledReferences": [],
  "autoReferenced": true,
  "defineConstraints": [],
  "versionDefines": [],
  "noEngineReferences": true
}
""";

        WriteFile(Path.Combine(sharedPath, $"{projectName}.csproj"), csproj);
        WriteFile(Path.Combine(sharedPath, "Interfaces", "SharedDtos.cs"), contracts);
        WriteFile(Path.Combine(sharedPath, $"{projectName}.asmdef"), asmdef);
        WriteFile(Path.Combine(sharedPath, "package.json"), packageJson);
    }

    private static void GenerateGitIgnore(string rootPath)
    {
        var gitIgnore = """
# OS / Editor
.DS_Store
Thumbs.db
.idea/
.vs/
*.suo
*.user
*.userprefs
*.DotSettings.user

# .NET build outputs
**/bin/
**/obj/

# Unity generated folders
/Client/[Ll]ibrary/
/Client/[Tt]emp/
/Client/[Ll]ogs/
/Client/[Uu]ser[Ss]ettings/
/Client/[Oo]bj/
/Client/[Bb]uild/
/Client/[Bb]uilds/
/Client/[Mm]emoryCaptures/
/Client/[Rr]ecordings/

# Unity generated project/IDE files
/Client/*.csproj
/Client/*.sln
/Client/*.slnx
/Client/*.unityproj
/Client/*.pidb
/Client/*.booproj
/Client/*.svd
/Client/*.pdb
/Client/*.mdb
/Client/*.opendb
/Client/*.VC.db

# NuGetForUnity restored packages
/Client/Assets/Packages/

# Logs
*.log
""";

        WriteFile(Path.Combine(rootPath, ".gitignore"), gitIgnore);
    }

    private static void GenerateServer(
        string serverPath,
        TransportKind transport,
        SerializerKind serializer,
        ResolvedVersions versions)
    {
        var serverProjectName = Path.GetFileName(serverPath);
        var transportPackage = GetTransportPackage(transport);
        var serializerPackage = GetSerializerPackage(serializer);

        var csproj = $$"""
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <RootNamespace>Server</RootNamespace>
  </PropertyGroup>

  <ItemGroup>
    <ProjectReference Include="..\..\Shared\Shared.csproj" />
  </ItemGroup>

  <ItemGroup>
    <PackageReference Include="ULinkRPC.Server" Version="{{versions.Server}}" />
    <PackageReference Include="{{transportPackage}}" Version="{{versions.Transport}}" />
    <PackageReference Include="{{serializerPackage}}" Version="{{versions.Serializer}}" />
  </ItemGroup>
</Project>
""";

        var program = $$"""
using Shared.Interfaces;

Console.WriteLine("{{serverProjectName}} started.");
Console.WriteLine("Selected transport: {{transport}}");
Console.WriteLine("Selected serializer: {{serializer}}");

var demo = new PingReply
{
    Message = "ULinkRPC starter is ready.",
    ServerTime = DateTimeOffset.UtcNow
};

Console.WriteLine($"Demo shared DTO => {demo.Message} @ {demo.ServerTime:O}");
""";

        WriteFile(Path.Combine(serverPath, $"{serverProjectName}.csproj"), csproj);
        WriteFile(Path.Combine(serverPath, "Program.cs"), program);
    }

    private static void GenerateUnityClient(string clientPath, string sharedProjectName, string companyId, TransportKind transport, SerializerKind serializer, ResolvedVersions versions)
    {
        Directory.CreateDirectory(Path.Combine(clientPath, "Assets"));
        Directory.CreateDirectory(Path.Combine(clientPath, "Packages"));
        Directory.CreateDirectory(Path.Combine(clientPath, "ProjectSettings"));

        var manifest = $$"""
{
  "dependencies": {
    "com.github-glitchenzo.nugetforunity": "4.5.0",
    "com.unity.ide.rider": "3.0.39",
    "com.unity.ide.visualstudio": "2.0.23",
    "com.unity.modules.uielements": "1.0.0",
    "com.unity.ugui": "1.0.0",
    "com.{{companyId}}.shared": "file:../../{{sharedProjectName}}"
  },
  "scopedRegistries": [
    {
      "name": "OpenUPM",
      "url": "https://package.openupm.com",
      "scopes": [
        "com.github-glitchenzo.nugetforunity"
      ]
    }
  ]
}
""";

        var transportPackage = GetTransportPackage(transport);
        var serializerPackage = GetSerializerPackage(serializer);
        var packagesConfig = $$"""
<?xml version="1.0" encoding="utf-8"?>
<packages>
  <package id="ULinkRPC.Client" version="{{versions.Client}}" />
  <package id="{{transportPackage}}" version="{{versions.Transport}}" manuallyInstalled="true" />
  <package id="{{serializerPackage}}" version="{{versions.Serializer}}" manuallyInstalled="true" />
</packages>
""";

        var nugetConfig = """
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <clear />
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" enableCredentialProvider="false" />
  </packageSources>
  <config>
    <add key="packageInstallLocation" value="CustomWithinAssets" />
    <add key="repositoryPath" value="./Packages" />
    <add key="PackagesConfigDirectoryPath" value="." />
    <add key="slimRestore" value="true" />
    <add key="PreferNetStandardOverNetFramework" value="true" />
  </config>
</configuration>
""";

        var readme = $$"""
# Unity Client Starter (Unity 2022 LTS)

1. Open this folder with Unity 2022 LTS.
2. Wait for `NuGetForUnity` import.
3. In Unity: `NuGet -> Restore Packages` to install ULinkRPC latest packages.
4. Shared code is provided by local UPM package:
   - `com.{{companyId}}.shared` -> `../../{{sharedProjectName}}`

Selected transport: {{transport}}
Selected serializer: {{serializer}}
""";

        var projectVersion = "m_EditorVersion: 2022.3.62f3c1\nm_EditorVersionWithRevision: 2022.3.62f3c1 (1623fc0bbb97)\n";

        WriteFile(Path.Combine(clientPath, "Packages", "manifest.json"), manifest);
        WriteFile(Path.Combine(clientPath, "Assets", "packages.config"), packagesConfig);
        WriteFile(Path.Combine(clientPath, "Assets", "NuGet.config"), nugetConfig);
        WriteFile(Path.Combine(clientPath, "README.md"), readme);
        WriteFile(Path.Combine(clientPath, "ProjectSettings", "ProjectVersion.txt"), projectVersion);
    }

    private static string GetTransportPackage(TransportKind transport) => transport switch
    {
        TransportKind.Tcp => "ULinkRPC.Transport.Tcp",
        TransportKind.WebSocket => "ULinkRPC.Transport.WebSocket",
        TransportKind.Kcp => "ULinkRPC.Transport.Kcp",
        TransportKind.Loopback => "ULinkRPC.Transport.Loopback",
        _ => throw new ArgumentOutOfRangeException(nameof(transport), transport, null)
    };

    private static string GetSerializerPackage(SerializerKind serializer) => serializer switch
    {
        SerializerKind.Json => "ULinkRPC.Serializer.Json",
        SerializerKind.MemoryPack => "ULinkRPC.Serializer.MemoryPack",
        _ => throw new ArgumentOutOfRangeException(nameof(serializer), serializer, null)
    };

    private static void WriteFile(string path, string content)
    {
        var normalized = content.Replace("\r\n", "\n").TrimStart('\ufeff');
        if (!normalized.EndsWith('\n'))
        {
            normalized += "\n";
        }

        File.WriteAllText(path, normalized, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }
}
