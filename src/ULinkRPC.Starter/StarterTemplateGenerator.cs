namespace ULinkRPC.Starter;

internal sealed class StarterTemplateGenerator(Action<string, string> runDotNet, Action<string, string> runGit)
{
    public void GenerateTemplate(string rootPath, string projectName, ClientEngineKind clientEngine, TransportKind transport, SerializerKind serializer, ResolvedVersions versions)
    {
        GenerateTemplate(rootPath, projectName, clientEngine, transport, serializer, clientEngine.GetDefaultNuGetForUnitySource(), versions);
    }

    public void GenerateTemplate(string rootPath, string projectName, ClientEngineKind clientEngine, TransportKind transport, SerializerKind serializer, NuGetForUnitySourceKind nuGetForUnitySource, ResolvedVersions versions)
    {
        var context = CreateContext(rootPath, projectName, clientEngine, transport, serializer, nuGetForUnitySource, versions);

        GenerateGitIgnore(context.Paths.RootPath);
        GenerateGitAttributes(context);
        StarterSharedTemplate.Generate(context);
        StarterServerTemplate.Generate(context);
        GenerateSolution(context.Paths.ServerRootPath);
        GenerateClientTemplate(context);
        GenerateCodeGenToolManifest(context);
        GenerateCodeGenScripts(context);
        RunCodeGen(context);
        InitializeGit(context.Paths.RootPath);
    }

    private static StarterTemplateContext CreateContext(
        string rootPath,
        string projectName,
        ClientEngineKind clientEngine,
        TransportKind transport,
        SerializerKind serializer,
        NuGetForUnitySourceKind nuGetForUnitySource,
        ResolvedVersions versions)
    {
        var paths = new StarterPaths(
            rootPath,
            Path.Combine(rootPath, "Shared"),
            Path.Combine(rootPath, "Server"),
            Path.Combine(rootPath, "Server", "Server"),
            Path.Combine(rootPath, "Client"));

        Directory.CreateDirectory(paths.SharedPath);
        Directory.CreateDirectory(paths.ServerRootPath);
        Directory.CreateDirectory(paths.ServerAppPath);
        Directory.CreateDirectory(paths.ClientPath);

        return new StarterTemplateContext(
            projectName,
            MakeCompanyId(projectName),
            clientEngine,
            transport,
            serializer,
            nuGetForUnitySource,
            versions,
            paths);
    }

    private static void GenerateClientTemplate(StarterTemplateContext context)
    {
        switch (context.ClientEngine)
        {
            case ClientEngineKind.Unity:
            case ClientEngineKind.UnityCn:
            case ClientEngineKind.Tuanjie:
                StarterUnityTemplate.Generate(context);
                return;
            case ClientEngineKind.Godot:
                StarterGodotTemplate.Generate(context);
                return;
            default:
                throw new ArgumentOutOfRangeException(nameof(context.ClientEngine), context.ClientEngine, null);
        }
    }

    private void InitializeGit(string rootPath)
    {
        runGit(rootPath, "init");
    }

    private void GenerateSolution(string serverPath)
    {
        var solutionPath = Path.Combine(serverPath, "Server.slnx");
        runDotNet(serverPath, "new sln -n \"Server\" --format slnx");
        runDotNet(serverPath, $"sln \"{solutionPath}\" add \"..{Path.DirectorySeparatorChar}Shared{Path.DirectorySeparatorChar}Shared.csproj\"");
        runDotNet(serverPath, $"sln \"{solutionPath}\" add \"Server{Path.DirectorySeparatorChar}Server.csproj\"");
    }

    private static string MakeCompanyId(string projectName)
    {
        var filtered = new string(projectName.Where(char.IsLetterOrDigit).ToArray());
        return string.IsNullOrWhiteSpace(filtered) ? "ulinkrpc.sample" : $"ulinkrpc.{filtered.ToLowerInvariant()}";
    }

    private void GenerateCodeGenToolManifest(StarterTemplateContext context)
    {
        runDotNet(context.Paths.RootPath, "new tool-manifest");
        runDotNet(context.Paths.RootPath, $"tool install ULinkRPC.CodeGen --version {context.Versions.CodeGen}");
    }

    private void RunCodeGen(StarterTemplateContext context)
    {
        runDotNet(
            context.Paths.ServerAppPath,
            BuildServerCodeGenCommand(context));

        runDotNet(
            context.Paths.ClientPath,
            BuildClientCodeGenCommand(context));
    }

    private static string BuildServerCodeGenCommand(StarterTemplateContext context) =>
        $"tool run ulinkrpc-codegen -- --contracts \"{context.Paths.SharedPath}\" --mode server --server-output \"Generated\" --server-namespace \"Server.Generated\"";

    private static string BuildClientCodeGenCommand(StarterTemplateContext context) =>
        $"tool run ulinkrpc-codegen -- --contracts \"{context.Paths.SharedPath}\" --mode {context.ClientCodeGenMode} --output \"{context.ClientCodeGenOutput}\" --namespace \"Rpc.Generated\"";

    private static void GenerateCodeGenScripts(StarterTemplateContext context)
    {
        var powerShellScript = $$"""
param(
    [switch]$NoRestore
)

$ErrorActionPreference = 'Stop'

$root = $PSScriptRoot
$serverPath = Join-Path $root 'Server\Server'
$clientPath = Join-Path $root 'Client'

$serverArgs = @(
    'tool', 'run', 'ulinkrpc-codegen', '--',
    '--contracts', (Join-Path $root 'Shared'),
    '--mode', 'server',
    '--server-output', 'Generated',
    '--server-namespace', 'Server.Generated'
)

$clientArgs = @(
    'tool', 'run', 'ulinkrpc-codegen', '--',
    '--contracts', (Join-Path $root 'Shared'),
    '--mode', '{{context.ClientCodeGenMode}}',
    '--output', '{{context.ClientCodeGenOutput}}',
    '--namespace', 'Rpc.Generated'
)

if (-not $NoRestore) {
    & dotnet tool restore
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet tool restore failed with exit code $LASTEXITCODE"
    }
}

Push-Location $serverPath
try {
    & dotnet @serverArgs
    if ($LASTEXITCODE -ne 0) {
        throw "Server codegen failed with exit code $LASTEXITCODE"
    }
}
finally {
    Pop-Location
}

Push-Location $clientPath
try {
    & dotnet @clientArgs
    if ($LASTEXITCODE -ne 0) {
        throw "Client codegen failed with exit code $LASTEXITCODE"
    }
}
finally {
    Pop-Location
}
""";

        var shellScript = $$"""
#!/usr/bin/env sh
set -eu

NO_RESTORE=0
if [ "${1-}" = "--no-restore" ]; then
  NO_RESTORE=1
fi

ROOT_DIR="$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)"
SERVER_PATH="$ROOT_DIR/Server/Server"
CLIENT_PATH="$ROOT_DIR/Client"

if [ "$NO_RESTORE" -ne 1 ]; then
  dotnet tool restore
fi

(
  cd "$SERVER_PATH"
  dotnet tool run ulinkrpc-codegen -- \
    --contracts "$ROOT_DIR/Shared" \
    --mode server \
    --server-output Generated \
    --server-namespace Server.Generated
)

(
  cd "$CLIENT_PATH"
  dotnet tool run ulinkrpc-codegen -- \
    --contracts "$ROOT_DIR/Shared" \
    --mode {{context.ClientCodeGenMode}} \
    --output "{{context.ClientCodeGenOutput.Replace("\\", "/")}}" \
    --namespace Rpc.Generated
)
""";

        StarterFileWriter.Write(Path.Combine(context.Paths.RootPath, "codegen.ps1"), powerShellScript);
        StarterFileWriter.Write(Path.Combine(context.Paths.RootPath, "codegen.sh"), shellScript);
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
/_artifacts/
/Client/.godot/

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

# Godot generated files
/Client/.mono/
/Client/export_presets.cfg
/Client/*.csproj
/Client/*.sln

# Logs
*.log
""";

        StarterFileWriter.Write(Path.Combine(rootPath, ".gitignore"), gitIgnore);
    }

    private static void GenerateGitAttributes(StarterTemplateContext context)
    {
        if (context.ClientEngine != ClientEngineKind.Godot)
            return;

        var gitAttributes = """
* text=auto eol=lf

*.cs text eol=lf
*.csproj text eol=lf
*.sln text eol=lf
*.slnx text eol=lf
*.props text eol=lf
*.targets text eol=lf

*.gd text eol=lf
*.tscn text eol=lf
*.tres text eol=lf
*.shader text eol=lf

*.json text eol=lf
*.md text eol=lf
*.yml text eol=lf
*.yaml text eol=lf
*.xml text eol=lf

# Git LFS: commit-worthy binary assets and distributables
*.png filter=lfs diff=lfs merge=lfs -text
*.jpg filter=lfs diff=lfs merge=lfs -text
*.jpeg filter=lfs diff=lfs merge=lfs -text
*.gif filter=lfs diff=lfs merge=lfs -text
*.webp filter=lfs diff=lfs merge=lfs -text
*.ico filter=lfs diff=lfs merge=lfs -text
*.mp3 filter=lfs diff=lfs merge=lfs -text
*.wav filter=lfs diff=lfs merge=lfs -text
*.ogg filter=lfs diff=lfs merge=lfs -text
*.ttf filter=lfs diff=lfs merge=lfs -text
*.otf filter=lfs diff=lfs merge=lfs -text
*.zip filter=lfs diff=lfs merge=lfs -text
*.nupkg filter=lfs diff=lfs merge=lfs -text

# Non-text binaries that should never be line-normalized
*.dll binary
*.exe binary
*.pdb binary
*.so binary
*.dylib binary
*.bin binary
""";

        StarterFileWriter.Write(Path.Combine(context.Paths.RootPath, ".gitattributes"), gitAttributes);
    }
}
