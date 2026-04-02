# Geren.Server.Exporter

`Geren.Server.Exporter` is a `dotnet tool` that scans an ASP.NET Core Minimal API project and exports a custom JSON specification (`*.gerenapi.json`) for later client generation.

It does **not** run your server and does **not** generate OpenAPI.

## Install

```powershell
dotnet tool install -g Geren.Server.Exporter
```

## Quickstart

```powershell
geren-server-exporter --project .\MyServerApi.csproj --output-dir .\gerenapi
```

## Output

- `<ProjectName>.gerenapi.json` in `--output-dir`
- `<ProjectName>.gerenapi.log` (warnings copy) in `--output-dir`
- Warnings are also written to stderr

## Settings file (optional)

```json
{
  "Project": "C:\\path\\to\\MyServerApi.csproj",
  "OutputDirectory": "C:\\path\\to\\gerenapi",
  "OutputFileName": "",
  "ExcludeTypes": [
    "Microsoft.EntityFrameworkCore.DbContext"
  ],
  "Configuration": "Release",
  "Platform": "AnyCPU"
}
```

```powershell
geren-server-exporter -s .\settings.json
```

## Consume in a client project

1. Add the exported file as `AdditionalFiles`:

```xml
<ItemGroup>
  <AdditionalFiles Include="..\gerenapi\MyServerApi.gerenapi.json" Geren="gerenapi" />
</ItemGroup>
```

2. Install the generator:

```powershell
dotnet add package Geren.OpenApiClientGenerator
```

3. Build once: `dotnet build`

## Important limitations

- `MapGroup(...)` prefixes are detected only when the prefix is a **compile-time constant string** (`"stat"`, `nameof(...)`, etc.).
- Avoid `MapGroup(Func<string>)`, reflection-based wrappers, and any runtime logic for constructing prefixes.
- Similarly, a route template must be a compile-time constant string.
- If the HTTP method cannot be determined (for example, `MapMethods(...)` with a non-constant method list), the endpoint is skipped and a warning is emitted.

## DI parameters and excluded types

The exporter tries to infer which handler parameters are “real API parameters”.

It excludes common DI/system parameters by default:

```csharp
System.Threading.CancellationToken
System.Security.Claims.ClaimsPrincipal
Microsoft.AspNetCore.Http.HttpContext
Microsoft.AspNetCore.Http.HttpRequest
Microsoft.AspNetCore.Http.HttpResponse
Microsoft.Extensions.Logging.ILogger
```

You can add your own excluded types via `ExcludeTypes` in `settings.json` (fully-qualified type names).

Tip: run `geren-server-exporter --help` to see available options and exit codes.
