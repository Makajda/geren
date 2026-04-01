# Geren OpenAPI Client Generator

Geren generates typed `HttpClient` clients from OpenAPI `.json` files (MSBuild `AdditionalFiles`) at compile time.
It ships as a Roslyn source generator (analyzer), so it runs during `dotnet build` and produces `.g.cs` files.

## Packages

- `Geren.OpenApiClientGenerator`
  - The source generator (as `analyzers/dotnet/cs/Geren.Client.Generator.dll`).
- `Geren.OpenApi.Server`
  - Server-side OpenAPI helpers: schema transformers for `x-compile` and `x-metadata`.
- `Geren.Server.Exporter`
  - Exporter scans the project and builds a Geren JSON specification using the Minimal API.

## Samples

See `samples/README.md` for an end-to-end sample (server generates OpenAPI JSON, Blazor WebAssembly client consumes it via `AdditionalFiles` and generates typed clients).

### Server (optional): Produce OpenAPI JSON

1. Add packages:

```powershell
dotnet add package Geren.OpenApi.Server
dotnet add package Microsoft.Extensions.ApiDescription.Server
```

2. Configure OpenAPI output (example):

```xml
<PropertyGroup>
  <OpenApiDocumentsDirectory>..</OpenApiDocumentsDirectory>
  <OpenApiGenerateDocumentsOptions>--file-name my-open-api</OpenApiGenerateDocumentsOptions>
</PropertyGroup>
```

3. Wire it up in `Program.cs`:

```csharp
builder.Services.AddOpenApi(options => options.AddSchemaTransformer<Geren.Server.Transformer>());
```

### Client: Generate the API client

1. Add your OpenAPI JSON file as an `AdditionalFiles` item:

```xml
<ItemGroup>
  <AdditionalFiles Include="..\my-open-api.json" Geren="openapi" />
</ItemGroup>
```

2. Add the generator package:

```powershell
dotnet add package Geren.OpenApiClientGenerator
```

3. Build once: `dotnet build`. Geren will generate:

- A shared `FactoryBridge` helper.
- A `...Extensions` type with `AddGerenClients(...)`.
- Typed client classes.

## Configuration

### Root Namespace

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

### JsonSerializerOptions

Default JsonSerializerOptions is JsonSerializerOptions.Web. You can override it:

```csharp
FactoryBridge.Jsop = new();
```

## Troubleshooting

### CS9137 With ASP.NET OpenAPI Source Generators
If you see `CS9137` related to `Microsoft.AspNetCore.OpenApi.SourceGenerators`, add:

```xml
<PropertyGroup>
  <InterceptorsNamespaces>$(InterceptorsNamespaces);Microsoft.AspNetCore.OpenApi.Generated</InterceptorsNamespaces>
</PropertyGroup>
```

## Reference

### Generation Contract

- Input files: `AdditionalFiles` with `Geren="openapi"` or `Geren="gerenapi"`.
- The generator emits client classes, one `Extensions` type, and one shared `FactoryBridge` helper per OpenAPI file.

### Naming Contract

- Namespace suffix is derived from the OpenAPI file name (without extension), sanitized by `ToLetterOrDigitName`.
- Final namespace is `{RootNamespace}.{NamespaceFromFileName}.{NamespaceFromSections}`.
- Path sections used for naming exclude template segments (`{...}`).

Path to class/method mapping:

- method name: `operationId ?? (methodHttp + last section)`.
- class name: `penultimate section + "Http" or RootHttp`.
- namespace: `remaining sections`
- Duplicate generated key `{SpaceName}.{ClassName}.{MethodName}` produces `GEREN006` and the endpoint is skipped.

### Diagnostics

- `GEREN001` JSON read failed (`Warning`)
- `GEREN002` OpenAPI parse failed (`Error`)
- `GEREN003` Unsupported parameter location (`Error`)
- `GEREN004` Unsupported query parameter type (`Warning`)
- `GEREN005` Unsupported request body (`Error`)
- `GEREN006` Duplicate generated method name (`Error`)
- `GEREN007` Unresolved schema reference (`Error`)
- `GEREN008` Missing parameter location (`Error`)
- `GEREN014` Ambiguous schema reference (`Error`)
- `GEREN015` Path placeholder and parameter name mismatch (`Error`)


# Geren.Server.Exporter

  - dotnet tool geren-server-exporter for exporting GerenAPI JSON from a Minimal API server project.

## Install

```powershell
dotnet tool install -g geren.server.exporter
```

## Usage

```powershell
geren-server-exporter --project .\MyServerApi.csproj --output-dir .\gerenapiresult
```

Then for `Geren.OpenApiClientGenerator` in a client project:

```xml
<AdditionalFiles Include=".\MyServerApi-gerenapi.json" Geren="gerenapi" />
```

## Important about `MapGroup` and `route template`

Group prefixes are taken **only** when they are specified by a **compile-time constant**:

```csharp
const string const_name = "stat";
app.MapGroup("stat").RequireAuthorization(...)
   .MapGroup(nameof(..)).WithTags("tag")
   .MapGroup($"{nameof(..)}/{nameof(..)}")
   .MapGroup(const_name).MapGet("item/{id:int}", ...);
```

Do not use `MapGroup(Func<string>)`, `MapGroup(MethodBase)`, custom wrapper extensions with reflection and any runtime logic for constructing the prefix—the exporter is not required to (and usually cannot) determine such prefixes.

Similar a route template.

## Warnings

The exporter writes warnings to stderr and file output.log.
Some endpoints may be skipped if the exporter could not unambiguously determine the HTTP method (for example, `MapMethods(...)` with a non-constant list of methods) - in this case, the warning `GERENEXP004` will be issued.

## Important about DI types

Default excluded types:

```csharp
System.Threading.CancellationToken
System.Security.Claims.ClaimsPrincipal
Microsoft.AspNetCore.Http.HttpContext
Microsoft.AspNetCore.Http.HttpRequest
Microsoft.AspNetCore.Http.HttpResponse
Microsoft.Extensions.Logging.ILogger
```

In the `settings.json`, you can specify additional types to exclude in the parameters.

```json
{
  "Project": "...\\MyServerApi.csproj",
  "OutputDirectory": "...\\result-dir",
  "OutputFileName": "",
  "ExcludeTypes": [
    "Microsoft.EntityFrameworkCore.DbContext"
  ]
}
```

```powershell
geren-server-exporter -s ...\settings.json
```
