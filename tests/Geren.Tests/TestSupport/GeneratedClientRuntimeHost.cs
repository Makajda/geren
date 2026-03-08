using System.Reflection;
using System.Runtime.Loader;

namespace Geren.Tests.TestSupport;

internal static class GeneratedClientRuntimeHost {
    internal static async Task<Uri?> InvokeAsync(EndpointSpec endpoint, params object?[] arguments) {
        var clientCode = EmitClient.Run(
            new[] { endpoint }.GroupBy(static item => (object)new { item.SpaceName, item.ClassName }).Single(),
            "Generated.Runtime",
            endpoint.ClassName);

        var syntaxTree = CSharpSyntaxTree.ParseText(clientCode, new CSharpParseOptions(LanguageVersion.Preview), path: "GeneratedClient.g.cs");
        var compilation = CSharpCompilation.Create(
            assemblyName: $"GeneratedClient_{Guid.NewGuid():N}",
            syntaxTrees: [syntaxTree],
            references: TestCompilationFactory.MetadataReferences,
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        using var peStream = new MemoryStream();
        var emitResult = compilation.Emit(peStream);
        emitResult.Success.Should().BeTrue(string.Join(Environment.NewLine, emitResult.Diagnostics));

        peStream.Position = 0;
        var loadContext = new AssemblyLoadContext($"GeneratedClient_{Guid.NewGuid():N}", isCollectible: true);
        try {
            var assembly = loadContext.LoadFromStream(peStream);
            var clientType = assembly.GetType($"Generated.Runtime.{endpoint.ClassName}")!;
            var method = clientType.GetMethod(endpoint.MethodName, BindingFlags.Instance | BindingFlags.Public)!;

            var handler = new RuntimeRecordingHandler();
            using var httpClient = new HttpClient(handler) {
                BaseAddress = new Uri("https://example.test")
            };

            var client = Activator.CreateInstance(clientType, httpClient)!;
            var invocationArguments = new object?[arguments.Length + 1];
            Array.Copy(arguments, invocationArguments, arguments.Length);
            invocationArguments[^1] = CancellationToken.None;

            var task = (Task)method.Invoke(client, invocationArguments)!;
            await task.ConfigureAwait(false);

            return handler.LastRequestUri;
        }
        finally {
            loadContext.Unload();
        }
    }
}
