namespace Geren.Client.Generator.Tests.TestSupport;

internal sealed class TestAnalyzerConfigOptions(IReadOnlyDictionary<string, string> values) : AnalyzerConfigOptions {
    private readonly IReadOnlyDictionary<string, string> _values = values;

    public override bool TryGetValue(string key, out string value) => _values.TryGetValue(key, out value!);
}

