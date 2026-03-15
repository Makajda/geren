namespace Geren.Tests.Emit;

public sealed class EmitExtensionsTests {
    [Fact]
    public void Run_should_emit_resilience_overloads_when_enabled() {
        var code = EmitExtensions.Run(
            rootNamespace: "Company.Generated",
            namespaceFromFile: "Pets",
            spaceName: "Company.Generated.Pets",
            names: ["Orders.Client", "StatusClient"]);

        code.Should().Contain("using Microsoft.Extensions.Http.Resilience;");
        code.Should().Contain("bool? useResilience = null");
        code.Should().Contain("configureResilience");
        code.Should().Contain("global::Company.Generated.FactoryBridge.AddClient<Orders.Client>");
        code.Should().Contain("AddGerenOrders_Client");
    }
}
