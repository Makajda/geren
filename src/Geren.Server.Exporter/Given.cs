global using Microsoft.CodeAnalysis;
global using System.Collections.Immutable;

internal sealed record Endpoint(
    ImmutableArray<string> HttpMethods,
    string RouteTemplate,
    ImmutableArray<string> RouteParameters,
    string Handler,
    ImmutableArray<ParamSpec> Parameters,
    string? ReturnType);

internal sealed record ParamSpec(string Name, string Type, string Source);

internal sealed record WarningLocation(string File, int Line, int Column);

internal sealed record WarningSpec(string Code, string Message, WarningLocation? Location = null);

internal static class Given {
    internal static readonly SymbolDisplayFormat FullyQualifiedMethodFormat = new(
        globalNamespaceStyle: SymbolDisplayGlobalNamespaceStyle.Included,
        typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces,
        genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters,
        memberOptions:
        SymbolDisplayMemberOptions.IncludeContainingType |
        SymbolDisplayMemberOptions.IncludeParameters |
        SymbolDisplayMemberOptions.IncludeType,
        parameterOptions:
        SymbolDisplayParameterOptions.IncludeType |
        SymbolDisplayParameterOptions.IncludeName,
        miscellaneousOptions:
        SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier |
        SymbolDisplayMiscellaneousOptions.EscapeKeywordIdentifiers);
}
