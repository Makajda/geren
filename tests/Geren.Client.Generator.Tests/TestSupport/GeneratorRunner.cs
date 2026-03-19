namespace Geren.Tests.TestSupport;

internal static class GeneratorRunner {
    internal sealed record AdditionalFile(string Path, string Text, bool OptIn);

    internal sealed record RunResult(
        GeneratorDriverRunResult DriverResult,
        Compilation InputCompilation,
        Compilation OutputCompilation,
        ImmutableArray<Diagnostic> OutputDiagnostics);

    internal static RunResult Run(
        CSharpCompilation compilation,
        IEnumerable<AdditionalFile> additionalFiles,
        string? rootNamespace = null) {
        var addTexts = additionalFiles.Select(static f => (AdditionalText)new InMemoryAdditionalText(f.Path, f.Text)).ToImmutableArray();

        var global = new Dictionary<string, string>(StringComparer.Ordinal);
        if (!string.IsNullOrWhiteSpace(rootNamespace))
            global["build_property.Geren_RootNamespace"] = rootNamespace!;

        var perAdditional = new Dictionary<string, IReadOnlyDictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var f in additionalFiles) {
            if (!f.OptIn)
                continue;

            perAdditional[f.Path] = new Dictionary<string, string>(StringComparer.Ordinal) {
                ["build_metadata.AdditionalFiles.Geren"] = "true"
            };
        }

        var optionsProvider = new TestAnalyzerConfigOptionsProvider(global, perAdditional);
        var generator = new Geren.Client.Generator.Generator();
        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            generators: [generator.AsSourceGenerator()],
            additionalTexts: addTexts,
            parseOptions: (CSharpParseOptions)compilation.SyntaxTrees.First().Options,
            optionsProvider: optionsProvider);

        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out var diagnostics);
        var result = driver.GetRunResult();
        return new(result, compilation, outputCompilation, diagnostics);
    }
}

