using System.Text;

namespace ULinkRPC.CodeGen;

internal static partial class Program
{
    private const string DefaultUnityOutputRelativePath = "Assets/Scripts/Rpc/RpcGenerated";
    private const string DefaultUnityRuntimeNamespace = "Rpc.Generated";
    private const string DefaultCoreRuntimeUsing = "ULinkRPC.Core";
    private const string DefaultServerRuntimeUsing = "ULinkRPC.Server";

    private static int Main(string[] args)
    {
        if (args.Length > 0 && (args[0] == "-h" || args[0] == "--help"))
        {
            PrintUsage();
            return 0;
        }

        if (!TryParseCliArguments(args, out var rawOptions, out var error))
        {
            Console.Error.WriteLine(error);
            PrintUsage();
            return 1;
        }

        if (!TryResolveGenerationOptions(rawOptions, out var options, out error))
        {
            Console.Error.WriteLine(error);
            PrintUsage();
            return 1;
        }

        List<RpcServiceInfo> services;
        try
        {
            services = FindRpcServicesFromSource(options.ContractsPath);
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

        if (options.Mode == OutputMode.Unity)
            Directory.CreateDirectory(options.OutputPath);

        if (options.Mode == OutputMode.Server)
        {
            if (string.IsNullOrWhiteSpace(options.ServerNamespace))
                options = options with { ServerNamespace = GetDefaultServerNamespace(services) };

            Directory.CreateDirectory(options.ServerOutputPath);
        }

        var generated = 0;
        foreach (var svc in services)
        {
            if (options.Mode == OutputMode.Unity)
            {
                var (client, _) = GenerateCode(svc, options.UnityNamespace, DefaultCoreRuntimeUsing, DefaultServerRuntimeUsing);
                if (client != null)
                {
                    var clientTypeName = GetClientTypeName(svc.InterfaceName);
                    File.WriteAllText(Path.Combine(options.OutputPath, $"{clientTypeName}.cs"), client, Encoding.UTF8);
                    generated++;
                }

                if (svc.HasCallback)
                {
                    var cbBinder = GenerateCallbackBinderCode(svc, options.UnityNamespace, DefaultCoreRuntimeUsing, DefaultCoreRuntimeUsing);
                    var cbBinderTypeName = GetCallbackBinderTypeName(svc.CallbackInterfaceName!);
                    File.WriteAllText(Path.Combine(options.OutputPath, $"{cbBinderTypeName}.cs"), cbBinder, Encoding.UTF8);
                    generated++;
                }
            }

            if (options.Mode == OutputMode.Server)
            {
                var serverBinder = GenerateBinderCode(svc, options.ServerNamespace, DefaultCoreRuntimeUsing, DefaultServerRuntimeUsing);
                var binderTypeName = GetBinderTypeName(svc.InterfaceName);
                File.WriteAllText(Path.Combine(options.ServerOutputPath, $"{binderTypeName}.cs"), serverBinder, Encoding.UTF8);
                generated++;

                if (svc.HasCallback)
                {
                    var cbProxy = GenerateCallbackProxyCode(svc, options.ServerNamespace, DefaultCoreRuntimeUsing, DefaultServerRuntimeUsing);
                    var cbProxyTypeName = GetCallbackProxyTypeName(svc.CallbackInterfaceName!);
                    File.WriteAllText(Path.Combine(options.ServerOutputPath, $"{cbProxyTypeName}.cs"), cbProxy, Encoding.UTF8);
                    generated++;
                }
            }
        }

        if (options.Mode == OutputMode.Unity)
        {
            var facade = GenerateClientFacadeCode(services, options.UnityNamespace, DefaultCoreRuntimeUsing);
            File.WriteAllText(Path.Combine(options.OutputPath, "RpcApi.cs"), facade, Encoding.UTF8);
            generated++;
        }

        if (options.Mode == OutputMode.Server)
        {
            var allBinder = GenerateAllServicesBinder(services, options.ServerNamespace, DefaultServerRuntimeUsing);
            File.WriteAllText(Path.Combine(options.ServerOutputPath, "AllServicesBinder.cs"), allBinder, Encoding.UTF8);
            generated++;
        }

        Console.WriteLine($"Generated {generated} files for {services.Count} service(s).");
        return 0;
    }

}
