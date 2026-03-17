namespace Geren.Client.Generator;

[Generator]
public sealed class Generator : IIncrementalGenerator {
    public void Initialize(IncrementalGeneratorInitializationContext context) {
        var rootNamespace = context.AnalyzerConfigOptionsProvider.Select(static (options, _) =>
            options.GlobalOptions.TryGetValue("build_property.Geren_RootNamespace", out var configured)
                && !string.IsNullOrWhiteSpace(configured) ? configured.Trim() : "Geren");

        // Probe
        var probed = context.AdditionalTextsProvider
            .Select(static (text, cancellationToken) => ProbeInc.Probe(text, cancellationToken));
        context.RegisterSourceOutput(probed.Where(p => p.Diagnostic is not null),
            static (spc, p) => spc.ReportDiagnostic(p.Diagnostic!));

        // Parse
        var parsed = probed.Where(static p => p.Success).Select(static (p, _) => ParseInc.Parse(p.FilePath!, p.Text!));
        context.RegisterSourceOutput(parsed.Where(static p => p.Diagnostic is not null),
            static (spc, p) => spc.ReportDiagnostic(p.Diagnostic!));

        // Map
        var maped = parsed
            .Where(static p => p.Success)
            .Combine(context.CompilationProvider)
            .Combine(rootNamespace)
            .Select(static (x, _) => MapInc.Map(x.Left.Right, x.Right, x.Left.Left.Document!, x.Left.Left.FilePath!));
        context.RegisterSourceOutput(maped.SelectMany(static (r, _) => r.Diagnostics),
            static (spc, r) => spc.ReportDiagnostic(r));

        // FactoryBridge
        context.RegisterSourceOutput(maped.Where(static n => !n.Endpoints.IsEmpty).Collect().Combine(rootNamespace), static (spc, t) => {
            if (t.Left.Any())
                spc.AddSource($"{t.Right}.FactoryBridge.g.cs", SourceText.From(NormalizeEol(EmitFactoryBridge.Run(t.Right)), Encoding.UTF8));
        });

        // Extensions and Clients
        context.RegisterSourceOutput(maped.Where(static n => !n.Endpoints.IsEmpty).Combine(rootNamespace), (spc, x) => {
            var (map, rootNamespace) = x;

            string spaceFromName = $"{rootNamespace}.{map.NamespaceFromFile}";

            if (map.UnresolvedSchemaTypes.Any()) {
                var unresolvedTypes = EmitUnresolvedTypes.Run(spaceFromName, map.UnresolvedSchemaTypes);
                spc.AddSource($"{spaceFromName}.UnresolvedTypes.{map.HintFilePath}.g.cs", SourceText.From(NormalizeEol(unresolvedTypes), Encoding.UTF8));
            }

            var extensions = EmitExtensions.Run(rootNamespace, map.NamespaceFromFile, spaceFromName,
                map.Endpoints.Select(e => $"{(string.IsNullOrEmpty(e.SpaceName) ? string.Empty : e.SpaceName + ".")}{e.ClassName}")
                .Distinct(StringComparer.Ordinal));
            spc.AddSource($"{spaceFromName}.Extensions.{map.HintFilePath}.g.cs", SourceText.From(NormalizeEol(extensions), Encoding.UTF8));

            var files = map.Endpoints.GroupBy(e => new { e.SpaceName, e.ClassName });
            foreach (var file in files) {
                string fileSpaceName = $"{spaceFromName}{(string.IsNullOrEmpty(file.Key.SpaceName) ? string.Empty : "." + file.Key.SpaceName)}";
                var code = EmitClient.Run(file, rootNamespace, fileSpaceName, file.Key.ClassName);
                spc.AddSource($"{fileSpaceName}.{file.Key.ClassName}.{map.HintFilePath}.g.cs", SourceText.From(NormalizeEol(code), Encoding.UTF8));
            }
        });
    }

    private static string NormalizeEol(string text) => text.Replace("\r\n", "\n").Replace('\r', '\n');
}
