# Geren OpenAPI Client Generator

Geren generates typed `HttpClient` clients from OpenAPI `.json` files (MSBuild `AdditionalFiles`) at compile time.
It ships as a Roslyn source generator (analyzer), so it runs during `dotnet build` and produces `.g.cs` files.

## Packages

- `Geren.OpenApiClientGenerator`
  - The source generator (as `analyzers/dotnet/cs/Geren.Generator.dll`).
  - Dependencies required by the generated code (for example `Microsoft.Extensions.Http` and `Microsoft.Extensions.Http.Resilience`).
  - Recommended to reference as `PrivateAssets="all"` so it does not flow to downstream packages.
- `Geren.OpenApi.Server`
  - Server-side OpenAPI helpers: schema transformers for `x-compile` and `x-metadata`.
  - Targets `net10.0`.

## Prerequisites

- An SDK-style .NET project.
- An OpenAPI JSON file in the client project (OpenAPI 3.x).
- If you use `Geren.OpenApi.Server`, you need a `net10.0` server project.

## Quick Start

## Samples

See `samples/README.md` for an end-to-end sample (server generates OpenAPI JSON, Blazor WebAssembly client consumes it via `AdditionalFiles` and generates typed clients).

### Server (optional): Produce OpenAPI JSON

1. Add packages:

```xml
<ItemGroup>
  <PackageReference Include="Geren.OpenApi.Server" Version="0.2.7" />
  <PackageReference Include="Microsoft.Extensions.ApiDescription.Server" Version="10.0.3" PrivateAssets="all" />
</ItemGroup>
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
builder.Services.AddOpenApi(options =>
  options.AddSchemaTransformer<Geren.Server.Transformer>());

app.MapOpenApi();
```

### Client: Generate the API client

1. Add your OpenAPI JSON file as an `AdditionalFiles` item:

```xml
<ItemGroup>
  <AdditionalFiles Include="..\my-open-api.json" />
</ItemGroup>
```

2. Add the generator package (recommended as a private asset):

```xml
<ItemGroup>
  <PackageReference Include="Geren.OpenApiClientGenerator" Version="0.2.7" PrivateAssets="all" />
</ItemGroup>
```

3. Build once: `dotnet build`. Geren will generate:

- A shared `FactoryBridge` helper.
- A `...Extensions` type with `AddGerenClients(...)`.
- Typed client classes grouped by OpenAPI path sections.

Typical usage is to register generated clients in DI:

```csharp
// The exact namespace/type depends on the OpenAPI file name.
// For example "petstore.json" typically produces something under "Geren.Petstore".
services.AddGerenClients();
```

## Configuration

### Root Namespace

By default the root namespace is `Geren`. You can override it:

```xml
<PropertyGroup>
  <Geren_RootNamespace>MyCompany.Generated</Geren_RootNamespace>
</PropertyGroup>

<ItemGroup>
  <CompilerVisibleProperty Include="Geren_RootNamespace" />
</ItemGroup>
```

## Troubleshooting

### Nothing Is Generated

- The file must be `AdditionalFiles` and end with `.json`.
- The JSON root object must contain the top-level property `openapi`.
- Run `dotnet build` (the generator runs at compile time).

### CS9137 With ASP.NET OpenAPI Source Generators

If you see `CS9137` related to `Microsoft.AspNetCore.OpenApi.SourceGenerators`, add:

```xml
<PropertyGroup>
  <InterceptorsNamespaces>$(InterceptorsNamespaces);Microsoft.AspNetCore.OpenApi.Generated</InterceptorsNamespaces>
</PropertyGroup>
```

## Reference

### Generation Contract (High Level)

- Input files: `AdditionalFiles` with extension `.json`.
- A file is accepted when the top-level JSON object contains property `openapi`.
- Parsing is done via `Microsoft.OpenApi`.
- The generator emits client classes, one `Extensions` type, and one shared `FactoryBridge` helper per OpenAPI file.

### Naming Contract (High Level)

- Namespace suffix is derived from the OpenAPI file name (without extension), sanitized by `ToLetterOrDigitName`.
- Final namespace is `{RootNamespace}.{NamespaceSuffix}.{section0}` (section rules below).
- Path sections used for naming exclude template segments (`{...}`).

Path to class/method mapping:

- If no non-template sections: class `WebApiClient`, method `operationId ?? method + "Root"`.
- If one non-template section: class `WebApiClient`, method `operationId ?? method + section0`.
- If two non-template sections: class `section0`, method `operationId ?? method + section1`.
- If three or more non-template sections: namespace `section0`, class `section1`, method `operationId ?? method + join(sections[2..], "_")`.
- Duplicate generated key `{SpaceName}.{ClassName}.{MethodName}` produces `GEREN006` and the endpoint is skipped.

### HTTP, Parameters, Serialization

- Supported HTTP methods: `GET`, `POST`, `PUT`, `DELETE`.
- Supported parameter locations: `path`, `query`.
- Supported query types: `string`, `int`, `long`, `bool`, `double`.
- Query values are URL encoded.
- `bool` query values are emitted as lowercase `true` or `false`.
- Path placeholders are URL encoded with invariant formatting.

### Request/Response Mapping

- Request body media types: `application/json`, `text/plain`.
- `POST`/`PUT`/`DELETE` support body when schema is supported.
- `DELETE` with body uses `HttpRequestMessage(HttpMethod.Delete, ...)` and `SendAsync`.
- Return type selection prefers the first available `2xx` response (`200`, `201`, `202`, then other `2xx`), then falls back to explicit codes (`200`, `201`, `default`).
- `text/plain` response maps to `string`.
- Empty/unknown response content maps to no return body (`Task`).

### Diagnostics

- `GEREN001` JSON read failed (`Warning`)
- `GEREN002` OpenAPI parse failed (`Error`)
- `GEREN003` Unsupported parameter location (`Error`)
- `GEREN004` Unsupported query parameter type (`Error`)
- `GEREN005` Unsupported request body (`Error`)
- `GEREN006` Duplicate generated method name (`Error`)
- `GEREN007` Unresolved schema reference (`Error`)
- `GEREN008` Missing parameter location
- `GEREN014` Ambiguous schema reference (`Error`)
- `GEREN015` Path placeholder and parameter name mismatch (`Error`)

## NuGet Pipeline

Build/pack both packages with package content validation:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\Invoke-NuGetPipeline.ps1
```

Enable package analysis (CI/release mode):

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\Invoke-NuGetPipeline.ps1 -EnablePackageAnalysis
```

The pipeline validates these package layouts:

- `Geren.OpenApi.Server`: `lib/net10.0/Geren.Server.dll`, `README.md`, `LICENSE.txt`
- `Geren.OpenApiClientGenerator`: `analyzers/dotnet/cs/Geren.Generator.dll`, `analyzers/dotnet/cs/Microsoft.OpenApi.dll`, `README.md`, `LICENSE.txt`
