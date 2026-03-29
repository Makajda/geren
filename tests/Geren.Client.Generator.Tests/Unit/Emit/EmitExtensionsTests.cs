namespace Geren.Client.Generator.Tests.Unit.Emit;

public sealed class EmitExtensionsTests {
    [Fact]
    public void Run_RegistersAllClientsAndHasSingleClientExtensions() {
        var code = EmitExtensions.Run(
            rootNamespace: "Acme",
            namespaceFromFile: "Petstore",
            names: ["RootClient", "Pets.PetsClient"]);

        code.Should().Contain("namespace Acme.Petstore;");
        code.Should().Contain("AddGerenClients");
        code.Should().Contain("FactoryBridge.AddClient<RootClient>");
        code.Should().Contain("FactoryBridge.AddClient<Pets.PetsClient>");
        code.Should().Contain("AddGerenRootClient");
        code.Should().Contain("AddGerenPets_PetsClient");
    }
}
