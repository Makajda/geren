using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace Geren.Server;

public sealed class Transformer : IOpenApiSchemaTransformer {
    public Task TransformAsync(OpenApiSchema schema, OpenApiSchemaTransformerContext context, CancellationToken cancellationToken) {
        var type = context.JsonTypeInfo?.Type;
        if (type is not null) {
            string xtype = ClrTypeFormatter.Format(type);
            if (!ClrTypeFormatter.Aliases.Contains(xtype)) {
                string key = type.IsGenericType ? "x-generic" : "x-type";
                schema.Extensions ??= new Dictionary<string, IOpenApiExtension>();
                schema.Extensions.Add(key, new JsonNodeExtension(xtype));
            }
        }

        return Task.CompletedTask;
    }
}
