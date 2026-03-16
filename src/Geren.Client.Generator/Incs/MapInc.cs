namespace Geren.Client.Generator.Incs;

internal sealed record MapInc(string HintFilePath, string NamespaceFromFile, ImmutableArray<EndpointSpec> Endpoints, ImmutableArray<Diagnostic> Diagnostics) {
    internal static MapInc Map(Compilation compilation, OpenApiDocument doc, string filePath)
        => new MapSession().BuildMap(compilation, doc, filePath);
}
