namespace Geren.Incs;

internal class PackeInc {
    internal bool HasHttp { get; }
    internal bool HasResilience { get; }
    internal ImmutableArray<Diagnostic> Diagnostics { get; }

    private PackeInc(bool hasHttp, bool hasResilience, ImmutableArray<Diagnostic> diagnostics) {
        HasHttp = hasHttp;
        HasResilience = hasResilience;
        Diagnostics = diagnostics;
    }

    //static
    internal static PackeInc Validate(Compilation compilation) {
        var diagnostics = ImmutableArray.CreateBuilder<Diagnostic>();
        bool hasHttp = compilation.GetTypeByMetadataName("Microsoft.Extensions.DependencyInjection.IHttpClientBuilder") is not null;
        bool hasResilience = compilation.GetTypeByMetadataName("Microsoft.Extensions.Http.Resilience.ResilienceHandlerContext") is not null;
        if (!hasHttp)
            diagnostics.Add(Diagnostic.Create(Dide.MissingMicrosoftExtensionsHttp, Location.None));

        if (!hasResilience)
            diagnostics.Add(Diagnostic.Create(Dide.MissingMicrosoftExtensionsHttpResilience, Location.None));

        return new(hasHttp, hasResilience, diagnostics.ToImmutable());
    }
}
