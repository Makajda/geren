namespace Geren.Emit;

internal static class EmitRegistrations {
    internal static string Run(string rootNamespace, string spaceName, IEnumerable<string> names) {
        return $$"""
#nullable enable
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using System;

namespace {{spaceName}};

public static class GeneratedExtensions
{
    public static IServiceCollection AddGerens(
        this IServiceCollection services,
        Action<IHttpClientBuilder>? configureBuilder = null,
        Action<HttpClient>? configureClient = null,
        bool useStandardOrConfigureResilience = true,
        Action<ResilienceHttpClientBuilderOptions>? configureResilience = null)
    {
{{AllReg()}}
        return services;
    }

{{SingleReg()}}
}
""";

        string AllReg() => string.Join(Givenn.NewLine, names.Select(name => $$"""
        global::{{rootNamespace}}.Common.AddClient<{{name}}>(services, configureBuilder, configureClient, useStandardResilience, configureResilience);
"""));

        // Registration for each class
        string SingleReg() => string.Join(Givenn.NewLine + Givenn.NewLine, names.Select(name => $$"""
    public static IHttpClientBuilder AddGeren{{name.Replace(".", "_")}}(this IServiceCollection services,
        Action<HttpClient>? configureClient = null) => global::{{rootNamespace}}.Common.AddClient<{{name}}>(configureClient);
"""));
    }
}
