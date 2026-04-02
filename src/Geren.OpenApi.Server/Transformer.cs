using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace Geren.Server;

/// <summary>
/// Adds Geren schema extensions to OpenAPI schemas produced by ASP.NET Core.
/// </summary>
/// <remarks>
/// This transformer writes one of the following extensions to <see cref="OpenApiSchema.Extensions"/>:
/// <list type="bullet">
/// <item>
/// <description><c>x-metadata</c> for non-generic, non-array CLR types (stable metadata name).</description>
/// </item>
/// <item>
/// <description><c>x-compile</c> for arrays and generic types (a fully-qualified, compilable type name).</description>
/// </item>
/// </list>
/// These extensions can later be consumed by the Geren client generator to produce strongly typed clients.
/// </remarks>
public sealed class Transformer : IOpenApiSchemaTransformer {
    /// <summary>
    /// Applies Geren schema extensions to a single schema node.
    /// </summary>
    /// <param name="schema">Schema being produced by the OpenAPI pipeline.</param>
    /// <param name="context">Transformer context (includes CLR type information when available).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A completed task.</returns>
    public Task TransformAsync(OpenApiSchema schema, OpenApiSchemaTransformerContext context, CancellationToken cancellationToken) {
        ApplySchemaExtensions(schema, context.JsonTypeInfo?.Type);
        return Task.CompletedTask;
    }

    internal static void ApplySchemaExtensions(OpenApiSchema schema, Type? type) {
        if (type is not null) {
            if (!ClrTypeFormatter.Aliases.ContainsKey(type)) {
                string key;
                string value;
                if (type.IsArray || type.IsGenericType) {
                    key = "x-compile";
                    value = ClrTypeFormatter.Format(type);
                }
                else {
                    key = "x-metadata";
                    value = GetMetadataName(type);
                }

                schema.Extensions ??= new Dictionary<string, IOpenApiExtension>();
                schema.Extensions[key] = new JsonNodeExtension(value);
            }
        }
    }

    private static string GetMetadataName(Type type) {
        if (type.IsNested)
            return $"{GetMetadataName(type.DeclaringType!)}+{type.Name}";

        if (type.Namespace is null)
            return type.Name;

        return $"{type.Namespace}.{type.Name}";
    }
}
