namespace Geren.Tests.Incs;

public sealed class PackeIncTests {
    [Fact]
    public void Validate_should_report_missing_http_and_resilience() {
        var result = PackeInc.Validate(TestCompilationFactory.Create());

        result.HasHttp.Should().BeFalse();
        result.HasResilience.Should().BeFalse();
        result.Diagnostics.Select(static diagnostic => diagnostic.Id)
            .Should()
            .BeEquivalentTo(["GEREN009", "GEREN010"]);
    }

    [Fact]
    public void Validate_should_report_only_missing_resilience_when_http_is_available() {
        var result = PackeInc.Validate(TestCompilationFactory.Create(includeHttpClientBuilder: true));

        result.HasHttp.Should().BeTrue();
        result.HasResilience.Should().BeFalse();
        result.Diagnostics.Select(static diagnostic => diagnostic.Id)
            .Should()
            .Equal(["GEREN010"]);
    }

    [Fact]
    public void Validate_should_not_report_diagnostics_when_all_symbols_exist() {
        var result = PackeInc.Validate(TestCompilationFactory.Create(includeHttpClientBuilder: true, includeResilience: true));

        result.HasHttp.Should().BeTrue();
        result.HasResilience.Should().BeTrue();
        result.Diagnostics.Should().BeEmpty();
    }
}
