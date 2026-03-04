namespace Geren.Incs;

internal sealed class MapInc {
    public string FilePrefix { get; }
    public string NamespaceFromFile { get; }
    public ImmutableArray<EndpointSpec> Endpoints { get; }
    public ImmutableArray<Diagnostic> Diagnostics { get; }

    private MapInc(string filePrefix, string namespaceFromFile, ImmutableArray<EndpointSpec> endpoints, ImmutableArray<Diagnostic> diagnostics) {
        FilePrefix = filePrefix;
        NamespaceFromFile = namespaceFromFile;
        Endpoints = endpoints;
        Diagnostics = diagnostics;
    }

    //static
    internal static MapInc Create(
        string filePrefix,
        string namespaceFromFile,
        ImmutableArray<EndpointSpec> endpoints,
        ImmutableArray<Diagnostic> diagnostics)
        => new(filePrefix, namespaceFromFile, endpoints, diagnostics);

    internal static MapInc Map(Compilation compilation, OpenApiDocument doc, string filePath)
        => new MapSession(compilation, doc, filePath).BuildMap();
}
