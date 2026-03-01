#nullable enable
using System;
using System.Net.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Gereb.Generated.V1;

public static class GeneratedApiServiceCollectionExtensions
{
    public static IServiceCollection AddGeneratedApiClients(this IServiceCollection services,
        Func<IServiceProvider, HttpClient>? httpClientFactory = null)
    {
        httpClientFactory ??= static sp => sp.GetRequiredService<HttpClient>();
        services.AddScoped<Root>(sp => new Root(httpClientFactory(sp)));
        return services;
    }

    public static IServiceCollection AddGeneratedApiClientsFromFactory(this IServiceCollection services, string clientName)
        => services.AddGeneratedApiClients(sp => global::Gereb.Generated.FactoryBridge.CreateClientFromFactory(sp, clientName));

    // Registration for Root
    public static IServiceCollection AddRootClient(
        this IServiceCollection services,
        Func<IServiceProvider, HttpClient>? httpClientFactory = null)
    {
        httpClientFactory ??= static sp => sp.GetRequiredService<HttpClient>();
        services.AddScoped<Root>(sp => new Root(httpClientFactory(sp)));
        return services;
    }

    public static IServiceCollection AddRootClientFromFactory(this IServiceCollection services, string clientName)
        => services.AddRootClient(sp => global::Gereb.Generated.FactoryBridge.CreateClientFromFactory(sp, clientName));
}
