namespace Geren.Client.Generator.Map;

internal sealed class MapSession {
    private readonly ImmutableArray<Diagnostic>.Builder _diagnostics = ImmutableArray.CreateBuilder<Diagnostic>();
    private TypeResolver _typeResolver = default!;
    internal MapInc BuildMap(Compilation compilation, string rootNamespace, string filePath, ImmutableArray<Purpoint> purpoints) {
        string namespaceFromFile = Given.ToLetterOrDigitName(Path.GetFileNameWithoutExtension(filePath) ?? string.Empty);
        _typeResolver = new($"{rootNamespace}.{namespaceFromFile}", compilation, _diagnostics);
        var endpoints = ImmutableArray.CreateBuilder<Mapoint>();
        foreach (var (point, returnPurtype, bodyPurtype, purparams) in purpoints) {
            string returnType = _typeResolver.Resolve(returnPurtype);
            string bodyType = _typeResolver.Resolve(bodyPurtype);
            ImmutableArray<ParamSpec>.Builder ps = ImmutableArray.CreateBuilder<ParamSpec>();
            foreach (var pp in purparams)
                ps.Add(new(pp.Name, pp.Identifier, _typeResolver.Resolve(pp.Type)));

            endpoints.Add(new(point, returnType, bodyType, ps.ToImmutable()));
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
