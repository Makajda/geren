namespace Geren.Client.Generator.Tests.TestSupport;

internal static class CompilationFactory {
    internal static CSharpCompilation Create(string assemblyName, params string[] sources) {
        var parseOptions = new CSharpParseOptions(LanguageVersion.Latest);
        var trees = new List<SyntaxTree>(sources.Length + 1) {
            CSharpSyntaxTree.ParseText(ImplicitUsings, parseOptions, path: "ImplicitUsings.g.cs")
        };
        trees.AddRange(sources.Select(s => CSharpSyntaxTree.ParseText(s, parseOptions)));

        var references = new List<MetadataReference>();
        foreach (var path in GetTrustedPlatformAssemblyPaths())
            references.Add(MetadataReference.CreateFromFile(path));

        // Non-framework dependencies used by generated code
        AddRef(references, typeof(Microsoft.Extensions.DependencyInjection.IServiceCollection));
        AddRef(references, typeof(Microsoft.Extensions.DependencyInjection.IHttpClientBuilder));
        AddRef(references, typeof(Microsoft.Extensions.Http.Resilience.ResilienceHandlerContext));
        AddRef(references, typeof(Polly.ResiliencePipelineBuilder<>));

        return CSharpCompilation.Create(
            assemblyName,
            trees,
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));
    }

    private const string ImplicitUsings = """
        global using System;
        global using System.Collections.Generic;
        global using System.Globalization;
        global using System.IO;
        global using System.Linq;
        global using System.Net.Http;
        global using System.Net.Http.Json;
        global using System.Threading;
        global using System.Threading.Tasks;
        """;

    internal static IReadOnlyList<string> GetTrustedPlatformAssemblyPaths() {
        var tpa = (string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES");
        tpa.Should().NotBeNull("TRUSTED_PLATFORM_ASSEMBLIES must be present in test runtime");
        return [.. tpa!.Split(Path.PathSeparator).Where(static p => !string.IsNullOrWhiteSpace(p))];
    }

    private static void AddRef(List<MetadataReference> references, Type type) {
        var location = type.Assembly.Location;
        if (string.IsNullOrWhiteSpace(location))
            return;

        if (references.Any(r => string.Equals(((PortableExecutableReference)r).FilePath, location, StringComparison.OrdinalIgnoreCase)))
            return;

        references.Add(MetadataReference.CreateFromFile(location));
    }
}
