using Geren.Server.Exporter.Common;
using Geren.Server.Exporter.Extract;
using Geren.Server.Exporter.Tests.TestSupport;

namespace Geren.Server.Exporter.Tests;

public class WarningTests {
    [Fact]
    public void Extract_ShouldWarnAndSkip_WhenRouteTemplateIsNotConstant() {
        var compilation = TestCompilation.Create(
            """
            using Microsoft.AspNetCore.Routing;
            using Microsoft.AspNetCore.Builder;
            using System;

            static class App {
                public static void Map(IEndpointRouteBuilder app) {
                    var t = "distance";
                    app.MapGet(t, (Delegate)(Func<int>)(() => 1));
                }
            }
            """,
            mainPath: "C:\\src\\App.cs");

        var (endpoints, warnings) = Extractor.Extract(compilation, excludeTypes: Array.Empty<string>(), CancellationToken.None);

        endpoints.Should().BeEmpty();
        warnings.Should().ContainSingle(w => w.Id == Dide.SkipTemplate);
        warnings[0].Location.Should().NotBeNull();
        warnings[0].Location!.File.Should().Be("C:\\src\\App.cs");
    }

    [Fact]
    public void Extract_ShouldWarnAndSkip_WhenHandlerMethodCannotBeResolved() {
        var compilation = TestCompilation.Create(
            """
            using Microsoft.AspNetCore.Routing;
            using Microsoft.AspNetCore.Builder;
            using System;

            static class App {
                public static void Map(IEndpointRouteBuilder app) {
                    app.MapGet("distance", (Delegate)null);
                }
            }
            """);

        var (endpoints, warnings) = Extractor.Extract(compilation, excludeTypes: Array.Empty<string>(), CancellationToken.None);

        endpoints.Should().BeEmpty();
        warnings.Should().ContainSingle(w => w.Id == Dide.SkipHandler);
    }

    [Fact]
    public void Extract_ShouldWarnAndSkip_WhenHttpMethodIsUnknown() {
        var compilation = TestCompilation.Create(
            """
            using Microsoft.AspNetCore.Routing;
            using Microsoft.AspNetCore.Builder;
            using System;

            static class MyExtensions {
                public static void MapFoo(this IEndpointRouteBuilder app, string pattern, Delegate handler) { }
            }

            static class App {
                public static void Map(IEndpointRouteBuilder app) {
                    app.MapFoo("distance", (Delegate)(Func<int>)(() => 1));
                }
            }
            """);

        var (endpoints, warnings) = Extractor.Extract(compilation, excludeTypes: Array.Empty<string>(), CancellationToken.None);

        endpoints.Should().BeEmpty();
        warnings.Should().ContainSingle(w => w.Id == Dide.SkipMethod);
    }

    [Fact]
    public void Extract_ShouldWarn_WhenIEndpointRouteBuilderIsMissing() {
        var compilation = TestCompilation.Create(
            """
            // empty
            """,
            includeAspNetStubs: false);

        var (endpoints, warnings) = Extractor.Extract(compilation, excludeTypes: Array.Empty<string>(), CancellationToken.None);

        endpoints.Should().BeEmpty();
        warnings.Should().ContainSingle(w => w.Id == Dide.UnableIEndpointRouteBuilder);
    }
}
