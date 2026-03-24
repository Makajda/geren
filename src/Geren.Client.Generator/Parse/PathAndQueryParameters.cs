using Microsoft.CodeAnalysis.CSharp;

namespace Geren.Client.Generator.Parse;

internal static class PathAndQueryParameters {
    internal static (ImmutableArray<Purparam> Params, ImmutableArray<ParamSpec> Queries) Split(
        string rawPath,
        ImmutableArray<IOpenApiParameter> parameters,
        ImmutableArray<Diagnostic>.Builder diagnostics) {
        var pathParams = ImmutableArray.CreateBuilder<Purparam>();
        var queryParams = ImmutableArray.CreateBuilder<ParamSpec>();
        var usedParamIdentifiers = new HashSet<string>(StringComparer.Ordinal);

        foreach (var parameter in parameters) {
            if (parameter is null || parameter.In is null || parameter.Name is null) {
                diagnostics.Add(Diagnostic.Create(Dide.MissingParamLocation, Location.None, parameter?.Name ?? "<noname>", rawPath));
                continue;
            }

            string inValue = parameter.In.Value.ToString().ToLowerInvariant();
            if (inValue == "path") {
                string identifier = ToParameterIdentifier(parameter.Name, usedParamIdentifiers);
                PurposeType paramType = SchemaToPurpose.Convert(parameter.Schema);
                pathParams.Add(new(parameter.Name, identifier, paramType));

                continue;
            }

            if (inValue == "query") {
                string identifier = ToParameterIdentifier(parameter.Name, usedParamIdentifiers);
                var (type, _) = SchemaToPurpose.Convert(parameter.Schema);
                type = parameter.Required || type.EndsWith("?", StringComparison.Ordinal) ? type : type + "?";
                if (IsSupportedQueryType(type))
                    queryParams.Add(new(parameter.Name, identifier, type));
                else
                    diagnostics.Add(Diagnostic.Create(Dide.UnsupportedQueryType, Location.None, parameter.Name, rawPath, type));

                continue;
            }

            diagnostics.Add(Diagnostic.Create(Dide.UnsupportedParamLocation, Location.None, parameter.Name, inValue));
        }

        return (pathParams.ToImmutable(), queryParams.ToImmutable());
    }

    private static string ToParameterIdentifier(string name, HashSet<string> usedIdentifiers) {
        string baseIdentifier = Given.ToLetterOrDigitName(name);
        if (baseIdentifier.Length == 0 || baseIdentifier == "_")
            baseIdentifier = "p";

        baseIdentifier = char.ToLowerInvariant(baseIdentifier[0]) + baseIdentifier.Substring(1);
        if (SyntaxFacts.GetKeywordKind(baseIdentifier) != SyntaxKind.None
            || SyntaxFacts.GetContextualKeywordKind(baseIdentifier) != SyntaxKind.None)
            baseIdentifier += "_";

        string candidate = baseIdentifier;
        var index = 2;
        while (!usedIdentifiers.Add(candidate)) {
            candidate = baseIdentifier + "_" + index;
            index++;
        }

        return candidate;
    }

    private static bool IsSupportedQueryType(string typeName) {
        string normalizedType = TrimNullable(typeName);
        if (IsSupportedQueryScalarType(normalizedType))
            return true;

        return TryGetCollectionElementType(normalizedType, out string elementType)
            && IsSupportedQueryScalarType(TrimNullable(elementType));
    }

    private static string TrimNullable(string typeName)
        => typeName.EndsWith("?", StringComparison.Ordinal) ? typeName.Substring(0, typeName.Length - 1) : typeName;

    private static bool IsSupportedQueryScalarType(string typeName)
        => typeName == "string" || typeName == "int" || typeName == "long" || typeName == "bool" || typeName == "double";

    private static bool TryGetCollectionElementType(string typeName, out string elementType) {
        const string prefix = "System.Collections.Generic.IReadOnlyList<";
        if (typeName.StartsWith(prefix, StringComparison.Ordinal) && typeName.EndsWith(">", StringComparison.Ordinal)) {
            elementType = typeName.Substring(prefix.Length, typeName.Length - prefix.Length - 1);
            return true;
        }

        elementType = string.Empty;
        return false;
    }
}
