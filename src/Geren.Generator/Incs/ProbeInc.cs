using System.Text.Json;

namespace Geren.Generator.Incs;

internal sealed class ProbeInc {
    internal bool Success { get; }
    internal string? FilePath { get; }
    internal string? Text { get; }
    internal Diagnostic? Diagnostic { get; }

    private ProbeInc(string filePath, string text) {
        Success = true;
        FilePath = filePath;
        Text = text;
    }

    private ProbeInc() { }
    private ProbeInc(Diagnostic diagnostic) => Diagnostic = diagnostic;

    //static
    internal static ProbeInc Skip() => new();
    internal static ProbeInc Take(string filePath, string text) => new(filePath, text);
    internal static ProbeInc Warn(Diagnostic diagnostic) => new(diagnostic);

    internal static ProbeInc Probe(AdditionalText file, CancellationToken cancellationToken) {
        var filePath = file.Path;
        if (!filePath.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            return Skip();

        try {
            var text = file.GetText(cancellationToken)?.ToString();
            if (string.IsNullOrWhiteSpace(text))
                return Warn(Diagnostic.Create(Dide.JsonReadError, Location.None, $"Invalid JSON in {filePath}: File is empty."));

            var reader = new Utf8JsonReader(Encoding.UTF8.GetBytes(text), isFinalBlock: true, state: default);

            if (!reader.Read() || reader.TokenType != JsonTokenType.StartObject)
                return Warn(Diagnostic.Create(Dide.JsonReadError, Location.None, $"Invalid JSON in {filePath}: Root is not JSON object."));

            bool hasProperty = false;
            bool hasOpenApiProperty = false;
            while (reader.Read()) {
                if (reader.TokenType == JsonTokenType.PropertyName && reader.CurrentDepth == 1) {
                    hasProperty = true;
                    var propertyName = reader.GetString();
                    if (string.Equals(propertyName, "openapi", StringComparison.Ordinal)) {
                        hasOpenApiProperty = true;
                        break;
                    }
                }
            }

            if (!hasProperty)
                return Warn(Diagnostic.Create(Dide.JsonReadError, Location.None, $"Invalid JSON in {filePath}: Object has no properties."));

            if (!hasOpenApiProperty)
                return Skip();

            return Take(filePath, text!);
        }
        catch (Exception ex) {
            return Warn(Diagnostic.Create(Dide.JsonReadError, Location.None, $"Invalid JSON in {filePath}: {ex.Message}"));
        }
    }
}
