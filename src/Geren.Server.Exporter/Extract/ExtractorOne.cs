using Microsoft.CodeAnalysis.Operations;

namespace Geren.Server.Exporter.Extract;

internal static class ExtractorOne {
    internal static void Extract(
        Compilation compilation,
        INamedTypeSymbol endpointRouteBuilder,
        SemanticModel semanticModel,
        InvocationExpressionSyntax invocation,
        string[] excludeTypes,
        ImmutableArray<Erpoint>.Builder endpoints,
        ImmutableArray<ErWarning>.Builder warnings,
        CancellationToken cancellationToken) {

        cancellationToken.ThrowIfCancellationRequested();

        var methodName = invocation.Expression switch {
            MemberAccessExpressionSyntax m => m.Name.Identifier.ValueText,
            IdentifierNameSyntax i => i.Identifier.ValueText,
            GenericNameSyntax g => g.Identifier.ValueText,
            _ => null,
        };
        if (methodName is null || !methodName.StartsWith("Map", StringComparison.Ordinal))
            return;

        var methodSymbol = semanticModel.GetSymbolInfo(invocation, cancellationToken).Symbol as IMethodSymbol;
        methodSymbol ??= semanticModel.GetSymbolInfo(invocation, cancellationToken).CandidateSymbols.OfType<IMethodSymbol>().FirstOrDefault();
        if (methodSymbol is null
            || !methodSymbol.Name.StartsWith("Map", StringComparison.Ordinal)
            || !Given.IsEndpointRouteBuilderExtension(methodSymbol, endpointRouteBuilder))
            return;

        var routeTemplateArgIndex = Given.GetRouteTemplateArgumentIndex(methodSymbol);
        var handlerArgIndex = GetHandlerArgumentIndex(compilation, methodSymbol);
        if (routeTemplateArgIndex < 0 || handlerArgIndex < 0)
            return;

        if (invocation.ArgumentList is null || invocation.ArgumentList.Arguments.Count <= Math.Max(routeTemplateArgIndex, handlerArgIndex))
            return;

        var routeTemplateExpression = invocation.ArgumentList.Arguments[routeTemplateArgIndex].Expression;
        if (!Given.TryGetConstantString(semanticModel, routeTemplateExpression, cancellationToken, out var routeTemplate)) {
            warnings.Add(Dide.Create(invocation, "GERENEXP002", $"Skipped '{methodSymbol.Name}': route template is not a constant string."));
            return;
        }

        if (invocation.Expression is MemberAccessExpressionSyntax memberAccess)
            routeTemplate = ChainedMapGroup.AddPrefix(routeTemplate, endpointRouteBuilder, semanticModel, memberAccess.Expression, cancellationToken);

        routeTemplate = NormalizeRouteTemplate(routeTemplate);

        var routeParameterNames = RouteParameterNames.Extract(routeTemplate);

        var handlerExpression = invocation.ArgumentList.Arguments[handlerArgIndex].Expression;
        var operation = semanticModel.GetOperation(handlerExpression, cancellationToken);
        var handlerMethod = operation is null ? null : GetHandlerMethodSymbol(operation);
        if (handlerMethod is null) {
            warnings.Add(Dide.Create(invocation, "GERENEXP003", $"Skipped '{methodSymbol.Name}': unable to resolve handler method symbol."));
            return;
        }

        var httpMethods = HttpMethods.Get(methodSymbol, invocation, semanticModel, cancellationToken);
        if (httpMethods.IsEmpty) {
            warnings.Add(Dide.Create(invocation, "GERENEXP004", $"Skipped '{methodSymbol.Name}': unknown HTTP method(s) (unable to infer from map call)."));
            return;
        }

        var parameters = InferParameters.Get(handlerMethod, routeParameterNames, httpMethods, excludeTypes);
        var returnType = ReturnType.Unwrap(handlerMethod.ReturnType, compilation);

        endpoints.Add(new(
            HttpMethods: httpMethods,
            RouteTemplate: routeTemplate,
            RouteParameters: [.. routeParameterNames.OrderBy(static p => p, StringComparer.Ordinal)],
            Handler: handlerMethod.ToDisplayString(Given.FullyQualifiedMethodFormat),
            Parameters: parameters,
            ReturnType: returnType?.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)));
    }

    private static string NormalizeRouteTemplate(string template) {
        if (string.IsNullOrWhiteSpace(template))
            return "/";

        template = template.Trim().Replace('\\', '/');

        // Treat "~/..." as rooted.
        if (template.StartsWith("~/", StringComparison.Ordinal))
            template = template[1..];

        // Collapse duplicate slashes.
        var sb = new StringBuilder(template.Length);
        bool prevSlash = false;
        foreach (char ch in template) {
            if (ch == '/') {
                if (prevSlash)
                    continue;

                prevSlash = true;
                sb.Append('/');
                continue;
            }

            prevSlash = false;
            sb.Append(ch);
        }

        template = sb.ToString();

        if (!template.StartsWith('/'))
            template = "/" + template;

        // Keep "/" but trim trailing slashes for other routes.
        while (template.Length > 1 && template.EndsWith('/'))
            template = template[..^1];

        return template;
    }

    private static int GetHandlerArgumentIndex(Compilation compilation, IMethodSymbol methodSymbol) {
        var delegateType = compilation.GetTypeByMetadataName("System.Delegate");
        for (var i = methodSymbol.Parameters.Length - 1; i >= 0; i--) {
            var paramType = methodSymbol.Parameters[i].Type;
            if (delegateType is not null && SymbolEqualityComparer.Default.Equals(paramType, delegateType))
                return i;

            if (paramType.TypeKind == TypeKind.Delegate)
                return i;
        }

        return -1;
    }

    private static IMethodSymbol? GetHandlerMethodSymbol(IOperation operation) {
        while (true) {
            switch (operation) {
                case IDelegateCreationOperation d:
                    operation = d.Target;
                    continue;
                case IConversionOperation c:
                    operation = c.Operand;
                    continue;
                case IParenthesizedOperation p:
                    operation = p.Operand;
                    continue;
                case IAnonymousFunctionOperation a:
                    return a.Symbol;
                case IMethodReferenceOperation m:
                    return m.Method;
                default:
                    return null;
            }
        }
    }
}
