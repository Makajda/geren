# Formats

This repo uses two related “contracts”:

- **OpenAPI + Geren extensions** (`x-metadata`, `x-compile`) — produced on the server side.
- **GerenAPI JSON** (`*.gerenapi.json`) — produced by `Geren.Server.Exporter` and consumed by the client generator.

The contracts are designed to provide *unambiguous type identities* for code generation.

## OpenAPI schema extensions

`Geren.OpenApi.Server` can attach extra data to `OpenApiSchema.Extensions`:

- `x-metadata`: a stable CLR metadata name (for non-generic, non-array types), for example `MyApp.Dto.Pet`.
- `x-compile`: a fully-qualified, compilable type name (for arrays and generic types), for example `global::MyApp.Dto.Paged<global::MyApp.Dto.Pet>`.

The client generator prefers these extensions when mapping OpenAPI schemas to C# types.

## GerenAPI JSON (exporter output)

`Geren.Server.Exporter` outputs a JSON document with:

- `gerenapi`: a spec version string (for example `1.0.0`)
- `endpoints`: an array of endpoints

An endpoint includes (camelCase in JSON):

- `method`: `Get` / `Post` / `Put` / `Patch` / `Delete`
- `path`: normalized route template (leading `/`, placeholders kept as `{name}`)
- `returnType`: optional type string
- `returnTypeBy`: optional type source (`metadata` / `compile` / `reference`)
- `bodyType`, `bodyTypeBy`, `bodyMedia`: optional request body description
- `params`: optional array of route-bound parameters
- `queries`: optional array of query parameters

### Type identity rules

When a type is emitted into JSON, it should be:

- stable across builds
- unique (no ambiguity for the generator)

In practice:

- exporter uses fully-qualified type identities (see `Byres`)
- generator maps those identities to compilable type names

### Placeholder types

If the generator cannot resolve a referenced type unambiguously, it emits a dedicated `_UnresolvedTypes.g.cs` file with placeholder types and continues generation.

