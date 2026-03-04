namespace Geren;

[Generator]
public sealed class ApiClientGenerator : IIncrementalGenerator {
    public void Initialize(IncrementalGeneratorInitializationContext context) {
        var packed = context.CompilationProvider.Select(static (compilation, _) => ValidatePackages(compilation));
        context.RegisterSourceOutput(packed.SelectMany((d, _) => d), static (spc, d) => spc.ReportDiagnostic(d));

        var rootNamespace = context.AnalyzerConfigOptionsProvider.Select(static (options, _) =>
            options.GlobalOptions.TryGetValue("build_property.Geren_RootNamespace", out var configured)
                && !string.IsNullOrWhiteSpace(configured) ? configured.Trim() : "Geren");

        var allowGeneration = packed.SelectMany(static (d, _) => d.IsDefaultOrEmpty ? [Unit.Value] : ImmutableArray<Unit>.Empty);
        context.RegisterSourceOutput(allowGeneration.Combine(rootNamespace), static (spc, spaceName) =>
            spc.AddSource("Common.g.cs", SourceText.From(NormalizeEol(EmitCommon.Run(spaceName.Right)), Encoding.UTF8)));

        // Probe
        var probed = context.AdditionalTextsProvider.Combine(packed)
            .Where(n => n.Right.IsDefaultOrEmpty)
            .Select(static (text, cancellationToken) => ProbeInc.Probe(text.Left, cancellationToken));
        context.RegisterSourceOutput(probed.Where(p => p.Diagnostic is not null),
            static (spc, p) => spc.ReportDiagnostic(p.Diagnostic!));

        // Parse
        var parsed = probed.Where(static p => p.Success).Select(static (p, _) => ParseInc.Parse(p.FilePath!, p.Text!));
        context.RegisterSourceOutput(parsed.Where(static p => p.Diagnostic is not null),
            static (spc, p) => spc.ReportDiagnostic(p.Diagnostic!));

        // Map
        var maped = parsed
            .Where(static p => p.Document is not null && p.FilePath is not null)
            .Combine(context.CompilationProvider)
            .Select(static (x, _) => MapInc.Map(x.Right, x.Left.Document!, x.Left.FilePath!));
        context.RegisterSourceOutput(maped.SelectMany(static (r, _) => r.Diagnostics),
            static (spc, r) => spc.ReportDiagnostic(r));

        // Emit
        context.RegisterSourceOutput(maped.Combine(rootNamespace), (spc, x) => {
            var (map, rootNamespace) = x;
            string spaceName = $"{rootNamespace}.{map.NamespaceFromFile}";
            string prefix = $"{map.FilePrefix}.{map.NamespaceFromFile}";
            var files = map.Endpoints.GroupBy(e => new { e.SpaceName, e.ClassName });
            foreach (var file in files) {
                var code = EmitClient.Run(file, $"{spaceName}{file.Key.SpaceName}", file.Key.ClassName);
                spc.AddSource($"{prefix}{file.Key.SpaceName}.{file.Key.ClassName}.g.cs", SourceText.From(NormalizeEol(code), Encoding.UTF8));
            }

            var registrations = EmitRegistrations.Run(rootNamespace, spaceName,
                map.Endpoints.Select(e => GetNameWithNamespace(e.SpaceName, e.ClassName)).Distinct());
            spc.AddSource($"{prefix}.Extensions.g.cs", SourceText.From(NormalizeEol(registrations), Encoding.UTF8));
        });
    }

    private static string GetNameWithNamespace(string spaceName, string className) {
        string name = spaceName.TrimStart('.');
        return string.IsNullOrEmpty(name) ? className : $"{name}.{className}";
    }

    private static string NormalizeEol(string text) => text.Replace("\r\n", "\n").Replace('\r', '\n');

    private static ImmutableArray<Diagnostic> ValidatePackages(Compilation compilation) {
        var diagnostics = ImmutableArray.CreateBuilder<Diagnostic>();
        bool hasnotHttp = compilation.GetTypeByMetadataName("Microsoft.Extensions.DependencyInjection.IHttpClientBuilder") is null;
        bool hasnotResilience = compilation.GetTypeByMetadataName("Microsoft.Extensions.Http.Resilience.ResilienceHandlerContext") is null;
        if (hasnotHttp)
            diagnostics.Add(Diagnostic.Create(Givenn.MissingMicrosoftExtensionsHttp, Location.None));

        if (hasnotResilience)
            diagnostics.Add(Diagnostic.Create(Givenn.MissingMicrosoftExtensionsHttpResilience, Location.None));

        return diagnostics.ToImmutable();
    }
}
