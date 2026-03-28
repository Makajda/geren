using Geren.Server.Exporter.Common;
using Geren.Server.Exporter.Extract;
using Geren.Server.Exporter.Tests.TestSupport;

namespace Geren.Server.Exporter.Tests;

public class HttpMethodsTests {
    [Fact]
    public void Extract_ShouldHandle_MapMethods_WithSingleConstantMethod() {
        var compilation = TestCompilation.Create(
            """
            using Microsoft.AspNetCore.Routing;
            using Microsoft.AspNetCore.Builder;
            using System;

            static class App {
                public static void Map(IEndpointRouteBuilder app) {
                    app.MapMethods("distance", new[] { "GET" }, (Delegate)(Func<int>)(() => 1));
                }
            }
            """);

        var (endpoints, warnings) = Extractor.Extract(compilation, excludeTypes: Array.Empty<string>(), CancellationToken.None);

        warnings.Should().BeEmpty();
        endpoints.Should().ContainSingle();
        endpoints[0].Method.Should().Be("GET");
        endpoints[0].Path.Should().Be("/distance");
    }

    [Fact]
    public void Extract_ShouldWarn_AndSkip_When_MapMethodsHasMultipleMethods() {
        var compilation = TestCompilation.Create(
            """
            using Microsoft.AspNetCore.Routing;
            using Microsoft.AspNetCore.Builder;
            using System;

            static class App {
                public static void Map(IEndpointRouteBuilder app) {
                    app.MapMethods("distance", new[] { "GET", "POST" }, (Delegate)(Func<int>)(() => 1));
                }
            }
            """,
            mainPath: "C:\\src\\App.cs");

        var (endpoints, warnings) = Extractor.Extract(compilation, excludeTypes: Array.Empty<string>(), CancellationToken.None);

        endpoints.Should().BeEmpty();
        warnings.Should().ContainSingle(w => w.Id == Dide.SkipMethod);
        warnings[0].Location.Should().NotBeNull();
        warnings[0].Location!.File.Should().Be("C:\\src\\App.cs");
    }
}
