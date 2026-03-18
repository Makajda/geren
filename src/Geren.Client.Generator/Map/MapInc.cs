namespace Geren.Client.Generator.Map;

internal sealed record MapInc(
    string HintFilePath,
    string NamespaceFromFile,
    ImmutableArray<Mapoint> Endpoints,
    ImmutableArray<Diagnostic> Diagnostics,
    ImmutableArray<UnresolvedSchemaType> UnresolvedSchemaTypes) {
    internal static MapInc Map(Compilation compilation, string rootNamespace, string filePath, ImmutableArray<Purpoint> purpoints)
        => new MapSession().BuildMap(compilation, rootNamespace, filePath, purpoints);
}
