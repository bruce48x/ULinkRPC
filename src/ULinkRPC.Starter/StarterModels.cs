namespace ULinkRPC.Starter;

internal enum TransportKind
{
    Tcp,
    WebSocket,
    Kcp
}

internal enum SerializerKind
{
    Json,
    MemoryPack
}

internal sealed record ResolvedVersions(
    string Core,
    string Server,
    string Client,
    string Transport,
    string Serializer,
    string CodeGen,
    string? SerializerRuntime,
    string? SerializerRuntimeCore);

internal sealed record StarterCliOptions(
    string ProjectName,
    string OutputDir,
    TransportKind? Transport,
    SerializerKind? Serializer);

internal sealed record StarterPaths(
    string RootPath,
    string SharedPath,
    string ServerRootPath,
    string ServerAppPath,
    string ClientPath);

internal sealed record StarterTemplateContext(
    string ProjectName,
    string CompanyId,
    TransportKind Transport,
    SerializerKind Serializer,
    ResolvedVersions Versions,
    StarterPaths Paths)
{
    public string SharedProjectName => Path.GetFileName(Paths.SharedPath);
    public string ServerProjectName => Path.GetFileName(Paths.ServerAppPath);
}

internal sealed record UnityClientArtifacts(
    string Manifest,
    string PackagesConfig,
    string NuGetConfig,
    string Readme,
    string ProjectVersion,
    string EditorBuildSettings,
    string TesterScript,
    string TesterScriptMeta,
    string SceneContent,
    string SceneMeta,
    string AutoOpenSceneEditorScript);

internal static class UnityPackageVersions
{
    public const string Kcp = "2.7.0";
    public const string MicrosoftBclAsyncInterfaces = "10.0.2";
    public const string SystemBuffers = "4.6.1";
    public const string SystemCollectionsImmutable = "6.0.0";
    public const string SystemIoPipelinesForJson = "10.0.2";
    public const string SystemMemoryForJson = "4.6.3";
    public const string SystemMemoryForKcp = "4.5.4";
    public const string SystemTextEncodingsWeb = "10.0.2";
    public const string SystemTextJson = "10.0.2";
    public const string SystemThreadingTasksExtensionsForJson = "4.6.3";
    public const string SystemThreadingTasksExtensionsForKcp = "4.5.4";
    public const string SystemRuntimeCompilerServicesUnsafe = "6.1.2";
    public const string SystemIoPipelines = "10.0.3";
}
