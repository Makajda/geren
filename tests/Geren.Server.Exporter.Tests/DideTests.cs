using Geren.Server.Exporter.Common;

namespace Geren.Server.Exporter.Tests;

public class DideTests {
    [Fact]
    public void ToStrings_ShouldIncludeLocation_WhenPresent() {
        var warnings = new[] {
            new ErWarning("X1", "Hello", new ErLocation("C:\\a.cs", 10, 20)),
            new ErWarning("X2", "World", null),
        };

        var lines = Dide.ToStrings(warnings).ToArray();

        lines.Should().HaveCount(2);
        lines[0].Should().Be("X1: Hello: C:\\a.cs(10,20)");
        lines[1].Should().Be("X2: World");
    }
}

