namespace Geren.Client.Generator.Tests.Unit.Emit;

public sealed class EmitFactoryBridgeTests {
    [Fact]
    public void Run_EmitsHelpersUsedByClients() {
        var code = EmitFactoryBridge.Run("Acme");

        code.Should().Contain("public static class FactoryBridge");
        code.Should().Contain("public static void AddClient<TClient>(");
        code.Should().Contain("public abstract partial class GerenClientBase");
        code.Should().Contain("protected static string BuildRequestUri");
        code.Should().Contain("protected static void AddQueryParameter(");
        code.Should().Contain("protected static string FormatPathValue(");
    }
}

