; Shipped analyzer releases
; https://github.com/dotnet/roslyn-analyzers/blob/main/src/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md

## Release 0.3.1

### New Rules

Rule ID  | Category | Severity | Notes
---------|----------|----------|------
GEREN001 | Geren    | Warning  | JSON read failed
GEREN002 | Geren    | Error    | OpenAPI parse failed
GEREN003 | Geren    | Error    | Unsupported parameter location
GEREN004 | Geren    | Error    | Unsupported query parameter type
GEREN005 | Geren    | Error    | Unsupported request body
GEREN006 | Geren    | Error    | DuplicateMethodName
GEREN007 | Geren    | Warning  | Unresolved schema reference
GEREN008 | Geren    | Error    | Missing parameter location
GEREN014 | Geren    | Error    | Ambiguous schema reference
GEREN015 | Geren    | Error    | Path placeholder and parameter name mismatch
