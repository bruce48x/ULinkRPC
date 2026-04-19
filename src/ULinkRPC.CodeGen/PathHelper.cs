using System.Text.RegularExpressions;

namespace ULinkRPC.CodeGen;

internal static class PathHelper
{
    public const string DefaultUnityOutputRelativePath = "Assets/Scripts/Rpc/Generated";
    public const string DefaultGodotOutputRelativePath = "Scripts/Rpc/Generated";
    public const string DefaultClientRuntimeNamespace = "Rpc.Generated";

    public static bool IsUnityProject(string path) =>
        Directory.Exists(Path.Combine(path, "Assets")) &&
        Directory.Exists(Path.Combine(path, "Packages"));

    public static bool IsGodotProject(string path) =>
        File.Exists(Path.Combine(path, "project.godot"));

    public static bool IsServerProjectDirectory(string path) =>
        !IsUnityProject(path) &&
        Directory.Exists(path) &&
        Directory.EnumerateFiles(path, "*.csproj", SearchOption.TopDirectoryOnly).Any();

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

    public static string? FindGodotProjectRoot(string startPath)
    {
        var dir = new DirectoryInfo(startPath);
        while (dir != null)
        {
            if (IsGodotProject(dir.FullName))
                return dir.FullName;
            dir = dir.Parent;
        }

        return null;
    }

    public static string? FindClientProjectRoot(string startPath, OutputMode mode) => mode switch
    {
        OutputMode.Unity => FindUnityProjectRoot(startPath),
        OutputMode.Godot => FindGodotProjectRoot(startPath),
        _ => null
    };

    public static string GetDefaultClientOutputRelativePath(OutputMode mode) => mode switch
    {
        OutputMode.Unity => DefaultUnityOutputRelativePath,
        OutputMode.Godot => DefaultGodotOutputRelativePath,
        _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, "Unsupported client mode.")
    };

    public static string? TryFindNearestAssemblyDefinitionName(string startPath)
    {
        if (string.IsNullOrWhiteSpace(startPath))
            return null;

        var currentPath = Directory.Exists(startPath)
            ? startPath
            : Path.GetDirectoryName(startPath);
        if (string.IsNullOrWhiteSpace(currentPath))
            return null;

        var dir = new DirectoryInfo(currentPath);
        while (dir != null)
        {
            var asmdefs = dir.GetFiles("*.asmdef", SearchOption.TopDirectoryOnly);
            if (asmdefs.Length == 1)
                return TryReadAssemblyDefinitionName(asmdefs[0].FullName);

            if (asmdefs.Length > 1)
                return null;

            dir = dir.Parent;
        }

        return null;
    }

    public static string? FindServerProjectRoot(string startPath)
    {
        var dir = new DirectoryInfo(startPath);
        while (dir != null)
        {
            if (IsServerProjectDirectory(dir.FullName))
                return dir.FullName;
            dir = dir.Parent;
        }

        return null;
    }

    public static string DeriveNamespaceFromOutputPath(string outputPath)
    {
        if (string.IsNullOrWhiteSpace(outputPath))
            return DefaultClientRuntimeNamespace;

        var fullPath = Path.GetFullPath(outputPath);
        var segments = fullPath
            .Split([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
                   StringSplitOptions.RemoveEmptyEntries)
            .ToList();

        if (segments.Count == 0)
            return DefaultClientRuntimeNamespace;

        var startIndex = 0;
        for (var i = 0; i < segments.Count - 1; i++)
        {
            if (segments[i].Equals("Assets", StringComparison.OrdinalIgnoreCase) &&
                segments[i + 1].Equals("Scripts", StringComparison.OrdinalIgnoreCase))
            {
                startIndex = i + 2;
                break;
            }

            if (segments[i].Equals("Scripts", StringComparison.OrdinalIgnoreCase))
            {
                startIndex = i + 1;
                break;
            }
        }

        var relevantSegments = segments.Skip(startIndex).ToList();
        if (relevantSegments.Count == 0)
            return DefaultClientRuntimeNamespace;

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
            ? DefaultClientRuntimeNamespace
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

    private static string? TryReadAssemblyDefinitionName(string asmdefPath)
    {
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(File.ReadAllText(asmdefPath));
            if (doc.RootElement.TryGetProperty("name", out var nameElement) &&
                nameElement.ValueKind == System.Text.Json.JsonValueKind.String)
            {
                return nameElement.GetString();
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
        catch (System.Text.Json.JsonException)
        {
        }

        return null;
    }
}
