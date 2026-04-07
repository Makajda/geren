using Geren.Server.Exporter.Common;
using Geren.Server.Exporter.Extract;
using Geren.Server.Exporter.Tests.TestSupport;

namespace Geren.Server.Exporter.Tests;

public class RouteTemplateNormalizationTests {
    [Theory]
    [InlineData("setItems/{tourId:int}", "/setItems/{tourId}")]
    [InlineData("setItems/{tourId:int?}", "/setItems/{tourId}")]
    [InlineData("setItems/{tourId?}", "/setItems/{tourId}")]
    [InlineData("{*name}", "/{name}")]
    [InlineData("{**name}", "/{name}")]
    public void Extract_ShouldNormalizeRoutePlaceholders(string template, string expected) {
        var source = """
            using Microsoft.AspNetCore.Routing;
            using Microsoft.AspNetCore.Builder;
            using System;

            sealed class MyDto { }

            static class App {
                public static void Map(IEndpointRouteBuilder app) {
                    app.MapPost("__TEMPLATE__", (Delegate)(Func<int, MyDto, string>)((tourId, dto) => "ok"));
                }
            }
            """.Replace("__TEMPLATE__", template, StringComparison.Ordinal);

        var compilation = TestCompilation.Create(source);

        EndpointFilters.TryCreate([], [], out EndpointFilters filters, out _);
        var (endpoints, warnings) = Extractor.Extract(compilation, [], filters, CancellationToken.None);

        warnings.Should().BeEmpty();
        endpoints.Should().ContainSingle();
        endpoints[0].Path.Should().Be(expected);
    }

    [Fact]
    public void Extract_ShouldNotTreat_DoubleBraces_AsPlaceholders() {
        var compilation = TestCompilation.Create(
            """
            using Microsoft.AspNetCore.Routing;
            using Microsoft.AspNetCore.Builder;
            using System;

            static class App {
                public static void Map(IEndpointRouteBuilder app) {
                    app.MapGet("{{literal}}/{id:int}", (Delegate)(Func<int, int>)(id => id));
                }
            }
            """);

        EndpointFilters.TryCreate([], [], out EndpointFilters filters, out _);
        var (endpoints, warnings) = Extractor.Extract(compilation, [], filters, CancellationToken.None);

        warnings.Should().BeEmpty();
        endpoints.Should().ContainSingle();
        endpoints[0].Path.Should().Be("/{{literal}}/{id}");
    }
}
