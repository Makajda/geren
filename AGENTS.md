# Agent Notes (geren)

This repo is frequently edited with Codex/agentic tooling. These notes capture local conventions so automated changes stay consistent and low-friction for maintainers.

## Environment

- Primary dev OS: Windows.
- Prefer Windows-style paths in docs/examples (`src\Geren.Client\...`), and be mindful that PowerShell uses `;` to chain commands (not `&&`/`||`).
- Keep generated text files consistent with repo EOL expectations (prefer `\n` inside generated sources; normalize when emitting).

## Source Generators

- Generators must only emit code via Roslyn (`context.AddSource` / `spc.AddSource`). Do not write files to disk from a generator.
- Keep pipelines incremental and narrow:
  - Filter inputs early (skip non-owned `AdditionalFiles` silently).
  - Prefer small, dedicated `RegisterSourceOutput` registrations over monolithic “do everything” blocks.
  - Ensure outputs are deterministic (stable ordering, stable hint names).
- Treat `AdditionalFiles` carefully:
  - Do not warn/error for files not explicitly opted-in for this generator.
  - Use `AnalyzerConfigOptionsProvider` + `build_metadata.AdditionalFiles.*` to identify opted-in files.
- When using custom `AdditionalFiles` metadata (e.g., `Geren="true"`), it must be made compiler-visible:
  - Package scenario: ship `build/` + `buildTransitive/` `.props` that add `CompilerVisibleItemMetadata`.
  - ProjectReference/manual analyzer scenario: consumers may need the equivalent `CompilerVisibleItemMetadata` in their project/`Directory.Build.props`.
- If a generator needs MSBuild properties from `AnalyzerConfigOptionsProvider`, ensure they are requested via `CompilerVisibleProperty`.

## Generated Text Quality

- Generated code should compile cleanly and follow the repo’s `.editorconfig` conventions.
- Prefer:
  - `#nullable enable`
  - explicit namespaces
  - no trailing whitespace
  - deterministic formatting (avoid culture-sensitive formatting; keep stable indentation)
- If you change configuration knobs or expected usage, update `README.md` accordingly.

## Packaging / NuGet

- When a package relies on MSBuild integration (props/targets), prefer `buildTransitive/` for transitive behavior.
- Keep `scripts\Invoke-NuGetPipeline.ps1` in sync with packaging changes:
  - If you add/remove packed files, update `RequiredEntries` checks.
- If you add an MSBuild import file (`.props`/`.targets`), ensure it is included in the `.csproj` with `Pack="true"` and correct `PackagePath`.

## Exporter Tool

- The exporter is a separate console tool and is built in CI; keep its dependencies compatible with MSBuild loading rules (watch `Microsoft.Build.*` references and `Microsoft.Build.Locator` guidance).

