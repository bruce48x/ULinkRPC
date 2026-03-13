using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ULinkRPC.CodeGen;

internal static partial class ContractParser
{
    private static bool TryParseRpcReturnType(
        IMethodSymbol methodSymbol,
        TypeSyntax returnTypeSyntax,
        out string? returnType,
        out bool isVoid)
    {
        returnType = null;
        isVoid = false;

        if (methodSymbol.ReturnType is not INamedTypeSymbol returnTypeSymbol)
            return false;

        var returnTypeNamespace = returnTypeSymbol.ContainingNamespace?.ToDisplayString();
        if (!string.Equals(returnTypeSymbol.Name, ValueTaskTypeName, StringComparison.Ordinal) ||
            !string.Equals(returnTypeNamespace, ValueTaskNamespace, StringComparison.Ordinal))
            return false;

        if (returnTypeSymbol.TypeArguments.Length == 0)
        {
            isVoid = true;
            return true;
        }

        if (returnTypeSymbol.TypeArguments.Length != 1)
            return false;

        returnType = TryGetValueTaskGenericType(returnTypeSyntax);
        return !string.IsNullOrWhiteSpace(returnType);
    }

    private static string GetTypeFullName(INamedTypeSymbol symbol) =>
        symbol.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat);

    private static void AddTypeNamespaces(ISet<string> target, ITypeSymbol? typeSymbol)
    {
        if (typeSymbol == null) return;

        switch (typeSymbol)
        {
            case IArrayTypeSymbol arrayType:
                AddTypeNamespaces(target, arrayType.ElementType);
                return;
            case IPointerTypeSymbol pointerType:
                AddTypeNamespaces(target, pointerType.PointedAtType);
                return;
            case INamedTypeSymbol namedType:
                AddNamespace(target, namedType.ContainingNamespace);
                foreach (var typeArgument in namedType.TypeArguments)
                    AddTypeNamespaces(target, typeArgument);
                if (namedType.IsTupleType)
                    foreach (var tupleElement in namedType.TupleElements)
                        AddTypeNamespaces(target, tupleElement.Type);
                return;
        }
    }

    private static void AddNamespace(ISet<string> target, INamespaceSymbol? namespaceSymbol)
    {
        if (namespaceSymbol == null || namespaceSymbol.IsGlobalNamespace) return;
        var ns = namespaceSymbol.ToDisplayString();
        if (!string.IsNullOrWhiteSpace(ns))
            target.Add(ns);
    }

    private static string? TryGetValueTaskGenericType(TypeSyntax returnType)
    {
        if (returnType is GenericNameSyntax generic &&
            string.Equals(generic.Identifier.ValueText, ValueTaskTypeName, StringComparison.Ordinal) &&
            generic.TypeArgumentList.Arguments.Count == 1)
            return generic.TypeArgumentList.Arguments[0].ToString();

        if (returnType is QualifiedNameSyntax qualified)
            return TryGetValueTaskGenericType(qualified.Right);

        if (returnType is AliasQualifiedNameSyntax aliasQualified)
            return TryGetValueTaskGenericType(aliasQualified.Name);

        return null;
    }
}
