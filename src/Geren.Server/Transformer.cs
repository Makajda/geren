using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace Geren.Server;

public sealed class Transformer : IOpenApiSchemaTransformer {
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
                schema.Extensions.Add(key, new JsonNodeExtension(value));
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
