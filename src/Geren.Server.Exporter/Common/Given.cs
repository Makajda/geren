namespace Geren.Server.Exporter.Common;

internal static class Given {
    internal static readonly SymbolDisplayFormat FullyQualifiedMethodFormat = new(
        globalNamespaceStyle: SymbolDisplayGlobalNamespaceStyle.Included,
        typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces,
        genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters,
        memberOptions:
        SymbolDisplayMemberOptions.IncludeContainingType |
        SymbolDisplayMemberOptions.IncludeParameters |
        SymbolDisplayMemberOptions.IncludeType,
        parameterOptions:
        SymbolDisplayParameterOptions.IncludeType |
        SymbolDisplayParameterOptions.IncludeName,
        miscellaneousOptions:
        SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier |
        SymbolDisplayMiscellaneousOptions.EscapeKeywordIdentifiers);


    internal static bool TryGetConstantString(SemanticModel semanticModel, ExpressionSyntax expression, CancellationToken cancellationToken, out string value) {
        var constant = semanticModel.GetConstantValue(expression, cancellationToken);
        if (constant.HasValue && constant.Value is string s) {
            value = s;
            return true;
        }

        value = string.Empty;
        return false;
    }

    internal static int GetRouteTemplateArgumentIndex(IMethodSymbol methodSymbol) {
        for (var i = 0; i < methodSymbol.Parameters.Length; i++)
            if (methodSymbol.Parameters[i].Type.SpecialType == SpecialType.System_String)
                return i;

        return -1;
    }


    internal static bool IsEndpointRouteBuilderExtension(IMethodSymbol methodSymbol, INamedTypeSymbol endpointRouteBuilder) {
        IMethodSymbol original = methodSymbol.ReducedFrom ?? methodSymbol;
        if (original.IsExtensionMethod && original.Parameters.Length > 0)
            return IsEndpointRouteBuilderType(original.Parameters[0].Type, endpointRouteBuilder);

        return false;
    }

    internal static bool IsEndpointRouteBuilderType(ITypeSymbol type, INamedTypeSymbol endpointRouteBuilder) {
        if (SymbolEqualityComparer.Default.Equals(type, endpointRouteBuilder))
            return true;

        if (type is INamedTypeSymbol named) {
            foreach (var iface in named.AllInterfaces) {
                if (SymbolEqualityComparer.Default.Equals(iface, endpointRouteBuilder))
                    return true;
            }
        }

        if (type is ITypeParameterSymbol typeParameter) {
            foreach (var constraint in typeParameter.ConstraintTypes) {
                if (IsEndpointRouteBuilderType(constraint, endpointRouteBuilder))
                    return true;
            }
        }

        return false;
    }

    internal static PurposeType GetPurposeType(ITypeSymbol type) =>
        new(type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
            type.TypeKind == TypeKind.Array || (type is INamedTypeSymbol named && named.IsGenericType)
            ? Puresolve.Compile
            : Puresolve.Metadata);
}
