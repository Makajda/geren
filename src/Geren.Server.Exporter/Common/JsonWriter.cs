using System.Text.Encodings.Web;
using System.Text.Json;

namespace Geren.Server.Exporter.Common;

internal static class JsonWriter {
    public static string Write(List<Endpoint> endpoints, IReadOnlyList<Dide.WarningSpec>? warnings = null) {
        using MemoryStream stream = new(capacity: 32 * 1024);
        using (Utf8JsonWriter writer = new(stream, new JsonWriterOptions { Indented = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping })) {
            writer.WriteStartObject();
            writer.WriteString("schema", "geren-minimal-api-spec");
            writer.WriteNumber("version", 1);

            writer.WritePropertyName("endpoints");
            writer.WriteStartArray();
            for (var i = 0; i < endpoints.Count; i++)
                WriteEndpoint(writer, endpoints[i]);

            writer.WriteEndArray();

            if (warnings is not null) {
                writer.WritePropertyName("warnings");
                writer.WriteStartArray();
                for (var i = 0; i < warnings.Count; i++)
                    WriteWarning(writer, warnings[i]);
                writer.WriteEndArray();
            }

            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }

    private static void WriteEndpoint(Utf8JsonWriter writer, Endpoint endpoint) {
        writer.WriteStartObject();

        writer.WritePropertyName("httpMethods");
        writer.WriteStartArray();
        for (var i = 0; i < endpoint.HttpMethods.Length; i++)
            writer.WriteStringValue(endpoint.HttpMethods[i]);

        writer.WriteEndArray();

        writer.WriteString("routeTemplate", endpoint.RouteTemplate);

        writer.WritePropertyName("routeParameters");
        writer.WriteStartArray();
        for (var i = 0; i < endpoint.RouteParameters.Length; i++)
            writer.WriteStringValue(endpoint.RouteParameters[i]);

        writer.WriteEndArray();

        writer.WriteString("handler", endpoint.Handler);

        writer.WritePropertyName("parameters");
        writer.WriteStartArray();
        for (var i = 0; i < endpoint.Parameters.Length; i++)
            WriteParameter(writer, endpoint.Parameters[i]);

        writer.WriteEndArray();

        if (endpoint.ReturnType is null)
            writer.WriteNull("returnType");
        else
            writer.WriteString("returnType", endpoint.ReturnType);

        writer.WriteEndObject();
    }

    private static void WriteWarning(Utf8JsonWriter writer, Dide.WarningSpec warning) {
        writer.WriteStartObject();
        writer.WriteString("code", warning.Id);
        writer.WriteString("message", warning.Message);

        if (warning.Location is not null) {
            writer.WritePropertyName("location");
            writer.WriteStartObject();
            writer.WriteString("file", warning.Location.File);
            writer.WriteNumber("line", warning.Location.Line);
            writer.WriteNumber("column", warning.Location.Column);
            writer.WriteEndObject();
        }

        writer.WriteEndObject();
    }

    private static void WriteParameter(Utf8JsonWriter writer, ParamSpec parameter) {
        writer.WriteStartObject();
        writer.WriteString("name", parameter.Name);
        writer.WriteString("type", parameter.Type);
        writer.WriteString("source", parameter.Source);
        writer.WriteEndObject();
    }
}
