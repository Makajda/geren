namespace Geren.Client.Generator.Common;

// the file is shared between the the generator and the exporter
internal sealed record Maparam(
    string Name,
    string Identifier,
    string Type);

internal sealed record Purparam(
    string Name,
    string Identifier,
    PurposeType Type);

internal sealed record Purpoint(
    string Method,
    string Path,
    string? OperationId,
    PurposeType ReturnType,
    PurposeType? BodyType,
    MediaTypes BodyMedia,
    ImmutableArray<Purparam> Params,
    ImmutableArray<Maparam> Queries);

internal enum Puresolve {
    None,
    Metadata,
    Compile,
    Reference
}

internal record struct PurposeType(string Type, Puresolve Puresolve = Puresolve.None);

internal enum MediaTypes {
    None,
    Text_Plain,
    Application_Json
}

internal sealed record ErDocument(
    string Gerenapi,
    ImmutableArray<Purparam> Endpoints);
