# Configuration

This document describes configuration points for `Geren.OpenApiClientGenerator`.

## Root Namespace

By default the root namespace is `Geren`. You can override it:

```xml
<PropertyGroup>
  <Geren_RootNamespace>MyCompany.Generated</Geren_RootNamespace>
</PropertyGroup>
```

Note: if you reference the generator via `ProjectReference`/manual `<Analyzer Include=...>`, you may also need:

```xml
<ItemGroup>
  <CompilerVisibleProperty Include="Geren_RootNamespace" />
</ItemGroup>
```

## JSON

Default JSON behavior uses `JsonSerializerDefaults.Web`.

To customize JSON serialization options globally for all generated clients, implement a partial method once:

```csharp
using System.Text.Json;

namespace MyCompany.Generated;

public abstract partial class GerenClientBase
{
    static partial void OnConfigureJsonSerializerOptions(JsonSerializerOptions options)
    {
        options.PropertyNameCaseInsensitive = true;
    }
}
```

## Request hooks

Generated clients call hooks in this order:

1. `IGerenClientRequestHooksAsync` (if registered)
2. `IGerenClientRequestHooks` (if registered)
3. `GerenClientBase.OnPrepareRequest` (if implemented)

### Static hook (`OnPrepareRequest`)

Use the static partial hook for global, non-DI customization (simple headers, feature flags, etc):

```csharp
using System.Net.Http;

namespace MyCompany.Generated;

public abstract partial class GerenClientBase
{
    static partial void OnPrepareRequest(HttpRequestMessage request)
    {
        request.Headers.Add("X-Client", "value");
    }
}
```

### DI hook (sync)

Use `IGerenClientRequestHooks` when you need scoped per-request data (for example, auth in Blazor Server/SSR):

```csharp
using System.Net.Http;

public sealed class MyHooks(TokenHelper tokenHelper) : Geren.IGerenClientRequestHooks
{
    public void PrepareRequest(HttpRequestMessage request)
    {
        string token = tokenHelper.AccessToken;
        if (!string.IsNullOrWhiteSpace(token))
            request.Headers.Authorization = new("Bearer", token);
    }
}

services.AddScoped<Geren.IGerenClientRequestHooks, MyHooks>();
services.AddGerenClients();
```

### DI hook (async)

Use `IGerenClientRequestHooksAsync` when request preparation requires awaiting asynchronous operations
(for example, token refresh in Blazor WebAssembly):

```csharp
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

public sealed class MyAsyncHooks(TokenProvider tokenProvider) : Geren.IGerenClientRequestHooksAsync
{
    public async ValueTask PrepareRequestAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        string token = await tokenProvider.GetAccessToken();
        if (!string.IsNullOrWhiteSpace(token))
            request.Headers.Authorization = new("Bearer", token);
    }
}

services.AddScoped<Geren.IGerenClientRequestHooksAsync, MyAsyncHooks>();
services.AddGerenClients();
```

