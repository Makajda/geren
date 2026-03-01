namespace Geren;

[Generator]
public sealed class ApiClientGenerator : IIncrementalGenerator {
    public void Initialize(IncrementalGeneratorInitializationContext context) {
        //if (!System.Diagnostics.Debugger.IsAttached) System.Diagnostics.Debugger.Launch();

        var rootNamespace = context.AnalyzerConfigOptionsProvider.Select(static (options, _) => {
            if (options.GlobalOptions.TryGetValue("build_property.Geren_RootNamespace", out var configured)
                && !string.IsNullOrWhiteSpace(configured))
                return configured.Trim();
            return "Gereb.Generated";
        });

        context.RegisterSourceOutput(rootNamespace, static (spc, ns) => {
            var bridge = EmitFactoryBridge.Run(ns);
            spc.AddSource("FactoryBridge.g.cs", SourceText.From(NormalizeEol(bridge), Encoding.UTF8));
        });

        // Probe
        var probed = context.AdditionalTextsProvider.Select(static (file, ct) => ProbeInc.Probe(file, ct));
        context.RegisterSourceOutput(probed.Where(p => p.Diagnostic is not null),
            static (spc, p) => spc.ReportDiagnostic(p.Diagnostic!));

        // Parse
        var parsed = probed.Where(static p => p.Success).Select(static (p, _) => ParseInc.Parse(p.FilePath!, p.Text!));
        context.RegisterSourceOutput(parsed.Where(static p => p.Diagnostic is not null),
            static (spc, p) => spc.ReportDiagnostic(p.Diagnostic!));

        // Map endpoints
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
            var ns = x.Right;
            string prefix = $"{map.FilePrefix}.{map.NamespaceSuffix}";
            var clients = EmitClients.Run(map, ns);
            foreach (var (name, code) in clients)
                spc.AddSource($"{prefix}.{name}", SourceText.From(NormalizeEol(code), Encoding.UTF8));

            var registrations = EmitRegistrations.Run(ns, map.NamespaceSuffix, map.Endpoints.Select(e => e.ClassName).Distinct());
            spc.AddSource($"{prefix}.Extensions.g.cs", SourceText.From(NormalizeEol(registrations), Encoding.UTF8));
        });
    }

    private static string NormalizeEol(string text) => text.Replace("\r\n", "\n").Replace('\r', '\n');
}
