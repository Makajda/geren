using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Geren.Map;

internal class SchemaTypeName(Compilation _compilation, ImmutableArray<Diagnostic>.Builder _diagnostics) {
    private readonly Dictionary<string, string> _resolvedSchemaTypeCache = new(StringComparer.Ordinal);
    private readonly HashSet<string> _reportedUnresolvedSchemaTypes = new(StringComparer.Ordinal);

    internal string Resolve(IOpenApiSchema? schema) {
        const string defaultType = "string";
        if (schema is null)
            return defaultType;

        bool hasExtensions = schema.Extensions is not null;
        if (hasExtensions && schema.Extensions!.TryGetValue("x-generic", out IOpenApiExtension nodeGeneric)) {
            if (nodeGeneric is JsonNodeExtension node)
                return ResolveGenericTypeByCompile(node.Node.GetValue<string>());
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
        if (_resolvedSchemaTypeCache.TryGetValue(simpleType, out string cached))
            return cached;

        string result;
        INamedTypeSymbol? symbol = _compilation.GetTypeByMetadataName(simpleType);
        if (symbol is null) {
            result = "object";
            if (_reportedUnresolvedSchemaTypes.Add(simpleType))
                _diagnostics.Add(Diagnostic.Create(Givenn.UnresolvedSchemaReference, Location.None, simpleType, simpleType));
        }
        else
            result = symbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

        _resolvedSchemaTypeCache[simpleType] = result;
        return result;
    }

    private string ResolveGenericTypeByCompile(string genericType) {
        if (_resolvedSchemaTypeCache.TryGetValue(genericType, out string cached))
            return cached;

        var source = $$"""class __TypeProbe {{{genericType}} m;}""";
        var parseOptions = (CSharpParseOptions)_compilation.SyntaxTrees.First().Options;
        var tree = CSharpSyntaxTree.ParseText(source, parseOptions, path: "__Geren_TypeProbe.g.cs");
        var newCompilation = _compilation.AddSyntaxTrees(tree);
        var model = newCompilation.GetSemanticModel(tree);

        var root = tree.GetRoot();
        var field = root.DescendantNodes().OfType<FieldDeclarationSyntax>().First();
        var typeSyntax = field.Declaration.Type;
        var typeInfo = model.GetTypeInfo(typeSyntax);
        var type = typeInfo.Type;
        string result;
        if (type is null || type is IErrorTypeSymbol || tree.GetDiagnostics().Count() > 0) {// semantic || syntax
            _diagnostics.Add(Diagnostic.Create(//todo
                Givenn.AmbiguousSchemaReference, Location.None, "", genericType, "cannot be resolved by the compiler"));
            result = "object";
        }
        else
            result = genericType;// type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

        _resolvedSchemaTypeCache[genericType] = result;
        return result;
    }

    // Further only ResolveReferencedSchemaType with GetSymbolsWithName
    private string ResolveReferencedSchemaType(string referenceId) {
        string simpleType = Givenn.ToLetterOrDigitName(referenceId);
        if (_resolvedSchemaTypeCache.TryGetValue(simpleType, out string cached))
            return cached;

        SortedSet<string> candidateNames = new(StringComparer.Ordinal);

        foreach (var symbol in _compilation.GetSymbolsWithName(simpleType, SymbolFilter.Type)
            .OfType<INamedTypeSymbol>()
            .OrderBy(static s => s.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat), StringComparer.Ordinal)) {
            candidateNames.Add(symbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat));
        }

        CollectTypeNamesByName(_compilation.Assembly.GlobalNamespace, simpleType, candidateNames);
        foreach (var assembly in _compilation.SourceModule.ReferencedAssemblySymbols)
            CollectTypeNamesByName(assembly.GlobalNamespace, simpleType, candidateNames);

        string result;
        if (candidateNames.Count > 1) {
            result = "object";
            _diagnostics.Add(Diagnostic.Create(
                Givenn.AmbiguousSchemaReference, Location.None, referenceId, simpleType, FormatAmbiguousMatches(candidateNames)));
        }
        else if (candidateNames.Count == 0) {
            result = "object";
            if (_reportedUnresolvedSchemaTypes.Add(simpleType))
                _diagnostics.Add(Diagnostic.Create(Givenn.UnresolvedSchemaReference, Location.None, referenceId, simpleType));
        }
        else
            result = candidateNames.Min;

        _resolvedSchemaTypeCache[simpleType] = result;
        return result;
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
