namespace Geren.Client.Generator.Tests.Unit.Map;

public sealed class MapIncTests {
    [Fact]
    public void Map_HintFilePath_IsCaseInsensitive() {
        var compilation = CompilationFactory.Create("t", "public sealed class Dummy { }");
        var purpoints = ImmutableArray<Purpoint>.Empty;

        var m1 = MapInc.Map(compilation, "Root", @"C:\Specs\Pets.json", purpoints, CancellationToken.None);
        var m2 = MapInc.Map(compilation, "Root", @"c:\specs\pets.json", purpoints, CancellationToken.None);

        m1.HintFilePath.Should().Be(m2.HintFilePath);
    }

    [Fact]
    public void Map_ResolvesMetadataTypes_AndCollectsUnresolved() {
        var compilation = CompilationFactory.Create("t", """
            namespace Dto;
            public sealed class Known { }
            """);

        var points = ImmutableArray.Create(
            new Purpoint(
                Method: Givens.Get,
                Path: "/known",
                OperationId: "GetKnown",
                ReturnType: "Dto.Known",
                ReturnTypeBy: Byres.Metadata,
                BodyType: null,
                BodyTypeBy: null,
                BodyMedia: null,
                Params: ImmutableArray<Purparam>.Empty,
                Queries: ImmutableArray<Maparam>.Empty),
            new Purpoint(
                Method: Givens.Get,
                Path: "/missing",
                OperationId: "GetMissing",
                ReturnType: "Dto.Missing",
                ReturnTypeBy: Byres.Metadata,
                BodyType: null,
                BodyTypeBy: null,
                BodyMedia: null,
                Params: ImmutableArray<Purparam>.Empty,
                Queries: ImmutableArray<Maparam>.Empty));

        var map = MapInc.Map(compilation, "Acme", @"C:\Specs\Pets.json", points, CancellationToken.None);

        map.Endpoints.Should().HaveCount(2);
        map.Endpoints[0].ReturnType.Should().Be("global::Dto.Known");
        map.Endpoints[1].ReturnType.Should().StartWith("global::Acme.Pets.__GerenUnresolvedType_");

        map.UnresolvedSchemaTypes.Should().ContainSingle(u => u.Kind == "metadata" && u.Requested == "Dto.Missing");
        map.Diagnostics.Should().ContainSingle(d => d.Id == "GEREN007");
    }
}

