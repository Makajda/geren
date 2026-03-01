#nullable enable
using System;
using System.Net.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Gereb.Generated;

internal static class FactoryBridge
{
    internal static HttpClient CreateClientFromFactory(IServiceProvider sp, string clientName)
    {
        var factoryType = Type.GetType("System.Net.Http.IHttpClientFactory, Microsoft.Extensions.Http", throwOnError: false);
        if (factoryType is null)
        {
            throw new InvalidOperationException("IHttpClientFactory type not found. Add package Microsoft.Extensions.Http.");
        }

        var factory = sp.GetService(factoryType);
        if (factory is null)
        {
            throw new InvalidOperationException("IHttpClientFactory service is not registered. Call services.AddHttpClient().");
        }

        var createClient = factoryType.GetMethod("CreateClient", new[] { typeof(string) });
        if (createClient is null)
        {
            throw new InvalidOperationException("IHttpClientFactory.CreateClient(string) method was not found.");
        }

        var client = createClient.Invoke(factory, new object[] { clientName }) as HttpClient;
        if (client is null)
        {
            throw new InvalidOperationException("IHttpClientFactory.CreateClient returned null.");
        }

        return client;
    }
}
