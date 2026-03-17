namespace Geren.Client.Generator.Incs;

internal sealed record MapInc(
    string HintFilePath,
    string NamespaceFromFile,
    ImmutableArray<EndpointSpec> Endpoints,
    ImmutableArray<Diagnostic> Diagnostics,
    ImmutableArray<UnresolvedSchemaType> UnresolvedSchemaTypes) {
    internal static MapInc Map(Compilation compilation, string rootNamespace, OpenApiDocument doc, string filePath)
        => new MapSession().BuildMap(compilation, rootNamespace, doc, filePath);
}
