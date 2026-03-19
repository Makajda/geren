using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Geren.Client.Generator.Map;

internal class TypeResolver(
    string _rootFileNamespace,
    Compilation _compilation,
    Dictionary<string, UnresolvedSchemaType> _unresolvedByPlaceholder,
    ImmutableArray<Diagnostic>.Builder _diagnostics) {
    private readonly Dictionary<string, string> _resolvedSchemaTypeCache = new(StringComparer.Ordinal);
    private readonly HashSet<string> _reportedUnresolvedSchemaTypes = new(StringComparer.Ordinal);

    internal string Resolve(PurposeType? schema) {
        if (schema is null)
            return "string";

        var (type, purpose) = schema.Value;

        return purpose switch {
            PurposeTypes.Metadata => ResolveByMetadata(type),
            PurposeTypes.Compile => ResolveByCompile(type),
            PurposeTypes.Reference => ResolveByReference(type),
            _ => type
        };
    }

    private string GetOrCreatePlaceholderTypeName(string kind, string requested, string? details = null) {
        string name = "__GerenUnresolvedType_" + ComputeStableHash12($"{kind}:{requested}");
        if (!_unresolvedByPlaceholder.ContainsKey(name)) {
            _unresolvedByPlaceholder[name] = new UnresolvedSchemaType(
                PlaceholderTypeName: name,
                Kind: kind,
                Requested: requested,
                Details: details);
        }
        else if (details is not null) {
            var existing = _unresolvedByPlaceholder[name];
            if (existing.Details is null) {
                _unresolvedByPlaceholder[name] = existing with { Details = details };
            }
        }

        return $"global::{_rootFileNamespace}.{name}";
    }

    private string ResolveByMetadata(string simpleType) {
        if (_resolvedSchemaTypeCache.TryGetValue(simpleType, out string cached))
            return cached;

        string result;
        INamedTypeSymbol? symbol = _compilation.GetTypeByMetadataName(simpleType);
        if (symbol is null) {
            if (_reportedUnresolvedSchemaTypes.Add(simpleType))
                _diagnostics.Add(Diagnostic.Create(Dide.UnresolvedSchemaReference, Location.None, "by metadata", simpleType));

            result = GetOrCreatePlaceholderTypeName(kind: "metadata", requested: simpleType);
        }
        else
            result = symbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

        _resolvedSchemaTypeCache[simpleType] = result;
        return result;
    }

    private string ResolveByCompile(string genericType) {
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
            result = GetOrCreatePlaceholderTypeName(kind: "compile", requested: genericType);
            if (_reportedUnresolvedSchemaTypes.Add(genericType))
                _diagnostics.Add(Diagnostic.Create(Dide.UnresolvedSchemaReference, Location.None, "by compile", genericType));
        }
        else
            result = type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

        _resolvedSchemaTypeCache[genericType] = result;
        return result;
    }

    // ResolveByReference with GetSymbolsWithName
    private string ResolveByReference(string referenceId) {
        string simpleType = Given.ToLetterOrDigitName(referenceId);
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
            string message = FormatAmbiguousMatches(candidateNames);
            result = GetOrCreatePlaceholderTypeName(
                kind: "ambiguous",
                requested: simpleType,
                details: $"referenceId: {referenceId}; matches: {message}");
            _diagnostics.Add(Diagnostic.Create(
                Dide.AmbiguousSchemaReference, Location.None, referenceId, simpleType, message));
        }
        else if (candidateNames.Count == 0) {
            result = GetOrCreatePlaceholderTypeName(
                kind: "reference",
                requested: simpleType,
                details: $"referenceId: {referenceId}");
            if (_reportedUnresolvedSchemaTypes.Add(simpleType))
                _diagnostics.Add(Diagnostic.Create(Dide.UnresolvedSchemaReference, Location.None, referenceId, simpleType));
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

    // For PlaceholderTypeName
    private static string ComputeStableHash12(string value) {
        // FNV-1a 64-bit over UTF-16 chars (stable across runtimes for the same string)
        const ulong offsetBasis = 14695981039346656037UL;
        const ulong prime = 1099511628211UL;

        ulong hash = offsetBasis;
        for (var i = 0; i < value.Length; i++) {
            hash ^= value[i];
            hash *= prime;
        }

        // 12 hex chars = 48 bits; keep it short for readability
        var shortHash = hash & 0xFFFFFFFFFFFFUL;
        return shortHash.ToString("X12");
    }
}
