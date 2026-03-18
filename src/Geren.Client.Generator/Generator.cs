namespace Geren.Client.Generator;

[Generator]
public sealed class Generator : IIncrementalGenerator {
    public void Initialize(IncrementalGeneratorInitializationContext context) {
        var rootNamespace = context.AnalyzerConfigOptionsProvider.Select(static (options, _) =>
            options.GlobalOptions.TryGetValue("build_property.Geren_RootNamespace", out var configured)
                && !string.IsNullOrWhiteSpace(configured) ? configured.Trim() : "Geren");

        // Parse
        var parsed = context.AdditionalTextsProvider
            .Combine(context.AnalyzerConfigOptionsProvider)
            .Select(static (x, cancellationToken) =>
                x.Right.GetOptions(x.Left).TryGetValue("build_metadata.AdditionalFiles.Geren", out var value)
                    && string.Equals(value, "true", StringComparison.OrdinalIgnoreCase) ? x.Left : null)
            .Where(static p => p is not null)
            .Select(static (p, cancellationToken) => ParseInc.Parse(p!, cancellationToken));

        context.RegisterSourceOutput(parsed.SelectMany(static (r, _) => r.Diagnostics),
            static (spc, r) => spc.ReportDiagnostic(r));

        // Map
        var maped = parsed
            .Where(static p => p.Success)
            .Combine(context.CompilationProvider)
            .Combine(rootNamespace)
            .Select(static (x, _) => {
                var ((p, compilation), rootNamespace) = x;
                return MapInc.Map(compilation, rootNamespace, p.FilePath, p.Purpoints);
            });

        context.RegisterSourceOutput(maped.SelectMany(static (r, _) => r.Diagnostics),
            static (spc, r) => spc.ReportDiagnostic(r));

        // Unresolved Types
        var unresolved = maped.Where(static p => !p.UnresolvedSchemaTypes.IsEmpty);
        context.RegisterSourceOutput(unresolved.Combine(rootNamespace), static (spc, x) => {
            var (map, rootNamespace) = x;
            string spaceFromName = $"{rootNamespace}.{map.NamespaceFromFile}";
            spc.AddSource($"{map.HintFilePath}.UnresolvedTypes.g.cs",
                SourceText.From(NormalizeEol(EmitUnresolvedTypes.Run(spaceFromName, map.UnresolvedSchemaTypes)), Encoding.UTF8));
        });

        // exist endpoints
        var ended = maped.Where(static p => !p.Endpoints.IsEmpty);

        // FactoryBridge
        var hasAnyEndpoints = ended.Select(static (_, _) => true).Collect().Select(static (flags, _) => flags.Any(f => f));
        context.RegisterSourceOutput(hasAnyEndpoints.Combine(rootNamespace), static (spc, t) => {
            if (t.Left)
                spc.AddSource("FactoryBridge.g.cs", SourceText.From(NormalizeEol(EmitFactoryBridge.Run(t.Right)), Encoding.UTF8));
        });

        // Extensions
        var extensed = ended
            .Where(static p => !p.Endpoints.IsEmpty)
            .Select(static (m, _) => new {
                m.HintFilePath,
                m.NamespaceFromFile,
                Classes = m.Endpoints
                    .Select(e => (string.IsNullOrEmpty(e.SpaceName) ? string.Empty : e.SpaceName + ".") + e.ClassName)
                    .Distinct(StringComparer.Ordinal)
            });

        context.RegisterSourceOutput(extensed.Combine(rootNamespace), static (spc, x) => {
            var (map, rootNamespace) = x;
            spc.AddSource($"{map.HintFilePath}.Extensions.g.cs",
                SourceText.From(NormalizeEol(EmitExtensions.Run(rootNamespace, map.NamespaceFromFile, map.Classes)), Encoding.UTF8));
        });

        // Clients
        var cliented = ended
            .SelectMany((m, cancellationToken) => m.Endpoints.GroupBy(e => new Client(m.HintFilePath, m.NamespaceFromFile, e.SpaceName, e.ClassName)))
            .Combine(rootNamespace);
        context.RegisterSourceOutput(cliented, (spc, x) => {
            var (map, rootNamespace) = x;
            var client = map.Key;
            string dotSpace = string.IsNullOrEmpty(client.SpaceName) ? string.Empty : "." + client.SpaceName;
            string spaceName = $"{rootNamespace}.{client.NamespaceFromFile}{dotSpace}";
            var code = EmitClient.Run(map, rootNamespace, spaceName, client.ClassName);
            spc.AddSource($"{client.HintFilePath}{dotSpace}.{client.ClassName}.g.cs", SourceText.From(NormalizeEol(code), Encoding.UTF8));
        });
    }

    record SpaceClass(string SpaceName, string ClassName);
    record Client(string HintFilePath, string NamespaceFromFile, string SpaceName, string ClassName);
    private static string NormalizeEol(string text) => text.Replace("\r\n", "\n").Replace('\r', '\n');
}
