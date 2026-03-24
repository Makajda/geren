namespace Geren.Server.Exporter.Common;

internal sealed record Documant(
    string Gerenapi,
    ImmutableArray<Endpoint> Endpoints,
    ImmutableArray<Dide.Warning>? Warnings = null);

internal sealed record Endpoint(
    ImmutableArray<string> HttpMethods,
    string RouteTemplate,
    ImmutableArray<string> RouteParameters,
    string Handler,
    ImmutableArray<ParamSpec> Parameters,
    string? ReturnType);

internal sealed record ParamSpec(string Name, string Type, string Source);
