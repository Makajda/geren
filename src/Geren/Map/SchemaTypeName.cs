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

        if (schema is OpenApiSchemaReference schemaReference)
            if (TryResolveReferencedSchemaType(schemaReference, out var referenceType))
                return referenceType;


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

    private bool TryResolveReferencedSchemaType(OpenApiSchemaReference schema, out string typeName) {
        typeName = string.Empty;
        string? genericName = default!;
        if (schema.Extensions is not null && schema.Extensions.TryGetValue("x-dotnet-type", out IOpenApiExtension nodeExtension))
            if (nodeExtension is JsonNodeExtension node)
                genericName = node.Node.GetValue<string>();

        string source, className = default!;
        if (genericName is null) {
            if (schema.Reference.Id is null)
                return false;

            source = schema.Reference.Id;
            className = Givenn.ToLetterOrDigitName(source);
        }
        else {
            source = className = genericName;
        }

        var qualifiedTypeName = ResolveKnownCompilationTypeName(className, out var ambiguousMatches);
        if (qualifiedTypeName is not null) {
            typeName = qualifiedTypeName;
            return true;
        }

        if (ambiguousMatches is not null) {
            HasFatalEndpointError = true;
            if (_reportedAmbiguousSchemaTypes.Add(className))
                _diagnostics.Add(Diagnostic.Create(Givenn.AmbiguousSchemaReference, Location.None, source, className, ambiguousMatches));

            typeName = "object";
            return true;
        }

        if (_reportedUnresolvedSchemaTypes.Add(className))
            _diagnostics.Add(Diagnostic.Create(Givenn.UnresolvedSchemaReference, Location.None, source, className));

        typeName = "object";
        return true;
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
