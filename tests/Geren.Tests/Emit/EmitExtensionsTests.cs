namespace Geren.Tests.Emit;

public sealed class EmitExtensionsTests {
    [Fact]
    public void Run_should_emit_resilience_overloads_when_enabled() {
        var code = EmitExtensions.Run(
            hasResilience: true,
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

    [Fact]
    public void Run_should_emit_simple_registrations_when_resilience_is_disabled() {
        var code = EmitExtensions.Run(
            hasResilience: false,
            rootNamespace: "Company.Generated",
            namespaceFromFile: "Pets",
            spaceName: "Company.Generated.Pets",
            names: ["StatusClient"]);

        code.Should().NotContain("using Microsoft.Extensions.Http.Resilience;");
        code.Should().Contain("Action<HttpClient>? configureClient = null)");
        code.Should().Contain("AddGerenStatusClient");
        code.Should().Contain("return services;");
    }
}
