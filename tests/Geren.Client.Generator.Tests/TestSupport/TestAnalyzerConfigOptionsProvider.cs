namespace Geren.Client.Generator.Tests.TestSupport;

internal sealed class TestAnalyzerConfigOptionsProvider(
    IReadOnlyDictionary<string, string> globalOptions,
    IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> additionalTextOptionsByPath) : AnalyzerConfigOptionsProvider {
    private readonly TestAnalyzerConfigOptions _global = new(globalOptions);
    private readonly IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> _additionalTextOptionsByPath = additionalTextOptionsByPath;

    public override AnalyzerConfigOptions GlobalOptions => _global;

    public override AnalyzerConfigOptions GetOptions(SyntaxTree tree) => new TestAnalyzerConfigOptions(new Dictionary<string, string>());

    public override AnalyzerConfigOptions GetOptions(AdditionalText textFile) {
        if (_additionalTextOptionsByPath.TryGetValue(textFile.Path, out var options))
            return new TestAnalyzerConfigOptions(options);

        return new TestAnalyzerConfigOptions(new Dictionary<string, string>());
    }
}

