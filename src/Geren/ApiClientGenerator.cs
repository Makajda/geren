namespace Geren;

[Generator]
public sealed class ApiClientGenerator : IIncrementalGenerator {
    public void Initialize(IncrementalGeneratorInitializationContext context) {
        var rootNamespace = context.AnalyzerConfigOptionsProvider.Select(static (options, _) =>
            options.GlobalOptions.TryGetValue("build_property.Geren_RootNamespace", out var configured)
                && !string.IsNullOrWhiteSpace(configured) ? configured.Trim() : "Geren");

        context.RegisterSourceOutput(rootNamespace, static (spc, ns) => {
            var bridge = EmitCommon.Run(ns);
            spc.AddSource("Common.g.cs", SourceText.From(NormalizeEol(bridge), Encoding.UTF8));
        });

        // Probe
        var probed = context.AdditionalTextsProvider.Select(static (file, ct) => ProbeInc.Probe(file, ct));
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
            .Select(static (x, _) => MapInc.Map(x.Left.Document!, x.Left.FilePath!, x.Right));
        context.RegisterSourceOutput(maped.SelectMany(static (r, _) => r.Diagnostics),
            static (spc, r) => spc.ReportDiagnostic(r));

        // Emit
        var emitInput = maped.Combine(rootNamespace);
        context.RegisterSourceOutput(emitInput, (spc, x) => {
            var map = x.Left;
            string spaceName = $"{x.Right}.{map.NamespaceFromFile}";
            string prefix = $"{map.FilePrefix}.{map.NamespaceFromFile}";
            var files = map.Endpoints.GroupBy(e => new { e.SpaceName, e.ClassName });
            foreach (var file in files) {
                var code = EmitClient.Run(file, $"{spaceName}{file.Key.SpaceName}", file.Key.ClassName);
                spc.AddSource($"{prefix}{file.Key.SpaceName}.{file.Key.ClassName}.g.cs", SourceText.From(NormalizeEol(code), Encoding.UTF8));
            }

            var registrations = EmitRegistrations.Run(x.Right, spaceName,
                map.Endpoints.Select(e => GetNameWithNamespace(e.SpaceName, e.ClassName)).Distinct());
            spc.AddSource($"{prefix}.Extensions.g.cs", SourceText.From(NormalizeEol(registrations), Encoding.UTF8));
        });
    }

    private static string GetNameWithNamespace(string spaceName, string className) {
        string name = spaceName.TrimStart('.');
        return string.IsNullOrEmpty(name) ? className : $"{name}.{className}";
    }

    private static string NormalizeEol(string text) => text.Replace("\r\n", "\n").Replace('\r', '\n');
}
