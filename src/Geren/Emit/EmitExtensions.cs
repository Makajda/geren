namespace Geren.Emit;

internal static class EmitExtensions {
    internal static string Run(bool hasResilience, string rootNamespace, string namespaceFromFile, string spaceName, IEnumerable<string> names) {
        return $$"""
#nullable enable
using Microsoft.Extensions.DependencyInjection;{{(hasResilience ? UsingResilience : string.Empty)}}
using System;

namespace {{spaceName}};

public static class Geren{{namespaceFromFile}}Extensions
{
    public static IServiceCollection AddGerenClients(
        this IServiceCollection services,
        Action<IHttpClientBuilder>? configureBuilder = null,{{(hasResilience ? ParamsWithResilience : ParamsWithoutResilience)}}
    {
{{AllReg(hasResilience ? ", useResilience, resiliencePipelineName, configureResilience" : string.Empty)}}
        return services;
    }

{{SingleReg()}}
}
""";

        string AllReg(string chunkResilience) => string.Join(Givenn.NewLine, names.Select(name => $$"""
        global::{{rootNamespace}}.FactoryBridge.AddClient<{{name}}>(services, configureClient, configureBuilder{{chunkResilience}});
"""));

        // Registration for each class
        string SingleReg() => string.Join(Givenn.NewLine + Givenn.NewLine, names.Select(name => $$"""
    public static IHttpClientBuilder AddGeren{{name.Replace(".", "_")}}(this IServiceCollection services,
        Action<HttpClient>? configureClient = null) => global::{{rootNamespace}}.FactoryBridge.AddClient<{{name}}>(services, configureClient);
"""));
    }

    private static string UsingResilience => Givenn.NewLine + $$"""
using Microsoft.Extensions.Http.Resilience;
using Polly;
""";

    private static string ParamsWithResilience => Givenn.NewLine + $$"""
        Action<HttpClient>? configureClient = null,
        bool? useResilience = null,
        string? resiliencePipelineName = null,
        Action<ResiliencePipelineBuilder<HttpResponseMessage>, ResilienceHandlerContext>? configureResilience = null)
""";

    private static string ParamsWithoutResilience => Givenn.NewLine + $$"""
        Action<HttpClient>? configureClient = null)
""";
}
