namespace Geren.Emit;

internal sealed class EmitCommon {
    internal static string Run(string rootNamespace) {
        return $$"""
#nullable enable
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using System;

namespace {{rootNamespace}};

internal static class Common
{
    internal static void AddClient<TClient>(
        IServiceCollection services,
        Action<HttpClient>? configureClient,
        Action<IHttpClientBuilder>? configureBuilder,
        bool useStandardResilience,
        Action<ResilienceHttpClientBuilderOptions>? configureResilience)
        where TClient : class
    {
        var builder = services.AddHttpClient<TClient>();

        if (configureClient is not null)
            builder.ConfigureHttpClient(configureClient);

        if (useStandardResilience)
        {
            if (configureResilience is not null)
                builder.AddStandardResilienceHandler(configureResilience);
            else
                builder.AddStandardResilienceHandler();
        }

        configureBuilder?.Invoke(builder);
    }

    internal static IHttpClientBuilder AddClient<TClient>(Action<HttpClient>? configureClient = null)
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
