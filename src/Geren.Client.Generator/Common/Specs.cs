namespace Geren.Client.Generator.Common;

internal sealed record EndpointSpec(
    string Method,
    string Path,
    string SpaceName,
    string ClassName,
    string MethodName,
    string ReturnType,
    string? BodyType,
    string? BodyMediaType,
    ImmutableArray<ParamSpec> Params,
    ImmutableArray<ParamSpec> Queries);

internal sealed record ParamSpec(
    string Name,
    string Identifier,
    string TypeName);
