namespace ULinkRPC.Starter;

internal static class StarterCli
{
    public static void PrintUsage()
    {
        Console.WriteLine("Usage:");
        Console.WriteLine("  ulinkrpc-starter [--version]");
        Console.WriteLine("  ulinkrpc-starter new [--name MyGame] [--output ./out] [--client-engine unity|unity-cn|tuanjie|godot] [--transport tcp|websocket|kcp] [--serializer json|memorypack] [--nugetforunity-source embedded|openupm]");
        Console.WriteLine("  ulinkrpc-starter codegen [--project-root ./MyGame] [--no-restore]");
    }

    public static bool TryParseArgs(string[] args, out StarterCliOptions options, out string error)
    {
        if (args.Length == 0)
        {
            return TryParseNewArgs([], out options, out error);
        }

        if (args.Length == 1 && args[0] == "--version")
        {
            options = new StarterCliOptions(StarterCommandKind.New, true, null, null);
            error = string.Empty;
            return true;
        }

        var firstArg = args[0];
        if (firstArg.Equals("new", StringComparison.OrdinalIgnoreCase))
            return TryParseNewArgs(args[1..], out options, out error);

        if (firstArg.Equals("codegen", StringComparison.OrdinalIgnoreCase))
            return TryParseCodeGenArgs(args[1..], out options, out error);

        if (firstArg.StartsWith("-", StringComparison.Ordinal))
            return TryParseNewArgs(args, out options, out error);

        options = default!;
        error = $"Unknown command: {firstArg}";
        return false;
    }

    private static bool TryParseNewArgs(string[] args, out StarterCliOptions options, out string error)
    {
        var projectName = "ULinkApp";
        var outputDir = Directory.GetCurrentDirectory();
        ClientEngineKind? clientEngine = null;
        TransportKind? transport = null;
        SerializerKind? serializer = null;
        NuGetForUnitySourceKind? nuGetForUnitySource = null;
        error = string.Empty;

        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            if (arg is "--name" && i + 1 < args.Length)
            {
                projectName = args[++i];
                continue;
            }

            if (arg is "--output" && i + 1 < args.Length)
            {
                outputDir = args[++i];
                continue;
            }

            if (arg is "--version")
            {
                options = new StarterCliOptions(StarterCommandKind.New, true, null, null);
                return true;
            }

            if (arg.Equals("new", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (arg is "--client-engine" && i + 1 < args.Length)
            {
                if (!TryParseClientEngine(args[++i], out var parsed))
                {
                    options = default!;
                    error = "Invalid --client-engine value.";
                    return false;
                }

                clientEngine = parsed;
                continue;
            }

            if (arg is "--transport" && i + 1 < args.Length)
            {
                if (!TryParseTransport(args[++i], out var parsed))
                {
                    options = default!;
                    error = "Invalid --transport value.";
                    return false;
                }

                transport = parsed;
                continue;
            }

            if (arg is "--serializer" && i + 1 < args.Length)
            {
                if (!TryParseSerializer(args[++i], out var parsed))
                {
                    options = default!;
                    error = "Invalid --serializer value.";
                    return false;
                }

                serializer = parsed;
                continue;
            }

            if (arg is "--nugetforunity-source" && i + 1 < args.Length)
            {
                if (!TryParseNuGetForUnitySource(args[++i], out var parsed))
                {
                    options = default!;
                    error = "Invalid --nugetforunity-source value.";
                    return false;
                }

                nuGetForUnitySource = parsed;
                continue;
            }

            options = default!;
            error = $"Unknown or incomplete option: {arg}";
            return false;
        }

        if (string.IsNullOrWhiteSpace(projectName))
        {
            options = default!;
            error = "--name cannot be empty.";
            return false;
        }

        options = new StarterCliOptions(
            StarterCommandKind.New,
            false,
            new StarterNewCommandOptions(projectName, outputDir, clientEngine, transport, serializer, nuGetForUnitySource),
            null);
        return true;
    }

    private static bool TryParseCodeGenArgs(string[] args, out StarterCliOptions options, out string error)
    {
        var projectRoot = Directory.GetCurrentDirectory();
        var noRestore = false;
        error = string.Empty;

        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            if (arg is "--project-root" && i + 1 < args.Length)
            {
                projectRoot = args[++i];
                continue;
            }

            if (arg is "--no-restore")
            {
                noRestore = true;
                continue;
            }

            options = default!;
            error = $"Unknown or incomplete option: {arg}";
            return false;
        }

        options = new StarterCliOptions(
            StarterCommandKind.CodeGen,
            false,
            null,
            new StarterCodeGenCommandOptions(projectRoot, noRestore));
        return true;
    }

    public static ClientEngineKind PromptClientEngine()
    {
        Console.WriteLine("Select client engine:");
        Console.WriteLine("  1) Unity");
        Console.WriteLine("  2) Unity CN");
        Console.WriteLine("  3) Tuanjie");
        Console.WriteLine("  4) Godot");
        while (true)
        {
            Console.Write("> ");
            var line = Console.ReadLine()?.Trim();
            switch (line)
            {
                case "1": return ClientEngineKind.Unity;
                case "2": return ClientEngineKind.UnityCn;
                case "3": return ClientEngineKind.Tuanjie;
                case "4": return ClientEngineKind.Godot;
            }

            Console.WriteLine("Please enter 1-4.");
        }
    }

    public static TransportKind PromptTransport()
    {
        Console.WriteLine("Select transport:");
        Console.WriteLine("  1) TCP");
        Console.WriteLine("  2) WebSocket");
        Console.WriteLine("  3) KCP");
        while (true)
        {
            Console.Write("> ");
            var line = Console.ReadLine()?.Trim();
            switch (line)
            {
                case "1": return TransportKind.Tcp;
                case "2": return TransportKind.WebSocket;
                case "3": return TransportKind.Kcp;
            }

            Console.WriteLine("Please enter 1-3.");
        }
    }

    public static SerializerKind PromptSerializer()
    {
        Console.WriteLine("Select serializer:");
        Console.WriteLine("  1) JSON");
        Console.WriteLine("  2) MemoryPack");
        while (true)
        {
            Console.Write("> ");
            var line = Console.ReadLine()?.Trim();
            switch (line)
            {
                case "1": return SerializerKind.Json;
                case "2": return SerializerKind.MemoryPack;
            }

            Console.WriteLine("Please enter 1-2.");
        }
    }

    private static bool TryParseTransport(string raw, out TransportKind transport)
    {
        var normalized = raw.Trim().ToLowerInvariant();
        switch (normalized)
        {
            case "tcp": transport = TransportKind.Tcp; return true;
            case "websocket":
            case "ws": transport = TransportKind.WebSocket; return true;
            case "kcp": transport = TransportKind.Kcp; return true;
            default: transport = default; return false;
        }
    }

    private static bool TryParseSerializer(string raw, out SerializerKind serializer)
    {
        var normalized = raw.Trim().ToLowerInvariant();
        switch (normalized)
        {
            case "json": serializer = SerializerKind.Json; return true;
            case "memorypack": serializer = SerializerKind.MemoryPack; return true;
            default: serializer = default; return false;
        }
    }

    private static bool TryParseClientEngine(string raw, out ClientEngineKind clientEngine)
    {
        var normalized = raw.Trim().ToLowerInvariant();
        switch (normalized)
        {
            case "unity": clientEngine = ClientEngineKind.Unity; return true;
            case "unity-china":
            case "unity-cn":
            case "unitycn":
                clientEngine = ClientEngineKind.UnityCn;
                return true;
            case "tuanjie":
                clientEngine = ClientEngineKind.Tuanjie;
                return true;
            case "godot": clientEngine = ClientEngineKind.Godot; return true;
            default: clientEngine = default; return false;
        }
    }

    private static bool TryParseNuGetForUnitySource(string raw, out NuGetForUnitySourceKind source)
    {
        var normalized = raw.Trim().ToLowerInvariant();
        switch (normalized)
        {
            case "embedded":
                source = NuGetForUnitySourceKind.Embedded;
                return true;
            case "openupm":
                source = NuGetForUnitySourceKind.OpenUpm;
                return true;
            default:
                source = default;
                return false;
        }
    }
}
