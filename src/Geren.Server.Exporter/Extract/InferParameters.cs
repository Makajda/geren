namespace Geren.Server.Exporter.Extract;

internal static class InferParameters {
    internal static ImmutableArray<ParamSpec> Get(
        IMethodSymbol handlerMethod,
        HashSet<string> routeParameterNames,
        ImmutableArray<string> httpMethods,
        string[] excludeTypes) {

        var allowBody = httpMethods.Any(static m => m is "POST" or "PUT" or "PATCH");
        var bodyAssigned = false;

        var builder = ImmutableArray.CreateBuilder<ParamSpec>(handlerMethod.Parameters.Length);
        foreach (var parameter in handlerMethod.Parameters) {
            if (IsServicesOrInfrastructureParameter(parameter, excludeTypes))
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

    private static bool IsServicesOrInfrastructureParameter(IParameterSymbol parameter, string[] excludeTypes) {
        if (HasFromServicesAttribute(parameter))
            return true;

        return IsInfrastructureType(parameter.Type, excludeTypes);
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

    private static bool IsInfrastructureType(ITypeSymbol type, string[] excludeTypes) {
        type = UnwrapNullable(type);

        if (type is IArrayTypeSymbol array)
            type = UnwrapNullable(array.ElementType);

        if (type is not INamedTypeSymbol named)
            return false;

        string fullName = $"{named.ContainingNamespace?.ToDisplayString()}.{named.Name}";
        if (excludeTypes.Contains(fullName))
            return true;

        // Common binding-only parameters in Minimal APIs: not part of a client contract.
        if (fullName switch {
            "System.Threading.CancellationToken"
            or "System.Security.Claims.ClaimsPrincipal"
            or "Microsoft.AspNetCore.Http.HttpContext"
            or "Microsoft.AspNetCore.Http.HttpRequest"
            or "Microsoft.AspNetCore.Http.HttpResponse"
            or "Microsoft.Extensions.Logging.ILogger" => true,
            _ => false
        })
            return true;

        // Treat most Microsoft.Extensions.* as DI infrastructure by default.
        var ns = named.ContainingNamespace?.ToDisplayString() ?? string.Empty;
        if (ns.StartsWith("Microsoft.Extensions.", StringComparison.Ordinal)
            && !ns.StartsWith("Microsoft.Extensions.Primitives", StringComparison.Ordinal))
            return true;

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
