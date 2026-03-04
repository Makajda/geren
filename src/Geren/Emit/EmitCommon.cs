namespace Geren.Emit;

internal sealed class EmitCommon {
    internal static string Run(string rootNamespace) {
        return $$"""
#nullable enable
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using Polly;
using System;

namespace {{rootNamespace}};

internal static class Common
{
    internal static void AddClient<TClient>(
        IServiceCollection services,
        Action<HttpClient>? configureClient,
        Action<IHttpClientBuilder>? configureBuilder,
        bool? useResilience,
        string? resiliencePipelineName,
        Action<ResiliencePipelineBuilder<HttpResponseMessage>,ResilienceHandlerContext>? configureResilience)
        where TClient : class
    {
        var builder = services.AddHttpClient<TClient>();

        if (configureClient is not null)
            builder.ConfigureHttpClient(configureClient);

        if (useResilience ?? false)
        {
            if (resiliencePipelineName is not null && configureResilience is not null)
                builder.AddResilienceHandler(resiliencePipelineName, configureResilience);
            else
                builder.AddStandardResilienceHandler();
        }

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
}
