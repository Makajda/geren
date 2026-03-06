namespace Geren.Map;

internal class SchemaTypeName(Compilation compilation, ImmutableArray<Diagnostic>.Builder _diagnostics) {
    private readonly Dictionary<string, string?> _resolvedSchemaTypeCache = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _ambiguousSchemaTypeCache = new(StringComparer.Ordinal);
    private readonly HashSet<string> _reportedUnresolvedSchemaTypes = new(StringComparer.Ordinal);
    private readonly HashSet<string> _reportedAmbiguousSchemaTypes = new(StringComparer.Ordinal);

    internal bool HasFatalEndpointError { get; private set; }
    internal bool Clean() => HasFatalEndpointError = false;

    internal string Resolve(IOpenApiSchema? schema) {
        const string defaultType = "string";
        if (schema is null)
            return defaultType;

        if (schema is OpenApiSchemaReference schemaReference) {
            if (schema.Extensions is not null && schema.Extensions.TryGetValue("x-dotnet-type", out IOpenApiExtension nodeExtension)) {
                if (nodeExtension is JsonNodeExtension node)
                    return ResolveReferencedSchemaGenericType(node.Node.GetValue<string>());
            }
            else if (schemaReference.Reference.Id is not null)
                return ResolveReferencedSchemaType(schemaReference.Reference.Id);
        }

        if (schema.Format == "int64") return "long";
        if (schema.Format == "int32") return "int";

        if (schema.Type.HasValue) return schema.Type.Value switch {
            JsonSchemaType.Null => "string",
            JsonSchemaType.Array => $"System.Collections.Generic.IReadOnlyList<{Resolve(schema.Items)}>",
            JsonSchemaType.Boolean => "bool",
            JsonSchemaType.Integer => "int",
            JsonSchemaType.Number => "double",
            JsonSchemaType.String => "string",
            JsonSchemaType.Object => "object",
            _ => defaultType
        };
        return defaultType;
    }

    private string ResolveReferencedSchemaGenericType(string dotnet_type) {
        return "object";//todo
    }

    private string ResolveReferencedSchemaType(string referenceId) {
        var simpleTypeName = Givenn.ToLetterOrDigitName(referenceId);

        var qualifiedTypeName = ResolveKnownCompilationTypeName(simpleTypeName, out var ambiguousMatches);
        if (qualifiedTypeName is not null)
            return qualifiedTypeName;

        if (ambiguousMatches is not null) {
            HasFatalEndpointError = true;
            if (_reportedAmbiguousSchemaTypes.Add(simpleTypeName))
                _diagnostics.Add(Diagnostic.Create(Givenn.AmbiguousSchemaReference, Location.None, referenceId, simpleTypeName, ambiguousMatches));

            return "object";
        }

        if (_reportedUnresolvedSchemaTypes.Add(simpleTypeName))
            _diagnostics.Add(Diagnostic.Create(Givenn.UnresolvedSchemaReference, Location.None, referenceId, simpleTypeName));

        return "object";
    }

    private string? ResolveKnownCompilationTypeName(string typeName, out string? ambiguousMatches) {
        ambiguousMatches = null;
        if (_ambiguousSchemaTypeCache.TryGetValue(typeName, out var cachedAmbiguous)) {
            ambiguousMatches = cachedAmbiguous;
            return null;
        }

        if (_resolvedSchemaTypeCache.TryGetValue(typeName, out var cached))
            return cached;

        SortedSet<string> candidateNames = new(StringComparer.Ordinal);

        foreach (var symbol in compilation.GetSymbolsWithName(typeName, SymbolFilter.Type)
            .OfType<INamedTypeSymbol>()
            .OrderBy(static s => s.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat), StringComparer.Ordinal)) {
            candidateNames.Add(symbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat));
        }

        CollectTypeNamesByName(compilation.Assembly.GlobalNamespace, typeName, candidateNames);
        foreach (var assembly in compilation.SourceModule.ReferencedAssemblySymbols)
            CollectTypeNamesByName(assembly.GlobalNamespace, typeName, candidateNames);

        if (candidateNames.Count > 1) {
            ambiguousMatches = FormatAmbiguousMatches(candidateNames);
            _ambiguousSchemaTypeCache[typeName] = ambiguousMatches;
            return null;
        }

        var resolved = candidateNames.Count == 1
            ? candidateNames.Min
            : null;
        _resolvedSchemaTypeCache[typeName] = resolved;
        return resolved;
    }

    private static string FormatAmbiguousMatches(SortedSet<string> candidateNames) {
        const int previewLimit = 5;
        var preview = candidateNames.Take(previewLimit).ToArray();
        if (candidateNames.Count <= preview.Length)
            return string.Join(", ", preview);

        return string.Join(", ", preview) + ", ... (+" + (candidateNames.Count - preview.Length) + " more)";
    }

    private static void CollectTypeNamesByName(INamespaceSymbol @namespace, string typeName, SortedSet<string> output) {
        foreach (var member in @namespace.GetMembers()) {
            if (member is INamespaceSymbol childNamespace) {
                CollectTypeNamesByName(childNamespace, typeName, output);
                continue;
            }

            if (member is INamedTypeSymbol typeSymbol)
                CollectTypeNamesByName(typeSymbol, typeName, output);
        }
    }

    private static void CollectTypeNamesByName(INamedTypeSymbol typeSymbol, string typeName, SortedSet<string> output) {
        if (typeSymbol.Name == typeName)
            output.Add(typeSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat));

        foreach (var nested in typeSymbol.GetTypeMembers())
            CollectTypeNamesByName(nested, typeName, output);
    }
}
