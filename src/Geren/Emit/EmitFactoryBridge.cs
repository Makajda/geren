namespace Geren.Emit;

internal sealed class EmitFactoryBridge {
    internal static string Run(bool hasResilience, string rootNamespace) {
        return $$"""
#nullable enable
using Microsoft.Extensions.DependencyInjection;{{(hasResilience ? UsingResilience : string.Empty)}}
using System;

namespace {{rootNamespace}};

internal static class Common
{
    internal static void AddClient<TClient>(
        IServiceCollection services,
        Action<HttpClient>? configureClient,{{(hasResilience ? ParamsWithResilience : ParamsWithoutResilience)}}
        where TClient : class
    {
        var builder = services.AddHttpClient<TClient>();

        if (configureClient is not null)
            builder.ConfigureHttpClient(configureClient);

{{(hasResilience ? ChunkResilience : string.Empty)}}
        configureBuilder?.Invoke(builder);
    }

    internal static IHttpClientBuilder AddClient<TClient>(
        IServiceCollection services,
        Action<HttpClient>? configureClient = null)
        where TClient : class
    {
        var builder = services.AddHttpClient<TClient>();
        if (configureClient is not null)
            builder.ConfigureHttpClient(configureClient);

        return builder;
    }
}
""";
    }

    private static string UsingResilience => Givenn.NewLine + $$"""
using Microsoft.Extensions.Http.Resilience;
using Polly;
""";

    private static string ParamsWithResilience => Givenn.NewLine + $$"""
        Action<IHttpClientBuilder>? configureBuilder,
        bool? useResilience,
        string? resiliencePipelineName,
        Action<ResiliencePipelineBuilder<HttpResponseMessage>, ResilienceHandlerContext>? configureResilience)
""";

    private static string ParamsWithoutResilience => Givenn.NewLine + $$"""
        Action<IHttpClientBuilder>? configureBuilder)
""";
    private static string ChunkResilience => $$"""
        if (useResilience ?? false)
        {
            if (resiliencePipelineName is not null && configureResilience is not null)
                builder.AddResilienceHandler(resiliencePipelineName, configureResilience);
            else
                builder.AddStandardResilienceHandler();
        }
""" + Givenn.NewLine;
}
