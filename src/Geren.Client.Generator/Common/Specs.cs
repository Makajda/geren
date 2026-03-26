namespace Geren.Client.Generator.Common;

internal sealed record Mapoint(
    string Method,
    string Path,
    string SpaceName,
    string ClassName,
    string MethodName,
    string? ReturnType,
    string? BodyType,
    MediaTypes? BodyMedia,
    ImmutableArray<Maparam> Params,
    ImmutableArray<Maparam> Queries);

internal sealed record UnresolvedSchemaType(
    string PlaceholderTypeName,
    string Kind,
    string Requested,
    string? Details = null);

internal record struct PurposeType(string Name, Byres? By = null);
