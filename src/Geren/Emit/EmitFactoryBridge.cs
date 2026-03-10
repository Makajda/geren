namespace Geren.Emit;

internal sealed class EmitFactoryBridge {
    internal static string Run(string rootNamespace) {
        return $$"""
#nullable enable
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using Polly;
using System.Globalization;

namespace {{rootNamespace}};

internal static class FactoryBridge
{
    internal static void AddClient<TClient>(
        IServiceCollection services,
        Action<HttpClient>? configureClient,
        Action<IHttpClientBuilder>? configureBuilder,
        bool? useResilience,
        string? resiliencePipelineName,
        Action<ResiliencePipelineBuilder<HttpResponseMessage>, ResilienceHandlerContext>? configureResilience)
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

{{EmitHelpers()}}
}
""";
    }

    private static string EmitHelpers() => $$"""
    internal static string BuildRequestUri(string path, Action<List<string>>? configureQuery = null)
    {
        if (configureQuery is null)
            return path;

        var query = new List<string>();
        configureQuery(query);
        return query.Count == 0 ? path : path + "?" + string.Join("&", query);
    }

    // AddQueryParameter
    internal static void A(List<string> query, string name, object? value)
    {
        if (value is null)
            return;

        if (value is System.Collections.IEnumerable values && value is not string)
        {
            foreach (var item in values)
                A(query, name, item);

            return;
        }

        query.Add(name + "=" + V(value));
    }

    // FormatPathValue
    internal static string V(object? value)=> Uri.EscapeDataString(
        value switch
        {
            bool boolean => boolean ? "true" : "false",
            IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
            _ => Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty
        });
""";
}
