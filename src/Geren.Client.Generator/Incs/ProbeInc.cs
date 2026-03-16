using System.Text.Json;

namespace Geren.Client.Generator.Incs;

internal sealed record ProbeInc(string? FilePath = null, string? Text = null, bool Success = false, Diagnostic? Diagnostic = null) {
    private static ProbeInc Skip() => new();
    private static ProbeInc Take(string filePath, string text) => new(filePath, text, true);
    private static ProbeInc Diag(Diagnostic diagnostic) => new(null, null, false, diagnostic);

    internal static ProbeInc Probe(AdditionalText file, CancellationToken cancellationToken) {
        var filePath = file.Path;
        if (!filePath.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            return Skip();

        try {
            string? text = file.GetText(cancellationToken)?.ToString();
            if (string.IsNullOrWhiteSpace(text))
                return Diag(Diagnostic.Create(Dide.JsonReadError, Location.None, $"Invalid JSON in {filePath}: File is empty."));

            Utf8JsonReader reader = new(Encoding.UTF8.GetBytes(text), isFinalBlock: true, state: default);

            if (!reader.Read() || reader.TokenType != JsonTokenType.StartObject)
                return Diag(Diagnostic.Create(Dide.JsonReadError, Location.None, $"Invalid JSON in {filePath}: Root is not JSON object."));

            bool hasProperty = false;
            bool hasOpenApiProperty = false;
            while (reader.Read()) {
                if (reader.TokenType == JsonTokenType.PropertyName && reader.CurrentDepth == 1) {
                    hasProperty = true;
                    string? propertyName = reader.GetString();
                    if (string.Equals(propertyName, "openapi", StringComparison.Ordinal)) {
                        hasOpenApiProperty = true;
                        break;
                    }
                }
            }

            if (!hasProperty)
                return Diag(Diagnostic.Create(Dide.JsonReadError, Location.None, $"Invalid JSON in {filePath}: Object has no properties."));

            if (!hasOpenApiProperty)
                return Skip();

            return Take(filePath, text!);
        }
        catch (Exception ex) {
            return Diag(Diagnostic.Create(Dide.JsonReadError, Location.None, $"Invalid JSON in {filePath}: {ex.Message}"));
        }
    }
}
