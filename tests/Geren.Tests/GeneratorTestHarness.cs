using System.Collections.Immutable;
using System.Net.Http.Json;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;

namespace Geren.Tests;

internal static class GeneratorTestHarness {
    internal static TestRunResult RunGenerator(
        string source,
        string openApi,
        string openApiPath = "v1.json",
        string? rootNamespace = null)
        => RunGenerator(source, [new InMemoryAdditionalText(openApiPath, openApi)], rootNamespace);

    internal static TestRunResult RunGenerator(
        string source,
        ImmutableArray<AdditionalText> additionalTexts,
        string? rootNamespace = null) {
        var parseOptions = new CSharpParseOptions(LanguageVersion.Preview);
        var syntaxTree = CSharpSyntaxTree.ParseText(SourceText.From(source, Encoding.UTF8), parseOptions);
        var compilation = CSharpCompilation.Create(
            "GeneratorTests",
            [syntaxTree],
            GetFrameworkReferences(),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        IIncrementalGenerator generator = new ApiClientGenerator();
        AnalyzerConfigOptionsProvider? optionsProvider = rootNamespace is null
            ? null
            : new TestAnalyzerConfigOptionsProvider(new Dictionary<string, string>(StringComparer.Ordinal) {
                ["build_property.Geren_RootNamespace"] = rootNamespace
            });

        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            generators: [generator.AsSourceGenerator()],
            additionalTexts: additionalTexts,
            parseOptions: parseOptions,
            optionsProvider: optionsProvider);

        driver = driver.RunGenerators(compilation);
        var runResult = driver.GetRunResult();
        var generatorRun = runResult.Results[0];
        return new TestRunResult(generatorRun.Diagnostics, generatorRun.GeneratedSources);
    }

    internal static CompiledAssemblyResult CompileGeneratedClientAssembly(
        string source,
        ImmutableArray<GeneratedSourceResult> generatedSources) {
        var parseOptions = new CSharpParseOptions(LanguageVersion.Preview);
        var sourceTree = CSharpSyntaxTree.ParseText(SourceText.From(source, Encoding.UTF8), parseOptions);

        var generatedTrees = generatedSources
            .Where(static s => IsClientSource(s.HintName))
            .Select(s => CSharpSyntaxTree.ParseText(s.SourceText, parseOptions, path: s.HintName))
            .ToImmutableArray();

        var compilation = CSharpCompilation.Create(
            "GeneratedRuntime_" + Guid.NewGuid().ToString("N"),
            [sourceTree, .. generatedTrees],
            GetFrameworkReferences(),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        using var stream = new MemoryStream();
        var emitResult = compilation.Emit(stream);
        if (!emitResult.Success)
            return new CompiledAssemblyResult(emitResult.Diagnostics, null);

        stream.Position = 0;
        var assembly = Assembly.Load(stream.ToArray());
        return new CompiledAssemblyResult(emitResult.Diagnostics, assembly);
    }

    internal static string NormalizeCode(string text)
        => text.Replace("\r\n", "\n").Replace('\r', '\n').Trim();

    internal static string ToSnapshotFileName(string hintName) {
        var match = Regex.Match(hintName, "([^.]+\\.g\\.cs)$", RegexOptions.CultureInvariant);
        return match.Success ? match.Groups[1].Value : hintName;
    }

    private static bool IsClientSource(string hintName)
        => !hintName.EndsWith("FactoryBridge.g.cs", StringComparison.Ordinal)
           && !hintName.EndsWith("Extensions.g.cs", StringComparison.Ordinal);

    private static ImmutableArray<MetadataReference> GetFrameworkReferences() {
        var tpa = AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") as string;
        tpa.Should().NotBeNullOrWhiteSpace();

        var locations = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var path in tpa!.Split(Path.PathSeparator))
            _ = locations.Add(path);

        foreach (var assembly in new[] {
            typeof(object).Assembly,
            typeof(Enumerable).Assembly,
            typeof(HttpClient).Assembly,
            typeof(JsonContent).Assembly
        }) {
            if (!string.IsNullOrWhiteSpace(assembly.Location))
                _ = locations.Add(assembly.Location);
        }

        return locations
            .Select(static location => (MetadataReference)MetadataReference.CreateFromFile(location))
            .ToImmutableArray();
    }
}

internal sealed record TestRunResult(
    ImmutableArray<Diagnostic> Diagnostics,
    ImmutableArray<GeneratedSourceResult> GeneratedSources);

internal sealed record CompiledAssemblyResult(
    ImmutableArray<Diagnostic> Diagnostics,
    Assembly? Assembly);

internal sealed class InMemoryAdditionalText(string path, string content) : AdditionalText {
    public override string Path { get; } = path;

    public override SourceText GetText(CancellationToken cancellationToken = default)
        => SourceText.From(content, Encoding.UTF8);
}

internal sealed class TestAnalyzerConfigOptionsProvider : AnalyzerConfigOptionsProvider {
    private static readonly AnalyzerConfigOptions Empty = new TestAnalyzerConfigOptions(new Dictionary<string, string>(StringComparer.Ordinal));

    private readonly AnalyzerConfigOptions _global;

    internal TestAnalyzerConfigOptionsProvider(IReadOnlyDictionary<string, string> globalOptions) {
        _global = new TestAnalyzerConfigOptions(globalOptions);
    }

    public override AnalyzerConfigOptions GlobalOptions => _global;

    public override AnalyzerConfigOptions GetOptions(SyntaxTree tree) => Empty;

    public override AnalyzerConfigOptions GetOptions(AdditionalText textFile) => Empty;

    private sealed class TestAnalyzerConfigOptions(IReadOnlyDictionary<string, string> values) : AnalyzerConfigOptions {
        public override bool TryGetValue(string key, out string value)
            => values.TryGetValue(key, out value!);
    }
}
