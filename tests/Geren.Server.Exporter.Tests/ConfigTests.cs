namespace Geren.Server.Exporter.Tests;

public class ConfigTests {
    [Fact]
    public void Configuration_ShouldRespectPriority_AndBeCorrectType() {
        string[] args = { "-c", "Debug" };

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> {
                {"Configuration", "Release"},
                {"Platform", "AnyCPU"}
            })
            .AddCommandLine(args, Config.SwitchMappings)
            .Build();

        var settings = config.Get<Settings>();

        settings.Should().NotBeNull();
        settings.Configuration.Should().Be("Debug", "from comand line");
        settings.Platform.Should().Be("AnyCPU", "default");
    }
}
