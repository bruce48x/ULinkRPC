namespace ULinkRPC.CodeGen;

internal static class CliParser
{
    public static void PrintUsage()
    {
        Console.WriteLine("ULinkRPC.CodeGen usage:");
        Console.WriteLine("  ulinkrpc-codegen [options]");
        Console.WriteLine();
        Console.WriteLine("Options:");
        Console.WriteLine("  --contracts <path>      Path to contract sources");
        Console.WriteLine("  --output <path>         Output directory for generated files");
        Console.WriteLine("  --namespace <ns>        Namespace for generated client code");
        Console.WriteLine("  --server-output <path>  Output directory for server binders");
        Console.WriteLine("  --server-namespace <ns> Namespace for server binders");
        Console.WriteLine("  --mode <unity|godot|server> Generation mode (optional if current directory can be auto-detected)");
        Console.WriteLine();
        Console.WriteLine("Defaults:");
        Console.WriteLine("  unity: output defaults to Assets/Scripts/Rpc/Generated under Unity project root.");
        Console.WriteLine("  godot: output defaults to Scripts/Rpc/Generated under Godot project root.");
        Console.WriteLine("  client modes: namespace defaults to value derived from output path.");
        Console.WriteLine("  server: output defaults to ./Generated");
    }

    public static bool TryParseCliArguments(string[] args, out RawOptions options, out string error)
    {
        options = RawOptions.Empty;
        var contractsPath = string.Empty;
        var outputPath = string.Empty;
        var clientNamespace = string.Empty;
        var serverOutputPath = string.Empty;
        var serverNamespace = string.Empty;
        var mode = OutputMode.Unknown;
        error = string.Empty;

        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            if (arg == "--contracts" && i + 1 < args.Length)
                contractsPath = args[++i];
            else if (arg == "--output" && i + 1 < args.Length)
                outputPath = args[++i];
            else if (arg == "--namespace" && i + 1 < args.Length)
                clientNamespace = args[++i];
            else if (arg == "--server-output" && i + 1 < args.Length)
                serverOutputPath = args[++i];
            else if (arg == "--server-namespace" && i + 1 < args.Length)
                serverNamespace = args[++i];
            else if (arg == "--mode" && i + 1 < args.Length)
            {
                var value = args[++i];
                if (!TryParseMode(value, out var parsedMode))
                {
                    error = $"Unknown mode: {value}";
                    return false;
                }
                mode = parsedMode;
            }
            else
            {
                error = $"Unknown or incomplete option: {arg}";
                return false;
            }
        }

        options = new RawOptions(contractsPath, outputPath, clientNamespace, serverOutputPath, serverNamespace, mode);
        return true;
    }

    public static bool TryResolveGenerationOptions(
        RawOptions raw,
        out ResolvedOptions options,
        out string error,
        string? currentDirectory = null)
    {
        options = ResolvedOptions.Empty;
        error = string.Empty;

        var cwd = string.IsNullOrWhiteSpace(currentDirectory)
            ? Directory.GetCurrentDirectory()
            : Path.GetFullPath(currentDirectory);
        if (string.IsNullOrWhiteSpace(raw.ContractsPath))
        {
            error = "Missing required option: --contracts <path>";
            return false;
        }

        var mode = raw.Mode;
        if (mode == OutputMode.Unknown)
        {
            mode = DetectModeFromCurrentDirectory(cwd);
            if (mode == OutputMode.Unknown)
            {
                error = "Missing option: --mode <unity|godot|server>. Auto-detection only works inside a Unity project, a Godot project, or a server project directory.";
                return false;
            }
        }

        var contractsPath = Path.GetFullPath(raw.ContractsPath);
        var outputPath = string.Empty;
        var clientNamespace = string.Empty;
        var serverOutputPath = string.Empty;
        var serverNamespace = string.Empty;

        if (mode is OutputMode.Unity or OutputMode.Godot)
        {
            if (string.IsNullOrWhiteSpace(raw.OutputPath))
            {
                var clientRoot = PathHelper.FindClientProjectRoot(cwd, mode);
                if (clientRoot == null)
                {
                    error = mode == OutputMode.Unity
                        ? "Unity mode requires --output when current directory is not inside a Unity project."
                        : "Godot mode requires --output when current directory is not inside a Godot project.";
                    return false;
                }
                outputPath = Path.Combine(clientRoot, PathHelper.GetDefaultClientOutputRelativePath(mode));
            }
            else
            {
                outputPath = Path.GetFullPath(raw.OutputPath);
            }

            clientNamespace = string.IsNullOrWhiteSpace(raw.ClientNamespace)
                ? PathHelper.DeriveNamespaceFromOutputPath(outputPath)
                : raw.ClientNamespace;

            if (!IsValidNamespace(clientNamespace))
            {
                error = $"Invalid namespace '{clientNamespace}'. A namespace must be a valid C# identifier (e.g., 'MyApp.Generated').";
                return false;
            }
        }

        if (mode == OutputMode.Server)
        {
            var serverRoot = PathHelper.FindServerProjectRoot(cwd) ?? cwd;
            serverNamespace = raw.ServerNamespace;
            serverOutputPath = string.IsNullOrWhiteSpace(raw.ServerOutputPath)
                ? Path.Combine(serverRoot, "Generated")
                : Path.GetFullPath(raw.ServerOutputPath);

            if (!string.IsNullOrEmpty(serverNamespace) && !IsValidNamespace(serverNamespace))
            {
                error = $"Invalid namespace '{serverNamespace}'. A namespace must be a valid C# identifier (e.g., 'MyApp.Server').";
                return false;
            }
        }

        if (!Directory.Exists(contractsPath))
        {
            error = $"Contracts path not found: {contractsPath}";
            return false;
        }

        options = new ResolvedOptions(contractsPath, outputPath, clientNamespace, serverOutputPath, serverNamespace, mode);
        return true;
    }

    private static OutputMode DetectModeFromCurrentDirectory(string currentDirectory)
    {
        if (PathHelper.FindUnityProjectRoot(currentDirectory) is not null)
            return OutputMode.Unity;

        if (PathHelper.FindGodotProjectRoot(currentDirectory) is not null)
            return OutputMode.Godot;

        if (PathHelper.FindServerProjectRoot(currentDirectory) is not null)
            return OutputMode.Server;

        return OutputMode.Unknown;
    }

    private static bool IsValidNamespace(string ns)
    {
        if (string.IsNullOrWhiteSpace(ns))
            return false;

        foreach (var segment in ns.Split('.'))
        {
            if (string.IsNullOrWhiteSpace(segment))
                return false;
            if (char.IsDigit(segment[0]))
                return false;
            foreach (var ch in segment)
                if (!char.IsLetterOrDigit(ch) && ch != '_')
                    return false;
        }

        return true;
    }

    private static bool TryParseMode(string value, out OutputMode mode)
    {
        switch (value.ToLowerInvariant())
        {
            case "unity":
                mode = OutputMode.Unity;
                return true;
            case "godot":
                mode = OutputMode.Godot;
                return true;
            case "server":
                mode = OutputMode.Server;
                return true;
            default:
                mode = OutputMode.Unknown;
                return false;
        }
    }
}
