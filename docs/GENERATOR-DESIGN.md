# Generator design (Geren.Client.Generator)

This document describes the internal design of the client source generator.

## Inputs

The generator reads MSBuild `AdditionalFiles`, but **only** files explicitly opted in via:

- `build_metadata.AdditionalFiles.Geren = openapi` (OpenAPI input)
- `build_metadata.AdditionalFiles.Geren = gerenapi` (exporter output)

Non-opted-in `AdditionalFiles` must be skipped silently to avoid noise from unrelated generators/files.

The root namespace is read from:

- `build_property.Geren_RootNamespace` (defaults to `Geren`)

## Pipeline overview

High level:

1. **Parse** additional file into a normalized endpoint model (`Purpoint`).
2. **Map** the model into emitted-shape endpoints (`Mapoint`) with resolved C# type names.
3. **Emit** sources:
   - `_FactoryBridge.g.cs` (shared helpers + base type for hooks)
   - `_*Extensions.g.cs` (DI registration)
   - `*.g.cs` clients
   - `_UnresolvedTypes.g.cs` (only when needed)

The generator is incremental:

- it filters opted-in `AdditionalFiles` early
- it emits deterministic outputs with stable ordering and stable hint names

## Determinism / stable outputs

- EOL is normalized to `\n` when emitting sources.
- When more than one document is present, a hash-based hint prefix is used to avoid hint collisions across documents.
- Unresolved placeholder types are sorted (kind → requested → placeholder name) to keep diff noise low.

## Global hooks (request + JSON)

Generated clients derive from a generated base type (`GerenClientBase`) that provides:

- a shared `JsonSerializerOptions` instance (created once)
- a global request hook called for every generated request

Consumers can implement these hooks once per document via partial methods on `GerenClientBase`.

## Tests

Besides unit tests, the repo uses **snapshot/golden tests** to prevent accidental changes to generated sources.

- Snapshots live under `tests\Geren.Client.Generator.Tests\Snapshots\`.
- To update snapshots after an intentional change:

```powershell
$env:GEREN_UPDATE_SNAPSHOTS = "1"
pwsh scripts\Invoke-Tests.ps1 -Configuration Release -NoRestore
Remove-Item Env:\GEREN_UPDATE_SNAPSHOTS
```

