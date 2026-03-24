namespace Geren.Server.Exporter.Common;

internal sealed record Endpoint(
    ImmutableArray<string> HttpMethods,
    string RouteTemplate,
    ImmutableArray<string> RouteParameters,
    string Handler,
    ImmutableArray<ParamSpec> Parameters,
    string? ReturnType);

internal sealed record ParamSpec(string Name, string Type, string Source);
