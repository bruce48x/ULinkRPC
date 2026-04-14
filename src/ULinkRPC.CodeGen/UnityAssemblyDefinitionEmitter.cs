using System.Text;
using System.Text.Json;

namespace ULinkRPC.CodeGen;

internal static class UnityAssemblyDefinitionEmitter
{
    private const string GeneratedAssemblyName = "ULinkRPC.Generated";

    public static bool TryWriteDefaultAssemblyDefinition(ResolvedOptions options)
    {
        if (options.Mode != OutputMode.Unity)
            return false;

        if (Directory.EnumerateFiles(options.OutputPath, "*.asmdef", SearchOption.TopDirectoryOnly).Any())
            return false;

        var references = new List<string>();
        var contractAssemblyName = PathHelper.TryFindNearestAssemblyDefinitionName(options.ContractsPath);
        if (!string.IsNullOrWhiteSpace(contractAssemblyName))
            references.Add(contractAssemblyName);

        references.Add("ULinkRPC.Core");
        references.Add("ULinkRPC.Client");

        var asmdefPath = Path.Combine(options.OutputPath, $"{GeneratedAssemblyName}.asmdef");
        var content = BuildAssemblyDefinitionJson(references);
        File.WriteAllText(asmdefPath, content, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        return true;
    }

    private static string BuildAssemblyDefinitionJson(IReadOnlyList<string> references)
    {
        var payload = new Dictionary<string, object?>
        {
            ["name"] = GeneratedAssemblyName,
            ["references"] = references,
            ["includePlatforms"] = Array.Empty<string>(),
            ["excludePlatforms"] = Array.Empty<string>(),
            ["allowUnsafeCode"] = false,
            ["overrideReferences"] = false,
            ["precompiledReferences"] = Array.Empty<string>(),
            ["autoReferenced"] = true,
            ["defineConstraints"] = Array.Empty<string>(),
            ["versionDefines"] = Array.Empty<object>(),
            ["noEngineReferences"] = false
        };

        return JsonSerializer.Serialize(payload, new JsonSerializerOptions
        {
            WriteIndented = true
        }) + Environment.NewLine;
    }
}
