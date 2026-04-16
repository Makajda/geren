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

## Design docs (internal)

- `docs\FORMAT.md`
- `docs\GENERATOR-DESIGN.md`
- `docs\EXPORTER-DESIGN.md`

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

See `docs\CONFIGURATION.md`.

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

`Geren.Server.Exporter` is a `dotnet tool` that exports `*.gerenapi.json` from an ASP.NET Core Minimal API server project (without running it).
See `src\Geren.Server.Exporter\README.md` and `docs\EXPORTER-DESIGN.md`.
