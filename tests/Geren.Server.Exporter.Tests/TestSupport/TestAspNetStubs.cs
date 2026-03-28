namespace Geren.Server.Exporter.Tests.TestSupport;

internal static class TestAspNetStubs {
    internal const string Source = """
        #nullable enable
        using System;
        using System.Collections.Generic;

        namespace Microsoft.AspNetCore.Routing {
            public interface IEndpointRouteBuilder { }

            public sealed class RouteGroupBuilder : IEndpointRouteBuilder { }
        }

        namespace Microsoft.AspNetCore.Mvc {
            public interface IActionResult { }

            public class ActionResult : IActionResult { }

            public class ActionResult<T> : ActionResult { }

            public sealed class FromRouteAttribute : Attribute { }

            public sealed class FromServicesAttribute : Attribute { }
        }

        namespace Microsoft.AspNetCore.Http {
            public interface IResult { }

            public sealed class HttpContext { }
            public sealed class HttpRequest { }
            public sealed class HttpResponse { }

            public static class TypedResults {
                public static HttpResults.Ok<T> Ok<T>(T value) => new();
                public static HttpResults.Created<T> Created<T>(T value) => new();
                public static HttpResults.NotFound NotFound() => new();
            }
        }

        namespace Microsoft.AspNetCore.Http.Metadata {
            public sealed class FromRouteAttribute : Attribute { }
            public sealed class FromServicesAttribute : Attribute { }
        }

        namespace Microsoft.AspNetCore.Http.HttpResults {
            public class Ok<T> : Microsoft.AspNetCore.Http.IResult { }
            public class Created<T> : Microsoft.AspNetCore.Http.IResult { }
            public class NotFound : Microsoft.AspNetCore.Http.IResult { }
        }

        namespace Microsoft.AspNetCore.Http.Results {
            public class Results<T1, T2> : Microsoft.AspNetCore.Http.IResult { }
            public class Results<T1, T2, T3> : Microsoft.AspNetCore.Http.IResult { }
        }

        namespace Microsoft.Extensions.Logging {
            public interface ILogger { }
        }

        namespace Microsoft.Extensions.Primitives {
            public readonly struct StringValues { }
        }

        namespace Microsoft.AspNetCore.Builder {
            using Microsoft.AspNetCore.Routing;

            public static class MinimalApiEndpointRouteBuilderExtensions {
                public static RouteGroupBuilder MapGroup(this IEndpointRouteBuilder app, string prefix) => new();

                public static IEndpointRouteBuilder WithTags(this IEndpointRouteBuilder app, params string[] tags) => app;
                public static IEndpointRouteBuilder RequireAuthorization(this IEndpointRouteBuilder app) => app;

                public static void MapGet(this IEndpointRouteBuilder app, string pattern, global::System.Delegate handler) { }
                public static void MapPost(this IEndpointRouteBuilder app, string pattern, global::System.Delegate handler) { }
                public static void MapPut(this IEndpointRouteBuilder app, string pattern, global::System.Delegate handler) { }
                public static void MapPatch(this IEndpointRouteBuilder app, string pattern, global::System.Delegate handler) { }
                public static void MapDelete(this IEndpointRouteBuilder app, string pattern, global::System.Delegate handler) { }

                public static void MapMethods(this IEndpointRouteBuilder app, string pattern, IEnumerable<string> methods, global::System.Delegate handler) { }
            }
        }
        """;
}
