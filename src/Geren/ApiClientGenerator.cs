namespace Geren;

[Generator]
public sealed class ApiClientGenerator : IIncrementalGenerator {
    public void Initialize(IncrementalGeneratorInitializationContext context) {
        var packed = context.CompilationProvider.Select(static (compilation, _) => PackeInc.Validate(compilation));
        context.RegisterSourceOutput(packed.SelectMany((p, _) => p.Diagnostics), static (spc, d) => spc.ReportDiagnostic(d));

        var rootNamespace = context.AnalyzerConfigOptionsProvider.Select(static (options, _) =>
            options.GlobalOptions.TryGetValue("build_property.Geren_RootNamespace", out var configured)
                && !string.IsNullOrWhiteSpace(configured) ? configured.Trim() : "Geren");

        var allowGeneration = packed.SelectMany(static (p, _) => p.HasHttp ? [true] : ImmutableArray<bool>.Empty);
        context.RegisterSourceOutput(allowGeneration.Combine(packed).Combine(rootNamespace), static (spc, t) =>
            spc.AddSource("FactoryBridge.g.cs", SourceText.From(NormalizeEol(EmitFactoryBridge.Run(t.Left.Right.HasResilience, t.Right)), Encoding.UTF8)));

        // Probe
        var probed = context.AdditionalTextsProvider.Combine(packed)
            .Where(n => n.Right.HasHttp)
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
        context.RegisterSourceOutput(maped.Combine(packed).Combine(rootNamespace), (spc, x) => {
            var ((map, packe), rootNamespace) = x;
            string spaceName = $"{rootNamespace}.{map.NamespaceFromFile}";
            string prefix = $"{map.FilePrefix}.{map.NamespaceFromFile}";
            var files = map.Endpoints.GroupBy(e => new { e.SpaceName, e.ClassName });
            foreach (var file in files) {
                var code = EmitClient.Run(file, $"{spaceName}{file.Key.SpaceName}", file.Key.ClassName);
                spc.AddSource($"{prefix}{file.Key.SpaceName}.{file.Key.ClassName}.g.cs", SourceText.From(NormalizeEol(code), Encoding.UTF8));
            }

            var extensions = EmitExtensions.Run(packe.HasResilience, rootNamespace, spaceName,
                map.Endpoints.Select(e => GetNameWithNamespace(e.SpaceName, e.ClassName)).Distinct());
            spc.AddSource($"{prefix}.Extensions.g.cs", SourceText.From(NormalizeEol(extensions), Encoding.UTF8));
        });
    }

    private static string GetNameWithNamespace(string spaceName, string className) {
        string name = spaceName.TrimStart('.');
        return string.IsNullOrEmpty(name) ? className : $"{name}.{className}";
    }

    private static string NormalizeEol(string text) => text.Replace("\r\n", "\n").Replace('\r', '\n');
}
