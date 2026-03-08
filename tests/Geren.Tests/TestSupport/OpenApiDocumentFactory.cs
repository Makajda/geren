namespace Geren.Tests.TestSupport;

internal static class OpenApiDocumentFactory {
    internal static OpenApiDocument Load(string text) {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(text));
        var readResult = OpenApiDocument.Load(stream);

        readResult.Diagnostic?.Errors.Should().BeEmpty("test OpenAPI documents must stay valid");
        readResult.Document.Should().NotBeNull();

        return readResult.Document!;
    }
}
