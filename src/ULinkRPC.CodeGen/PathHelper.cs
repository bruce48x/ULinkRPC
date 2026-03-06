using System.Text.RegularExpressions;

namespace ULinkRPC.CodeGen;

internal static class PathHelper
{
    public const string DefaultUnityOutputRelativePath = "Assets/Scripts/Rpc/RpcGenerated";
    public const string DefaultUnityRuntimeNamespace = "Rpc.Generated";

    public static bool IsUnityProject(string path) =>
        Directory.Exists(Path.Combine(path, "Assets")) &&
        Directory.Exists(Path.Combine(path, "Packages"));

    public static string? FindUnityProjectRoot(string startPath)
    {
        var dir = new DirectoryInfo(startPath);
        while (dir != null)
        {
            if (IsUnityProject(dir.FullName))
                return dir.FullName;
            dir = dir.Parent;
        }
        return null;
    }

    public static string DeriveNamespaceFromOutputPath(string outputPath)
    {
        if (string.IsNullOrWhiteSpace(outputPath))
            return DefaultUnityRuntimeNamespace;

        var fullPath = Path.GetFullPath(outputPath);
        var segments = fullPath
            .Split([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
                   StringSplitOptions.RemoveEmptyEntries)
            .ToList();

        if (segments.Count == 0)
            return DefaultUnityRuntimeNamespace;

        var startIndex = 0;
        for (var i = 0; i < segments.Count - 1; i++)
        {
            if (segments[i].Equals("Assets", StringComparison.OrdinalIgnoreCase) &&
                segments[i + 1].Equals("Scripts", StringComparison.OrdinalIgnoreCase))
            {
                startIndex = i + 2;
                break;
            }
        }

        var relevantSegments = segments.Skip(startIndex).ToList();
        if (relevantSegments.Count == 0)
            return DefaultUnityRuntimeNamespace;

        var normalizedSegments = new List<string>();
        for (var i = 0; i < relevantSegments.Count; i++)
        {
            var current = relevantSegments[i];
            if (i > 0)
            {
                var previous = relevantSegments[i - 1];
                if (current.EndsWith("Generated", StringComparison.Ordinal) &&
                    current.StartsWith(previous, StringComparison.Ordinal) &&
                    current.Length > previous.Length)
                {
                    current = current[previous.Length..];
                }
            }

            var identifier = ToNamespaceIdentifier(current);
            if (!string.IsNullOrWhiteSpace(identifier))
                normalizedSegments.Add(identifier);
        }

        return normalizedSegments.Count == 0
            ? DefaultUnityRuntimeNamespace
            : string.Join('.', normalizedSegments);
    }

    public static string ToNamespaceIdentifier(string segment)
    {
        if (string.IsNullOrWhiteSpace(segment))
            return string.Empty;

        var sanitized = Regex.Replace(segment, "[^A-Za-z0-9_]", string.Empty);
        if (string.IsNullOrWhiteSpace(sanitized))
            return string.Empty;

        if (char.IsDigit(sanitized[0]))
            sanitized = $"_{sanitized}";

        return sanitized;
    }
}
