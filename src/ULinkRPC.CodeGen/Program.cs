using System.Text;

namespace ULinkRPC.CodeGen;

internal static class Program
{
    private const string CoreRuntimeUsing = "ULinkRPC.Core";
    private const string ClientRuntimeUsing = "ULinkRPC.Client";
    private const string ServerRuntimeUsing = "ULinkRPC.Server";

    private static int Main(string[] args)
    {
        if (args.Length > 0 && (args[0] == "-h" || args[0] == "--help"))
        {
            CliParser.PrintUsage();
            return 0;
        }

        if (!CliParser.TryParseCliArguments(args, out var rawOptions, out var error))
        {
            Console.Error.WriteLine(error);
            CliParser.PrintUsage();
            return 1;
        }

        if (!CliParser.TryResolveGenerationOptions(rawOptions, out var options, out error))
        {
            Console.Error.WriteLine(error);
            CliParser.PrintUsage();
            return 1;
        }

        List<RpcServiceInfo> services;
        try
        {
            services = ContractParser.FindRpcServicesFromSource(options.ContractsPath);
        }
        catch (InvalidOperationException ex)
        {
            Console.Error.WriteLine(ex.Message);
            return 1;
        }

        if (services.Count == 0)
        {
            Console.Error.WriteLine("No [RpcService] interfaces found.");
            return 1;
        }

        if (options.Mode == OutputMode.Server)
        {
            if (string.IsNullOrWhiteSpace(options.ServerNamespace))
                options = options with { ServerNamespace = NamingHelper.GetDefaultServerNamespace(services) };
        }

        try
        {
            return GenerateFiles(services, options);
        }
        catch (IOException ex)
        {
            Console.Error.WriteLine($"I/O error: {ex.Message}");
            return 1;
        }
        catch (UnauthorizedAccessException ex)
        {
            Console.Error.WriteLine($"Access denied: {ex.Message}");
            return 1;
        }
    }

    private static int GenerateFiles(List<RpcServiceInfo> services, ResolvedOptions options)
    {
        if (options.Mode == OutputMode.Unity)
            Directory.CreateDirectory(options.OutputPath);

        if (options.Mode == OutputMode.Server)
            Directory.CreateDirectory(options.ServerOutputPath);

        var generated = 0;
        foreach (var svc in services)
        {
            if (options.Mode == OutputMode.Unity)
            {
                var client = ClientEmitter.GenerateClient(svc, options.UnityNamespace, CoreRuntimeUsing);
                var clientTypeName = NamingHelper.GetClientTypeName(svc.InterfaceName);
                File.WriteAllText(Path.Combine(options.OutputPath, $"{clientTypeName}.cs"), client, Encoding.UTF8);
                generated++;

                if (svc.HasCallback)
                {
                    var cbBinder = ClientEmitter.GenerateCallbackBinder(svc, options.UnityNamespace, CoreRuntimeUsing);
                    var cbBinderTypeName = NamingHelper.GetCallbackBinderTypeName(svc.CallbackInterfaceName!);
                    File.WriteAllText(Path.Combine(options.OutputPath, $"{cbBinderTypeName}.cs"), cbBinder, Encoding.UTF8);
                    generated++;
                }
            }

            if (options.Mode == OutputMode.Server)
            {
                var binder = ServerEmitter.GenerateBinder(svc, options.ServerNamespace, CoreRuntimeUsing, ServerRuntimeUsing);
                var binderTypeName = NamingHelper.GetBinderTypeName(svc.InterfaceName);
                File.WriteAllText(Path.Combine(options.ServerOutputPath, $"{binderTypeName}.cs"), binder, Encoding.UTF8);
                generated++;

                if (svc.HasCallback)
                {
                    var cbProxy = ServerEmitter.GenerateCallbackProxy(svc, options.ServerNamespace, CoreRuntimeUsing, ServerRuntimeUsing);
                    var cbProxyTypeName = NamingHelper.GetCallbackProxyTypeName(svc.CallbackInterfaceName!);
                    File.WriteAllText(Path.Combine(options.ServerOutputPath, $"{cbProxyTypeName}.cs"), cbProxy, Encoding.UTF8);
                    generated++;
                }
            }
        }

        if (options.Mode == OutputMode.Unity)
        {
            var facade = FacadeEmitter.GenerateClientFacade(
                services,
                options.UnityNamespace,
                CoreRuntimeUsing,
                ClientRuntimeUsing);
            File.WriteAllText(Path.Combine(options.OutputPath, "RpcApi.cs"), facade, Encoding.UTF8);
            generated++;

            if (UnityAssemblyDefinitionEmitter.TryWriteDefaultAssemblyDefinition(options))
                generated++;
        }

        if (options.Mode == OutputMode.Server)
        {
            var allBinder = ServerEmitter.GenerateAllServicesBinder(services, options.ServerNamespace, ServerRuntimeUsing);
            File.WriteAllText(Path.Combine(options.ServerOutputPath, "AllServicesBinder.cs"), allBinder, Encoding.UTF8);
            generated++;
        }

        Console.WriteLine($"Generated {generated} files for {services.Count} service(s).");
        return 0;
    }
}
