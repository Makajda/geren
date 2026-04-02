# Exporter design (Geren.Server.Exporter)

This document describes the internal design of the exporter tool.

## Goal

`Geren.Server.Exporter` is a standalone console tool that:

1. opens a `*.csproj` via `MSBuildWorkspace`
2. builds a Roslyn `Compilation`
3. scans the syntax trees for ASP.NET Core Minimal API endpoint mappings (`MapGet`, `MapPost`, ...)
4. emits a **custom JSON spec** (GerenAPI), which is later consumed by the client generator

The exporter does **not** produce OpenAPI and does **not** require running the application.

## Discovery approach

The exporter walks all `InvocationExpressionSyntax` nodes and selects calls where:

- the invoked method name starts with `Map`
- the method is an extension method for `Microsoft.AspNetCore.Routing.IEndpointRouteBuilder`
- the first argument (route template) is a compile-time constant string

If an endpoint cannot be extracted unambiguously, the exporter emits a structured warning and skips the endpoint.

## Route templates and normalization

The exporter normalizes route templates into a stable format:

- leading `/` is enforced
- directory separators are normalized (`\` → `/`)
- duplicate slashes are collapsed
- trailing `/` is trimmed (except `/`)
- placeholders are normalized:
  - `{id:int}` → `{id}`
  - `{id?}` → `{id}`
  - `{*name}` → `{name}`

This normalization keeps client code stable and avoids accidental diffs.

## MapGroup prefixes

The exporter supports fluent chains like:

```csharp
app.MapGroup("stat").RequireAuthorization().MapGet("ping", () => "ok");
```

and it attempts to prepend constant group prefixes to endpoint routes.

Important limitation:

- Prefixes are extracted only when the `MapGroup(...)` prefix argument is a **compile-time constant string**.
- Wrapper extensions with runtime logic (reflection, `Func<string>`, interpolated runtime values) are intentionally not supported.

The reason is fundamental: the exporter operates on syntax and semantic model and cannot reliably evaluate runtime code.

## Parameters and types

The exporter infers:

- route parameters (by `[FromRoute]` or by matching placeholder names)
- body parameter (first complex type for POST/PUT/PATCH)
- query parameters (everything else, with GET/DELETE simulating `AsParameters`)

Types are emitted using fully-qualified names to avoid ambiguity.

## Return type unwrapping

Return type extraction unwraps common wrappers:

- `Task<T>`, `ValueTask<T>`
- `ActionResult<T>` and common Minimal API typed results wrappers

For the “result zoo” (`IActionResult`, `IResult`, etc.) the exporter keeps return type empty/unknown, since no stable DTO type can be inferred.

## Warnings

Warnings are structured and may include a source location (file/line/column).
They can be written next to the output JSON for troubleshooting and CI visibility.

