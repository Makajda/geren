using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace Geren.Server;

public sealed class Transformer : IOpenApiSchemaTransformer {
    public Task TransformAsync(OpenApiSchema schema, OpenApiSchemaTransformerContext context, CancellationToken cancellationToken) {
        var type = context.JsonTypeInfo?.Type;
        if (type is not null && type.IsGenericType) {
            schema.Extensions ??= new Dictionary<string, IOpenApiExtension>();
            //schema.Extensions.Add("x-dotnet-type", new JsonNodeExtension(Convert.ToBase64String(Encoding.UTF8.GetBytes(ClrTypeFormatter.Format(type))).Replace("==", "")));
            schema.Extensions.Add("x-dotnet-type", new JsonNodeExtension(ClrTypeFormatter.Format(type)));
        }

        return Task.CompletedTask;
    }
}
