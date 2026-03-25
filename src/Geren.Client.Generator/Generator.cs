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
                x.Right.GetOptions(x.Left).TryGetValue("build_metadata.AdditionalFiles.Geren", out string? JsonFormat)
                    ? new { x.Left, JsonFormat }
                    : null)
            .Where(static p => p is not null)
            .Select(static (p, cancellationToken) => ParseInc.Parse(p!.Left, p.JsonFormat, cancellationToken));

        context.RegisterSourceOutput(parsed.SelectMany(static (r, _) => r.Diagnostics),
            static (spc, r) => spc.ReportDiagnostic(r));

        // Map
        var maped = parsed
            .Where(static p => p.Success)
            .Combine(context.CompilationProvider)
            .Combine(rootNamespace)
            .Select(static (x, cancellationToken) => {
                var ((p, compilation), rootNamespace) = x;
                return MapInc.Map(compilation, rootNamespace, p.FilePath, p.Purpoints, cancellationToken);
            });

        context.RegisterSourceOutput(maped.SelectMany(static (r, _) => r.Diagnostics),
            static (spc, r) => spc.ReportDiagnostic(r));

        // Need Hint
        var needHint = maped.Collect().Select(static (p, _) => p.Count() > 1);
        var hinted = maped.Combine(needHint).Combine(rootNamespace).Select((x, _) => {
            var ((Map, needHint), RootNamespace) = x;
            string Hint = needHint ? Map.HintFilePath + "." : string.Empty;
            return (Map, Hint, RootNamespace);
        });

        // Unresolved Types
        context.RegisterSourceOutput(hinted.Where(static p => !p.Map.UnresolvedSchemaTypes.IsEmpty), static (spc, x) => {
            string space = $"{x.RootNamespace}.{x.Map.NamespaceFromFile}";
            spc.AddSource($"{x.Hint}_UnresolvedTypes.g.cs",
                SourceText.From(NormalizeEol(EmitUnresolvedTypes.Run(space, x.Map.UnresolvedSchemaTypes)), Encoding.UTF8));
        });

        // exist endpoints
        var ended = hinted.Where(static p => !p.Map.Endpoints.IsEmpty);

        // FactoryBridge
        var hasAnyEndpoints = ended.Select(static (_, _) => true).Collect().Select(static (flags, _) => flags.Any(f => f));
        context.RegisterSourceOutput(hasAnyEndpoints.Combine(rootNamespace), static (spc, t) => {
            if (t.Left)
                spc.AddSource("FactoryBridge.g.cs", SourceText.From(NormalizeEol(EmitFactoryBridge.Run(t.Right)), Encoding.UTF8));
        });

        // Extensions
        context.RegisterSourceOutput(ended, static (spc, x) => {
            spc.AddSource($"{x.Hint}Extensions.g.cs",
                SourceText.From(NormalizeEol(EmitExtensions.Run(x.RootNamespace, x.Map.NamespaceFromFile,
                x.Map.Endpoints
                    .Select(static e => NameDot(e.SpaceName) + e.ClassName)
                    .Distinct(StringComparer.Ordinal))), Encoding.UTF8));
        });

        // Clients
        var cliented = ended.SelectMany((x, _) => x.Map.Endpoints.GroupBy(e =>
                new Client(x.Hint, x.RootNamespace, x.Map.NamespaceFromFile, e.SpaceName, e.ClassName)));

        context.RegisterSourceOutput(cliented, (spc, x) => {
            var (hint, rootNamespace, namespaceFromFile, spaceName, className) = x.Key;
            string dotSpace = string.IsNullOrEmpty(spaceName) ? string.Empty : "." + spaceName;
            string spaceReal = $"{rootNamespace}.{namespaceFromFile}{dotSpace}";
            string code = NormalizeEol(EmitClient.Run(x, rootNamespace, spaceReal, className));
            spc.AddSource($"{hint}{NameDot(spaceName)}{className}.g.cs", SourceText.From(code, Encoding.UTF8));
        });
    }

    record Client(string Hint, string RootNamespace, string NamespaceFromFile, string SpaceName, string ClassName);
    private static string NormalizeEol(string text) => text.Replace("\r\n", "\n").Replace('\r', '\n');
    private static string NameDot(string name) => string.IsNullOrWhiteSpace(name) ? string.Empty : name + ".";
}
