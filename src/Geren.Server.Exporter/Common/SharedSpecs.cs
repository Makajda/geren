using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Geren.Server.Exporter.Common;

// the file is shared between the the generator and the exporter
internal sealed record Maparam(
    string Name,
    string Identifier,
    string Type);

internal sealed record Purparam(
    string Name,
    string? Identifier,
    string Type,
    Byres? By);

internal sealed record Purpoint(
    string Method,
    string Path,
    string? OperationId,
    string? ReturnType,
    Byres? ReturnTypeBy,
    string? BodyType,
    Byres? BodyTypeBy,
    MediaTypes? BodyMedia,
    ImmutableArray<Purparam>? Params,
    ImmutableArray<Maparam>? Queries);

internal enum Byres {
    Metadata,
    Compile,
    Reference
}

internal enum MediaTypes {
    Text_Plain,
    Application_Json
}

internal sealed record ErDocument(
    string Gerenapi,
    ImmutableArray<Purpoint> Endpoints);

internal static class Givens {
    internal const string Get = "Get";
    internal const string Post = "Post";
    internal const string Put = "Put";
    internal const string Patch = "Patch";
    internal const string Delete = "Delete";

    internal static readonly JsonSerializerOptions JsonSerializerOptions = new() {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    };
}
