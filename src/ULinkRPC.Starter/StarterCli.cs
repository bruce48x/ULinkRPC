namespace ULinkRPC.Starter;

internal static class StarterCli
{
    public static void PrintUsage()
    {
        Console.WriteLine("Usage: ulinkrpc-starter [--version] [--name MyGame] [--output ./out] [--client-engine unity|tuanjie|godot] [--transport tcp|websocket|kcp] [--serializer json|memorypack]");
    }

    public static bool TryParseArgs(string[] args, out StarterCliOptions options, out string error)
    {
        var projectName = "ULinkApp";
        var outputDir = Directory.GetCurrentDirectory();
        var showVersion = false;
        ClientEngineKind? clientEngine = null;
        TransportKind? transport = null;
        SerializerKind? serializer = null;
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
                showVersion = true;
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

        options = new StarterCliOptions(projectName, outputDir, showVersion, clientEngine, transport, serializer);
        return true;
    }

    public static ClientEngineKind PromptClientEngine()
    {
        Console.WriteLine("Select client engine:");
        Console.WriteLine("  1) Unity");
        Console.WriteLine("  2) Tuanjie");
        Console.WriteLine("  3) Godot");
        while (true)
        {
            Console.Write("> ");
            var line = Console.ReadLine()?.Trim();
            switch (line)
            {
                case "1": return ClientEngineKind.Unity;
                case "2": return ClientEngineKind.Tuanjie;
                case "3": return ClientEngineKind.Godot;
            }

            Console.WriteLine("Please enter 1-3.");
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
            case "tuanjie":
            case "unity-china":
            case "unitycn":
                clientEngine = ClientEngineKind.Tuanjie;
                return true;
            case "godot": clientEngine = ClientEngineKind.Godot; return true;
            default: clientEngine = default; return false;
        }
    }
}
