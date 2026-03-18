namespace Geren.Client.Generator.Common;

internal sealed record PointSpec(
    string Method,
    string Path,
    string SpaceName,
    string ClassName,
    string MethodName,
    string? BodyMediaType,
    ImmutableArray<ParamSpec> Queries);

internal sealed record ParamSpec(
    string Name,
    string Identifier,
    string Type);

internal sealed record Purparam(
    string Name,
    string Identifier,
    PurposeType Type);

internal sealed record Purpoint(
    PointSpec Point,
    PurposeType ReturnType,
    PurposeType? BodyType,
    ImmutableArray<Purparam> Params);

internal sealed record Mapoint(
    PointSpec Point,
    string ReturnType,
    string? BodyType,
    ImmutableArray<ParamSpec> Params);

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
