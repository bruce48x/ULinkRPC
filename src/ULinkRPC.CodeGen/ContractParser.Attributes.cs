using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ULinkRPC.CodeGen;

internal static partial class ContractParser
{
    private static bool TryGetAttributeTypeValue(
        SyntaxList<AttributeListSyntax> attributeLists,
        SemanticModel semanticModel,
        string attributeBaseName,
        out string? typeName,
        out string? fullTypeName)
    {
        foreach (var attribute in attributeLists.SelectMany(list => list.Attributes))
        {
            if (!IsMatchingAttribute(attribute, semanticModel, attributeBaseName))
                continue;

            if (TryExtractAttributeTypeArgument(attribute, semanticModel, out typeName, out fullTypeName))
                return true;
        }

        typeName = null;
        fullTypeName = null;
        return false;
    }

    private static bool TryGetAttributeIntValue(
        SyntaxList<AttributeListSyntax> attributeLists,
        SemanticModel semanticModel,
        string attributeBaseName,
        out int value)
    {
        foreach (var attribute in attributeLists.SelectMany(list => list.Attributes))
        {
            if (!IsMatchingAttribute(attribute, semanticModel, attributeBaseName))
                continue;

            if (TryExtractAttributeIntArgument(attribute, semanticModel, out var intValue))
            {
                value = intValue;
                return true;
            }
        }

        value = default;
        return false;
    }

    private static bool IsMatchingAttribute(
        AttributeSyntax attribute, SemanticModel semanticModel, string attributeBaseName)
    {
        foreach (var symbol in GetAttributeCandidateSymbols(attribute, semanticModel))
        {
            var containingType = symbol.ContainingType;
            if (containingType == null) continue;

            var typeName = containingType.Name;
            if (string.Equals(typeName, attributeBaseName, StringComparison.Ordinal) ||
                string.Equals(typeName, $"{attributeBaseName}Attribute", StringComparison.Ordinal))
                return true;
        }

        return IsAttributeName(attribute.Name, attributeBaseName);
    }

    private static IEnumerable<IMethodSymbol> GetAttributeCandidateSymbols(
        AttributeSyntax attribute, SemanticModel semanticModel)
    {
        var symbolInfo = semanticModel.GetSymbolInfo(attribute);
        if (symbolInfo.Symbol is IMethodSymbol methodSymbol)
            yield return methodSymbol;

        foreach (var candidate in symbolInfo.CandidateSymbols.OfType<IMethodSymbol>())
            yield return candidate;
    }

    private static bool TryExtractAttributeIntArgument(
        AttributeSyntax attribute, SemanticModel semanticModel, out int value)
    {
        value = default;
        var arguments = attribute.ArgumentList?.Arguments;
        if (arguments == null || arguments.Value.Count == 0)
            return false;

        foreach (var argument in arguments.Value)
        {
            if (!IsNamedAttributeArgument(argument)) continue;
            if (TryGetIntConstant(argument.Expression, semanticModel, out value))
                return true;
        }

        foreach (var argument in arguments.Value)
        {
            if (IsNamedAttributeArgument(argument)) continue;
            if (TryGetIntConstant(argument.Expression, semanticModel, out value))
                return true;
        }

        return false;
    }

    private static bool TryExtractAttributeTypeArgument(
        AttributeSyntax attribute,
        SemanticModel semanticModel,
        out string? typeName,
        out string? fullTypeName)
    {
        typeName = null;
        fullTypeName = null;
        var arguments = attribute.ArgumentList?.Arguments;
        if (arguments == null || arguments.Value.Count == 0)
            return false;

        foreach (var argument in arguments.Value)
        {
            if (!IsNamedAttributeArgument(argument)) continue;
            if (TryGetTypeReference(argument.Expression, semanticModel, out typeName, out fullTypeName))
                return true;
        }

        foreach (var argument in arguments.Value)
        {
            if (IsNamedAttributeArgument(argument)) continue;
            if (TryGetTypeReference(argument.Expression, semanticModel, out typeName, out fullTypeName))
                return true;
        }

        return false;
    }

    private static bool IsNamedAttributeArgument(AttributeArgumentSyntax argument) =>
        argument.NameColon != null || argument.NameEquals != null;

    private static bool TryGetIntConstant(ExpressionSyntax expression, SemanticModel semanticModel, out int value)
    {
        value = default;
        var constant = semanticModel.GetConstantValue(expression);
        if (!constant.HasValue || constant.Value == null)
            return false;

        switch (constant.Value)
        {
            case int intValue:
                value = intValue;
                return true;
            case long longValue when longValue is >= int.MinValue and <= int.MaxValue:
                value = (int)longValue;
                return true;
            case short shortValue:
                value = shortValue;
                return true;
            case byte byteValue:
                value = byteValue;
                return true;
            default:
                return false;
        }
    }

    private static bool TryGetTypeReference(
        ExpressionSyntax expression,
        SemanticModel semanticModel,
        out string? typeName,
        out string? fullTypeName)
    {
        typeName = null;
        fullTypeName = null;

        if (expression is not TypeOfExpressionSyntax typeOfExpression)
            return false;

        if (semanticModel.GetTypeInfo(typeOfExpression.Type).Type is not INamedTypeSymbol typeSymbol)
            return false;

        typeName = typeSymbol.Name;
        fullTypeName = GetTypeFullName(typeSymbol);
        return true;
    }

    private static bool IsAttributeName(NameSyntax attributeName, string attributeBaseName)
    {
        var simpleName = GetRightMostName(attributeName);
        return string.Equals(simpleName, attributeBaseName, StringComparison.Ordinal) ||
               string.Equals(simpleName, $"{attributeBaseName}Attribute", StringComparison.Ordinal);
    }

    private static string GetRightMostName(NameSyntax nameSyntax) => nameSyntax switch
    {
        IdentifierNameSyntax id => id.Identifier.ValueText,
        GenericNameSyntax generic => generic.Identifier.ValueText,
        QualifiedNameSyntax qualified => GetRightMostName(qualified.Right),
        AliasQualifiedNameSyntax aliasQualified => GetRightMostName(aliasQualified.Name),
        _ => nameSyntax.ToString()
    };
}
