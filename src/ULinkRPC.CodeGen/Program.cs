using System.Text;
using System.Text.RegularExpressions;

namespace ULinkRPC.CodeGen;

internal static class Program
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

        var services = FindRpcServicesFromSource(options.ContractsPath);
        if (services.Count == 0)
        {
            Console.Error.WriteLine("No [RpcService] interfaces found.");
            return 1;
        }

        if (options.Mode == OutputMode.Unity)
        {
            Directory.CreateDirectory(options.OutputPath);
        }

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
                var (client, _) = GenerateCode(svc, options.UnityNamespace, DefaultCoreRuntimeUsing,
                    DefaultServerRuntimeUsing);
                if (client != null)
                {
                    var clientTypeName = GetClientTypeName(svc.InterfaceName);
                    File.WriteAllText(Path.Combine(options.OutputPath, $"{clientTypeName}.cs"), client, Encoding.UTF8);
                    generated++;
                }

                if (svc.HasCallback)
                {
                    var cbBinder = GenerateCallbackBinderCode(svc, options.UnityNamespace,
                        DefaultCoreRuntimeUsing, DefaultCoreRuntimeUsing);
                    var cbBinderTypeName = GetCallbackBinderTypeName(svc.CallbackInterfaceName!);
                    File.WriteAllText(Path.Combine(options.OutputPath, $"{cbBinderTypeName}.cs"), cbBinder, Encoding.UTF8);
                    generated++;
                }
            }

            if (options.Mode == OutputMode.Server)
            {
                var serverBinder = GenerateBinderCode(svc, options.ServerNamespace, DefaultCoreRuntimeUsing,
                    DefaultServerRuntimeUsing);
                var binderTypeName = GetBinderTypeName(svc.InterfaceName);
                File.WriteAllText(Path.Combine(options.ServerOutputPath, $"{binderTypeName}.cs"), serverBinder, Encoding.UTF8);
                generated++;

                if (svc.HasCallback)
                {
                    var cbProxy = GenerateCallbackProxyCode(svc, options.ServerNamespace, DefaultCoreRuntimeUsing,
                        DefaultServerRuntimeUsing);
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

    private static void PrintUsage()
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

    private static bool TryParseCliArguments(string[] args, out RawOptions options, out string error)
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
            {
                contractsPath = args[++i];
            }
            else if (arg == "--output" && i + 1 < args.Length)
            {
                outputPath = args[++i];
            }
            else if (arg == "--namespace" && i + 1 < args.Length)
            {
                unityNamespace = args[++i];
            }
            else if (arg == "--server-output" && i + 1 < args.Length)
            {
                serverOutputPath = args[++i];
            }
            else if (arg == "--server-namespace" && i + 1 < args.Length)
            {
                serverNamespace = args[++i];
            }
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

        options = new RawOptions(
            contractsPath,
            outputPath,
            unityNamespace,
            serverOutputPath,
            serverNamespace,
            mode);

        return true;
    }

    private static bool TryResolveGenerationOptions(RawOptions raw, out ResolvedOptions options, out string error)
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
                var unityRoot = FindUnityProjectRoot(cwd);
                if (unityRoot == null)
                {
                    error = "Unity mode requires --output when current directory is not inside a Unity project.";
                    return false;
                }

                outputPath = Path.Combine(unityRoot, DefaultUnityOutputRelativePath);
            }
            else
            {
                outputPath = Path.GetFullPath(raw.OutputPath);
            }

            unityNamespace = string.IsNullOrWhiteSpace(raw.UnityNamespace)
                ? DeriveNamespaceFromOutputPath(outputPath)
                : raw.UnityNamespace;
        }

        if (mode == OutputMode.Server)
        {
            serverNamespace = raw.ServerNamespace;
            if (string.IsNullOrWhiteSpace(raw.ServerOutputPath))
            {
                serverOutputPath = Path.Combine(cwd, "Generated");
            }
            else
            {
                serverOutputPath = Path.GetFullPath(raw.ServerOutputPath);
            }
        }

        if (!Directory.Exists(contractsPath))
        {
            error = $"Contracts path not found: {contractsPath}";
            return false;
        }

        options = new ResolvedOptions(
            contractsPath,
            outputPath,
            unityNamespace,
            serverOutputPath,
            serverNamespace,
            mode);

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

    private static List<RpcServiceInfo> FindRpcServicesFromSource(string contractsPath)
    {
        var files = Directory.GetFiles(contractsPath, "*.cs", SearchOption.AllDirectories);
        var sourceFiles = new List<SourceFileInfo>();
        var services = new List<RpcServiceInfo>();

        foreach (var file in files)
        {
            var text = File.ReadAllText(file);
            var ns = ParseNamespace(text);
            var usingDirectives = ParseUsingDirectives(text);
            sourceFiles.Add(new SourceFileInfo(text, usingDirectives));
            services.AddRange(ParseServices(text, ns, usingDirectives));
        }

        foreach (var svc in services)
        {
            if (string.IsNullOrEmpty(svc.CallbackInterfaceName))
                continue;

            foreach (var sourceFile in sourceFiles)
            {
                var cbMethods = ParseCallbackInterface(sourceFile.Text, svc.CallbackInterfaceName);
                if (cbMethods != null)
                {
                    svc.CallbackMethods = cbMethods;
                    svc.AddUsingDirectives(sourceFile.UsingDirectives);
                    break;
                }
            }
        }

        return services;
    }

    private static List<RpcCallbackMethodInfo>? ParseCallbackInterface(string text, string callbackName)
    {
        var pattern = $@"public\s+interface\s+{Regex.Escape(callbackName)}\s*{{";
        var match = Regex.Match(text, pattern, RegexOptions.Multiline);
        if (!match.Success)
            return null;

        var braceIndex = text.IndexOf('{', match.Index);
        if (braceIndex < 0)
            return null;

        var endIndex = FindMatchingBrace(text, braceIndex);
        if (endIndex < 0)
            return null;

        var body = text.Substring(braceIndex + 1, endIndex - braceIndex - 1);
        return ParseCallbackMethods(body);
    }

    private static List<RpcCallbackMethodInfo> ParseCallbackMethods(string body)
    {
        var methodRegex = new Regex(@"\[RpcMethod\((\d+)\)\]\s*([^\r\n;]+);", RegexOptions.Multiline);
        var matches = methodRegex.Matches(body);
        var methods = new List<RpcCallbackMethodInfo>();

        foreach (Match match in matches)
        {
            var methodId = int.Parse(match.Groups[1].Value);
            var signature = match.Groups[2].Value.Trim();
            if (!TryParseCallbackSignature(signature, out var name, out var parameters))
                continue;

            methods.Add(new RpcCallbackMethodInfo(name, methodId, parameters));
        }

        return methods;
    }

    private static bool TryParseCallbackSignature(string signature, out string name, out List<RpcParameterInfo> parameters)
    {
        name = string.Empty;
        parameters = new List<RpcParameterInfo>();

        var openParen = signature.IndexOf('(');
        var closeParen = signature.LastIndexOf(')');
        if (openParen <= 0 || closeParen <= openParen)
            return false;

        var header = signature.Substring(0, openParen).Trim();
        var paramList = signature.Substring(openParen + 1, closeParen - openParen - 1).Trim();
        var splitAt = FindLastTopLevelWhitespace(header);
        if (splitAt <= 0 || splitAt >= header.Length - 1)
            return false;

        var ret = header.Substring(0, splitAt).Trim();
        name = header.Substring(splitAt).Trim();

        if (!string.Equals(ret, "void", StringComparison.Ordinal))
            return false;

        parameters = ParseParameters(paramList);
        return true;
    }

    private static string ParseNamespace(string text)
    {
        var match = Regex.Match(text, @"^\s*namespace\s+([A-Za-z0-9_.]+)\s*$", RegexOptions.Multiline);
        return match.Success ? match.Groups[1].Value : string.Empty;
    }

    private static IReadOnlyList<string> ParseUsingDirectives(string text)
    {
        var matches = Regex.Matches(text, @"^\s*(?:global\s+)?using\s+([^;]+);\s*$", RegexOptions.Multiline);
        return matches
            .Cast<Match>()
            .Select(match => match.Groups[1].Value.Trim())
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.Ordinal)
            .ToList();
    }

    private static IEnumerable<RpcServiceInfo> ParseServices(string text, string ns, IReadOnlyList<string> usingDirectives)
    {
        var svcRegex = new Regex(
            @"\[RpcService\((\d+)\)\]\s*public\s+interface\s+(\w+)\s*(?::\s*IRpcService<\w+,\s*(\w+)>)?\s*{",
            RegexOptions.Multiline);
        var matches = svcRegex.Matches(text);

        foreach (Match match in matches)
        {
            var serviceId = int.Parse(match.Groups[1].Value);
            var ifaceName = match.Groups[2].Value;
            var ifaceFullName = string.IsNullOrEmpty(ns) ? ifaceName : $"{ns}.{ifaceName}";
            var callbackName = match.Groups[3].Success ? match.Groups[3].Value : null;

            var braceIndex = text.IndexOf('{', match.Index);
            if (braceIndex < 0)
                continue;

            var endIndex = FindMatchingBrace(text, braceIndex);
            if (endIndex < 0)
                continue;

            var body = text.Substring(braceIndex + 1, endIndex - braceIndex - 1);
            var methods = ParseMethods(body);
            if (methods.Count > 0)
            {
                var svc = new RpcServiceInfo(ifaceName, ifaceFullName, serviceId, methods, usingDirectives)
                {
                    CallbackInterfaceName = callbackName
                };
                yield return svc;
            }
        }
    }

    private static int FindMatchingBrace(string text, int startIndex)
    {
        var depth = 0;
        for (var i = startIndex; i < text.Length; i++)
        {
            if (text[i] == '{')
                depth++;
            else if (text[i] == '}')
            {
                depth--;
                if (depth == 0)
                    return i;
            }
        }
        return -1;
    }

    private static List<RpcMethodInfo> ParseMethods(string body)
    {
        var methodRegex = new Regex(@"\[RpcMethod\((\d+)\)\]\s*([^\r\n;]+);", RegexOptions.Multiline);
        var matches = methodRegex.Matches(body);
        var methods = new List<RpcMethodInfo>();

        foreach (Match match in matches)
        {
            var methodId = int.Parse(match.Groups[1].Value);
            var signature = match.Groups[2].Value.Trim();
            if (!TryParseMethodSignature(signature, out var name, out var parameters, out var retType, out var isVoid))
                continue;

            methods.Add(new RpcMethodInfo(name, methodId, parameters, retType, isVoid));
        }

        return methods;
    }

    private static bool TryParseMethodSignature(
        string signature,
        out string name,
        out List<RpcParameterInfo> parameters,
        out string? retType,
        out bool isVoid)
    {
        name = string.Empty;
        parameters = new List<RpcParameterInfo>();
        retType = null;
        isVoid = false;

        var openParen = signature.IndexOf('(');
        var closeParen = signature.LastIndexOf(')');
        if (openParen <= 0 || closeParen <= openParen)
            return false;

        var header = signature.Substring(0, openParen).Trim();
        var paramList = signature.Substring(openParen + 1, closeParen - openParen - 1).Trim();
        var splitAt = FindLastTopLevelWhitespace(header);
        if (splitAt <= 0 || splitAt >= header.Length - 1)
            return false;

        var ret = header.Substring(0, splitAt).Trim();
        name = header.Substring(splitAt).Trim();
        if (string.IsNullOrWhiteSpace(name))
            return false;

        if (string.Equals(ret, "ValueTask", StringComparison.Ordinal) ||
            string.Equals(ret, "System.Threading.Tasks.ValueTask", StringComparison.Ordinal))
        {
            isVoid = true;
        }
        else
        {
            var genericMatch = Regex.Match(ret, @"^(?:System\.Threading\.Tasks\.)?ValueTask<(.+)>$");
            if (genericMatch.Success)
                retType = genericMatch.Groups[1].Value.Trim();
            else
                retType = ret;
        }

        parameters = ParseParameters(paramList);

        return true;
    }

    private static List<RpcParameterInfo> ParseParameters(string paramList)
    {
        var result = new List<RpcParameterInfo>();
        if (string.IsNullOrWhiteSpace(paramList))
            return result;

        var parts = SplitTopLevel(paramList, ',');
        for (var i = 0; i < parts.Count; i++)
        {
            var parsed = TryParseParameter(parts[i], i + 1);
            if (parsed != null)
                result.Add(parsed);
        }

        return result;
    }

    private static RpcParameterInfo? TryParseParameter(string input, int index)
    {
        var param = input.Trim();
        if (string.IsNullOrWhiteSpace(param))
            return null;

        param = TrimLeadingParameterAttributes(param);
        var eqIndex = FindFirstTopLevel(param, '=');
        if (eqIndex >= 0)
            param = param.Substring(0, eqIndex).Trim();

        if (string.IsNullOrWhiteSpace(param))
            return null;

        var splitAt = FindLastTopLevelWhitespace(param);
        if (splitAt <= 0 || splitAt >= param.Length - 1)
            return new RpcParameterInfo(TrimParameterTypeModifiers(param), $"arg{index}");

        var typeName = TrimParameterTypeModifiers(param.Substring(0, splitAt).Trim());
        var name = param.Substring(splitAt).Trim();
        if (string.IsNullOrWhiteSpace(typeName))
            return null;

        if (string.IsNullOrWhiteSpace(name))
            name = $"arg{index}";

        return new RpcParameterInfo(typeName, name);
    }

    private static string TrimLeadingParameterAttributes(string input)
    {
        var text = input.Trim();
        while (text.StartsWith("[", StringComparison.Ordinal))
        {
            var depth = 0;
            var consumed = 0;
            for (var i = 0; i < text.Length; i++)
            {
                if (text[i] == '[')
                    depth++;
                else if (text[i] == ']')
                {
                    depth--;
                    if (depth == 0)
                    {
                        consumed = i + 1;
                        break;
                    }
                }
            }

            if (consumed == 0)
                break;

            text = text.Substring(consumed).TrimStart();
        }

        return text;
    }

    private static string TrimParameterTypeModifiers(string typePart)
    {
        var text = typePart.Trim();
        while (true)
        {
            if (text.StartsWith("in ", StringComparison.Ordinal))
            {
                text = text.Substring(3).TrimStart();
                continue;
            }

            if (text.StartsWith("out ", StringComparison.Ordinal))
            {
                text = text.Substring(4).TrimStart();
                continue;
            }

            if (text.StartsWith("ref ", StringComparison.Ordinal))
            {
                text = text.Substring(4).TrimStart();
                continue;
            }

            if (text.StartsWith("params ", StringComparison.Ordinal))
            {
                text = text.Substring(7).TrimStart();
                continue;
            }

            if (text.StartsWith("this ", StringComparison.Ordinal))
            {
                text = text.Substring(5).TrimStart();
                continue;
            }

            if (text.StartsWith("scoped ", StringComparison.Ordinal))
            {
                text = text.Substring(7).TrimStart();
                continue;
            }

            if (text.StartsWith("readonly ", StringComparison.Ordinal))
            {
                text = text.Substring(9).TrimStart();
                continue;
            }

            return text;
        }
    }

    private static int FindFirstTopLevel(string text, char target)
    {
        var angleDepth = 0;
        var parenDepth = 0;
        var bracketDepth = 0;
        var braceDepth = 0;
        for (var i = 0; i < text.Length; i++)
        {
            var ch = text[i];
            switch (ch)
            {
                case '<':
                    angleDepth++;
                    break;
                case '>':
                    if (angleDepth > 0) angleDepth--;
                    break;
                case '(':
                    parenDepth++;
                    break;
                case ')':
                    if (parenDepth > 0) parenDepth--;
                    break;
                case '[':
                    bracketDepth++;
                    break;
                case ']':
                    if (bracketDepth > 0) bracketDepth--;
                    break;
                case '{':
                    braceDepth++;
                    break;
                case '}':
                    if (braceDepth > 0) braceDepth--;
                    break;
                default:
                    if (ch == target && angleDepth == 0 && parenDepth == 0 && bracketDepth == 0 && braceDepth == 0)
                        return i;
                    break;
            }
        }

        return -1;
    }

    private static int FindLastTopLevelWhitespace(string text)
    {
        var angleDepth = 0;
        var parenDepth = 0;
        var bracketDepth = 0;
        var braceDepth = 0;
        var splitAt = -1;
        for (var i = 0; i < text.Length; i++)
        {
            var ch = text[i];
            switch (ch)
            {
                case '<':
                    angleDepth++;
                    break;
                case '>':
                    if (angleDepth > 0) angleDepth--;
                    break;
                case '(':
                    parenDepth++;
                    break;
                case ')':
                    if (parenDepth > 0) parenDepth--;
                    break;
                case '[':
                    bracketDepth++;
                    break;
                case ']':
                    if (bracketDepth > 0) bracketDepth--;
                    break;
                case '{':
                    braceDepth++;
                    break;
                case '}':
                    if (braceDepth > 0) braceDepth--;
                    break;
                default:
                    if (char.IsWhiteSpace(ch) && angleDepth == 0 && parenDepth == 0 && bracketDepth == 0 && braceDepth == 0)
                        splitAt = i;
                    break;
            }
        }

        return splitAt;
    }

    private static List<string> SplitTopLevel(string text, char separator)
    {
        var result = new List<string>();
        if (string.IsNullOrWhiteSpace(text))
            return result;

        var angleDepth = 0;
        var parenDepth = 0;
        var bracketDepth = 0;
        var braceDepth = 0;
        var lastIndex = 0;

        for (var i = 0; i < text.Length; i++)
        {
            var ch = text[i];
            switch (ch)
            {
                case '<':
                    angleDepth++;
                    break;
                case '>':
                    if (angleDepth > 0) angleDepth--;
                    break;
                case '(':
                    parenDepth++;
                    break;
                case ')':
                    if (parenDepth > 0) parenDepth--;
                    break;
                case '[':
                    bracketDepth++;
                    break;
                case ']':
                    if (bracketDepth > 0) bracketDepth--;
                    break;
                case '{':
                    braceDepth++;
                    break;
                case '}':
                    if (braceDepth > 0) braceDepth--;
                    break;
                default:
                    if (ch == separator && angleDepth == 0 && parenDepth == 0 && bracketDepth == 0 && braceDepth == 0)
                    {
                        result.Add(text.Substring(lastIndex, i - lastIndex).Trim());
                        lastIndex = i + 1;
                    }
                    break;
            }
        }

        if (lastIndex <= text.Length)
            result.Add(text.Substring(lastIndex).Trim());

        return result;
    }

    private static (string? Client, string? Binder) GenerateCode(
        RpcServiceInfo svc,
        string runtimeNamespace,
        string clientRuntimeUsing,
        string serverRuntimeUsing)
    {
        var ifaceName = svc.InterfaceName;
        var clientTypeName = GetClientTypeName(ifaceName);
        var contractUsings = ExcludeUsingDirectives(
            GetContractUsingDirectives(svc),
            "System",
            "System.Threading",
            "System.Threading.Tasks",
            clientRuntimeUsing);

        var clientBody = new StringBuilder();
        clientBody.Append("using System;\nusing System.Threading;\nusing System.Threading.Tasks;\n")
            .Append(FormatUsingBlock(contractUsings))
            .Append("using ")
            .Append(clientRuntimeUsing)
            .Append(";\n\nnamespace ")
            .Append(runtimeNamespace)
            .Append("\n{\n");
        clientBody.Append("    public sealed class ").Append(clientTypeName).Append(" : ").Append(ifaceName).Append("\n    {\n");
        clientBody.Append("        private const int ServiceId = ").Append(svc.ServiceId).Append(";\n");
        foreach (var m in svc.Methods)
        {
            var argType = GetRequestPayloadType(m);
            var retType = m.IsVoid ? "RpcVoid" : m.RetTypeName!;
            clientBody.Append("        private static readonly RpcMethod<").Append(argType).Append(", ")
                .Append(retType).Append("> ").Append(GetClientMethodFieldName(m.Name))
                .Append(" = new(ServiceId, ").Append(m.MethodId).Append(");\n");
        }
        clientBody.Append('\n');
        clientBody.Append("        private readonly IRpcClient _client;\n\n");
        clientBody.Append("        public ").Append(clientTypeName).Append("(IRpcClient client) { _client = client; }\n\n");

        foreach (var m in svc.Methods)
        {
            var argType = GetRequestPayloadType(m);
            var retType = m.IsVoid ? "RpcVoid" : m.RetTypeName!;
            var argVal = GetRequestPayloadValue(m);
            var methodParamSig = GetMethodParameterSignature(m);
            var sig = $"{m.Name}({methodParamSig})";
            var sigWithCt = string.IsNullOrEmpty(methodParamSig)
                ? $"{m.Name}(CancellationToken ct)"
                : $"{m.Name}({methodParamSig}, CancellationToken ct)";
            if (m.IsVoid)
            {
                clientBody.Append("        public async ValueTask ").Append(sig).Append("\n        {\n");
                clientBody.Append("            await ").Append(m.Name).Append("(")
                    .Append(GetForwardArguments(m.Parameters, includeCt: false)).Append(");\n        }\n\n");
                clientBody.Append("        public async ValueTask ").Append(sigWithCt).Append("\n        {\n");
                clientBody.Append("            await _client.CallAsync(").Append(GetClientMethodFieldName(m.Name))
                    .Append(", ").Append(argVal).Append(", ct);\n        }\n\n");
            }
            else
            {
                clientBody.Append("        public ValueTask<").Append(retType).Append("> ").Append(sig).Append("\n        {\n");
                clientBody.Append("            return ").Append(m.Name).Append("(")
                    .Append(GetForwardArguments(m.Parameters, includeCt: false)).Append(");\n        }\n\n");
                clientBody.Append("        public ValueTask<").Append(retType).Append("> ").Append(sigWithCt).Append("\n        {\n");
                clientBody.Append("            return _client.CallAsync(").Append(GetClientMethodFieldName(m.Name))
                    .Append(", ").Append(argVal).Append(", ct);\n        }\n\n");
            }
        }
        clientBody.Append("    }\n\n");
        clientBody.Append("    public static class ").Append(GetClientExtensionTypeName(ifaceName)).Append("\n    {\n");
        clientBody.Append("        public static ").Append(ifaceName).Append(" ").Append(GetClientFactoryMethodName(ifaceName))
            .Append("(this IRpcClient client)\n        {\n");
        clientBody.Append("            if (client is null) throw new ArgumentNullException(nameof(client));\n");
        clientBody.Append("            return new ").Append(clientTypeName).Append("(client);\n");
        clientBody.Append("        }\n");
        clientBody.Append("    }\n}\n");

        return (clientBody.ToString(), GenerateBinderCode(svc, runtimeNamespace, DefaultCoreRuntimeUsing,
            serverRuntimeUsing));
    }

    private static string GenerateBinderCode(RpcServiceInfo svc, string ns, string coreRuntimeUsing,
        string serverRuntimeUsing)
    {
        var ifaceName = svc.InterfaceName;
        var binderTypeName = GetBinderTypeName(ifaceName);
        var contractUsings = ExcludeUsingDirectives(
            GetContractUsingDirectives(svc),
            "System",
            "System.Threading.Tasks",
            coreRuntimeUsing,
            serverRuntimeUsing);
        var binderSb = new StringBuilder();
        binderSb.Append("using System;\nusing System.Threading.Tasks;\n")
            .Append(FormatUsingBlock(contractUsings))
            .Append("using ")
            .Append(coreRuntimeUsing)
            .Append(";\nusing ")
            .Append(serverRuntimeUsing)
            .Append(";\n\nnamespace ")
            .Append(ns)
            .Append("\n{\n");
        binderSb.Append("    public static class ").Append(binderTypeName).Append("\n    {\n");
        binderSb.Append("        private const int ServiceId = ").Append(svc.ServiceId).Append(";\n\n");
        binderSb.Append(GenerateDelegateBindOverload(svc));
        if (svc.HasCallback)
            binderSb.Append(GenerateCallbackFactoryBindOverload(svc));
        binderSb.Append("        public static void Bind(RpcServer server, ").Append(ifaceName).Append(" impl)\n        {\n");

        foreach (var m in svc.Methods)
        {
            var argType = GetRequestPayloadType(m);
            binderSb.Append("            server.Register(ServiceId, ").Append(m.MethodId).Append(", async (req, ct) =>\n            {\n");
            if (m.Parameters.Count == 1)
            {
                binderSb.Append("                var arg1 = server.Serializer.Deserialize<").Append(argType).Append(">(req.Payload.AsSpan())!;\n");
            }
            else if (m.Parameters.Count > 1)
            {
                binderSb.Append("                var (").Append(GetDeconstructVariableList(m.Parameters.Count)).Append(") = server.Serializer.Deserialize<")
                    .Append(argType).Append(">(req.Payload.AsSpan())!;\n");
            }
            if (m.IsVoid)
            {
                binderSb.Append("                await impl.").Append(m.Name).Append("(").Append(GetInvokeArguments(m.Parameters.Count)).Append(");\n");
                binderSb.Append("                return new RpcResponseEnvelope { RequestId = req.RequestId, Status = RpcStatus.Ok, Payload = Array.Empty<byte>() };\n");
            }
            else
            {
                binderSb.Append("                var resp = await impl.").Append(m.Name).Append("(").Append(GetInvokeArguments(m.Parameters.Count)).Append(");\n");
                binderSb.Append("                return new RpcResponseEnvelope { RequestId = req.RequestId, Status = RpcStatus.Ok, Payload = server.Serializer.Serialize(resp) };\n");
            }
            binderSb.Append("            });\n\n");
        }
        binderSb.Append("        }\n    }\n}\n");

        return binderSb.ToString();
    }

    private static string GenerateAllServicesBinder(List<RpcServiceInfo> services, string ns, string runtimeUsing)
    {
        var contractUsings = ExcludeUsingDirectives(GetContractUsingDirectives(services), runtimeUsing);
        var sb = new StringBuilder();
        sb.Append(FormatUsingBlock(contractUsings))
            .Append("using ")
            .Append(runtimeUsing)
            .Append(";\n\nnamespace ")
            .Append(ns)
            .Append("\n{\n");
        sb.Append("    public static class AllServicesBinder\n    {\n");
        sb.Append("        public static void BindAll(RpcServer server");
        foreach (var svc in services)
        {
            sb.Append(", ").Append(svc.InterfaceName).Append(" ").Append(GetServiceParamName(svc.InterfaceName));
        }
        sb.Append(")\n        {\n");
        foreach (var svc in services)
        {
            sb.Append("            ").Append(GetBinderTypeName(svc.InterfaceName))
                .Append(".Bind(server, ").Append(GetServiceParamName(svc.InterfaceName)).Append(");\n");
        }
        sb.Append("        }\n    }\n}\n");
        return sb.ToString();
    }

    private static string GenerateClientFacadeCode(List<RpcServiceInfo> services, string ns, string coreRuntimeUsing)
    {
        var groups = BuildFacadeGroups(services);
        var contractUsings = ExcludeUsingDirectives(GetContractUsingDirectives(services), "System", coreRuntimeUsing);
        var sb = new StringBuilder();
        sb.Append("using System;\n")
            .Append(FormatUsingBlock(contractUsings))
            .Append("using ")
            .Append(coreRuntimeUsing)
            .Append(";\n\nnamespace ")
            .Append(ns)
            .Append("\n{\n");

        sb.Append("    public sealed class RpcApi\n    {\n");
        sb.Append("        public RpcApi(IRpcClient client)\n        {\n");
        sb.Append("            if (client is null) throw new ArgumentNullException(nameof(client));\n");
        foreach (var group in groups)
            sb.Append("            ").Append(group.GroupName).Append(" = new ").Append(GetFacadeGroupTypeName(group.GroupName))
                .Append("(client);\n");
        sb.Append("        }\n\n");

        foreach (var group in groups)
            sb.Append("        public ").Append(GetFacadeGroupTypeName(group.GroupName)).Append(" ")
                .Append(group.GroupName).Append(" { get; }\n");

        sb.Append("    }\n\n");

        foreach (var group in groups)
        {
            sb.Append("    public sealed class ").Append(GetFacadeGroupTypeName(group.GroupName)).Append("\n    {\n");
            sb.Append("        public ").Append(GetFacadeGroupTypeName(group.GroupName)).Append("(IRpcClient client)\n        {\n");
            sb.Append("            if (client is null) throw new ArgumentNullException(nameof(client));\n");
            foreach (var member in group.Members)
                sb.Append("            ").Append(member.PropertyName).Append(" = client.")
                    .Append(GetClientFactoryMethodName(member.Service.InterfaceName)).Append("();\n");
            sb.Append("        }\n\n");

            foreach (var member in group.Members)
                sb.Append("        public ").Append(member.Service.InterfaceName).Append(" ")
                    .Append(member.PropertyName).Append(" { get; }\n");

            sb.Append("    }\n\n");
        }

        sb.Append("    public static class RpcApiExtensions\n    {\n");
        sb.Append("        public static RpcApi CreateRpcApi(this IRpcClient client)\n        {\n");
        sb.Append("            if (client is null) throw new ArgumentNullException(nameof(client));\n");
        sb.Append("            return new RpcApi(client);\n");
        sb.Append("        }\n");
        sb.Append("    }\n}\n");
        return sb.ToString();
    }

    private static string GenerateCallbackProxyCode(RpcServiceInfo svc, string ns, string coreRuntimeUsing,
        string serverRuntimeUsing)
    {
        var cbName = svc.CallbackInterfaceName!;
        var proxyTypeName = GetCallbackProxyTypeName(cbName);
        var contractUsings = ExcludeUsingDirectives(
            GetContractUsingDirectives(svc),
            "System",
            coreRuntimeUsing,
            serverRuntimeUsing);
        var sb = new StringBuilder();
        sb.Append("using System;\n")
            .Append(FormatUsingBlock(contractUsings))
            .Append("using ").Append(coreRuntimeUsing).Append(";\n")
            .Append("using ").Append(serverRuntimeUsing).Append(";\n\n")
            .Append("namespace ").Append(ns).Append("\n{\n");
        sb.Append("    public sealed class ").Append(proxyTypeName).Append(" : ").Append(cbName).Append("\n    {\n");
        sb.Append("        private const int ServiceId = ").Append(svc.ServiceId).Append(";\n");
        sb.Append("        private readonly RpcServer _server;\n\n");
        sb.Append("        public ").Append(proxyTypeName).Append("(RpcServer server) { _server = server; }\n\n");

        foreach (var m in svc.CallbackMethods)
        {
            var paramSig = GetMethodParameterSignature(m.Parameters);
            var argType = GetCallbackPayloadType(m);
            var argVal = GetCallbackPayloadValue(m);
            sb.Append("        public void ").Append(m.Name).Append("(").Append(paramSig).Append(")\n        {\n");
            sb.Append("            _server.PushAsync<").Append(argType).Append(">(ServiceId, ").Append(m.MethodId)
                .Append(", ").Append(argVal).Append(").AsTask().Wait();\n");
            sb.Append("        }\n\n");
        }

        sb.Append("    }\n}\n");
        return sb.ToString();
    }

    private static string GenerateCallbackBinderCode(RpcServiceInfo svc, string ns, string coreRuntimeUsing,
        string clientRuntimeUsing)
    {
        var cbName = svc.CallbackInterfaceName!;
        var binderTypeName = GetCallbackBinderTypeName(cbName);
        var contractUsings = ExcludeUsingDirectives(GetContractUsingDirectives(svc), "System", coreRuntimeUsing);
        var sb = new StringBuilder();
        sb.Append("using System;\n")
            .Append(FormatUsingBlock(contractUsings))
            .Append("using ").Append(coreRuntimeUsing).Append(";\n\n")
            .Append("namespace ").Append(ns).Append("\n{\n");
        sb.Append("    public static class ").Append(binderTypeName).Append("\n    {\n");
        sb.Append("        private const int ServiceId = ").Append(svc.ServiceId).Append(";\n\n");
        foreach (var m in svc.CallbackMethods)
        {
            var argType = GetCallbackPayloadType(m);
            sb.Append("        private static readonly RpcPushMethod<").Append(argType).Append("> ")
                .Append(GetCallbackMethodFieldName(m.Name)).Append(" = new(ServiceId, ").Append(m.MethodId).Append(");\n");
        }
        sb.Append('\n');
        sb.Append("        public static void Bind(IRpcClient client, ").Append(cbName)
            .Append(" receiver)\n        {\n");

        foreach (var m in svc.CallbackMethods)
        {
            var argType = GetCallbackPayloadType(m);
            sb.Append("            client.RegisterPushHandler(").Append(GetCallbackMethodFieldName(m.Name))
                .Append(", (arg) =>\n            {\n");
            if (m.Parameters.Count == 1)
            {
                sb.Append("                receiver.").Append(m.Name).Append("(arg);\n");
            }
            else if (m.Parameters.Count > 1)
            {
                sb.Append("                var (").Append(GetDeconstructVariableList(m.Parameters.Count))
                    .Append(") = arg;\n");
                sb.Append("                receiver.").Append(m.Name).Append("(")
                    .Append(GetInvokeArguments(m.Parameters.Count)).Append(");\n");
            }
            else
            {
                sb.Append("                receiver.").Append(m.Name).Append("();\n");
            }
            sb.Append("            });\n\n");
        }

        sb.Append("        }\n    }\n}\n");
        return sb.ToString();
    }

    private static string GetCallbackPayloadType(RpcCallbackMethodInfo method)
    {
        if (method.Parameters.Count == 0)
            return "RpcVoid";
        if (method.Parameters.Count == 1)
            return method.Parameters[0].TypeName;
        return $"({string.Join(", ", method.Parameters.Select(p => p.TypeName))})";
    }

    private static string GetCallbackPayloadValue(RpcCallbackMethodInfo method)
    {
        if (method.Parameters.Count == 0)
            return "default!";
        if (method.Parameters.Count == 1)
            return method.Parameters[0].Name;
        return $"({string.Join(", ", method.Parameters.Select(p => p.Name))})";
    }

    private static string GetCallbackProxyTypeName(string callbackInterfaceName)
    {
        return $"{GetServiceTypeName(callbackInterfaceName)}Proxy";
    }

    private static string GetCallbackBinderTypeName(string callbackInterfaceName)
    {
        return $"{GetServiceTypeName(callbackInterfaceName)}Binder";
    }

    private static string GetClientMethodFieldName(string methodName)
    {
        return $"{GetCamelCaseName(methodName)}RpcMethod";
    }

    private static string GetCallbackMethodFieldName(string methodName)
    {
        return $"{GetCamelCaseName(methodName)}PushMethod";
    }

    private static string GetClientExtensionTypeName(string ifaceName)
    {
        return $"{GetServiceTypeName(ifaceName)}ClientExtensions";
    }

    private static string GetClientFactoryMethodName(string ifaceName)
    {
        return $"Create{GetServiceTypeName(ifaceName)}";
    }

    private static string GetFacadeGroupTypeName(string groupName)
    {
        return $"{groupName}RpcGroup";
    }

    private static List<FacadeGroupInfo> BuildFacadeGroups(List<RpcServiceInfo> services)
    {
        return services
            .GroupBy(GetFacadeGroupName)
            .OrderBy(g => g.Key, StringComparer.Ordinal)
            .Select(g => new FacadeGroupInfo(g.Key, BuildFacadeMembers(g.ToList())))
            .ToList();
    }

    private static List<FacadeMemberInfo> BuildFacadeMembers(List<RpcServiceInfo> services)
    {
        var members = new List<FacadeMemberInfo>();
        var usedNames = new HashSet<string>(StringComparer.Ordinal);
        foreach (var svc in services.OrderBy(s => s.InterfaceName, StringComparer.Ordinal))
        {
            var baseName = GetFacadeServicePropertyName(svc.InterfaceName);
            var uniqueName = baseName;
            var suffix = 2;
            while (!usedNames.Add(uniqueName))
            {
                uniqueName = $"{baseName}{suffix}";
                suffix++;
            }

            members.Add(new FacadeMemberInfo(svc, uniqueName));
        }

        return members;
    }

    private static string GetFacadeGroupName(RpcServiceInfo svc)
    {
        var ns = GetNamespaceFromFullName(svc.InterfaceFullName);
        if (string.IsNullOrWhiteSpace(ns))
            return "Default";

        var firstSegment = ns.Split('.', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
        return ToPascalIdentifier(firstSegment ?? "Default");
    }

    private static string GetFacadeServicePropertyName(string ifaceName)
    {
        var serviceTypeName = GetServiceTypeName(ifaceName);
        if (serviceTypeName.EndsWith("Service", StringComparison.Ordinal) &&
            serviceTypeName.Length > "Service".Length)
            serviceTypeName = serviceTypeName.Substring(0, serviceTypeName.Length - "Service".Length);

        return ToPascalIdentifier(serviceTypeName);
    }

    private static string ToPascalIdentifier(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "Default";

        var parts = Regex.Split(value, "[^A-Za-z0-9]+")
            .Where(part => !string.IsNullOrWhiteSpace(part))
            .ToList();

        if (parts.Count == 0)
            return "Default";

        var sb = new StringBuilder();
        foreach (var part in parts)
        {
            var token = part.Trim();
            if (token.Length == 0)
                continue;

            sb.Append(char.ToUpperInvariant(token[0]));
            if (token.Length > 1)
                sb.Append(token.Substring(1));
        }

        if (sb.Length == 0)
            return "Default";

        if (char.IsDigit(sb[0]))
            sb.Insert(0, '_');

        return sb.ToString();
    }

    private static string GetMethodParameterSignature(List<RpcParameterInfo> parameters)
    {
        if (parameters.Count == 0)
            return string.Empty;
        return string.Join(", ", parameters.Select(p => $"{p.TypeName} {p.Name}"));
    }

    private static string GetServiceParamName(string ifaceName)
    {
        var baseName = GetServiceTypeName(ifaceName);
        if (baseName.Length == 0)
            return "service";
        return char.ToLowerInvariant(baseName[0]) + baseName.Substring(1);
    }

    private static string GenerateDelegateBindOverload(RpcServiceInfo svc)
    {
        var sb = new StringBuilder();
        var delegateParameters = svc.Methods
            .Select(m => $"{GetDelegateType(m)} {GetHandlerParameterName(m.Name)}")
            .ToList();

        sb.Append("        public static void Bind(RpcServer server");
        if (delegateParameters.Count > 0)
            sb.Append(", ").Append(string.Join(", ", delegateParameters));

        sb.Append(")\n        {\n");
        sb.Append("            Bind(server, new DelegateImpl(")
            .Append(string.Join(", ", svc.Methods.Select(m => GetHandlerParameterName(m.Name))))
            .Append("));\n");
        sb.Append("        }\n\n");
        sb.Append("        private sealed class DelegateImpl : ").Append(svc.InterfaceName).Append("\n        {\n");

        foreach (var method in svc.Methods)
        {
            sb.Append("            private readonly ")
                .Append(GetDelegateType(method))
                .Append(" ")
                .Append(GetHandlerFieldName(method.Name))
                .Append(";\n");
        }

        if (svc.Methods.Count > 0)
            sb.Append('\n');

        sb.Append("            public DelegateImpl(")
            .Append(string.Join(", ", svc.Methods.Select(m => $"{GetDelegateType(m)} {GetHandlerParameterName(m.Name)}")))
            .Append(")\n            {\n");

        foreach (var method in svc.Methods)
        {
            var handlerParam = GetHandlerParameterName(method.Name);
            var handlerField = GetHandlerFieldName(method.Name);
            sb.Append("                ")
                .Append(handlerField)
                .Append(" = ")
                .Append(handlerParam)
                .Append(" ?? throw new ArgumentNullException(nameof(")
                .Append(handlerParam)
                .Append("));\n");
        }
        sb.Append("            }\n\n");

        foreach (var method in svc.Methods)
        {
            var methodSig = $"{method.Name}({GetMethodParameterSignature(method)})";
            var invokeArgs = string.Join(", ", method.Parameters.Select(p => p.Name));
            sb.Append("            public ")
                .Append(GetInterfaceReturnType(method))
                .Append(" ")
                .Append(methodSig)
                .Append("\n            {\n");
            sb.Append("                return ")
                .Append(GetHandlerFieldName(method.Name))
                .Append("(")
                .Append(invokeArgs)
                .Append(");\n");
            sb.Append("            }\n\n");
        }

        sb.Append("        }\n\n");
        return sb.ToString();
    }

    private static string GenerateCallbackFactoryBindOverload(RpcServiceInfo svc)
    {
        var callbackInterfaceName = svc.CallbackInterfaceName!;
        var callbackProxyTypeName = GetCallbackProxyTypeName(callbackInterfaceName);
        var sb = new StringBuilder();
        sb.Append("        public static void Bind(RpcServer server, Func<")
            .Append(callbackInterfaceName)
            .Append(", ")
            .Append(svc.InterfaceName)
            .Append("> implFactory)\n        {\n");
        sb.Append("            if (implFactory is null) throw new ArgumentNullException(nameof(implFactory));\n");
        sb.Append("            var callback = new ")
            .Append(callbackProxyTypeName)
            .Append("(server);\n");
        sb.Append("            var impl = implFactory(callback) ?? throw new InvalidOperationException(\"Service implementation factory returned null.\");\n");
        sb.Append("            Bind(server, impl);\n");
        sb.Append("        }\n\n");
        return sb.ToString();
    }

    private static string GetMethodParameterSignature(RpcMethodInfo method)
    {
        if (method.Parameters.Count == 0)
            return string.Empty;

        return string.Join(", ", method.Parameters.Select(p => $"{p.TypeName} {p.Name}"));
    }

    private static string GetInterfaceReturnType(RpcMethodInfo method)
    {
        if (method.IsVoid)
            return "ValueTask";

        return $"ValueTask<{method.RetTypeName}>";
    }

    private static string GetDelegateType(RpcMethodInfo method)
    {
        var genericArgs = new List<string>();
        genericArgs.AddRange(method.Parameters.Select(p => p.TypeName));
        genericArgs.Add(GetInterfaceReturnType(method));
        return $"Func<{string.Join(", ", genericArgs)}>";
    }

    private static string GetHandlerParameterName(string methodName)
    {
        return $"{GetCamelCaseName(methodName)}Handler";
    }

    private static string GetHandlerFieldName(string methodName)
    {
        return $"_{GetCamelCaseName(methodName)}Handler";
    }

    private static string GetCamelCaseName(string value)
    {
        if (string.IsNullOrEmpty(value))
            return "method";

        return char.ToLowerInvariant(value[0]) + value.Substring(1);
    }

    private static string GetRequestPayloadType(RpcMethodInfo method)
    {
        if (method.Parameters.Count == 0)
            return "RpcVoid";

        if (method.Parameters.Count == 1)
            return method.Parameters[0].TypeName;

        return $"({string.Join(", ", method.Parameters.Select(p => p.TypeName))})";
    }

    private static string GetRequestPayloadValue(RpcMethodInfo method)
    {
        if (method.Parameters.Count == 0)
            return "default";

        if (method.Parameters.Count == 1)
            return method.Parameters[0].Name;

        return $"({string.Join(", ", method.Parameters.Select(p => p.Name))})";
    }

    private static string GetDeconstructVariableList(int parameterCount)
    {
        return string.Join(", ", Enumerable.Range(1, parameterCount).Select(i => $"arg{i}"));
    }

    private static string GetInvokeArguments(int parameterCount)
    {
        if (parameterCount == 0)
            return string.Empty;

        return string.Join(", ", Enumerable.Range(1, parameterCount).Select(i => $"arg{i}"));
    }

    private static string GetForwardArguments(IReadOnlyList<RpcParameterInfo> parameters, bool includeCt)
    {
        var args = new List<string>();
        if (parameters.Count > 0)
            args.AddRange(parameters.Select(p => p.Name));

        args.Add(includeCt ? "ct" : "CancellationToken.None");
        return string.Join(", ", args);
    }

    private static IReadOnlyList<string> GetContractUsingDirectives(RpcServiceInfo svc)
    {
        return svc.UsingDirectives
            .Distinct(StringComparer.Ordinal)
            .ToList();
    }

    private static IReadOnlyList<string> GetContractUsingDirectives(IEnumerable<RpcServiceInfo> services)
    {
        return services
            .SelectMany(svc => svc.UsingDirectives)
            .Distinct(StringComparer.Ordinal)
            .ToList();
    }

    private static string GetNamespaceFromFullName(string fullName)
    {
        var lastDot = fullName.LastIndexOf('.');
        return lastDot > 0 ? fullName.Substring(0, lastDot) : string.Empty;
    }

    private static string FormatUsingBlock(IEnumerable<string> namespaces)
    {
        var sb = new StringBuilder();
        foreach (var ns in namespaces)
            sb.Append("using ").Append(ns).Append(";\n");
        return sb.ToString();
    }

    private static IReadOnlyList<string> ExcludeUsingDirectives(IEnumerable<string> usingDirectives, params string[] excluded)
    {
        var excludedSet = new HashSet<string>(excluded, StringComparer.Ordinal);
        return usingDirectives
            .Where(directive => !excludedSet.Contains(directive))
            .ToList();
    }

    private static string GetDefaultServerNamespace(List<RpcServiceInfo> services)
    {
        var first = services.FirstOrDefault();
        if (first == null)
            return "ULinkRPC.Server.Generated";

        var ns = first.InterfaceFullName;
        var lastDot = ns.LastIndexOf('.');
        var baseNs = lastDot > 0 ? ns.Substring(0, lastDot) : ns;
        if (baseNs.EndsWith(".Contracts", StringComparison.Ordinal))
            baseNs = baseNs.Substring(0, baseNs.Length - ".Contracts".Length);

        if (string.IsNullOrWhiteSpace(baseNs))
            return "ULinkRPC.Server.Generated";

        return $"{baseNs}.Server.Generated";
    }

    private static string GetBinderTypeName(string ifaceName)
    {
        return $"{GetServiceTypeName(ifaceName)}Binder";
    }

    private static string GetClientTypeName(string ifaceName)
    {
        return $"{GetServiceTypeName(ifaceName)}Client";
    }

    private static string GetServiceTypeName(string ifaceName)
    {
        if (ifaceName.Length > 1 && ifaceName[0] == 'I' && char.IsUpper(ifaceName[1]))
            return ifaceName.Substring(1);

        return ifaceName;
    }

    private sealed class RpcServiceInfo
    {
        public string InterfaceName { get; }
        public string InterfaceFullName { get; }
        public int ServiceId { get; }
        public List<RpcMethodInfo> Methods { get; }
        public List<string> UsingDirectives { get; }
        public string? CallbackInterfaceName { get; set; }
        public List<RpcCallbackMethodInfo> CallbackMethods { get; set; } = new();

        public RpcServiceInfo(
            string interfaceName,
            string interfaceFullName,
            int serviceId,
            List<RpcMethodInfo> methods,
            IReadOnlyList<string> usingDirectives)
        {
            InterfaceName = interfaceName;
            InterfaceFullName = interfaceFullName;
            ServiceId = serviceId;
            Methods = methods;
            UsingDirectives = BuildUsingDirectives(interfaceFullName, usingDirectives);
        }

        public void AddUsingDirectives(IEnumerable<string> usingDirectives)
        {
            foreach (var directive in usingDirectives)
            {
                if (!UsingDirectives.Contains(directive, StringComparer.Ordinal))
                    UsingDirectives.Add(directive);
            }
        }

        public bool HasCallback => !string.IsNullOrEmpty(CallbackInterfaceName) && CallbackMethods.Count > 0;

        private static List<string> BuildUsingDirectives(string interfaceFullName, IEnumerable<string> usingDirectives)
        {
            var allUsings = new List<string>();
            foreach (var directive in usingDirectives)
            {
                if (!allUsings.Contains(directive, StringComparer.Ordinal))
                    allUsings.Add(directive);
            }

            var contractNamespace = GetNamespaceFromFullName(interfaceFullName);
            if (!string.IsNullOrWhiteSpace(contractNamespace) &&
                !allUsings.Contains(contractNamespace, StringComparer.Ordinal))
            {
                allUsings.Add(contractNamespace);
            }

            return allUsings;
        }
    }

    private sealed class SourceFileInfo
    {
        public string Text { get; }
        public IReadOnlyList<string> UsingDirectives { get; }

        public SourceFileInfo(string text, IReadOnlyList<string> usingDirectives)
        {
            Text = text;
            UsingDirectives = usingDirectives;
        }
    }

    private sealed class RpcMethodInfo
    {
        public string Name { get; }
        public int MethodId { get; }
        public List<RpcParameterInfo> Parameters { get; }
        public string? RetTypeName { get; }
        public bool IsVoid { get; }

        public RpcMethodInfo(string name, int methodId, List<RpcParameterInfo> parameters, string? retTypeName, bool isVoid)
        {
            Name = name;
            MethodId = methodId;
            Parameters = parameters;
            RetTypeName = retTypeName;
            IsVoid = isVoid;
        }
    }

    private sealed class RpcCallbackMethodInfo
    {
        public string Name { get; }
        public int MethodId { get; }
        public List<RpcParameterInfo> Parameters { get; }

        public RpcCallbackMethodInfo(string name, int methodId, List<RpcParameterInfo> parameters)
        {
            Name = name;
            MethodId = methodId;
            Parameters = parameters;
        }
    }

    private sealed class RpcParameterInfo
    {
        public string TypeName { get; }
        public string Name { get; }

        public RpcParameterInfo(string typeName, string name)
        {
            TypeName = typeName;
            Name = name;
        }
    }

    private sealed class FacadeGroupInfo
    {
        public string GroupName { get; }
        public List<FacadeMemberInfo> Members { get; }

        public FacadeGroupInfo(string groupName, List<FacadeMemberInfo> members)
        {
            GroupName = groupName;
            Members = members;
        }
    }

    private sealed class FacadeMemberInfo
    {
        public RpcServiceInfo Service { get; }
        public string PropertyName { get; }

        public FacadeMemberInfo(RpcServiceInfo service, string propertyName)
        {
            Service = service;
            PropertyName = propertyName;
        }
    }

    private static bool IsUnityProject(string path)
    {
        return Directory.Exists(Path.Combine(path, "Assets")) && Directory.Exists(Path.Combine(path, "Packages"));
    }

    private static string DeriveNamespaceFromOutputPath(string outputPath)
    {
        if (string.IsNullOrWhiteSpace(outputPath))
            return DefaultUnityRuntimeNamespace;

        var fullPath = Path.GetFullPath(outputPath);
        var segments = fullPath
            .Split(new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar }, StringSplitOptions.RemoveEmptyEntries)
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
                    current = current.Substring(previous.Length);
                }
            }

            var identifier = ToNamespaceIdentifier(current);
            if (!string.IsNullOrWhiteSpace(identifier))
                normalizedSegments.Add(identifier);
        }

        if (normalizedSegments.Count == 0)
            return DefaultUnityRuntimeNamespace;

        return string.Join('.', normalizedSegments);
    }

    private static string ToNamespaceIdentifier(string segment)
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

    private static string? FindUnityProjectRoot(string startPath)
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

    private sealed record RawOptions(
        string ContractsPath,
        string OutputPath,
        string UnityNamespace,
        string ServerOutputPath,
        string ServerNamespace,
        OutputMode Mode)
    {
        public static RawOptions Empty { get; } = new(
            string.Empty,
            string.Empty,
            string.Empty,
            string.Empty,
            string.Empty,
            OutputMode.Unknown);
    }

    private sealed record ResolvedOptions(
        string ContractsPath,
        string OutputPath,
        string UnityNamespace,
        string ServerOutputPath,
        string ServerNamespace,
        OutputMode Mode)
    {
        public static ResolvedOptions Empty { get; } = new(
            string.Empty,
            string.Empty,
            string.Empty,
            string.Empty,
            string.Empty,
            OutputMode.Unknown);
    }

    private enum OutputMode
    {
        Unknown,
        Unity,
        Server
    }
}
