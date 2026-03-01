# Geren Release Checklist

## Build and Generation

- [ ] `dotnet clean` was executed for solution/projects under `Src`.
- [ ] `dotnet build` succeeds for `Geren` (`0 errors`).
- [ ] `dotnet build` succeeds for consumer (`Gereb`) (`0 errors`).
- [ ] Generated files exist under `obj/Generated` in consumer project.
- [ ] Generated DI file exists (`GeneratedApiServiceCollectionExtensions.g.cs`).
- [ ] `FactoryBridge.g.cs` exists and is generated with configured root namespace.

## Diagnostics

- [ ] Missing OpenAPI file produces expected diagnostic.
- [ ] Invalid JSON produces expected diagnostic.
- [ ] Parse failure produces expected diagnostic.
- [ ] Unsupported parameter location (`header`/`cookie`) produces `GEREN003`.
- [ ] Missing parameter location (`parameter.in` is null) produces `GEREN008`.
- [ ] Unsupported query parameter type produces `GEREN004`.
- [ ] Unsupported request body media type produces `GEREN005`.
- [ ] Duplicate `ClassName.MethodName` produces error `GEREN006`.
- [ ] Missing shared DTO type for `$ref` produces `GEREN007`.
- [ ] Ambiguous shared DTO type for `$ref` produces `GEREN014` and endpoint is skipped.
- [ ] Path placeholder and `in:path` parameter mismatch produces `GEREN015` and endpoint is skipped.

## Naming Contract

- [ ] Namespace suffix is derived from OpenAPI file name (without extension).
- [ ] Default root namespace is `Gereb.Generated`.
- [ ] Consumer project exposes custom property via `<CompilerVisibleProperty Include="Geren_RootNamespace" />` when override is used.
- [ ] Root namespace override via `build_property.Geren_RootNamespace` works.
- [ ] Final namespace is `{RootNamespace}.{NamespaceSuffix}`.
- [ ] Path naming excludes only template sections (`{...}`) and keeps other path segments.
- [ ] If `sections.Length == 0`: class `Root`, method `operationId ?? method + Root`.
- [ ] If `sections.Length == 1`: class `Root`, method `operationId ?? method + sections[0]`.
- [ ] If `sections.Length >= 2`: class `sections[0]`, method `operationId ?? method + join(sections[1..], '_')`.
- [ ] Name sanitization replaces non-alnum with `_`, enforces valid first character, uppercases first character.
- [ ] Name collisions produce `GEREN006` error and duplicated endpoint is skipped.

## Determinism and Stability

- [ ] Two consecutive builds without changes produce stable generated output (no random diff churn).
- [ ] Namespace suffix is stable for same file name input.

## HTTP Coverage

- [ ] `GET` generation works.
- [ ] `POST` generation works (`application/json` body).
- [ ] `PUT` generation works (`application/json` body).
- [ ] `DELETE` generation works without body.
- [ ] `DELETE` generation works with `application/json` body.
- [ ] `DELETE` generation works with `text/plain` body.
- [ ] JSON body with schema `object` maps to method argument type `object`.
- [ ] JSON body with schema `array` maps to `IReadOnlyList<T>`.
- [ ] JSON body with schema `$ref` maps to referenced shared DTO type.
- [ ] `204/void` responses do not attempt to deserialize body.
- [ ] Query scalar values are URL-encoded.
- [ ] Query `bool` is serialized as lowercase `true/false`.
- [ ] Path placeholders are URL-encoded and invariant-culture formatted.

## OpenAPI Parameters

- [ ] Effective operation parameters merge `path.Parameters` and `operation.Parameters`.
- [ ] Operation-level parameter overrides path-level parameter by key `(name, in)`.

## DI and Runtime Wiring

- [ ] `AddGeneratedApiClients(...)` works.
- [ ] `AddGeneratedApiClientsFromFactory("name")` works (server scenario).
- [ ] `AddApi{Group}Client(...)` works for targeted registration.
- [ ] `AddApi{Group}ClientFromFactory("name")` works for targeted factory usage.

## Analyzer Metadata and Release Tracking

- [ ] `AnalyzerReleases.Unshipped.md` is updated for new/changed diagnostics.
- [ ] `AnalyzerReleases.Shipped.md` has valid headers/format.
- [ ] No unexpected `RS2001/RS2007/RS2008` warnings.

## Packaging

- [ ] `dotnet pack` succeeds for `Geren`.
- [ ] `.nupkg` contains:
- [ ] `analyzers/dotnet/cs/Geren.dll`
- [ ] `analyzers/dotnet/cs/Microsoft.OpenApi.Readers.dll`
- [ ] `analyzers/dotnet/cs/Microsoft.OpenApi.dll`
- [ ] `analyzers/dotnet/cs/SharpYaml.dll`
- [ ] `analyzers/dotnet/cs/System.Text.Json.dll`
- [ ] `analyzers/dotnet/cs/Microsoft.CodeAnalysis.Analyzers.dll`
- [ ] `analyzers/dotnet/cs/Microsoft.CodeAnalysis.dll`
- [ ] `analyzers/dotnet/cs/Microsoft.Bcl.AsyncInterfaces.dll`
- [ ] `analyzers/dotnet/cs/System.Buffers.dll`
- [ ] `analyzers/dotnet/cs/System.Collections.Immutable.dll`
- [ ] `analyzers/dotnet/cs/System.IO.Pipelines.dll`
- [ ] `analyzers/dotnet/cs/System.Memory.dll`
- [ ] `analyzers/dotnet/cs/System.Numerics.Vectors.dll`
- [ ] `analyzers/dotnet/cs/System.Reflection.Metadata.dll`
- [ ] `analyzers/dotnet/cs/System.Runtime.CompilerServices.Unsafe.dll`
- [ ] `analyzers/dotnet/cs/System.Text.Encoding.CodePages.dll`
- [ ] `analyzers/dotnet/cs/System.Text.Encodings.Web.dll`
- [ ] `analyzers/dotnet/cs/System.Threading.Tasks.Extensions.dll`
  - [ ] package README is included.
  - [ ] package LICENSE is included.
  - [ ] `scripts/Invoke-NuGetPipeline.ps1` passes.
  - [ ] CI/release run `Invoke-NuGetPipeline.ps1 -EnablePackageAnalysis`.
- [ ] README quick start promotes PackageReference and documents legacy analyzer includes as dev-only.

  ## Mandatory Smoke Test Project

- [ ] A fresh test project (clean environment) installs the package and generates code successfully.
- [ ] No manual `Analyzer Include` hacks are required in the test project.
- [ ] Expected diagnostics are shown in IDE/build for negative inputs.
