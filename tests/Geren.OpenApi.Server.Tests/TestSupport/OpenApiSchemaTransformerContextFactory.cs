namespace Geren.OpenApi.Server.Tests.TestSupport;

internal static class OpenApiSchemaTransformerContextFactory {
    internal static OpenApiSchemaTransformerContext Create(Type type) {
        var context = (OpenApiSchemaTransformerContext)RuntimeHelpers.GetUninitializedObject(typeof(OpenApiSchemaTransformerContext));
        var jsonTypeInfo = new DefaultJsonTypeInfoResolver().GetTypeInfo(type, new JsonSerializerOptions());

        SetBackingField(context, "JsonTypeInfo", jsonTypeInfo);
        SetBackingField(context, "DocumentName", "tests");
        TrySetBackingField(context, "ApplicationServices", null);

        return context;
    }

    internal static OpenApiSchemaTransformerContext CreateWithoutType() {
        var context = (OpenApiSchemaTransformerContext)RuntimeHelpers.GetUninitializedObject(typeof(OpenApiSchemaTransformerContext));
        SetBackingField(context, "DocumentName", "tests");
        TrySetBackingField(context, "ApplicationServices", null);
        return context;
    }

    private static void SetBackingField(object target, string propertyName, object? value) {
        var field = GetField(target.GetType(), propertyName);
        field.SetValue(target, value);
    }

    private static void TrySetBackingField(object target, string propertyName, object? value) {
        var field = target.GetType()
            .GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
            .SingleOrDefault(info => info.Name.Contains(propertyName, StringComparison.Ordinal));

        field?.SetValue(target, value);
    }

    private static FieldInfo GetField(Type type, string propertyName) {
        return type
            .GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
            .Single(info => info.Name.Contains(propertyName, StringComparison.Ordinal));
    }
}
