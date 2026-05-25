using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;

namespace ULinkRPC.Analyzers;

[Generator]
public sealed class ULinkRpcSourceGenerator : ISourceGenerator
{
    private const string CoreRuntimeUsing = "ULinkRPC.Core";
    private const string ClientRuntimeUsing = "ULinkRPC.Client";
    private const string ServerRuntimeUsing = "ULinkRPC.Server";

    private static readonly DiagnosticDescriptor GenerationFailed = new(
        "ULRPCGEN001",
        "ULinkRPC source generation failed",
        "{0}",
        "ULinkRPC.SourceGeneration",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public void Initialize(GeneratorInitializationContext context)
    {
    }

    public void Execute(GeneratorExecutionContext context)
    {
        try
        {
            var compilation = context.Compilation;
            var options = GeneratorOptions.From(context.AnalyzerConfigOptions, compilation);
            if (!options.GenerateClient && !options.GenerateServer)
                options = options.WithAutoDetectedModes(compilation);

            if (!options.GenerateClient && !options.GenerateServer)
                return;

            var services = RpcSymbolReader.FindServices(compilation);
            if (services.Count == 0)
                return;

            if (options.GenerateClient)
                EmitClient(context, services, options.ClientNamespace);

            if (options.GenerateServer)
                EmitServer(context, services, options.ServerNamespace);
        }
        catch (Exception ex)
        {
            context.ReportDiagnostic(Diagnostic.Create(GenerationFailed, Location.None, ex.Message));
        }
    }

    private static void EmitClient(GeneratorExecutionContext context, List<RpcServiceModel> services, string generatedNamespace)
    {
        foreach (var service in services)
        {
            context.AddSource(
                $"{Naming.GetClientTypeName(service.InterfaceName)}.g.cs",
                SourceText.From(ClientSourceEmitter.GenerateClient(service, generatedNamespace), Encoding.UTF8));

            if (service.HasCallback)
            {
                context.AddSource(
                    $"{Naming.GetCallbackBinderTypeName(service.CallbackInterfaceName!)}.g.cs",
                    SourceText.From(ClientSourceEmitter.GenerateCallbackBinder(service, generatedNamespace), Encoding.UTF8));
            }
        }

        context.AddSource(
            "RpcApi.g.cs",
            SourceText.From(ClientSourceEmitter.GenerateFacade(services, generatedNamespace), Encoding.UTF8));
    }

    private static void EmitServer(GeneratorExecutionContext context, List<RpcServiceModel> services, string generatedNamespace)
    {
        foreach (var service in services)
        {
            context.AddSource(
                $"{Naming.GetBinderTypeName(service.InterfaceName)}.g.cs",
                SourceText.From(ServerSourceEmitter.GenerateBinder(service, generatedNamespace), Encoding.UTF8));

            if (service.HasCallback)
            {
                context.AddSource(
                    $"{Naming.GetCallbackProxyTypeName(service.CallbackInterfaceName!)}.g.cs",
                    SourceText.From(ServerSourceEmitter.GenerateCallbackProxy(service, generatedNamespace), Encoding.UTF8));
            }
        }

        context.AddSource(
            "AllServicesBinder.g.cs",
            SourceText.From(ServerSourceEmitter.GenerateAllServicesBinder(services, generatedNamespace), Encoding.UTF8));
    }

    private sealed class GeneratorOptions
    {
        private const string ClientKey = "build_property.ULinkRPCGenerateClient";
        private const string ServerKey = "build_property.ULinkRPCGenerateServer";
        private const string ClientNamespaceKey = "build_property.ULinkRPCGeneratedNamespace";
        private const string ServerNamespaceKey = "build_property.ULinkRPCServerGeneratedNamespace";

        private GeneratorOptions(
            bool generateClient,
            bool generateServer,
            bool hasExplicitGenerationMode,
            string clientNamespace,
            string serverNamespace)
        {
            GenerateClient = generateClient;
            GenerateServer = generateServer;
            HasExplicitGenerationMode = hasExplicitGenerationMode;
            ClientNamespace = clientNamespace;
            ServerNamespace = serverNamespace;
        }

        public bool GenerateClient { get; }
        public bool GenerateServer { get; }
        public bool HasExplicitGenerationMode { get; }
        public string ClientNamespace { get; }
        public string ServerNamespace { get; }

        public GeneratorOptions WithAutoDetectedModes(Compilation compilation)
        {
            if (HasExplicitGenerationMode)
                return this;

            if (IsUnityCompilation(compilation))
                return this;

            var hasClientRuntime = compilation.GetTypeByMetadataName("ULinkRPC.Client.RpcClientRuntime") is not null;
            var hasServerRuntime = compilation.GetTypeByMetadataName("ULinkRPC.Server.RpcServiceRegistry") is not null;
            return new GeneratorOptions(
                generateClient: hasClientRuntime && !hasServerRuntime,
                generateServer: hasServerRuntime && !hasClientRuntime,
                hasExplicitGenerationMode: false,
                ClientNamespace,
                ServerNamespace);
        }

        public static GeneratorOptions From(AnalyzerConfigOptionsProvider provider, Compilation compilation)
        {
            var global = provider.GlobalOptions;
            var hasClientSetting = global.TryGetValue(ClientKey, out var clientValue);
            var hasServerSetting = global.TryGetValue(ServerKey, out var serverValue);
            var clientNamespace = GetString(global, ClientNamespaceKey, "Rpc.Generated");
            var hasClientMarker = TryGetClientGenerationAttribute(compilation, out var markerNamespace);
            if (hasClientMarker && !hasClientSetting && !string.IsNullOrWhiteSpace(markerNamespace))
                clientNamespace = markerNamespace!;

            return new GeneratorOptions(
                IsEnabled(clientValue) || (!hasClientSetting && hasClientMarker),
                IsEnabled(serverValue),
                hasClientSetting || hasServerSetting || hasClientMarker,
                clientNamespace,
                GetString(global, ServerNamespaceKey, "Server.Generated"));
        }

        private static bool IsUnityCompilation(Compilation compilation)
        {
            if (compilation.AssemblyName is not null &&
                compilation.AssemblyName.StartsWith("Assembly-CSharp", StringComparison.Ordinal))
            {
                return true;
            }

            return compilation.SourceModule.ReferencedAssemblySymbols.Any(static assembly =>
                assembly.Identity.Name.StartsWith("UnityEngine", StringComparison.Ordinal) ||
                assembly.Identity.Name.StartsWith("UnityEditor", StringComparison.Ordinal));
        }

        private static bool TryGetClientGenerationAttribute(Compilation compilation, out string? generatedNamespace)
        {
            generatedNamespace = null;
            foreach (var attribute in compilation.Assembly.GetAttributes())
            {
                var attributeClass = attribute.AttributeClass;
                if (attributeClass is null)
                    continue;

                if (!string.Equals(attributeClass.Name, "ULinkRPCGenerateClientAttribute", StringComparison.Ordinal))
                    continue;

                generatedNamespace = attribute.ConstructorArguments
                    .Select(static argument => argument.Value as string)
                    .FirstOrDefault(static value => !string.IsNullOrWhiteSpace(value));
                return true;
            }

            return false;
        }

        private static bool IsEnabled(string? value) =>
            string.Equals(value, "true", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(value, "1", StringComparison.Ordinal);

        private static string GetString(AnalyzerConfigOptions options, string key, string fallback) =>
            options.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value)
                ? value.Trim()
                : fallback;
    }

    private static class RpcSymbolReader
    {
        public static List<RpcServiceModel> FindServices(Compilation compilation)
        {
            var callbacks = new Dictionary<string, CallbackModel>(StringComparer.Ordinal);
            var services = new List<RpcServiceModel>();

            foreach (var type in EnumerateCandidateTypes(compilation))
            {
                if (type.TypeKind != TypeKind.Interface)
                    continue;

                var callbackAttribute = GetAttribute(type, "RpcCallbackAttribute");
                if (callbackAttribute is not null)
                {
                    var callback = TryCreateCallback(type, callbackAttribute);
                    if (callback is not null)
                        callbacks[callback.FullName] = callback;
                }

                var serviceAttribute = GetAttribute(type, "RpcServiceAttribute");
                if (serviceAttribute is null || !TryGetIntId(serviceAttribute, out var serviceId))
                    continue;

                var service = CreateService(type, serviceAttribute, serviceId);
                services.Add(service);
            }

            ValidateServiceIds(services);
            foreach (var service in services)
            {
                if (service.CallbackInterfaceFullName is null)
                    continue;

                if (!callbacks.TryGetValue(service.CallbackInterfaceFullName, out var callback))
                    throw new InvalidOperationException(
                        $"Callback interface '{service.CallbackInterfaceFullName}' declared by service '{service.FullName}' was not found or is missing a valid [RpcCallback] contract.");

                if (!string.Equals(callback.ServiceFullName, service.FullName, StringComparison.Ordinal))
                    throw new InvalidOperationException(
                        $"Callback interface '{callback.FullName}' is associated with '{callback.ServiceFullName}', but service '{service.FullName}' declared it as its callback.");

                service.CallbackMethods = callback.Methods;
            }

            return services
                .OrderBy(static service => service.ServiceId)
                .ThenBy(static service => service.FullName, StringComparer.Ordinal)
                .ToList();
        }

        private static IEnumerable<INamedTypeSymbol> EnumerateCandidateTypes(Compilation compilation)
        {
            var seenAssemblies = new HashSet<IAssemblySymbol>(SymbolEqualityComparer.Default);
            foreach (var assembly in compilation.SourceModule.ReferencedAssemblySymbols.Prepend(compilation.Assembly))
            {
                if (!seenAssemblies.Add(assembly))
                    continue;
                if (!SymbolEqualityComparer.Default.Equals(assembly, compilation.Assembly) && IsFrameworkAssembly(assembly.Identity.Name))
                    continue;

                foreach (var type in EnumerateTypes(assembly.GlobalNamespace))
                    yield return type;
            }
        }

        private static bool IsFrameworkAssembly(string assemblyName) =>
            assemblyName.StartsWith("System", StringComparison.Ordinal) ||
            assemblyName.StartsWith("Microsoft", StringComparison.Ordinal) ||
            string.Equals(assemblyName, "mscorlib", StringComparison.Ordinal) ||
            string.Equals(assemblyName, "netstandard", StringComparison.Ordinal);

        private static IEnumerable<INamedTypeSymbol> EnumerateTypes(INamespaceOrTypeSymbol symbol)
        {
            foreach (var member in symbol.GetMembers())
            {
                if (member is INamespaceOrTypeSymbol namespaceOrType)
                {
                    foreach (var type in EnumerateTypes(namespaceOrType))
                        yield return type;
                }

                if (member is INamedTypeSymbol namedType)
                {
                    yield return namedType;
                    foreach (var nested in EnumerateTypes(namedType))
                        yield return nested;
                }
            }
        }

        private static RpcServiceModel CreateService(INamedTypeSymbol type, AttributeData attribute, int serviceId)
        {
            var methods = new List<RpcMethodModel>();
            foreach (var member in type.GetMembers().OfType<IMethodSymbol>().OrderBy(static method => method.Name, StringComparer.Ordinal))
            {
                var methodAttribute = GetAttribute(member, "RpcMethodAttribute");
                if (methodAttribute is null || !TryGetIntId(methodAttribute, out var methodId))
                    continue;

                if (!IsValueTask(member.ReturnType, out var resultType, out var isVoid))
                    throw new InvalidOperationException(
                        $"Unsupported return type '{member.ReturnType.ToDisplayString()}' on {type.Name}.{member.Name}. RPC methods must return ValueTask or ValueTask<T>.");

                methods.Add(new RpcMethodModel(
                    member.Name,
                    methodId,
                    CreateParameters(member.Parameters),
                    isVoid ? null : TypeName(resultType!),
                    isVoid));
            }

            ValidateMethodIds(methods, type.Name, "MethodId", "[RpcMethod]");
            if (methods.Count == 0)
                throw new InvalidOperationException($"RPC service '{type.Name}' must declare at least one [RpcMethod] contract.");

            TryGetTypeArgument(attribute, out var callbackName, out var callbackFullName);
            return new RpcServiceModel(
                type.Name,
                TypeName(type),
                type.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat),
                serviceId,
                methods,
                callbackName,
                callbackFullName);
        }

        private static CallbackModel? TryCreateCallback(INamedTypeSymbol type, AttributeData attribute)
        {
            if (!TryGetTypeArgument(attribute, out _, out var serviceFullName) || serviceFullName is null)
                return null;

            var methods = new List<RpcCallbackMethodModel>();
            foreach (var member in type.GetMembers().OfType<IMethodSymbol>().OrderBy(static method => method.Name, StringComparer.Ordinal))
            {
                var pushAttribute = GetAttribute(member, "RpcPushAttribute");
                if (pushAttribute is null || !TryGetIntId(pushAttribute, out var methodId))
                    continue;

                if (!member.ReturnsVoid)
                    throw new InvalidOperationException($"RPC callback method '{type.Name}.{member.Name}' must return void.");

                methods.Add(new RpcCallbackMethodModel(member.Name, methodId, CreateParameters(member.Parameters)));
            }

            ValidateMethodIds(methods, type.Name, "PushId", "[RpcPush]");
            if (methods.Count == 0)
                throw new InvalidOperationException($"RPC callback interface '{type.Name}' must declare at least one valid [RpcPush] contract.");

            return new CallbackModel(type.Name, TypeName(type), serviceFullName, methods);
        }

        private static List<RpcParameterModel> CreateParameters(ImmutableArray<IParameterSymbol> parameters)
        {
            if (parameters.Length != 1)
                throw new InvalidOperationException("RPC methods and callbacks must declare exactly one DTO payload parameter.");

            return parameters
                .Select(static parameter => new RpcParameterModel(TypeName(parameter.Type), parameter.Name))
                .ToList();
        }

        private static bool IsValueTask(ITypeSymbol returnType, out ITypeSymbol? resultType, out bool isVoid)
        {
            resultType = null;
            isVoid = false;
            if (returnType is not INamedTypeSymbol named ||
                !string.Equals(named.Name, "ValueTask", StringComparison.Ordinal) ||
                !string.Equals(named.ContainingNamespace?.ToDisplayString(), "System.Threading.Tasks", StringComparison.Ordinal))
            {
                return false;
            }

            if (named.TypeArguments.Length == 0)
            {
                isVoid = true;
                return true;
            }

            if (named.TypeArguments.Length != 1)
                return false;

            resultType = named.TypeArguments[0];
            return true;
        }

        private static AttributeData? GetAttribute(ISymbol symbol, string attributeName)
        {
            var shortName = attributeName.EndsWith("Attribute", StringComparison.Ordinal)
                ? attributeName.Substring(0, attributeName.Length - "Attribute".Length)
                : attributeName;

            foreach (var attribute in symbol.GetAttributes())
            {
                var attributeClass = attribute.AttributeClass;
                if (attributeClass is null)
                    continue;

                if (string.Equals(attributeClass.Name, attributeName, StringComparison.Ordinal) ||
                    string.Equals(attributeClass.Name, shortName, StringComparison.Ordinal))
                    return attribute;
            }

            return null;
        }

        private static bool TryGetIntId(AttributeData attribute, out int id)
        {
            foreach (var argument in attribute.ConstructorArguments.Concat(attribute.NamedArguments.Select(static pair => pair.Value)))
            {
                if (argument.Value is int intValue)
                {
                    id = intValue;
                    return true;
                }
            }

            id = default;
            return false;
        }

        private static bool TryGetTypeArgument(AttributeData attribute, out string? name, out string? fullName)
        {
            foreach (var argument in attribute.ConstructorArguments.Concat(attribute.NamedArguments.Select(static pair => pair.Value)))
            {
                if (argument.Value is INamedTypeSymbol type)
                {
                    name = type.Name;
                    fullName = TypeName(type);
                    return true;
                }
            }

            name = null;
            fullName = null;
            return false;
        }

        private static string TypeName(ITypeSymbol type) =>
            "global::" + type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat).Replace("global::", string.Empty);

        private static void ValidateServiceIds(IReadOnlyList<RpcServiceModel> services)
        {
            var seen = new Dictionary<int, string>();
            foreach (var service in services)
            {
                if (service.ServiceId <= 0)
                    throw new InvalidOperationException($"Invalid ServiceId {service.ServiceId} found on '{service.InterfaceName}'. Each [RpcService] id must be greater than 0.");

                if (seen.TryGetValue(service.ServiceId, out var existingName))
                    throw new InvalidOperationException($"Duplicate ServiceId {service.ServiceId} found on '{existingName}' and '{service.InterfaceName}'. Each [RpcService] must have a unique id.");

                seen.Add(service.ServiceId, service.InterfaceName);
            }
        }

        private static void ValidateMethodIds<T>(IReadOnlyList<T> methods, string interfaceName, string idName, string attributeName)
            where T : IRpcMethodContract
        {
            var seen = new Dictionary<int, string>();
            foreach (var method in methods)
            {
                if (method.MethodId <= 0)
                    throw new InvalidOperationException($"Invalid {idName} {method.MethodId} found on '{method.Name}' in {interfaceName}. Each {attributeName} id must be greater than 0.");

                if (seen.TryGetValue(method.MethodId, out var existingName))
                    throw new InvalidOperationException($"Duplicate {idName} {method.MethodId} found on '{existingName}' and '{method.Name}' in {interfaceName}.");

                seen.Add(method.MethodId, method.Name);
            }
        }
    }

    private static class ClientSourceEmitter
    {
        public static string GenerateClient(RpcServiceModel service, string generatedNamespace)
        {
            var writer = new SourceWriter();
            writer.Header();
            writer.Line("using System;");
            writer.Line("using System.Threading;");
            writer.Line("using System.Threading.Tasks;");
            writer.Line($"using {CoreRuntimeUsing};");
            writer.Line();
            writer.OpenBlock($"namespace {generatedNamespace}");
            writer.OpenBlock($"public sealed class {Naming.GetClientTypeName(service.InterfaceName)} : {service.FullName}");
            writer.Line($"private const int ServiceId = {service.ServiceId};");

            foreach (var method in service.Methods)
            {
                var returnType = method.IsVoid ? "RpcVoid" : method.ReturnTypeName!;
                writer.Line($"private static readonly RpcMethod<{method.PayloadType}, {returnType}> {Naming.GetClientMethodFieldName(method.Name)} = new(ServiceId, {method.MethodId});");
            }

            writer.Line();
            writer.Line("private readonly IRpcClient _client;");
            writer.Line();
            writer.Line($"public {Naming.GetClientTypeName(service.InterfaceName)}(IRpcClient client) {{ _client = client; }}");
            writer.Line();

            foreach (var method in service.Methods)
            {
                var paramSig = Naming.GetParameterSignature(method.Parameters);
                var sigWithCt = string.IsNullOrEmpty(paramSig)
                    ? $"{method.Name}(CancellationToken ct)"
                    : $"{method.Name}({paramSig}, CancellationToken ct)";
                var fieldName = Naming.GetClientMethodFieldName(method.Name);

                if (method.IsVoid)
                {
                    writer.OpenBlock($"public async ValueTask {method.Name}({paramSig})");
                    writer.Line($"await {method.Name}({method.PayloadValue}, CancellationToken.None);");
                    writer.CloseBlock();
                    writer.Line();
                    writer.OpenBlock($"public async ValueTask {sigWithCt}");
                    writer.Line($"await _client.CallAsync({fieldName}, {method.PayloadValue}, ct);");
                    writer.CloseBlock();
                }
                else
                {
                    writer.OpenBlock($"public ValueTask<{method.ReturnTypeName}> {method.Name}({paramSig})");
                    writer.Line($"return {method.Name}({method.PayloadValue}, CancellationToken.None);");
                    writer.CloseBlock();
                    writer.Line();
                    writer.OpenBlock($"public ValueTask<{method.ReturnTypeName}> {sigWithCt}");
                    writer.Line($"return _client.CallAsync({fieldName}, {method.PayloadValue}, ct);");
                    writer.CloseBlock();
                }

                writer.Line();
            }

            writer.CloseBlock();
            writer.Line();
            writer.OpenBlock($"public static class {Naming.GetClientExtensionTypeName(service.InterfaceName)}");
            writer.OpenBlock($"public static {service.FullName} {Naming.GetClientFactoryMethodName(service.InterfaceName)}(this IRpcClient client)");
            writer.Line("if (client is null) throw new ArgumentNullException(nameof(client));");
            writer.Line($"return new {Naming.GetClientTypeName(service.InterfaceName)}(client);");
            writer.CloseBlock();
            writer.CloseBlock();
            writer.CloseBlock();
            return writer.ToString();
        }

        public static string GenerateCallbackBinder(RpcServiceModel service, string generatedNamespace)
        {
            var writer = new SourceWriter();
            writer.Header();
            writer.Line("using System;");
            writer.Line($"using {CoreRuntimeUsing};");
            writer.Line();
            writer.OpenBlock($"namespace {generatedNamespace}");
            writer.OpenBlock($"public static class {Naming.GetCallbackBinderTypeName(service.CallbackInterfaceName!)}");
            writer.Line($"private const int ServiceId = {service.ServiceId};");

            foreach (var method in service.CallbackMethods)
                writer.Line($"private static readonly RpcPushMethod<{method.PayloadType}> {Naming.GetCallbackMethodFieldName(method.Name)} = new(ServiceId, {method.MethodId});");

            writer.Line();
            writer.OpenBlock($"public static void Bind(IRpcClient client, {service.CallbackFullName} receiver)");
            foreach (var method in service.CallbackMethods)
            {
                writer.OpenBlock($"client.RegisterPushHandler({Naming.GetCallbackMethodFieldName(method.Name)}, arg =>");
                writer.Line($"receiver.{method.Name}(arg);");
                writer.CloseBlock(");");
            }

            writer.CloseBlock();
            writer.CloseBlock();
            writer.CloseBlock();
            return writer.ToString();
        }

        public static string GenerateFacade(List<RpcServiceModel> services, string generatedNamespace)
        {
            var groups = services
                .GroupBy(static service => Naming.GetFacadeGroupName(service.FullName))
                .OrderBy(static group => group.Key, StringComparer.Ordinal)
                .Select(static group => new FacadeGroupModel(group.Key, group.OrderBy(static service => service.InterfaceName, StringComparer.Ordinal).ToList()))
                .ToList();

            var callbacks = services.Where(static service => service.HasCallback).OrderBy(static service => service.CallbackInterfaceName, StringComparer.Ordinal).ToList();
            var writer = new SourceWriter();
            writer.Header();
            writer.Line("using System;");
            writer.Line("using System.Threading;");
            writer.Line("using System.Threading.Tasks;");
            writer.Line($"using {ClientRuntimeUsing};");
            writer.Line($"using {CoreRuntimeUsing};");
            writer.Line();
            writer.OpenBlock($"namespace {generatedNamespace}");
            writer.OpenBlock("public sealed class RpcApi");
            writer.OpenBlock("public RpcApi(IRpcClient client)");
            writer.Line("if (client is null) throw new ArgumentNullException(nameof(client));");
            foreach (var group in groups)
                writer.Line($"{group.GroupName} = new {group.GroupName}RpcGroup(client);");
            writer.CloseBlock();
            writer.Line();
            foreach (var group in groups)
                writer.Line($"public {group.GroupName}RpcGroup {group.GroupName} {{ get; }}");
            writer.CloseBlock();
            writer.Line();

            foreach (var group in groups)
            {
                writer.OpenBlock($"public sealed class {group.GroupName}RpcGroup");
                writer.OpenBlock($"public {group.GroupName}RpcGroup(IRpcClient client)");
                writer.Line("if (client is null) throw new ArgumentNullException(nameof(client));");
                foreach (var service in group.Services)
                    writer.Line($"{Naming.GetFacadeServicePropertyName(service.InterfaceName)} = new {Naming.GetClientTypeName(service.InterfaceName)}(client);");
                writer.CloseBlock();
                writer.Line();
                foreach (var service in group.Services)
                    writer.Line($"public {service.FullName} {Naming.GetFacadeServicePropertyName(service.InterfaceName)} {{ get; }}");
                writer.CloseBlock();
                writer.Line();
            }

            writer.OpenBlock("public sealed class RpcClient : IAsyncDisposable");
            writer.Line("private readonly RpcClientRuntime _runtime;");
            if (callbacks.Count > 0)
                writer.Line("private readonly RpcCallbackBindings? _callbacks;");
            writer.Line($"private global::{generatedNamespace}.RpcApi? _api;");
            writer.Line();
            writer.OpenBlock("public RpcClient(RpcClientOptions options)");
            writer.Line("Options = options ?? throw new ArgumentNullException(nameof(options));");
            writer.Line("_runtime = new RpcClientRuntime(options);");
            writer.CloseBlock();
            writer.Line();

            if (callbacks.Count > 0)
            {
                writer.OpenBlock("public RpcClient(RpcClientOptions options, RpcCallbackBindings callbacks) : this(options)");
                writer.Line("_callbacks = callbacks ?? throw new ArgumentNullException(nameof(callbacks));");
                writer.CloseBlock();
                writer.Line();
                EmitCallbackTypes(writer, callbacks, generatedNamespace);
            }

            writer.OpenBlock("public event Action<Exception?>? Disconnected");
            writer.Line("add => _runtime.Disconnected += value;");
            writer.Line("remove => _runtime.Disconnected -= value;");
            writer.CloseBlock();
            writer.Line();
            writer.Line("public RpcClientOptions Options { get; }");
            writer.Line($"public global::{generatedNamespace}.RpcApi Api => _api ??= new global::{generatedNamespace}.RpcApi(_runtime);");
            writer.Line();
            writer.OpenBlock("public ValueTask ConnectAsync(CancellationToken ct = default)");
            if (callbacks.Count > 0)
            {
                writer.Line("if (_callbacks is not null)");
                writer.Line("    _callbacks.Bind(_runtime);");
            }
            writer.Line("return _runtime.StartAsync(ct);");
            writer.CloseBlock();
            writer.Line();
            writer.OpenBlock("public ValueTask DisposeAsync()");
            writer.Line("return _runtime.DisposeAsync();");
            writer.CloseBlock();
            writer.CloseBlock();
            writer.CloseBlock();
            return writer.ToString();
        }

        private static void EmitCallbackTypes(SourceWriter writer, List<RpcServiceModel> callbacks, string generatedNamespace)
        {
            writer.OpenBlock("public sealed class RpcCallbackBindings");
            foreach (var service in callbacks)
            {
                var field = "_" + Naming.GetCallbackReceiverParamName(service.CallbackInterfaceName!);
                var parameter = Naming.GetCallbackReceiverParamName(service.CallbackInterfaceName!);
                writer.Line($"private {service.CallbackFullName}? {field};");
                writer.OpenBlock($"public void Add({service.CallbackFullName} {parameter})");
                writer.Line($"if ({parameter} is null) throw new ArgumentNullException(nameof({parameter}));");
                writer.OpenBlock($"if ({field} is not null)");
                writer.Line($"throw new InvalidOperationException(\"Callback receiver for '{service.CallbackInterfaceName}' is already registered.\");");
                writer.CloseBlock();
                writer.Line($"{field} = {parameter};");
                writer.CloseBlock();
                writer.Line();
            }

            writer.OpenBlock("internal void Bind(IRpcClient client)");
            writer.Line("if (client is null) throw new ArgumentNullException(nameof(client));");
            foreach (var service in callbacks)
            {
                var field = "_" + Naming.GetCallbackReceiverParamName(service.CallbackInterfaceName!);
                writer.OpenBlock($"if ({field} is not null)");
                writer.Line($"global::{generatedNamespace}.{Naming.GetCallbackBinderTypeName(service.CallbackInterfaceName!)}.Bind(client, {field});");
                writer.CloseBlock();
            }
            writer.CloseBlock();
            writer.CloseBlock();
            writer.Line();

            foreach (var service in callbacks)
            {
                writer.OpenBlock($"public abstract class {Naming.GetServiceTypeName(service.CallbackInterfaceName!)}Base : {service.CallbackFullName}");
                foreach (var method in service.CallbackMethods.OrderBy(static method => method.MethodId))
                {
                    writer.Line();
                    writer.OpenBlock($"public virtual void {method.Name}({Naming.GetParameterSignature(method.Parameters)})");
                    writer.CloseBlock();
                }
                writer.CloseBlock();
                writer.Line();
            }
        }
    }

    private static class ServerSourceEmitter
    {
        public static string GenerateBinder(RpcServiceModel service, string generatedNamespace)
        {
            var writer = new SourceWriter();
            writer.Header();
            writer.Line("using System;");
            writer.Line("using System.Threading.Tasks;");
            writer.Line($"using {CoreRuntimeUsing};");
            writer.Line($"using {ServerRuntimeUsing};");
            writer.Line();
            writer.OpenBlock($"namespace {generatedNamespace}");
            writer.OpenBlock($"public static class {Naming.GetBinderTypeName(service.InterfaceName)}");
            writer.Line($"private const int ServiceId = {service.ServiceId};");
            writer.Line();
            writer.OpenBlock($"public static void Bind(RpcServiceRegistry registry, {service.FullName} impl)");
            writer.Line("if (registry is null) throw new ArgumentNullException(nameof(registry));");
            writer.Line("if (impl is null) throw new ArgumentNullException(nameof(impl));");
            writer.Line("BindFactory(registry, _ => impl);");
            writer.CloseBlock();
            writer.Line();
            writer.OpenBlock($"public static void BindFactory(RpcServiceRegistry registry, Func<RpcSession, {service.FullName}> implFactory)");
            writer.Line("if (registry is null) throw new ArgumentNullException(nameof(registry));");
            writer.Line("if (implFactory is null) throw new ArgumentNullException(nameof(implFactory));");

            foreach (var method in service.Methods)
            {
                writer.OpenBlock($"registry.Register(ServiceId, {method.MethodId}, async (server, req, ct) =>");
                writer.Line($"var impl = server.GetOrAddScopedService(ServiceId, implFactory);");
                writer.Line($"var arg = server.Serializer.Deserialize<{method.PayloadType}>(req.Payload.Memory)!;");
                if (method.IsVoid)
                {
                    writer.Line($"await impl.{method.Name}(arg);");
                    writer.Line("return RpcEnvelopeCodec.EncodeResponse(req.RequestId, RpcStatus.Ok, ReadOnlyMemory<byte>.Empty);");
                }
                else
                {
                    writer.Line($"var resp = await impl.{method.Name}(arg);");
                    writer.Line("using var payloadFrame = server.Serializer.SerializeFrame(resp);");
                    writer.Line("return RpcEnvelopeCodec.EncodeResponse(req.RequestId, RpcStatus.Ok, payloadFrame.Memory);");
                }
                writer.CloseBlock(");");
                writer.Line();
            }

            writer.CloseBlock();
            if (service.HasCallback)
            {
                writer.Line();
                writer.OpenBlock($"public static void Bind(RpcServiceRegistry registry, Func<{service.CallbackFullName}, {service.FullName}> implFactory)");
                writer.Line("if (registry is null) throw new ArgumentNullException(nameof(registry));");
                writer.Line("if (implFactory is null) throw new ArgumentNullException(nameof(implFactory));");
                writer.Line($"BindFactory(registry, session => implFactory(new {Naming.GetCallbackProxyTypeName(service.CallbackInterfaceName!)}(session)) ?? throw new InvalidOperationException(\"Service implementation factory returned null.\"));");
                writer.CloseBlock();
            }
            writer.CloseBlock();
            writer.CloseBlock();
            return writer.ToString();
        }

        public static string GenerateCallbackProxy(RpcServiceModel service, string generatedNamespace)
        {
            var writer = new SourceWriter();
            writer.Header();
            writer.Line($"using {ServerRuntimeUsing};");
            writer.Line();
            writer.OpenBlock($"namespace {generatedNamespace}");
            writer.OpenBlock($"public sealed class {Naming.GetCallbackProxyTypeName(service.CallbackInterfaceName!)} : {service.CallbackFullName}");
            writer.Line($"private const int ServiceId = {service.ServiceId};");
            writer.Line("private readonly RpcSession _session;");
            writer.Line();
            writer.Line($"public {Naming.GetCallbackProxyTypeName(service.CallbackInterfaceName!)}(RpcSession session) {{ _session = session; }}");
            writer.Line();
            foreach (var method in service.CallbackMethods)
            {
                writer.OpenBlock($"public void {method.Name}({Naming.GetParameterSignature(method.Parameters)})");
                writer.Line($"_ = _session.PushAsync<{method.PayloadType}>(ServiceId, {method.MethodId}, {method.PayloadValue}).AsTask();");
                writer.CloseBlock();
                writer.Line();
            }
            writer.CloseBlock();
            writer.CloseBlock();
            return writer.ToString();
        }

        public static string GenerateAllServicesBinder(List<RpcServiceModel> services, string generatedNamespace)
        {
            var writer = new SourceWriter();
            writer.Header();
            writer.Line("using System;");
            writer.Line("using System.Linq;");
            writer.Line("using System.Reflection;");
            writer.Line($"using {ServerRuntimeUsing};");
            writer.Line();
            writer.Line($"[assembly: RpcGeneratedServicesBinder(typeof({generatedNamespace}.AllServicesBinder))]");
            writer.Line();
            writer.OpenBlock($"namespace {generatedNamespace}");
            writer.OpenBlock("public static class AllServicesBinder");
            writer.OpenBlock("public static void BindAll(RpcServiceRegistry registry)");
            foreach (var service in services)
            {
                var binder = Naming.GetBinderTypeName(service.InterfaceName);
                if (service.HasCallback)
                    writer.Line($"{binder}.Bind(registry, CreateCallbackServiceFactory<{service.FullName}, {service.CallbackFullName}>());");
                else
                    writer.Line($"{binder}.BindFactory(registry, CreateServiceFactory<{service.FullName}>());");
            }
            writer.CloseBlock();
            writer.Line();
            writer.OpenBlock("private static Func<RpcSession, TService> CreateServiceFactory<TService>()");
            writer.Line("var implType = ResolveImplementationType(typeof(TService));");
            writer.Line("var ctor = implType.GetConstructor(Type.EmptyTypes);");
            writer.OpenBlock("if (ctor is null)");
            writer.Line("throw new InvalidOperationException($\"No public parameterless constructor found for service implementation '{implType.FullName}'.\");");
            writer.CloseBlock();
            writer.Line("return _ => (TService)ctor.Invoke(Array.Empty<object?>());");
            writer.CloseBlock();
            writer.Line();
            writer.OpenBlock("private static Func<TCallback, TService> CreateCallbackServiceFactory<TService, TCallback>()");
            writer.Line("var implType = ResolveImplementationType(typeof(TService));");
            writer.Line("var callbackType = typeof(TCallback);");
            writer.Line("var callbackCtor = implType.GetConstructors(BindingFlags.Public | BindingFlags.Instance)");
            writer.Line("    .SingleOrDefault(static ctor =>");
            writer.Line("    {");
            writer.Line("        var parameters = ctor.GetParameters();");
            writer.Line("        return parameters.Length == 1 && parameters[0].ParameterType.IsAssignableFrom(typeof(TCallback));");
            writer.Line("    });");
            writer.OpenBlock("if (callbackCtor is not null)");
            writer.Line("return callback => (TService)callbackCtor.Invoke(new object?[] { callback });");
            writer.CloseBlock();
            writer.Line("var defaultCtor = implType.GetConstructor(Type.EmptyTypes);");
            writer.OpenBlock("if (defaultCtor is not null)");
            writer.Line("return _ => (TService)defaultCtor.Invoke(Array.Empty<object?>());");
            writer.CloseBlock();
            writer.Line("throw new InvalidOperationException($\"No suitable public constructor found for service implementation '{implType.FullName}'. Expected either a parameterless constructor or one accepting '{callbackType.FullName}'.\");");
            writer.CloseBlock();
            writer.Line();
            writer.OpenBlock("private static Type ResolveImplementationType(Type serviceType)");
            writer.Line("var implementations = typeof(AllServicesBinder).Assembly.GetTypes()");
            writer.Line("    .Where(type => !type.IsAbstract && !type.IsInterface && !type.IsNested && serviceType.IsAssignableFrom(type))");
            writer.Line("    .ToArray();");
            writer.OpenBlock("if (implementations.Length == 1)");
            writer.Line("return implementations[0];");
            writer.CloseBlock();
            writer.OpenBlock("if (implementations.Length == 0)");
            writer.Line("throw new InvalidOperationException($\"No service implementation found for '{serviceType.FullName}' in assembly '{typeof(AllServicesBinder).Assembly.GetName().Name}'.\");");
            writer.CloseBlock();
            writer.Line("var names = string.Join(\", \", implementations.Select(static type => type.FullName));");
            writer.Line("throw new InvalidOperationException($\"Multiple service implementations found for '{serviceType.FullName}': {names}. Use individual generated binders when you need explicit service instances or factories instead.\");");
            writer.CloseBlock();
            writer.CloseBlock();
            writer.CloseBlock();
            return writer.ToString();
        }
    }

    private interface IRpcMethodContract
    {
        string Name { get; }
        int MethodId { get; }
    }

    private sealed class RpcServiceModel
    {
        public RpcServiceModel(
            string interfaceName,
            string fullName,
            string metadataName,
            int serviceId,
            List<RpcMethodModel> methods,
            string? callbackInterfaceName,
            string? callbackFullName)
        {
            InterfaceName = interfaceName;
            FullName = fullName;
            MetadataName = metadataName;
            ServiceId = serviceId;
            Methods = methods;
            CallbackInterfaceName = callbackInterfaceName;
            CallbackFullName = callbackFullName;
        }

        public string InterfaceName { get; }
        public string FullName { get; }
        public string MetadataName { get; }
        public int ServiceId { get; }
        public List<RpcMethodModel> Methods { get; }
        public string? CallbackInterfaceName { get; }
        public string? CallbackFullName { get; }
        public string? CallbackInterfaceFullName => CallbackFullName;
        public List<RpcCallbackMethodModel> CallbackMethods { get; set; } = new();
        public bool HasCallback => CallbackFullName is not null && CallbackMethods.Count > 0;
    }

    private sealed class CallbackModel
    {
        public CallbackModel(string name, string fullName, string serviceFullName, List<RpcCallbackMethodModel> methods)
        {
            Name = name;
            FullName = fullName;
            ServiceFullName = serviceFullName;
            Methods = methods;
        }

        public string Name { get; }
        public string FullName { get; }
        public string ServiceFullName { get; }
        public List<RpcCallbackMethodModel> Methods { get; }
    }

    private sealed class RpcMethodModel : IRpcMethodContract
    {
        public RpcMethodModel(string name, int methodId, List<RpcParameterModel> parameters, string? returnTypeName, bool isVoid)
        {
            Name = name;
            MethodId = methodId;
            Parameters = parameters;
            ReturnTypeName = returnTypeName;
            IsVoid = isVoid;
        }

        public string Name { get; }
        public int MethodId { get; }
        public List<RpcParameterModel> Parameters { get; }
        public string? ReturnTypeName { get; }
        public bool IsVoid { get; }
        public string PayloadType => Parameters[0].TypeName;
        public string PayloadValue => Parameters[0].Name;
    }

    private sealed class RpcCallbackMethodModel : IRpcMethodContract
    {
        public RpcCallbackMethodModel(string name, int methodId, List<RpcParameterModel> parameters)
        {
            Name = name;
            MethodId = methodId;
            Parameters = parameters;
        }

        public string Name { get; }
        public int MethodId { get; }
        public List<RpcParameterModel> Parameters { get; }
        public string PayloadType => Parameters[0].TypeName;
        public string PayloadValue => Parameters[0].Name;
    }

    private sealed class RpcParameterModel
    {
        public RpcParameterModel(string typeName, string name)
        {
            TypeName = typeName;
            Name = name;
        }

        public string TypeName { get; }
        public string Name { get; }
    }

    private sealed class FacadeGroupModel
    {
        public FacadeGroupModel(string groupName, List<RpcServiceModel> services)
        {
            GroupName = groupName;
            Services = services;
        }

        public string GroupName { get; }
        public List<RpcServiceModel> Services { get; }
    }

    private static class Naming
    {
        public static string GetServiceTypeName(string interfaceName) =>
            interfaceName.Length > 1 && interfaceName[0] == 'I' && char.IsUpper(interfaceName[1])
                ? interfaceName.Substring(1)
                : interfaceName;

        public static string GetClientTypeName(string interfaceName) => GetServiceTypeName(interfaceName) + "Client";
        public static string GetBinderTypeName(string interfaceName) => GetServiceTypeName(interfaceName) + "Binder";
        public static string GetCallbackProxyTypeName(string callbackInterfaceName) => GetServiceTypeName(callbackInterfaceName) + "Proxy";
        public static string GetCallbackBinderTypeName(string callbackInterfaceName) => GetServiceTypeName(callbackInterfaceName) + "Binder";
        public static string GetClientExtensionTypeName(string interfaceName) => GetServiceTypeName(interfaceName) + "ClientExtensions";
        public static string GetClientFactoryMethodName(string interfaceName) => "Create" + GetServiceTypeName(interfaceName);
        public static string GetClientMethodFieldName(string methodName) => ToCamelCase(methodName) + "RpcMethod";
        public static string GetCallbackMethodFieldName(string methodName) => ToCamelCase(methodName) + "PushMethod";
        public static string GetCallbackReceiverParamName(string callbackInterfaceName) => ToCamelCase(GetServiceTypeName(callbackInterfaceName));

        public static string GetParameterSignature(IReadOnlyList<RpcParameterModel> parameters) =>
            string.Join(", ", parameters.Select(static parameter => parameter.TypeName + " " + parameter.Name));

        public static string GetFacadeServicePropertyName(string interfaceName)
        {
            var name = GetServiceTypeName(interfaceName);
            if (name.EndsWith("Service", StringComparison.Ordinal) && name.Length > "Service".Length)
                name = name.Substring(0, name.Length - "Service".Length);

            return ToPascalIdentifier(name);
        }

        public static string GetFacadeGroupName(string fullName)
        {
            var noGlobal = fullName.StartsWith("global::", StringComparison.Ordinal)
                ? fullName.Substring("global::".Length)
                : fullName;
            var firstDot = noGlobal.IndexOf('.');
            return firstDot < 0 ? "Default" : ToPascalIdentifier(noGlobal.Substring(0, firstDot));
        }

        private static string ToCamelCase(string value) =>
            string.IsNullOrEmpty(value) ? "value" : char.ToLowerInvariant(value[0]) + value.Substring(1);

        private static string ToPascalIdentifier(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return "Default";

            var builder = new StringBuilder();
            var nextUpper = true;
            foreach (var ch in value)
            {
                if (!char.IsLetterOrDigit(ch))
                {
                    nextUpper = true;
                    continue;
                }

                builder.Append(nextUpper ? char.ToUpperInvariant(ch) : ch);
                nextUpper = false;
            }

            if (builder.Length == 0)
                return "Default";

            if (char.IsDigit(builder[0]))
                builder.Insert(0, '_');

            return builder.ToString();
        }
    }

    private sealed class SourceWriter
    {
        private readonly StringBuilder _builder = new();
        private int _indent;

        public void Header()
        {
            Line("// <auto-generated/>");
            Line("// This file was auto-generated by ULinkRPC.Analyzers. Do not edit.");
            Line("#pragma warning disable");
            Line("#nullable enable");
            Line();
        }

        public void Line(string text)
        {
            for (var i = 0; i < _indent; i++)
                _builder.Append("    ");
            _builder.Append(text).Append('\n');
        }

        public void Line() => _builder.Append('\n');

        public void OpenBlock(string header)
        {
            Line(header);
            Line("{");
            _indent++;
        }

        public void CloseBlock(string suffix = "")
        {
            _indent--;
            Line("}" + suffix);
        }

        public override string ToString() => _builder.ToString();
    }
}
