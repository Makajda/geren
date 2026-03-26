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

    internal static Byres GetByres(ITypeSymbol type) =>
        type.TypeKind == TypeKind.Array || (type is INamedTypeSymbol named && named.IsGenericType)
            ? Byres.Compile
            : Byres.Metadata;

    internal static (string, Byres) GetNameAndByres(ITypeSymbol type) {
        string name = type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        return type.TypeKind == TypeKind.Array || (type is INamedTypeSymbol named && named.IsGenericType)
            ? (name, Byres.Compile)
            : (name.StartsWith("global::")
                ? name[8..]
                : name,
                Byres.Metadata);
    }

    internal static bool IsSimpleType(ITypeSymbol type) {
        type = UnwrapNullable(type);
        return type.SpecialType switch {
            SpecialType.System_Boolean or
            SpecialType.System_Byte or
            SpecialType.System_SByte or
            SpecialType.System_Int16 or
            SpecialType.System_UInt16 or
            SpecialType.System_Int32 or
            SpecialType.System_UInt32 or
            SpecialType.System_Int64 or
            SpecialType.System_UInt64 or
            SpecialType.System_Char or
            SpecialType.System_Decimal or
            SpecialType.System_Double or
            SpecialType.System_Single or
            SpecialType.System_String => true,
            _ => type.Name switch {
                "Guid" => type.ContainingNamespace?.ToDisplayString() == "System",
                "DateTime" => type.ContainingNamespace?.ToDisplayString() == "System",
                "DateTimeOffset" => type.ContainingNamespace?.ToDisplayString() == "System",
                "TimeSpan" => type.ContainingNamespace?.ToDisplayString() == "System",
                "Uri" => type.ContainingNamespace?.ToDisplayString() == "System",
                _ => false,
            },
        };
    }

    internal static ITypeSymbol UnwrapNullable(ITypeSymbol type) {
        if (type is INamedTypeSymbol named
            && named.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T
            && named.TypeArguments.Length == 1) {
            return named.TypeArguments[0];
        }

        return type;
    }
}
