namespace Geren.Client.Generator.Common;

internal sealed record Purparam(
    string Name,
    string Identifier,
    PurposeType Type);

internal sealed record Purpoint(
    string Method,
    string Path,
    string SpaceName,
    string ClassName,
    string MethodName,
    PurposeType ReturnType,
    PurposeType? BodyType,
    string? BodyMediaType,
    ImmutableArray<Purparam> Params,
    ImmutableArray<Maparam> Queries);

internal sealed record Maparam(
    string Name,
    string Identifier,
    string Type);

internal sealed record Mapoint(
    string Method,
    string Path,
    string SpaceName,
    string ClassName,
    string MethodName,
    string ReturnType,
    string? BodyType,
    string? BodyMediaType,
    ImmutableArray<Maparam> Params,
    ImmutableArray<Maparam> Queries);

internal sealed record UnresolvedSchemaType(
    string PlaceholderTypeName,
    string Kind,
    string Requested,
    string? Details = null);


internal enum PurposeTypes {
    None,
    Metadata,
    Compile,
    Reference
}

internal record struct PurposeType(string Type, PurposeTypes Purpose = PurposeTypes.None);
