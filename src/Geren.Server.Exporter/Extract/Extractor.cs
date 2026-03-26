namespace Geren.Server.Exporter.Extract;

internal static class Extractor {
    public static (ImmutableArray<Purpoint>, ImmutableArray<ErWarning>) Extract(Compilation compilation, string[] excludeTypes, CancellationToken cancellationToken) {
        var endpoints = ImmutableArray.CreateBuilder<Purpoint>();
        var warnings = ImmutableArray.CreateBuilder<ErWarning>();
        var endpointRouteBuilder = compilation.GetTypeByMetadataName("Microsoft.AspNetCore.Routing.IEndpointRouteBuilder");
        if (endpointRouteBuilder is null) {
            warnings.Add(Dide.Create("GERENEXP001", "Unable to find Microsoft.AspNetCore.Routing.IEndpointRouteBuilder in compilation; no endpoints will be discovered"));
            return (endpoints.ToImmutable(), warnings.ToImmutable());
        }

        foreach (var tree in compilation.SyntaxTrees.Where(n => ValidSyntaxTree(n.FilePath))) {
            cancellationToken.ThrowIfCancellationRequested();

            var root = tree.GetRoot(cancellationToken);
            var semanticModel = compilation.GetSemanticModel(tree, ignoreAccessibility: true);

            foreach (var invocation in root.DescendantNodes().OfType<InvocationExpressionSyntax>())
                ExtractorOne.Extract(compilation, endpointRouteBuilder, semanticModel, invocation, excludeTypes, endpoints, warnings, cancellationToken);
        }

        endpoints.Sort(static (a, b) => {
            var c = StringComparer.Ordinal.Compare(a.Path, b.Path);
            if (c != 0) return c;

            return StringComparer.Ordinal.Compare(a.Method, b.Method);
        });

        return (endpoints.ToImmutable(), warnings.ToImmutable());
    }

    private static bool ValidSyntaxTree(string? filePath) {
        if (string.IsNullOrWhiteSpace(filePath))
            return true;

        if (filePath.EndsWith(".g.cs", StringComparison.OrdinalIgnoreCase))
            return false;

        return !filePath.Contains("\\obj\\", StringComparison.OrdinalIgnoreCase)
            && !filePath.Contains("/obj/", StringComparison.OrdinalIgnoreCase)
            && !filePath.Contains("\\bin\\", StringComparison.OrdinalIgnoreCase)
            && !filePath.Contains("/bin/", StringComparison.OrdinalIgnoreCase);
    }
}
