using Geren.Server.Exporter.Extract;
using Geren.Server.Exporter.Tests.TestSupport;

namespace Geren.Server.Exporter.Tests;

public class ExtractionSmokeTests {
    [Fact]
    public void Extract_ShouldFind_MapGet_AndNormalizePath() {
        var compilation = TestCompilation.Create(
            """
            using Microsoft.AspNetCore.Routing;
            using Microsoft.AspNetCore.Builder;
            using System;

            static class App {
                public static void Map(IEndpointRouteBuilder app) {
                    app.MapGet("distance", (Delegate)(Func<int>)(() => 42));
                }
            }
            """,
            mainPath: "C:\\src\\App.cs");

        var (endpoints, warnings) = Extractor.Extract(compilation, excludeTypes: Array.Empty<string>(), CancellationToken.None);

        warnings.Should().BeEmpty();
        endpoints.Should().ContainSingle();

        var ep = endpoints[0];
        ep.Method.Should().Be("Get");
        ep.Path.Should().Be("/distance");
        ep.ReturnType.Should().Be("int");
        ep.ReturnTypeBy.Should().BeNull();
        ep.BodyType.Should().BeNull();
        ep.Params.Should().BeNull();
        ep.Queries.Should().BeNull();
    }
}
