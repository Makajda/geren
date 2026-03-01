# Geren OpenAPI Incremental Generator

`Geren` generates typed `HttpClient` clients from OpenAPI `.json` files (`AdditionalFiles`) at compile time.

## Quick Start

In consumer project:

```xml
<ItemGroup>
  <AdditionalFiles Include="..\v1.json" />

  <ProjectReference Include="..\Geren\Geren.csproj" ReferenceOutputAssembly="false" />
  <Analyzer Include="..\Geren\bin\$(Configuration)\netstandard2.0\Geren.dll" />
  <Analyzer Include="..\Geren\bin\$(Configuration)\netstandard2.0\Microsoft.OpenApi.Readers.dll" />
  <Analyzer Include="..\Geren\bin\$(Configuration)\netstandard2.0\Microsoft.OpenApi.dll" />
  <Analyzer Include="..\Geren\bin\$(Configuration)\netstandard2.0\SharpYaml.dll" />
</ItemGroup>
```

Optional root namespace override:

```xml
<PropertyGroup>
  <Geren_RootNamespace>MyCompany.Generated</Geren_RootNamespace>
</PropertyGroup>

<ItemGroup>
  <CompilerVisibleProperty Include="Geren_RootNamespace" />
</ItemGroup>
```

If property is not set, default root namespace is `Gereb.Generated`.

## Generation Contract

- Input files: `AdditionalFiles` with extension `.json`.
- Probe stage accepts file when top-level object contains property `openapi`.
- Parse stage uses `OpenApiStringReader`.
- For each input file generator emits:
- client classes grouped by `ClassName`
- extension class `GeneratedApiServiceCollectionExtensions`
- one shared `FactoryBridge` helper

## Naming Contract

- Namespace suffix is derived from file name without extension, sanitized by `ToLetterOrDigitName`.
- Final namespace is `{RootNamespace}.{NamespaceSuffix}`.
- Root namespace is `build_property.Geren_RootNamespace` or `Gereb.Generated` by default.
- Path sections used for naming exclude template segments (`{...}`).
- If no non-template sections: class `Root`, method `operationId ?? method + "Root"`.
- If one non-template section: class `Root`, method `operationId ?? method + section0`.
- If two or more non-template sections: class `section0`, method `operationId ?? method + join(sections[1..], "_")`.
- Duplicate generated key `{ClassName}.{MethodName}` produces `GEREN006` and endpoint is skipped.

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
- Path placeholder and path-parameter mismatch produces `GEREN015` and endpoint is skipped.

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

## Schema Type Resolution

- Primitive schema types map to C# scalar types.
- `array` maps to `System.Collections.Generic.IReadOnlyList<T>`.
- `object` or schema with properties maps to `object`.
- `$ref` is resolved against current compilation/references to fully-qualified type name.
- Unresolved `$ref` produces `GEREN007` (fallback type `object`).
- Ambiguous `$ref` (same simple type name in multiple namespaces) produces `GEREN014` and endpoint is skipped.

## Diagnostics

- `GEREN001` JSON read failed (`Warning`)
- `GEREN002` OpenAPI parse failed (`Error`)
- `GEREN003` Unsupported parameter location (`Error`)
- `GEREN004` Unsupported query parameter type (`Error`)
- `GEREN005` Unsupported request body (`Error`)
- `GEREN006` Duplicate generated method name (`Error`)
- `GEREN007` Unresolved schema reference (`Error`)
- `GEREN008` Missing parameter location (`Error`)
- `GEREN014` Ambiguous schema reference (`Error`)
- `GEREN015` Path placeholder and parameter name mismatch (`Error`)

## NuGet Pipeline

Build/pack with analyzer dependency validation:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\Invoke-NuGetPipeline.ps1
```

Enable package analysis (CI/release mode):

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\Invoke-NuGetPipeline.ps1 -EnablePackageAnalysis
```

The pipeline verifies that `.nupkg` contains:
- `analyzers/dotnet/cs/Geren.dll`
- `analyzers/dotnet/cs/Microsoft.OpenApi.Readers.dll`
- `analyzers/dotnet/cs/Microsoft.OpenApi.dll`
- `analyzers/dotnet/cs/SharpYaml.dll`
- `analyzers/dotnet/cs/System.Text.Json.dll`
- `README.md`
- `LICENSE.txt`

NuGet publish workflow:
- CI release file: `.github/workflows/release.yml`
- Trigger: push tag `v*` (for example `v0.2.0`) or manual `workflow_dispatch`
- Required repository secret: `NUGET_API_KEY`
