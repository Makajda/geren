namespace Geren.Client.Generator.Map;

internal sealed record MapInc(
    string HintFilePath,
    string NamespaceFromFile,
    ImmutableArray<Mapoint> Endpoints,
    ImmutableArray<UnresolvedSchemaType> UnresolvedSchemaTypes,
    ImmutableArray<Diagnostic> Diagnostics) {

    private static MapInc Empty => new(string.Empty, string.Empty, [], [], []);

    internal static MapInc Map(
        Compilation compilation,
        string rootNamespace,
        string filePath,
        ImmutableArray<Purpoint> purpoints,
        CancellationToken cancellationToken) {
        ImmutableArray<Diagnostic>.Builder _diagnostics = ImmutableArray.CreateBuilder<Diagnostic>();
        Dictionary<string, UnresolvedSchemaType> _unresolvedByPlaceholder = new(StringComparer.Ordinal);
        string namespaceFromFile = Given.ToLetterOrDigitName(Path.GetFileNameWithoutExtension(filePath) ?? string.Empty);
        TypeResolver _typeResolver = new($"{rootNamespace}.{namespaceFromFile}", compilation, _unresolvedByPlaceholder, _diagnostics);
        var endpoints = ImmutableArray.CreateBuilder<Mapoint>();
        foreach (var point in purpoints) {
            if (cancellationToken.IsCancellationRequested)
                return Empty;

            string returnType = _typeResolver.Resolve(point.ReturnType);
            string bodyType = _typeResolver.Resolve(point.BodyType);
            ImmutableArray<ParamSpec>.Builder ps = ImmutableArray.CreateBuilder<ParamSpec>();
            foreach (var param in point.Params)
                ps.Add(new(param.Name, param.Identifier, _typeResolver.Resolve(param.Type)));

            endpoints.Add(new(
                point.Method, point.Path, point.SpaceName, point.ClassName, point.MethodName,
                returnType, bodyType, point.BodyMediaType, ps.ToImmutable(), point.Queries));
        }

        ImmutableArray<UnresolvedSchemaType> unresolved = _unresolvedByPlaceholder.Count == 0
            ? []
            : [.. _unresolvedByPlaceholder.Values
                .OrderBy(static t => t.Kind, StringComparer.Ordinal)
                .ThenBy(static t => t.Requested, StringComparer.Ordinal)
                .ThenBy(static t => t.PlaceholderTypeName, StringComparer.Ordinal)];

        return new(
            CreateHintFilePath(filePath),
            namespaceFromFile,
            endpoints.ToImmutable(),
            unresolved,
            _diagnostics.ToImmutable());
    }

    private static string CreateHintFilePath(string filePath) {
        unchecked {
            uint hash = 2166136261;
            foreach (var ch in filePath) {
                hash ^= char.ToUpperInvariant(ch);
                hash *= 16777619;
            }

            return "h" + hash.ToString("x8");
        }
    }
}
