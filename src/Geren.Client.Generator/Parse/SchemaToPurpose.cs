namespace Geren.Client.Generator.Parse;

internal static class SchemaToPurpose {
    internal static PurposeType Convert(IOpenApiSchema? schema) {
        const string defaultType = "object";
        if (schema is null)
            return new(defaultType);

        bool hasExtensions = schema.Extensions is not null;
        if (hasExtensions && schema.Extensions!.TryGetValue("x-metadata", out IOpenApiExtension nodeExtension)) {
            if (nodeExtension is JsonNodeExtension node)
                return new(node.Node.GetValue<string>(), Byres.Metadata);
        }
        else if (hasExtensions && schema.Extensions!.TryGetValue("x-compile", out IOpenApiExtension nodeGeneric)) {
            if (nodeGeneric is JsonNodeExtension node)
                return new(Given.ArraysRestore(node.Node.GetValue<string>()), Byres.Compile);
        }
        else if (schema is OpenApiSchemaReference schemaReference)
            if (schemaReference.Reference.Id is not null)
                return new(schemaReference.Reference.Id, Byres.Reference);

        if (schema.Format == "int64") return new("long");
        if (schema.Format == "int32") return new("int");

        if (schema.Type.HasValue) return new(schema.Type.Value switch {
            JsonSchemaType.Null => "string",
            JsonSchemaType.Boolean => "bool",
            JsonSchemaType.Integer => "int",
            JsonSchemaType.Number => "double",
            JsonSchemaType.String => "string",
            JsonSchemaType.Object => "object",
            JsonSchemaType.Array => $"System.Collections.Generic.IReadOnlyList<{Convert(schema.Items).Name}>",
            _ => defaultType
        });
        return new(defaultType);
    }
}
