using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using System.Collections.Immutable;
using System.Text;

namespace Geren.Server.Exporter.Tests.TestSupport;

internal static class TestCompilation {
    internal static Compilation Create(
        string mainSource,
        string? mainPath = null,
        IEnumerable<(string path, string source)>? extraSources = null,
        bool includeAspNetStubs = true) {

        List<SyntaxTree> trees = [];

        if (includeAspNetStubs) {
            trees.Add(CSharpSyntaxTree.ParseText(
                SourceText.From(TestAspNetStubs.Source, Encoding.UTF8),
                options: new CSharpParseOptions(LanguageVersion.Preview),
                path: "C:\\stubs\\AspNetStubs.cs"));
        }

        trees.Add(CSharpSyntaxTree.ParseText(
            SourceText.From(mainSource, Encoding.UTF8),
            options: new CSharpParseOptions(LanguageVersion.Preview),
            path: mainPath ?? "C:\\src\\Program.cs"));

        if (extraSources is not null) {
            foreach (var (path, source) in extraSources) {
                trees.Add(CSharpSyntaxTree.ParseText(
                    SourceText.From(source, Encoding.UTF8),
                    options: new CSharpParseOptions(LanguageVersion.Preview),
                    path: path));
            }
        }

        var references = GetTrustedPlatformAssemblyReferences();

        return CSharpCompilation.Create(
            assemblyName: "TestAssembly",
            syntaxTrees: trees,
            references: references,
            options: new CSharpCompilationOptions(
                OutputKind.DynamicallyLinkedLibrary,
                nullableContextOptions: NullableContextOptions.Enable,
                optimizationLevel: OptimizationLevel.Release));
    }

    private static ImmutableArray<MetadataReference> GetTrustedPlatformAssemblyReferences() {
        var tpa = (string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES");
        if (string.IsNullOrWhiteSpace(tpa)) {
            // Extremely defensive fallback.
            return [
                MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(Enumerable).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(Task).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(Attribute).Assembly.Location),
            ];
        }

        var paths = tpa.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries);
        var refs = ImmutableArray.CreateBuilder<MetadataReference>(paths.Length);
        foreach (var p in paths) {
            if (!p.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
                continue;

            refs.Add(MetadataReference.CreateFromFile(p));
        }

        return refs.ToImmutable();
    }
}
