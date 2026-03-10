# Geren OpenAPI Incremental Generator for Blazor

`Geren` generates typed `HttpClient` clients from OpenAPI `.json` files (`AdditionalFiles`) at compile time.

## Quick Start

For a simple example and a quick start, place the server, client and SharedDto project folders in the same folder.

In consumer server project:
OpenApi extensions with x-compile and x-metadata schema transformers
```xml
<PropertyGroup>
	<OpenApiDocumentsDirectory>..</OpenApiDocumentsDirectory>
	<OpenApiGenerateDocumentsOptions>--file-name my-open-api</OpenApiGenerateDocumentsOptions>
</PropertyGroup>
```

```xml
<ItemGroup>
    <ProjectReference Include="..\SharedDto\SharedDto.csproj" />
	<PackageReference Include="Geren.OpenApi.Server" Version="0.2.1" />

	<PackageReference Include="Microsoft.Extensions.ApiDescription.Server" Version="10.0.3">
		<PrivateAssets>all</PrivateAssets>
		<IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
	</PackageReference>
</ItemGroup>
```

`Geren.OpenApi.Server` brings `Microsoft.AspNetCore.OpenApi` transitively.

In Program.cs

```
builder.Services.AddOpenApi(options => options.AddSchemaTransformer<Geren.Server.Transformer>());
```
```
app.MapOpenApi();
```

In consumer client project prefer the packaged analyzer and only surface your OpenAPI files:

```xml
<ItemGroup>
    <AdditionalFiles Include="..\my-open-api.json" />
    <ProjectReference Include="..\SharedDto\SharedDto.csproj" />
    <PackageReference Include="Geren.OpenApiClientGenerator" Version="0.2.1" PrivateAssets="all" />
</ItemGroup>
```

Optionally override the generated namespace:

```xml
<PropertyGroup>
  <Geren_RootNamespace>MyCompany.Generated</Geren_RootNamespace>
</PropertyGroup>

<ItemGroup>
  <CompilerVisibleProperty Include="Geren_RootNamespace" />
</ItemGroup>
```

If the property is not set, the default root namespace is `Geren`.

## Generation Contract

- Input files: `AdditionalFiles` with extension `.json`.
- Probe stage accepts file when top-level object contains property `openapi`.
- Parse stage uses `Microsoft.OpenApi`.
- For each input file generator emits:
- client classes grouped by `ClassName`
- extension class `Extensions`.
- One shared `FactoryBridge` helper

## Naming Contract

- Namespace suffix is derived from file name without extension, sanitized by `ToLetterOrDigitName`.
- Final namespace is `{RootNamespace}.{NamespaceSuffix}.{section0}`.
- Root namespace is `build_property.Geren_RootNamespace` or `Geren` by default.
- Path sections used for naming exclude template segments (`{...}`).
- If no non-template sections: class `WebApiClient`, method `operationId ?? method + "Root"`.
- If one non-template section: class `WebApiClient`, method `operationId ?? method + section0`.
- If two non-template sections: class `section0`, method `operationId ?? method + sections1`.
- If three or more non-template sections: .namespace `section0`, class `section1`, method `operationId ?? method + join(sections[2..], "_")`.
- Duplicate generated key `{SpaceName}.{ClassName}.{MethodName}` produces `GEREN006` and endpoint is skipped.

## HTTP, Parameters, and Serialization

- Supported HTTP methods: `GET`, `POST`, `PUT`, `DELETE`.
- Effective parameters are merged from `path.Parameters` and `operation.Parameters`.
- Merge key is `(name, in)`, operation-level value overrides path-level value.
- Supported parameter locations: `path`, `query`.
- Unsupported locations (`header`, `cookie`, other) produce `GEREN003`.
- Missing parameter location (`parameter.in == null`) produces `GEREN008`.
- Supported query types: `string`, `int`, `long`, `bool`, `double`.
- Unsupported query types produce `GEREN004`.
- Query values are URL encoded.
- `bool` query values are emitted as lowercase `true` or `false`.
- Path placeholders are serialized with `Convert.ToString(value, InvariantCulture)` and URL encoded.
- Path placeholder and path-parameter mismatch produces `GEREN015`.

## Request/Response Mapping

- Request body media types: `application/json`, `text/plain`.
- Unsupported request body media type produces `GEREN005`.
- `POST`/`PUT`/`DELETE` support body when schema is supported.
- `DELETE` with body is emitted via `HttpRequestMessage(HttpMethod.Delete, ...)` and `SendAsync`.
- Return type is selected from responses with priority:
- first available `2xx` (priority `200`, `201`, `202`, then other `2xx`)
- fallback by explicit code lookup: `200`, `201`, `default`
- `text/plain` response maps to `string`
- empty/unknown response content maps to no return body (`Task`)

## Diagnostics

- `GEREN001` JSON read failed (`Warning`)
- `GEREN002` OpenAPI parse failed (`Error`)
- `GEREN003` Unsupported parameter location (`Error`)
- `GEREN004` Unsupported query parameter type (`Error`)
- `GEREN005` Unsupported request body (`Error`)
- `GEREN006` Duplicate generated method name (`Error`)
- `GEREN007` Unresolved schema reference (`Error`)
- `GEREN008` Missing parameter location
- `GEREN009` Missing package Microsoft.Extensions.Http (`Error`)
- `GEREN010` Missing package Microsoft.Extensions.Http.Resilience (`Warning`)
- `GEREN014` Ambiguous schema reference (`Error`)
- `GEREN015` Path placeholder and parameter name mismatch (`Error`)

## NuGet Packages

The repository publishes two NuGet packages:

- `Geren.OpenApiClientGenerator`
- `Geren.OpenApi.Server`

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

- `Geren.OpenApiClientGenerator`: `analyzers/dotnet/cs/Geren.dll`, `analyzers/dotnet/cs/Microsoft.OpenApi.dll`, `README.md`, `LICENSE.txt`
- `Geren.OpenApi.Server`: `lib/net10.0/Geren.Server.dll`, `README.md`, `LICENSE.txt`
