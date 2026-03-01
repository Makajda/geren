namespace Geren.Incs;

internal sealed class MapInc {
    public string FilePrefix { get; }
    public string NamespaceSuffix { get; }
    public ImmutableArray<EndpointSpec> Endpoints { get; }
    public ImmutableArray<Diagnostic> Diagnostics { get; }

    private MapInc(string filePrefix, string namespaceSuffix, ImmutableArray<EndpointSpec> endpoints, ImmutableArray<Diagnostic> diagnostics) {
        FilePrefix = filePrefix;
        NamespaceSuffix = namespaceSuffix;
        Endpoints = endpoints;
        Diagnostics = diagnostics;
    }

    //static
    internal static MapInc Create(
        string filePrefix,
        string namespaceSuffix,
        ImmutableArray<EndpointSpec> endpoints,
        ImmutableArray<Diagnostic> diagnostics)
        => new(filePrefix, namespaceSuffix, endpoints, diagnostics);

    internal static MapInc Map(OpenApiDocument doc, string filePath, Compilation compilation)
        => new MapSession(doc, filePath, compilation).BuildMap();
}
