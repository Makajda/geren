namespace Geren.Client.Generator.Map;

internal sealed record MapInc(
    string HintFilePath,
    string NamespaceFromFile,
    ImmutableArray<Mapoint> Endpoints,
    ImmutableArray<UnresolvedSchemaType> UnresolvedSchemaTypes,
    ImmutableArray<Diagnostic> Diagnostics) {

    internal static MapInc Map(
        Compilation compilation,
        string rootNamespace,
        string filePath,
        ImmutableArray<Purpoint> purpoints,
        CancellationToken cancellationToken) {

        ImmutableArray<Diagnostic>.Builder _diagnostics = ImmutableArray.CreateBuilder<Diagnostic>();
        Dictionary<string, UnresolvedSchemaType> _unresolvedByPlaceholder = new(StringComparer.Ordinal);
        string namespaceFromFile = Given.ToLetterOrDigitName(Path.GetFileNameWithoutExtension(filePath) ?? string.Empty);
        TypeResolver _typeResolver = new($"{rootNamespace}.{namespaceFromFile}", compilation, _unresolvedByPlaceholder, _diagnostics, cancellationToken);
        var endpoints = ImmutableArray.CreateBuilder<Mapoint>();
        HashSet<string> seenMethodKeys = new(StringComparer.Ordinal);
        foreach (var point in purpoints) {
            cancellationToken.ThrowIfCancellationRequested();

            var (spaceName, className, methodName) = ResolveNames(point.Method, point.Path, point.OperationId);
            string methodKey = spaceName + "." + className + "." + methodName;
            if (!seenMethodKeys.Add(methodKey)) {
                _diagnostics.Add(Diagnostic.Create(Dide.DuplicateMethodName, Location.None, methodName, className, point.Path));
                continue;
            }

            string? returnType = point.ReturnType is null ? null : _typeResolver.Resolve(point.ReturnType, point.ReturnTypeBy);
            string? bodyType = point.BodyType is null ? null : _typeResolver.Resolve(point.BodyType, point.BodyTypeBy);
            ImmutableArray<Maparam>.Builder ps = ImmutableArray.CreateBuilder<Maparam>();
            foreach (var param in point.Params)
                ps.Add(new(param.Name, param.Identifier, _typeResolver.Resolve(param.Type, param.By)));

            endpoints.Add(new(
                point.Method, point.Path, spaceName, className, methodName,
                returnType, bodyType, point.BodyMedia, ps.ToImmutable(), point.Queries));
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

    private static (string SpaceName, string ClassName, string MethodName) ResolveNames(string method, string path, string? operationId) {
        string? withName = operationId is null ? null : Given.ToLetterOrDigitName(operationId);
        string[] sections = [.. path
            .Trim('/')
            .Split(['/'], StringSplitOptions.RemoveEmptyEntries)
            .Where(static s => !IsPathTemplateSegment(s))
            .Select(n=>Given.ToLetterOrDigitName(n))];

        int classIndex = sections.Length - 2;
        string methodName = withName ?? (method + sections.LastOrDefault());
        string className = classIndex >= 0 ? sections[classIndex] : "WebApiClient";
        string spaceName = classIndex > 0 ? string.Join(".", sections.Take(classIndex)) : string.Empty;
        return (spaceName, className, methodName);
    }

    private static bool IsPathTemplateSegment(string segment)
        => segment.Length > 1 && segment[0] == '{' && segment[segment.Length - 1] == '}';

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
