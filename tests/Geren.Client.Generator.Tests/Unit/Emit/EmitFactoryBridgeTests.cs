namespace Geren.Client.Generator.Tests.Unit.Emit;

public sealed class EmitFactoryBridgeTests {
    [Fact]
    public void Run_EmitsHelpersUsedByClients() {
        var code = EmitFactoryBridge.Run("Acme");

        code.Should().Contain("public static class FactoryBridge");
        code.Should().Contain("public static void AddClient<TClient>(");
        code.Should().Contain("public abstract partial class GerenClientBase");
        code.Should().Contain("protected GerenClientBase(HttpClient http, IServiceProvider services)");
        code.Should().Contain("services.GetService<global::Geren.IGerenClientRequestHooks>()");
        code.Should().Contain("protected static string BuildRequestUri");
        code.Should().Contain("protected static void AddQueryParameter(");
        code.Should().Contain("protected static string FormatPathValue(");

        var hooksIndex = code.IndexOf("_requestHooks?.PrepareRequest(request);", StringComparison.Ordinal);
        var partialIndex = code.IndexOf("OnPrepareRequest(request);", StringComparison.Ordinal);
        hooksIndex.Should().BeGreaterThan(0);
        partialIndex.Should().BeGreaterThan(0);
        hooksIndex.Should().BeLessThan(partialIndex);
    }
}
