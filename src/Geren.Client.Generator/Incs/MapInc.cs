namespace Geren.Client.Generator.Incs;

internal sealed class MapInc {
    public string HintFilePath { get; }
    public string NamespaceFromFile { get; }
    public ImmutableArray<EndpointSpec> Endpoints { get; }
    public ImmutableArray<Diagnostic> Diagnostics { get; }

    internal MapInc(string hintFilePath, string namespaceFromFile, ImmutableArray<EndpointSpec> endpoints, ImmutableArray<Diagnostic> diagnostics) {
        HintFilePath = hintFilePath;
        NamespaceFromFile = namespaceFromFile;
        Endpoints = endpoints;
        Diagnostics = diagnostics;
    }

    //static
    internal static MapInc Map(Compilation compilation, OpenApiDocument doc, string filePath)
        => new MapSession().BuildMap(compilation, doc, filePath);
}
