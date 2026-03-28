using Geren.Server.Exporter.Common;
using Geren.Server.Exporter.Extract;
using Geren.Server.Exporter.Tests.TestSupport;

namespace Geren.Server.Exporter.Tests;

public class ReturnTypeTests {
    [Fact]
    public void Extract_ShouldUnwrap_TypedHttpResults_GenericWrapper() {
        var compilation = TestCompilation.Create(
            """
            using Microsoft.AspNetCore.Routing;
            using Microsoft.AspNetCore.Builder;
            using System;
            using Microsoft.AspNetCore.Http.HttpResults;

            sealed class MyDto { }

            static class App {
                public static void Map(IEndpointRouteBuilder app) {
                    app.MapGet("dto", (Delegate)(Func<Ok<MyDto>>)(() => new Ok<MyDto>()));
                }
            }
            """);

        var (endpoints, warnings) = Extractor.Extract(compilation, excludeTypes: Array.Empty<string>(), CancellationToken.None);

        warnings.Should().BeEmpty();
        endpoints.Should().ContainSingle();
        endpoints[0].ReturnType.Should().Be("MyDto");
        endpoints[0].ReturnTypeBy.Should().Be(Byres.Metadata);
    }

    [Fact]
    public void Extract_ShouldUnwrap_ResultsUnion_WhenSinglePayloadTypeCanBeInferred() {
        var compilation = TestCompilation.Create(
            """
            using Microsoft.AspNetCore.Routing;
            using Microsoft.AspNetCore.Builder;
            using System;
            using Microsoft.AspNetCore.Http.HttpResults;
            using Microsoft.AspNetCore.Http.Results;

            sealed class MyDto { }

            static class App {
                public static void Map(IEndpointRouteBuilder app) {
                    app.MapGet("dto", (Delegate)(Func<Results<Ok<MyDto>, NotFound>>)(() => new Results<Ok<MyDto>, NotFound>()));
                }
            }
            """);

        var (endpoints, warnings) = Extractor.Extract(compilation, excludeTypes: Array.Empty<string>(), CancellationToken.None);

        warnings.Should().BeEmpty();
        endpoints.Should().ContainSingle();
        endpoints[0].ReturnType.Should().Be("MyDto");
        endpoints[0].ReturnTypeBy.Should().Be(Byres.Metadata);
    }

    [Fact]
    public void Extract_ShouldReturnNull_WhenResultsUnionIsAmbiguous() {
        var compilation = TestCompilation.Create(
            """
            using Microsoft.AspNetCore.Routing;
            using Microsoft.AspNetCore.Builder;
            using System;
            using Microsoft.AspNetCore.Http.HttpResults;
            using Microsoft.AspNetCore.Http.Results;

            sealed class A { }
            sealed class B { }

            static class App {
                public static void Map(IEndpointRouteBuilder app) {
                    app.MapGet("dto", (Delegate)(Func<Results<Ok<A>, Created<B>>>)(() => new Results<Ok<A>, Created<B>>()));
                }
            }
            """);

        var (endpoints, warnings) = Extractor.Extract(compilation, excludeTypes: Array.Empty<string>(), CancellationToken.None);

        warnings.Should().BeEmpty();
        endpoints.Should().ContainSingle();
        endpoints[0].ReturnType.Should().BeNull();
        endpoints[0].ReturnTypeBy.Should().BeNull();
    }

    [Fact]
    public void Extract_ShouldReturnNull_ForIResultReturnTypes() {
        var compilation = TestCompilation.Create(
            """
            using Microsoft.AspNetCore.Routing;
            using Microsoft.AspNetCore.Builder;
            using System;
            using Microsoft.AspNetCore.Http;
            using Microsoft.AspNetCore.Http.HttpResults;

            sealed class MyDto { }

            static class App {
                public static void Map(IEndpointRouteBuilder app) {
                    app.MapGet("dto", (Delegate)(Func<IResult>)(() => new Ok<MyDto>()));
                }
            }
            """);

        var (endpoints, warnings) = Extractor.Extract(compilation, excludeTypes: Array.Empty<string>(), CancellationToken.None);

        warnings.Should().BeEmpty();
        endpoints.Should().ContainSingle();
        endpoints[0].ReturnType.Should().BeNull();
        endpoints[0].ReturnTypeBy.Should().BeNull();
    }
}
