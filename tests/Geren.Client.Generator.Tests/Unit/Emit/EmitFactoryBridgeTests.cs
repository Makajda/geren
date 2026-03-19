namespace Geren.Client.Generator.Tests.Unit.Emit;

public sealed class EmitFactoryBridgeTests {
    [Fact]
    public void Run_EmitsHelpersUsedByClients() {
        var code = EmitFactoryBridge.Run("Acme");

        code.Should().Contain("internal static string BuildRequestUri");
        code.Should().Contain("internal static void A(");
        code.Should().Contain("internal static string V(");
    }
}

