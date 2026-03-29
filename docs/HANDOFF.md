# Handoff / Context for new sessions

Quick notes to resume work in this repository after a break/reboot, or when starting a fresh agent session.

## TL;DR

- The `geren` repo publishes NuGet packages from git tags `vX.Y.Z` via GitHub Actions.
- Release notes are auto-generated and categorized by PR labels (see `.github\\release.yml`).
- Main projects:
  - `src\\Geren.Client.Generator` — Roslyn source generator for the client side (reads opted-in `AdditionalFiles`).
  - `src\\Geren.Client` — client library/package that brings the generator + MSBuild integration.
  - `src\\Geren.OpenApi.Server` — server-side OpenAPI-related package (if used).
  - `src\\Geren.Server.Exporter` — console exporter that builds a `Compilation` from a `*.csproj` and writes our JSON spec into an output folder (used for client generation, not needed on the server runtime).

## Environment and style

- Primary dev OS: Windows (use `\\` paths in docs/examples).
- PowerShell chaining: use `;` (not `&&`/`||`).
- Keep generated text deterministic and aligned with repo `.editorconfig`; no trailing whitespace.

## Exporter (`Geren.Server.Exporter`)

### Purpose

The exporter is a standalone tool that extracts Minimal API endpoints into a JSON spec (not OpenAPI) from a Roslyn `Compilation`.

### Run (example)

```powershell
dotnet run --project src\\Geren.Server.Exporter\\Geren.Server.Exporter.csproj -- `
  --project C:\\path\\to\\Server\\Server.csproj `
  --output-dir C:\\path\\to\\out `
  --configuration Release
```

Supported flags/aliases are described in `src\\Geren.Server.Exporter\\Common\\Config.cs`.

### MapGroup limitation

At the moment, `MapGroup(...)` prefixes are reliably detected only for compile-time constant strings. Avoid wrappers like `MapGroup(Func<string>)`, `MapGroup(MethodBase)`, or reflection-based route-prefix computation if you need correct export.

## Source generator (`Geren.Client.Generator`)

### AdditionalFiles: how to recognize “our” files

In a consumer project, the generator should only read `AdditionalFiles` that are explicitly opted-in via compiler-visible item metadata:

- MSBuild item: `<AdditionalFiles Include="...\\file.json" Geren="openapi" />` (example; the value depends on the supported formats).
- To read `build_metadata.AdditionalFiles.Geren` in the generator, that metadata must be compiler-visible.

The package/transitive setup is done via MSBuild `.props`/`.targets` (see `Directory.Build.props` and `buildTransitive` integration in the relevant `.csproj`).

### Build properties from AnalyzerConfigOptionsProvider

If a generator reads `build_property.*`, the property must be compiler-visible (via `CompilerVisibleProperty`). Prefer shipping this from the package so consumers do not have to add it to their `.csproj`.

## Releases and labels

### How release notes are produced

`.github\\release.yml` defines:

- which PRs to exclude (`skip-changelog` and the standard `duplicate/invalid/question/wontfix`);
- categories (Breaking/Exporter/Client/Server/Features/Fixes/Documentation/Maintenance) by PR labels.

### Which labels to use

Keep the standard GitHub labels: `bug`, `documentation`, `enhancement`, `duplicate`, `invalid`, `question`, `wontfix`, `good first issue`, `help wanted`.

Additional repo-specific labels (create once in GitHub):

- `area:client` — client generator / client-side
- `area:server` — server side / OpenAPI server
- `area:exporter` — exporter
- `breaking` — breaking change
- `chore` — maintenance/refactor
- `dependencies` — dependency updates
- `skip-changelog` — PR excluded from release notes

## Recommended workflow (short)

1. Create a branch off `master`.
2. Make small, focused commits.
3. Open a PR into `master`.
4. Add labels (area + type; optionally `skip-changelog`).
5. Merge only when CI is green.
6. To publish: create a tag `vX.Y.Z` (workflow builds/packs/publishes).

## Where to look when something breaks

- Generator cannot see metadata/properties: check the consumer `obj\\*\\*.GeneratedMSBuildEditorConfig.editorconfig` (look for `build_metadata.AdditionalFiles.*` and `build_property.*`).
- Release notes grouped incorrectly: verify PR labels and `.github\\release.yml`.
- Exporter misses group prefixes: ensure `MapGroup("const")` is used (no reflection/Func).
