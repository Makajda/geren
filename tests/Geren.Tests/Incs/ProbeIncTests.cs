namespace Geren.Tests.Incs;

public sealed class ProbeIncTests {
    [Fact]
    public void Probe_should_skip_non_json_files() {
        var result = ProbeInc.Probe(new InMemoryAdditionalText("openapi.yaml", "openapi: 3.0.1"), CancellationToken.None);

        result.Success.Should().BeFalse();
        result.FilePath.Should().BeNull();
        result.Diagnostic.Should().BeNull();
    }

    [Fact]
    public void Probe_should_warn_for_empty_json() {
        var result = ProbeInc.Probe(new InMemoryAdditionalText("empty.json", " "), CancellationToken.None);

        result.Diagnostic.Should().NotBeNull();
        result.Diagnostic!.Id.Should().Be("GEREN001");
        result.Diagnostic.GetMessage().Should().Contain("File is empty");
    }

    [Fact]
    public void Probe_should_warn_when_json_root_is_not_object() {
        var result = ProbeInc.Probe(new InMemoryAdditionalText("broken.json", "[]"), CancellationToken.None);

        result.Diagnostic.Should().NotBeNull();
        result.Diagnostic!.Id.Should().Be("GEREN001");
        result.Diagnostic.GetMessage().Should().Contain("Root is not JSON object");
    }

    [Fact]
    public void Probe_should_warn_when_object_has_no_properties() {
        var result = ProbeInc.Probe(new InMemoryAdditionalText("empty-object.json", "{}"), CancellationToken.None);

        result.Diagnostic.Should().NotBeNull();
        result.Diagnostic!.GetMessage().Should().Contain("Object has no properties");
    }

    [Fact]
    public void Probe_should_skip_json_without_openapi_property() {
        var result = ProbeInc.Probe(new InMemoryAdditionalText("other.json", """{"hello":"world"}"""), CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Diagnostic.Should().BeNull();
    }

    [Fact]
    public void Probe_should_take_json_with_root_openapi_property() {
        const string json = """{"openapi":"3.0.1","info":{"title":"t","version":"1"},"paths":{}}""";

        var result = ProbeInc.Probe(new InMemoryAdditionalText("petstore.json", json), CancellationToken.None);

        result.Success.Should().BeTrue();
        result.FilePath.Should().Be("petstore.json");
        result.Text.Should().Be(json);
        result.Diagnostic.Should().BeNull();
    }
}
