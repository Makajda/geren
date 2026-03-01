namespace Geren.Emit;

internal sealed class EmitRegistrations {
    internal static string Run(string rootNamespace, string namespaceSuffix, IEnumerable<string> classNames) {
        return $$"""
#nullable enable
using System;
using System.Net.Http;
using Microsoft.Extensions.DependencyInjection;

namespace {{rootNamespace}}.{{namespaceSuffix}};

public static class GeneratedApiServiceCollectionExtensions
{
    public static IServiceCollection AddGeneratedApiClients(this IServiceCollection services,
        Func<IServiceProvider, HttpClient>? httpClientFactory = null)
    {
        httpClientFactory ??= static sp => sp.GetRequiredService<HttpClient>();
{{AllReg(classNames)}}
        return services;
    }

    public static IServiceCollection AddGeneratedApiClientsFromFactory(this IServiceCollection services, string clientName)
        => services.AddGeneratedApiClients(sp => global::{{rootNamespace}}.FactoryBridge.CreateClientFromFactory(sp, clientName));

{{SingleReg(rootNamespace, classNames)}}
}
""";
    }

    // Registration all classes
    private static string AllReg(IEnumerable<string> names) => string.Join(Givenn.NewLine, names.Select(className => $$"""
        services.AddScoped<{{className}}>(sp => new {{className}}(httpClientFactory(sp)));
"""));

    // Registration for each class
    private static string SingleReg(string rootNamespace, IEnumerable<string> names) => string.Join(Givenn.NewLine + Givenn.NewLine,
        names.Select(className => $$"""
    // Registration for {{className}}
    public static IServiceCollection Add{{className}}Client(
        this IServiceCollection services,
        Func<IServiceProvider, HttpClient>? httpClientFactory = null)
    {
        httpClientFactory ??= static sp => sp.GetRequiredService<HttpClient>();
        services.AddScoped<{{className}}>(sp => new {{className}}(httpClientFactory(sp)));
        return services;
    }

    public static IServiceCollection Add{{className}}ClientFromFactory(this IServiceCollection services, string clientName)
        => services.Add{{className}}Client(sp => global::{{rootNamespace}}.FactoryBridge.CreateClientFromFactory(sp, clientName));
"""));
}
