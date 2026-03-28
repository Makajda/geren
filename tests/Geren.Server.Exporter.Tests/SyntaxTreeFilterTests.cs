using Geren.Server.Exporter.Extract;
using Geren.Server.Exporter.Tests.TestSupport;

namespace Geren.Server.Exporter.Tests;

public class SyntaxTreeFilterTests {
    [Fact]
    public void Extract_ShouldSkip_ObjBinAndGeneratedSyntaxTrees() {
        var compilation = TestCompilation.Create(
            """
            // main file intentionally has no endpoints
            """,
            extraSources: new (string path, string source)[] {
                ("C:\\src\\obj\\Generated\\Endpoints.cs", """
                    using Microsoft.AspNetCore.Routing;
                    using Microsoft.AspNetCore.Builder;
                    using System;

                    static class ObjFile {
                        public static void Map(IEndpointRouteBuilder app) {
                            app.MapGet("in-obj", (Delegate)(Func<int>)(() => 1));
                        }
                    }
                    """),
                ("C:\\src\\bin\\Debug\\Endpoints.cs", """
                    using Microsoft.AspNetCore.Routing;
                    using Microsoft.AspNetCore.Builder;
                    using System;

                    static class BinFile {
                        public static void Map(IEndpointRouteBuilder app) {
                            app.MapGet("in-bin", (Delegate)(Func<int>)(() => 1));
                        }
                    }
                    """),
                ("C:\\src\\Generated.g.cs", """
                    using Microsoft.AspNetCore.Routing;
                    using Microsoft.AspNetCore.Builder;
                    using System;

                    static class GeneratedFile {
                        public static void Map(IEndpointRouteBuilder app) {
                            app.MapGet("in-g", (Delegate)(Func<int>)(() => 1));
                        }
                    }
                    """),
            });

        var (endpoints, warnings) = Extractor.Extract(compilation, excludeTypes: Array.Empty<string>(), CancellationToken.None);

        warnings.Should().BeEmpty();
        endpoints.Should().BeEmpty();
    }

    [Fact]
    public void Extract_ShouldNotSkip_NormalSyntaxTrees() {
        var compilation = TestCompilation.Create(
            """
            using Microsoft.AspNetCore.Routing;
            using Microsoft.AspNetCore.Builder;
            using System;

            static class App {
                public static void Map(IEndpointRouteBuilder app) {
                    app.MapGet("ok", (Delegate)(Func<int>)(() => 1));
                }
            }
            """,
            mainPath: "C:\\src\\App.cs");

        var (endpoints, warnings) = Extractor.Extract(compilation, excludeTypes: Array.Empty<string>(), CancellationToken.None);

        warnings.Should().BeEmpty();
        endpoints.Should().ContainSingle();
        endpoints[0].Path.Should().Be("/ok");
    }
}
