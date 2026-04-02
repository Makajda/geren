namespace Geren.Client.Generator.Map;

internal sealed record MapInc(
    string HintFilePath,
    string NamespaceFromFile,
    ImmutableArray<Mapoint> Endpoints,
    ImmutableArray<UnresolvedSchemaType> UnresolvedSchemaTypes,
    ImmutableArray<Diagnostic> Diagnostics) {

    // Design notes:
    // - This step translates parsed endpoints (Purpoint) into emit-ready endpoints (Mapoint).
    // - All type names must be unambiguous and compilable; unresolved types are represented by placeholder types.
    // - Generated method keys must be stable and unique to avoid producing duplicate member definitions.

    internal static MapInc Map(
        Compilation compilation,
        string rootNamespace,
        string filePath,
        ImmutableArray<Purpoint> purpoints,
        CancellationToken cancellationToken) {

        var diagnostics = ImmutableArray.CreateBuilder<Diagnostic>();
        var endpoints = ImmutableArray.CreateBuilder<Mapoint>();

        Dictionary<string, UnresolvedSchemaType> _unresolvedByPlaceholder = new(StringComparer.Ordinal);
        HashSet<string> seenMethodKeys = new(StringComparer.Ordinal);

        string namespaceFromFile = string.Join(".", Path.GetFileNameWithoutExtension(filePath).Split('.').Select(n => Given.ToLetterOrDigitName(n)));
        TypeResolver _typeResolver = new($"{rootNamespace}.{namespaceFromFile}", compilation, _unresolvedByPlaceholder, diagnostics, cancellationToken);

        foreach (var point in purpoints) {
            cancellationToken.ThrowIfCancellationRequested();

            var (spaceName, className, methodName) = ResolveNames(point.Method, point.Path, point.OperationId);
            string methodKey = spaceName + "." + className + "." + methodName;
            if (!seenMethodKeys.Add(methodKey)) {
                diagnostics.Add(Diagnostic.Create(Dide.DuplicateMethodName, Location.None, methodName, className, point.Path));
                continue;
            }

            string? returnType = point.ReturnType is null ? null : _typeResolver.Resolve(point.ReturnType, point.ReturnTypeBy);
            string? bodyType = point.BodyType is null ? null : _typeResolver.Resolve(point.BodyType, point.BodyTypeBy);
            ImmutableArray<Maparam>.Builder ps = ImmutableArray.CreateBuilder<Maparam>();
            if (point.Params is not null)
                foreach (var param in point.Params)
                    ps.Add(new(param.Name, param.Identifier, _typeResolver.Resolve(param.Type, param.By)));

            endpoints.Add(new(
                point.Method, point.Path, spaceName, className, methodName,
                returnType, bodyType, point.BodyMedia, ps.ToImmutable(), point.Queries ?? []));
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
            diagnostics.ToImmutable());
    }

    private static (string SpaceName, string ClassName, string MethodName) ResolveNames(string method, string path, string? operationId) {
        string? withName = operationId is null ? null : Given.ToLetterOrDigitName(operationId);
        // Naming contract:
        // - operationId (if present) wins for method name
        // - otherwise: Method + last non-template path segment
        // - class name: penultimate non-template segment + "Http", or "RootHttp" for short paths
        // - namespace: remaining non-template segments
        string[] sections = [.. path
            .Trim('/')
            .Split(['/'], StringSplitOptions.RemoveEmptyEntries)
            .Where(static s => !IsPathTemplateSegment(s))
            .Select(n=>Given.ToLetterOrDigitName(n))];

        int classIndex = sections.Length - 2;
        string methodName = withName ?? (method + sections.LastOrDefault());
        string className = classIndex >= 0 ? sections[classIndex] + "Http" : "RootHttp";
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
