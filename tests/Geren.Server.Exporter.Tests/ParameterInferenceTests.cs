using Geren.Server.Exporter.Common;
using Geren.Server.Exporter.Extract;
using Geren.Server.Exporter.Tests.TestSupport;

namespace Geren.Server.Exporter.Tests;

public class ParameterInferenceTests {
    [Fact]
    public void Extract_ShouldInfer_RouteQueryAndBody_AndSkipInfrastructureParameters() {
        var compilation = TestCompilation.Create(
            """
            using Microsoft.AspNetCore.Routing;
            using Microsoft.AspNetCore.Builder;
            using System;
            using System.Threading;
            using Microsoft.Extensions.Logging;

            sealed class MyDto { }

            static class App {
                public static void Map(IEndpointRouteBuilder app) {
                    app.MapPost("items/{id:int}", (Delegate)(Func<int, string, string, MyDto, object, ILogger, CancellationToken, MyDto>)Handler);
                }

                static MyDto Handler(
                    int id,
                    [Microsoft.AspNetCore.Mvc.FromRoute] string explicitRoute,
                    string q,
                    MyDto body,
                    [Microsoft.AspNetCore.Mvc.FromServices] object svc,
                    ILogger log,
                    CancellationToken ct) => body;
            }
            """);

        var (endpoints, warnings) = Extractor.Extract(compilation, excludeTypes: Array.Empty<string>(), CancellationToken.None);

        warnings.Should().BeEmpty();
        endpoints.Should().ContainSingle();

        var ep = endpoints[0];
        ep.Method.Should().Be(Givens.Post);
        ep.Path.Should().Be("/items/{id}");

        ep.Params.Should().NotBeNull();
        ep.Params!.Value.Select(p => p.Name).Should().BeEquivalentTo(["id", "explicitRoute"]);

        ep.Queries.Should().NotBeNull();
        ep.Queries!.Value.Should().ContainSingle(q => q.Name == "q" && q.Type == "string");

        ep.BodyType.Should().Be("MyDto");
        ep.BodyTypeBy.Should().Be(Byres.Metadata);
        ep.BodyMedia.Should().Be(MediaTypes.Application_Json);
    }

    [Fact]
    public void Extract_ShouldSkip_ParametersFromConfiguredExcludeTypes() {
        var compilation = TestCompilation.Create(
            """
            using Microsoft.AspNetCore.Routing;
            using Microsoft.AspNetCore.Builder;
            using System;

            namespace My.App { sealed class SkipMe { } }
            sealed class MyDto { }

            static class App {
                public static void Map(IEndpointRouteBuilder app) {
                    app.MapPost("items/{id}", (Delegate)(Func<int, My.App.SkipMe, MyDto, MyDto>)Handler);
                }

                static MyDto Handler(int id, My.App.SkipMe skip, MyDto body) => body;
            }
            """);

        var (endpoints, warnings) = Extractor.Extract(compilation, excludeTypes: new[] { "My.App.SkipMe" }, CancellationToken.None);

        warnings.Should().BeEmpty();
        endpoints.Should().ContainSingle();

        var ep = endpoints[0];
        ep.Params.Should().NotBeNull();
        ep.Params!.Value.Select(p => p.Name).Should().BeEquivalentTo(["id"]);
        ep.BodyType.Should().Be("MyDto");
    }
}
