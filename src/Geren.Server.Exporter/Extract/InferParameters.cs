namespace Geren.Server.Exporter.Extract;

internal static class InferParameters {
    internal static (
        PurposeType? bodyType,
        MediaTypes bodyMedia,
        ImmutableArray<Purparam> @params,
        ImmutableArray<Maparam> queries)
        Get(
        IMethodSymbol handlerMethod,
        HashSet<string> routeParameterNames,
        string httpMethod,
        string[] excludeTypes) {

        bool bodyAssigned = false;
        var allowBody = httpMethod is "POST" or "PUT" or "PATCH";
        PurposeType? bodyType = null;
        MediaTypes bodyMedia = MediaTypes.None;
        var @params = ImmutableArray.CreateBuilder<Purparam>(handlerMethod.Parameters.Length);
        var queries = ImmutableArray.CreateBuilder<Maparam>(handlerMethod.Parameters.Length);

        foreach (var parameter in handlerMethod.Parameters) {
            if (IsServicesOrInfrastructureParameter(parameter, excludeTypes))
                continue;

            if (routeParameterNames.Contains(parameter.Name) || HasFromRouteAttribute(parameter))
                @params.Add(new(parameter.Name, parameter.Name, Given.GetPurposeType(parameter.Type)));
            else if (IsSimpleType(parameter.Type)) {
                var format = new SymbolDisplayFormat(
                    typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameOnly,
                    miscellaneousOptions: SymbolDisplayMiscellaneousOptions.UseSpecialTypes); // Int32 to int without namespace, for readability in query parameters
                queries.Add(new(parameter.Name, parameter.Name, parameter.Type.ToDisplayString(format)));// SymbolDisplayFormat.MinimallyQualifiedFormat)));
            }
            else if (allowBody && !bodyAssigned) {// body is first complex parameter, if allowed by HTTP method
                bodyType = Given.GetPurposeType(parameter.Type);
                bodyMedia = MediaTypes.Application_Json;
                bodyAssigned = true;
            }
        }

        return (bodyType, bodyMedia, @params.ToImmutable(), queries.ToImmutable());
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
