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
        Console.WriteLine("  --namespace <ns>        Namespace for generated Unity code");
        Console.WriteLine("  --server-output <path>  Output directory for server binders");
        Console.WriteLine("  --server-namespace <ns> Namespace for server binders");
        Console.WriteLine("  --mode <unity|server>   Generation mode (required)");
        Console.WriteLine();
        Console.WriteLine("Defaults:");
        Console.WriteLine("  unity: output defaults to Assets/Scripts/Rpc/RpcGenerated under Unity project root.");
        Console.WriteLine("  unity: namespace defaults to value derived from output path.");
        Console.WriteLine("  server: output defaults to ./Generated");
    }

    public static bool TryParseCliArguments(string[] args, out RawOptions options, out string error)
    {
        options = RawOptions.Empty;
        var contractsPath = string.Empty;
        var outputPath = string.Empty;
        var unityNamespace = string.Empty;
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
                unityNamespace = args[++i];
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

        options = new RawOptions(contractsPath, outputPath, unityNamespace, serverOutputPath, serverNamespace, mode);
        return true;
    }

    public static bool TryResolveGenerationOptions(RawOptions raw, out ResolvedOptions options, out string error)
    {
        options = ResolvedOptions.Empty;
        error = string.Empty;

        var cwd = Directory.GetCurrentDirectory();
        if (string.IsNullOrWhiteSpace(raw.ContractsPath))
        {
            error = "Missing required option: --contracts <path>";
            return false;
        }

        if (raw.Mode == OutputMode.Unknown)
        {
            error = "Missing required option: --mode <unity|server>";
            return false;
        }

        var mode = raw.Mode;
        var contractsPath = Path.GetFullPath(raw.ContractsPath);
        var outputPath = string.Empty;
        var unityNamespace = string.Empty;
        var serverOutputPath = string.Empty;
        var serverNamespace = string.Empty;

        if (mode == OutputMode.Unity)
        {
            if (string.IsNullOrWhiteSpace(raw.OutputPath))
            {
                var unityRoot = PathHelper.FindUnityProjectRoot(cwd);
                if (unityRoot == null)
                {
                    error = "Unity mode requires --output when current directory is not inside a Unity project.";
                    return false;
                }
                outputPath = Path.Combine(unityRoot, PathHelper.DefaultUnityOutputRelativePath);
            }
            else
            {
                outputPath = Path.GetFullPath(raw.OutputPath);
            }

            unityNamespace = string.IsNullOrWhiteSpace(raw.UnityNamespace)
                ? PathHelper.DeriveNamespaceFromOutputPath(outputPath)
                : raw.UnityNamespace;
        }

        if (mode == OutputMode.Server)
        {
            serverNamespace = raw.ServerNamespace;
            serverOutputPath = string.IsNullOrWhiteSpace(raw.ServerOutputPath)
                ? Path.Combine(cwd, "Generated")
                : Path.GetFullPath(raw.ServerOutputPath);
        }

        if (!Directory.Exists(contractsPath))
        {
            error = $"Contracts path not found: {contractsPath}";
            return false;
        }

        options = new ResolvedOptions(contractsPath, outputPath, unityNamespace, serverOutputPath, serverNamespace, mode);
        return true;
    }

    private static bool TryParseMode(string value, out OutputMode mode)
    {
        switch (value.ToLowerInvariant())
        {
            case "unity":
                mode = OutputMode.Unity;
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
