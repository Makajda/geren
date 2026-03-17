namespace Geren.Client.Generator.Common;

internal static class Dide {
    internal static readonly DiagnosticDescriptor JsonReadError = new(
        "GEREN001",
        "JSON read failed",
        "{0}",
        "Geren",
        DiagnosticSeverity.Warning, true);

    internal static readonly DiagnosticDescriptor ParseError = new(
        "GEREN002",
        "OpenAPI parse failed",
        "{0}",
        "Geren",
        DiagnosticSeverity.Error, true);

    internal static readonly DiagnosticDescriptor UnsupportedParamLocation = new(
        "GEREN003",
        "Unsupported parameter location",
        "Parameter '{0}' uses unsupported location '{1}'. Only 'path' and 'query' are supported.",
        "Geren",
        DiagnosticSeverity.Error,
        true);

    internal static readonly DiagnosticDescriptor UnsupportedQueryType = new(
        "GEREN004",
        "Unsupported query parameter type",
        "Query parameter '{0}' in operation '{1}' has unsupported type '{2}'. Supported: string, int32, int64, boolean, number.",
        "Geren",
        DiagnosticSeverity.Error,
        true);

    internal static readonly DiagnosticDescriptor UnsupportedRequestBody = new(
        "GEREN005",
        "Unsupported request body",
        "Operation '{0}' uses unsupported request body media type '{1}'. Only 'application/json' and 'text/plain' are supported.",
        "Geren",
        DiagnosticSeverity.Error,
        true);

    internal static readonly DiagnosticDescriptor DuplicateMethodName = new(
        "GEREN006",
        "Duplicate generated method name",
        "Generated method name '{0}' is duplicated in class '{1}' for path '{2}'",
        "Geren",
        DiagnosticSeverity.Error,
        true);

    internal static readonly DiagnosticDescriptor UnresolvedSchemaReference = new(
        "GEREN007",
        "Unresolved schema reference",
        "Schema reference '{0}' resolved to type '{1}' which was not found in compilation",
        "Geren",
        DiagnosticSeverity.Warning,
        true);

    internal static readonly DiagnosticDescriptor MissingParamLocation = new(
        "GEREN008",
        "Missing parameter location",
        "Parameter '{0}' in path '{1}' has no 'in' location. Allowed: path, query, header, cookie.",
        "Geren",
        DiagnosticSeverity.Error,
        true);

    internal static readonly DiagnosticDescriptor AmbiguousSchemaReference = new(
        "GEREN014",
        "Ambiguous schema reference",
        "Schema reference '{0}' resolved to type name '{1}' with multiple matches: {2}",
        "Geren",
        DiagnosticSeverity.Error,
        true);

    internal static readonly DiagnosticDescriptor PathParameterNameMismatch = new(
        "GEREN015",
        "Path placeholder and parameter name mismatch",
        "Path '{0}' and path parameters do not match: {1}",
        "Geren",
        DiagnosticSeverity.Error,
        true);
}
