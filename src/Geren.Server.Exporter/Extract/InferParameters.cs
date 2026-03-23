namespace Geren.Server.Exporter.Extract;

internal static class InferParameters {
    internal static ImmutableArray<ParamSpec> Run(
        Compilation compilation,
        IMethodSymbol handlerMethod,
        HashSet<string> routeParameterNames,
        ImmutableArray<string> httpMethods) {

        var allowBody = httpMethods.Any(static m => m is "POST" or "PUT" or "PATCH");
        var bodyAssigned = false;
        var efDbContext = compilation.GetTypeByMetadataName("Microsoft.EntityFrameworkCore.DbContext");

        var builder = ImmutableArray.CreateBuilder<ParamSpec>(handlerMethod.Parameters.Length);
        foreach (var parameter in handlerMethod.Parameters) {
            if (IsServicesOrInfrastructureParameter(parameter, efDbContext))
                continue;

            string source = InferParameterSource(parameter, routeParameterNames, allowBody, ref bodyAssigned);
            builder.Add(new(
                Name: parameter.Name,
                Type: parameter.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                Source: source));
        }

        return builder.ToImmutable();
    }

    private static string InferParameterSource(
        IParameterSymbol parameter,
        HashSet<string> routeParameterNames,
        bool allowBody,
        ref bool bodyAssigned) {

        if (HasFromRouteAttribute(parameter))
            return "route";

        if (routeParameterNames.Contains(parameter.Name))
            return "route";

        if (allowBody && !bodyAssigned && !IsSimpleType(parameter.Type)) {
            bodyAssigned = true;
            return "body";
        }

        return "query";
    }

    private static bool HasFromRouteAttribute(IParameterSymbol parameter) {
        foreach (var attribute in parameter.GetAttributes()) {
            var cls = attribute.AttributeClass;
            if (cls is null)
                continue;

            var metadataName = cls.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            if (metadataName == "global::Microsoft.AspNetCore.Mvc.FromRouteAttribute"
                || metadataName == "global::Microsoft.AspNetCore.Http.Metadata.FromRouteAttribute") {
                return true;
            }
        }

        return false;
    }

    private static bool IsServicesOrInfrastructureParameter(IParameterSymbol parameter, INamedTypeSymbol? efDbContext) {
        if (HasFromServicesAttribute(parameter))
            return true;

        return IsInfrastructureType(parameter.Type, efDbContext);
    }

    private static bool HasFromServicesAttribute(IParameterSymbol parameter) {
        foreach (var attribute in parameter.GetAttributes()) {
            var cls = attribute.AttributeClass;
            if (cls is null)
                continue;

            var metadataName = cls.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            if (metadataName == "global::Microsoft.AspNetCore.Mvc.FromServicesAttribute"
                || metadataName == "global::Microsoft.AspNetCore.Http.Metadata.FromServicesAttribute"
                || metadataName == "global::Microsoft.AspNetCore.Http.FromServicesAttribute") {
                return true;
            }
        }

        return false;
    }

    private static bool IsInfrastructureType(ITypeSymbol type, INamedTypeSymbol? efDbContext) {
        type = UnwrapNullable(type);

        if (type is IArrayTypeSymbol array)
            type = UnwrapNullable(array.ElementType);

        if (type is not INamedTypeSymbol named)
            return false;

        // Common binding-only parameters in Minimal APIs: not part of a client contract.
        if (named.Name == "CancellationToken" && named.ContainingNamespace?.ToDisplayString() == "System.Threading")
            return true;

        if (named.Name == "ClaimsPrincipal" && named.ContainingNamespace?.ToDisplayString() == "System.Security.Claims")
            return true;

        if (named.ContainingNamespace?.ToDisplayString() == "Microsoft.AspNetCore.Http") {
            if (named.Name is "HttpContext" or "HttpRequest" or "HttpResponse")
                return true;
        }

        // ILogger / ILogger<T>
        if (named.ContainingNamespace?.ToDisplayString() == "Microsoft.Extensions.Logging"
            && named.Name == "ILogger") {
            return true;
        }

        // Treat most Microsoft.Extensions.* as DI infrastructure by default.
        var ns = named.ContainingNamespace?.ToDisplayString() ?? string.Empty;
        if (ns.StartsWith("Microsoft.Extensions.", StringComparison.Ordinal)
            && !ns.StartsWith("Microsoft.Extensions.Primitives", StringComparison.Ordinal))
            return true;

        // DbContext or derived (best-effort; no hard dependency).
        if (named.Name.EndsWith("DbContext", StringComparison.Ordinal))
            return true;

        if (efDbContext is not null) {
            for (INamedTypeSymbol? cur = named; cur is not null; cur = cur.BaseType) {
                if (SymbolEqualityComparer.Default.Equals(cur, efDbContext))
                    return true;
            }
        }

        return false;
    }

    private static bool IsSimpleType(ITypeSymbol type) {
        type = UnwrapNullable(type);

        if (type is IArrayTypeSymbol array)
            type = UnwrapNullable(array.ElementType);

        if (type.TypeKind == TypeKind.Enum)
            return true;

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

    private static ITypeSymbol UnwrapNullable(ITypeSymbol type) {
        if (type is INamedTypeSymbol named
            && named.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T
            && named.TypeArguments.Length == 1) {
            return named.TypeArguments[0];
        }

        return type;
    }
}
