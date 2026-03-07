namespace Geren.Map;

internal class SchemaTypeName(Compilation _compilation, ImmutableArray<Diagnostic>.Builder _diagnostics) {
    private readonly Dictionary<string, string?> _resolvedSchemaTypeCache = new(StringComparer.Ordinal);
    private readonly HashSet<string> _ambiguousSchemaTypeCache = new(StringComparer.Ordinal);
    private readonly HashSet<string> _reportedUnresolvedSchemaTypes = new(StringComparer.Ordinal);

    internal string Resolve(IOpenApiSchema? schema) {
        const string defaultType = "string";
        if (schema is null)
            return defaultType;

        bool hasExtensions = schema.Extensions is not null;
        if (hasExtensions && schema.Extensions!.TryGetValue("x-generic", out IOpenApiExtension nodeGeneric)) {
            if (nodeGeneric is JsonNodeExtension node)
                return SchemaTypeGeneric.Resolve(node.Node.GetValue<string>(), _compilation, _diagnostics);
        }
        else if (hasExtensions && schema.Extensions!.TryGetValue("x-type", out IOpenApiExtension nodeExtension)) {
            if (nodeExtension is JsonNodeExtension node)
                return ResolveMetadataSchemaType(node.Node.GetValue<string>());
        }
        else if (schema is OpenApiSchemaReference schemaReference)
            if (schemaReference.Reference.Id is not null)
                return ResolveReferencedSchemaType(schemaReference.Reference.Id);

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

    private string ResolveMetadataSchemaType(string simpleType) {
        INamedTypeSymbol? symbol = _compilation.GetTypeByMetadataName(simpleType);
        if (symbol is not null)
            return symbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

        if (_reportedUnresolvedSchemaTypes.Add(simpleType))
            _diagnostics.Add(Diagnostic.Create(Givenn.UnresolvedSchemaReference, Location.None, simpleType, simpleType));

        return "object";
    }

    // Further only ResolveReferencedSchemaType
    private string ResolveReferencedSchemaType(string referenceId) {
        string simpleType = Givenn.ToLetterOrDigitName(referenceId);
        if (_ambiguousSchemaTypeCache.Contains(simpleType))
            return "object";

        if (_resolvedSchemaTypeCache.TryGetValue(simpleType, out var cached))
            return cached ?? "object";

        SortedSet<string> candidateNames = new(StringComparer.Ordinal);

        foreach (var symbol in _compilation.GetSymbolsWithName(simpleType, SymbolFilter.Type)
            .OfType<INamedTypeSymbol>()
            .OrderBy(static s => s.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat), StringComparer.Ordinal)) {
            candidateNames.Add(symbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat));
        }

        CollectTypeNamesByName(_compilation.Assembly.GlobalNamespace, simpleType, candidateNames);
        foreach (var assembly in _compilation.SourceModule.ReferencedAssemblySymbols)
            CollectTypeNamesByName(assembly.GlobalNamespace, simpleType, candidateNames);

        if (candidateNames.Count > 1) {
            _ambiguousSchemaTypeCache.Add(simpleType);
            _diagnostics.Add(Diagnostic.Create(
                Givenn.AmbiguousSchemaReference, Location.None, referenceId, simpleType, FormatAmbiguousMatches(candidateNames)));
            return "object";
        }

        if (candidateNames.Count == 1) {
            string resolved = candidateNames.Min;
            _resolvedSchemaTypeCache[simpleType] = resolved;
            return resolved;
        }

        if (_reportedUnresolvedSchemaTypes.Add(simpleType))
            _diagnostics.Add(Diagnostic.Create(Givenn.UnresolvedSchemaReference, Location.None, referenceId, simpleType));

        return "object";
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
