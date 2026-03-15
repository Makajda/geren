namespace Geren.Tests.TestSupport;

internal sealed class InMemoryAdditionalText(string path, string text) : AdditionalText {
    private readonly SourceText _sourceText = SourceText.From(text, Encoding.UTF8);

    public override string Path { get; } = path;

    public override SourceText GetText(CancellationToken cancellationToken = default) => _sourceText;
}
