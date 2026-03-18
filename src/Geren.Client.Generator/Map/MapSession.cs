namespace Geren.Client.Generator.Map;

internal sealed class MapSession {
    private readonly ImmutableArray<Diagnostic>.Builder _diagnostics = ImmutableArray.CreateBuilder<Diagnostic>();
    private TypeResolver _typeResolver = default!;
    internal MapInc BuildMap(Compilation compilation, string rootNamespace, string filePath, ImmutableArray<Purpoint> purpoints) {
        string namespaceFromFile = Given.ToLetterOrDigitName(Path.GetFileNameWithoutExtension(filePath) ?? string.Empty);
        _typeResolver = new($"{rootNamespace}.{namespaceFromFile}", compilation, _diagnostics);
        var endpoints = ImmutableArray.CreateBuilder<Mapoint>();
        foreach (var point in purpoints) {
            string returnType = _typeResolver.Resolve(point.ReturnType);
            string bodyType = _typeResolver.Resolve(point.BodyType);
            ImmutableArray<ParamSpec>.Builder ps = ImmutableArray.CreateBuilder<ParamSpec>();
            foreach (var param in point.Params)
                ps.Add(new(param.Name, param.Identifier, _typeResolver.Resolve(param.Type)));

            endpoints.Add(new(
                point.Method, point.Path, point.SpaceName, point.ClassName, point.MethodName,
                returnType, bodyType, point.BodyMediaType, ps.ToImmutable(), point.Queries));
        }

        return new(
            CreateHintFilePath(filePath),
            namespaceFromFile,
            endpoints.ToImmutable(),
            _diagnostics.ToImmutable(),
            _typeResolver.GetUnresolvedSchemaTypes());
    }

    //static
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
