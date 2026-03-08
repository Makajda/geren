using System.Reflection;

namespace Geren.Tests.TestSupport;

internal static class TestCompilationFactory {
    private static readonly Lazy<ImmutableArray<MetadataReference>> SharedReferences = new(CreateReferences);
    internal static ImmutableArray<MetadataReference> MetadataReferences => SharedReferences.Value;

    internal static CSharpCompilation Create(
        IEnumerable<string>? userSources = null,
        bool includeHttpClientBuilder = false,
        bool includeResilience = false) {
        var sources = new List<string> {
            """
            namespace TestHost;
            public sealed class Placeholder;
            """
        };

        if (includeHttpClientBuilder)
            sources.Add(
                """
                namespace Microsoft.Extensions.DependencyInjection;

                public interface IServiceCollection { }
                public interface IHttpClientBuilder { }
                """
            );

        if (includeResilience)
            sources.Add(
                """
                namespace Microsoft.Extensions.Http.Resilience;

                public sealed class ResilienceHandlerContext { }
                """
            );

        if (userSources is not null)
            sources.AddRange(userSources);

        var syntaxTrees = sources
            .Select((source, index) => CSharpSyntaxTree.ParseText(
                source,
                new CSharpParseOptions(LanguageVersion.Preview),
                path: $"Test{index}.cs"))
            .ToArray();

        return CSharpCompilation.Create(
            assemblyName: $"Tests_{Guid.NewGuid():N}",
            syntaxTrees,
            SharedReferences.Value,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
    }

    private static ImmutableArray<MetadataReference> CreateReferences() {
        var paths = ((string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES"))
            ?.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
            ?? [];

        var references = new Dictionary<string, MetadataReference>(StringComparer.OrdinalIgnoreCase);
        foreach (var path in paths)
            references[path] = MetadataReference.CreateFromFile(path);

        AddAssembly(typeof(CSharpCompilation).Assembly);
        AddAssembly(typeof(OpenApiDocument).Assembly);
        AddAssembly(typeof(Enumerable).Assembly);

        return [.. references.Values];

        void AddAssembly(Assembly assembly) {
            if (!string.IsNullOrWhiteSpace(assembly.Location))
                references[assembly.Location] = MetadataReference.CreateFromFile(assembly.Location);
        }
    }
}
