# Samples

This folder contains a minimal end-to-end example:

- `Server`: ASP.NET Core minimal API that can generate an OpenAPI JSON document into `samples/openapi/sample.json`.
- `Console`: Console app that includes `samples/openapi/sample.json` as `AdditionalFiles` and calls the generated client.

## Regenerate OpenAPI JSON

`samples/Server/Geren.Samples.Server.csproj` is configured to write OpenAPI JSON into `samples/openapi/sample.json`.
To refresh it, build the server project and then rebuild the client/console projects. The server does not need to be run.

# Example Code Snippets

## Register clients

```csharp
builder.Services.AddGerenClients(http => http.BaseAddress = new(ApiAddress), b => b.AddHttpMessageHandler<AuthMessageHandler>());
```

## Set JsonSerializerOptions

```csharp
FactoryBridge.Jsop = new(JsonSerializerDefaults.Web)
{
    Converters =
    {
        new JsonCustomConverter(...)
    }
};
```

