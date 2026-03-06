using System.Text;
using System.Text.RegularExpressions;

namespace ULinkRPC.CodeGen;

internal static partial class Program
{
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
        {
            serviceTypeName = serviceTypeName.Substring(0, serviceTypeName.Length - "Service".Length);
        }

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
}
