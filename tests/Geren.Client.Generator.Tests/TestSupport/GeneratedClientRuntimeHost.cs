using System.Reflection;
using System.Runtime.Loader;

namespace Geren.Tests.TestSupport;

internal static class GeneratedClientRuntimeHost {
    internal static async Task<Uri?> InvokeAsync(Mapoint endpoint, params object?[] arguments) {
        var usingsCode = """
global using Microsoft.Extensions.DependencyInjection;
global using Microsoft.Extensions.Http.Resilience;
global using Polly;
global using System;
global using System.Collections.Generic;
global using System.Linq;
global using System.Net.Http;
global using System.Threading;
global using System.Threading.Tasks;
""";
        var factoryBridgeCode = EmitFactoryBridge.Run("Company.Generated");
        var clientCode = EmitClient.Run(
            new[] { endpoint }.GroupBy(static item => (object)new { item.SpaceName, item.ClassName }).Single(),
            "Company.Generated",
            "Company.Generated.Runtime",
            endpoint.ClassName);

        var syntaxTree = CSharpSyntaxTree.ParseText(clientCode, new CSharpParseOptions(LanguageVersion.Preview), path: "GeneratedClient.g.cs");
        var syntaxTreeUsings = CSharpSyntaxTree.ParseText(usingsCode, new CSharpParseOptions(LanguageVersion.Preview), path: "Usings.g.cs");
        var syntaxTreeFactoryBridge = CSharpSyntaxTree.ParseText(factoryBridgeCode, new CSharpParseOptions(LanguageVersion.Preview), path: "FactoryBridge.g.cs");

        var compilation = CSharpCompilation.Create(
            assemblyName: $"GeneratedClient_{Guid.NewGuid():N}",
            syntaxTrees: [syntaxTreeUsings, syntaxTreeFactoryBridge, syntaxTree],
            references: TestCompilationFactory.MetadataReferences,
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        using var peStream = new MemoryStream();
        var emitResult = compilation.Emit(peStream);
        emitResult.Success.Should().BeTrue(string.Join(Environment.NewLine, emitResult.Diagnostics));

        peStream.Position = 0;
        var loadContext = new AssemblyLoadContext($"GeneratedClient_{Guid.NewGuid():N}", isCollectible: true);
        try {
            var assembly = loadContext.LoadFromStream(peStream);
            var clientType = assembly.GetType($"Company.Generated.Runtime.{endpoint.ClassName}")!;
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
