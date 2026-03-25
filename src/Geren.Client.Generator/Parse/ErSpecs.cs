// The same file is in the exporter and generator projects

namespace Geren.Client.Generator.Parse;

internal sealed record Erdoc(
    string Gerenapi,
    ImmutableArray<Erpoint> Endpoints,
    ImmutableArray<ErWarning>? Warnings = null);

internal sealed record Erpoint(
    ImmutableArray<string> HttpMethods,
    string RouteTemplate,
    ImmutableArray<string> RouteParameters,
    string Handler,
    ImmutableArray<ErParamSpec> Parameters,
    string? ReturnType);

internal sealed record ErParamSpec(string Name, string Type, string Source);
internal sealed record ErLocation(string File, int Line, int Column);
internal sealed record ErWarning(string Id, string Message, ErLocation? Location = null);
