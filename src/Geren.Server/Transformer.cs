using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace Geren.Server;

public sealed class Transformer : IOpenApiSchemaTransformer {
    public Task TransformAsync(OpenApiSchema schema, OpenApiSchemaTransformerContext context, CancellationToken cancellationToken) {
        var type = context.JsonTypeInfo?.Type;
        if (type is not null) {
            if (!ClrTypeFormatter.Aliases.ContainsKey(type)) {
                string key;
                string value;
                if (type.IsGenericType) {
                    key = "x-generic";
                    value = ClrTypeFormatter.Format(type);
                }
                else {
                    key = "x-type";
                    value = GetMetadataName(type);
                }

                schema.Extensions ??= new Dictionary<string, IOpenApiExtension>();
                schema.Extensions.Add(key, new JsonNodeExtension(value));
            }
        }

        return Task.CompletedTask;
    }

    private static string GetMetadataName(Type type) {
        if (type.IsNested)
            return $"{GetMetadataName(type.DeclaringType!)}+{type.Name}";

        if (type.Namespace is null)
            return type.Name;

        return $"{type.Namespace}.{type.Name}";
    }
}
