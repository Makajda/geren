# Samples

This folder contains a minimal end-to-end example:

- `Server`: ASP.NET Core minimal API that can generate an OpenAPI JSON document into `samples/openapi/sample.json`.
- `Console`: Console app that includes `samples/openapi/sample.json` as `AdditionalFiles` and calls the generated client.

## Build Prerequisites

- .NET SDK 10.x installed.
- Local NuGet packages built into `artifacts/nuget`.

Build packages first:

```powershell
powershell -ExecutionPolicy Bypass -File ..\scripts\Invoke-NuGetPipeline.ps1 -Configuration Release -OutputDir ..\artifacts\nuget
```

## Build The Samples

From repository root:

```powershell
dotnet build .\samples\Geren.Samples.slnx -nologo --configfile .\samples\NuGet.config
```

## Run

Run the server:

```powershell
dotnet run --project .\samples\Server\Geren.Samples.Server.csproj --urls http://localhost:5000
```

Run the client:

```powershell
dotnet run --project .\samples\Console\Geren.Samples.Console.csproj
```

## Notes

- The client uses `samples/openapi/sample.json` (committed) so it can build even before the server is run.

## Regenerate OpenAPI JSON

`samples/Server/Geren.Samples.Server.csproj` is configured to write OpenAPI JSON into `samples/openapi/sample.json`.
To refresh it, build or run the server project and then rebuild the client/console projects.

## Using NuGet.org Instead Of Local Artifacts

This repository sample is optimized for contributors (it can consume packages built from the current branch).
If you want to build the samples against published packages from nuget.org:

1. Remove or rename `samples/NuGet.config`.
2. Ensure the `PackageReference` versions in `samples/Server/Geren.Samples.Server.csproj` and `samples/Client/Geren.Samples.Client.csproj` exist on nuget.org.
3. Build the samples normally:

```powershell
dotnet build .\samples\Geren.Samples.slnx -nologo
```

### Example Code Snippets

## Register clients

```csharp
builder.Services.AddGerenClients(http => http.BaseAddress = new(ApiAddress), b => b.AddHttpMessageHandler<TAuthMessageHandler>());
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

