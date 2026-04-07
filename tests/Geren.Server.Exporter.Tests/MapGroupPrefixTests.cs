using Geren.Server.Exporter.Common;
using Geren.Server.Exporter.Extract;
using Geren.Server.Exporter.Tests.TestSupport;

namespace Geren.Server.Exporter.Tests;

public class MapGroupPrefixTests {
    [Fact]
    public void Extract_ShouldApply_MapGroupPrefix_InFluentChain() {
        var compilation = TestCompilation.Create(
            """
            using Microsoft.AspNetCore.Routing;
            using Microsoft.AspNetCore.Builder;
            using System;

            static class App {
                public static void Map(IEndpointRouteBuilder app) {
                    app.MapGroup("stat")
                        .WithTags("x")
                        .RequireAuthorization()
                        .MapGet("distance", (Delegate)(Func<int>)(() => 42));
                }
            }
            """);

        EndpointFilters.TryCreate([], [], out EndpointFilters filters, out _);
        var (endpoints, warnings) = Extractor.Extract(compilation, [], filters, CancellationToken.None);

        warnings.Should().BeEmpty();
        endpoints.Should().ContainSingle();
        endpoints[0].Path.Should().Be("/stat/distance");
    }

    [Fact]
    public void Extract_ShouldApply_MapGroupPrefix_FromLocalAlias() {
        var compilation = TestCompilation.Create(
            """
            using Microsoft.AspNetCore.Routing;
            using Microsoft.AspNetCore.Builder;
            using System;

            static class App {
                public static void Map(IEndpointRouteBuilder app) {
                    var gb = app.MapGroup("stat");
                    gb.MapGet("distance", (Delegate)(Func<int>)(() => 42));
                }
            }
            """);

        EndpointFilters.TryCreate([], [], out EndpointFilters filters, out _);
        var (endpoints, warnings) = Extractor.Extract(compilation, [], filters, CancellationToken.None);

        warnings.Should().BeEmpty();
        endpoints.Should().ContainSingle();
        endpoints[0].Path.Should().Be("/stat/distance");
    }
}
