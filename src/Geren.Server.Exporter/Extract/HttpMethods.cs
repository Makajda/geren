namespace Geren.Server.Exporter.Extract;

internal static class HttpMethods {
    internal static ImmutableArray<string> Get(
        IMethodSymbol mapMethod,
        InvocationExpressionSyntax invocation,
        SemanticModel semanticModel,
        CancellationToken cancellationToken) {

        string name = mapMethod.Name;
        if (name.Equals("MapGet", StringComparison.Ordinal)) return ["GET"];
        if (name.Equals("MapPost", StringComparison.Ordinal)) return ["POST"];
        if (name.Equals("MapPut", StringComparison.Ordinal)) return ["PUT"];
        if (name.Equals("MapPatch", StringComparison.Ordinal)) return ["PATCH"];
        if (name.Equals("MapDelete", StringComparison.Ordinal)) return ["DELETE"];
        if (name.Equals("MapHead", StringComparison.Ordinal)) return ["HEAD"];
        if (name.Equals("MapOptions", StringComparison.Ordinal)) return ["OPTIONS"];

        if (name.Equals("MapMethods", StringComparison.Ordinal)) {
            int methodsArgIndex = FindHttpMethodsArgumentIndex(mapMethod);
            if (methodsArgIndex >= 0
                && invocation.ArgumentList is not null
                && invocation.ArgumentList.Arguments.Count > methodsArgIndex
                && TryGetConstantStringList(semanticModel, invocation.ArgumentList.Arguments[methodsArgIndex].Expression, cancellationToken, out var methods)) {
                return methods;
            }

            return [];
        }

        return [];
    }

    private static int FindHttpMethodsArgumentIndex(IMethodSymbol mapMethod) {
        for (int i = 0; i < mapMethod.Parameters.Length; i++) {
            var p = mapMethod.Parameters[i];
            if (p.Type is INamedTypeSymbol named
                && named.TypeArguments.Length == 1
                && named.TypeArguments[0].SpecialType == SpecialType.System_String
                && p.Name.Contains("method", StringComparison.OrdinalIgnoreCase)) {
                return i;
            }
        }

        for (int i = 0; i < mapMethod.Parameters.Length; i++) {
            var p = mapMethod.Parameters[i];
            if (p.Type is INamedTypeSymbol named
                && named.TypeArguments.Length == 1
                && named.TypeArguments[0].SpecialType == SpecialType.System_String) {
                return i;
            }
        }

        return -1;
    }

    private static bool TryGetConstantStringList(
        SemanticModel semanticModel,
        ExpressionSyntax expression,
        CancellationToken cancellationToken,
        out ImmutableArray<string> values) {

        if (expression is ArrayCreationExpressionSyntax arrayCreation
            && arrayCreation.Initializer is not null
            && TryGetConstantStringListFromInitializer(semanticModel, arrayCreation.Initializer.Expressions, cancellationToken, out values)) {
            return true;
        }

        if (expression is ImplicitArrayCreationExpressionSyntax implicitArray
            && implicitArray.Initializer is not null
            && TryGetConstantStringListFromInitializer(semanticModel, implicitArray.Initializer.Expressions, cancellationToken, out values)) {
            return true;
        }

        if (expression is InitializerExpressionSyntax initializer
            && TryGetConstantStringListFromInitializer(semanticModel, initializer.Expressions, cancellationToken, out values)) {
            return true;
        }

        values = [];
        return false;
    }

    private static bool TryGetConstantStringListFromInitializer(
        SemanticModel semanticModel,
        SeparatedSyntaxList<ExpressionSyntax> expressions,
        CancellationToken cancellationToken,
        out ImmutableArray<string> values) {

        var builder = ImmutableArray.CreateBuilder<string>();
        foreach (var e in expressions) {
            if (!Given.TryGetConstantString(semanticModel, e, cancellationToken, out var s) || s.Length == 0) {
                values = [];
                return false;
            }

            builder.Add(s.ToUpperInvariant());
        }

        values = builder.ToImmutable();
        return values.Length != 0;
    }
}
