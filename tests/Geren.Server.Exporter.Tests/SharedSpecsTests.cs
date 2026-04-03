namespace Geren.Server.Exporter.Tests;

public class SharedSpecsTests {
    [Fact]
    public void Compare_Equal() {
        var generator = GetLines("../../../../../src/Geren.Client.Generator/Common/SharedSpecs.cs");
        var exporter = GetLines("../../../../../src/Geren.Server.Exporter/Common/SharedSpecs.cs");

        generator.Should().Equal(exporter);

        static List<string> GetLines(string path) => [.. File.ReadAllLines(path).Where(line => !line.StartsWith("namespace"))];
    }
}
