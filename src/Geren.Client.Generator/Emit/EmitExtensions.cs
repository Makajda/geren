namespace Geren.Client.Generator.Emit;

internal static class EmitExtensions {
    internal static string Run(string rootNamespace, string namespaceFromFile, string spaceName, IEnumerable<string> names) {
        return $$"""
#nullable enable
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using Polly;
using System;

namespace {{spaceName}};

public static class {{namespaceFromFile}}Extensions
{
    public static IServiceCollection AddGerenClients(
        this IServiceCollection services,
        Action<IHttpClientBuilder>? configureBuilder = null,
        Action<HttpClient>? configureClient = null,
        bool? useResilience = null,
        string? resiliencePipelineName = null,
        Action<ResiliencePipelineBuilder<HttpResponseMessage>, ResilienceHandlerContext>? configureResilience = null) {
{{AllReg()}}
        return services;
    }

{{SingleReg()}}
}
""";

        string AllReg() => string.Join(Givencg.NewLine, names.Select(name => $$"""
        global::{{rootNamespace}}.FactoryBridge.AddClient<{{name}}>(services, configureClient, configureBuilder, useResilience, resiliencePipelineName, configureResilience);
"""));

        // Registration for each class
        string SingleReg() => string.Join(Givencg.NewLine + Givencg.NewLine, names.Select(name => $$"""
    public static IHttpClientBuilder AddGeren{{name.Replace(".", "_")}}(this IServiceCollection services,
        Action<HttpClient>? configureClient = null) => global::{{rootNamespace}}.FactoryBridge.AddClient<{{name}}>(services, configureClient);
"""));
    }
}
