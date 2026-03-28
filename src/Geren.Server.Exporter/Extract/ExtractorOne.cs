using Microsoft.CodeAnalysis.Operations;

namespace Geren.Server.Exporter.Extract;

internal static class ExtractorOne {
    internal static void Extract(
        Compilation compilation,
        INamedTypeSymbol endpointRouteBuilder,
        SemanticModel semanticModel,
        InvocationExpressionSyntax invocation,
        string[] excludeTypes,
        ImmutableArray<Purpoint>.Builder endpoints,
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
            warnings.Add(Dide.Create(invocation, "GERENEXP002", $"Skipped '{methodSymbol.Name}': route template is not a constant string"));
            return;
        }

        if (invocation.Expression is MemberAccessExpressionSyntax memberAccess)
            routeTemplate = ChainedMapGroup.AddPrefix(routeTemplate, endpointRouteBuilder, semanticModel, memberAccess.Expression, cancellationToken);

        routeTemplate = NormalizeRouteTemplate(routeTemplate);
        routeTemplate = NormalizeRouteTemplatePlaceholders(routeTemplate);

        var routeParameterNames = RouteParameterNames.Extract(routeTemplate);

        var handlerExpression = invocation.ArgumentList.Arguments[handlerArgIndex].Expression;
        var operation = semanticModel.GetOperation(handlerExpression, cancellationToken);
        var handlerMethod = operation is null ? null : GetHandlerMethodSymbol(operation);
        if (handlerMethod is null) {
            warnings.Add(Dide.Create(invocation, "GERENEXP003", $"Skipped '{methodSymbol.Name}': unable to resolve handler method symbol"));
            return;
        }

        var httpMethod = HttpMethods.Get(methodSymbol, invocation, semanticModel, cancellationToken);
        if (string.IsNullOrEmpty(httpMethod)) {
            warnings.Add(Dide.Create(invocation, "GERENEXP004", $"Skipped '{methodSymbol.Name}': unknown HTTP method"));
            return;
        }

        var (returnType, returnTypeBy) = ReturnType.Unwrap(handlerMethod.ReturnType, compilation);
        var (bodyType, bodyTypeBy, bodyMedia, @params, queries) = InferParameters.Get(handlerMethod, routeParameterNames, httpMethod, excludeTypes);

        endpoints.Add(new(httpMethod, routeTemplate, null, returnType, returnTypeBy, bodyType, bodyTypeBy, bodyMedia, @params, queries));
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

    private static string NormalizeRouteTemplatePlaceholders(string template) {
        if (template.Length == 0)
            return template;

        StringBuilder? sb = null;
        int i = 0;
        while (i < template.Length) {
            char ch = template[i];
            if (ch == '{') {
                if (i + 1 < template.Length && template[i + 1] == '{') {
                    sb ??= new StringBuilder(template.Length);
                    sb.Append("{{");
                    i += 2;
                    continue;
                }

                int end = template.IndexOf('}', i + 1);
                if (end < 0) {
                    sb?.Append(template, i, template.Length - i);
                    break;
                }

                string inner = template.Substring(i + 1, end - i - 1);
                string name = NormalizeRouteParameterInnerText(inner);

                if (sb is not null) {
                    if (name.Length == 0)
                        sb.Append(template, i, end - i + 1);
                    else
                        sb.Append('{').Append(name).Append('}');
                }
                else {
                    // Only allocate if we need to change anything (including whitespace cleanup).
                    if (name.Length != 0 && !inner.AsSpan().Trim().SequenceEqual(name.AsSpan())) {
                        sb = new StringBuilder(template.Length);
                        sb.Append(template, 0, i);
                        sb.Append('{').Append(name).Append('}');
                    }
                }

                i = end + 1;
                continue;
            }

            if (ch == '}' && i + 1 < template.Length && template[i + 1] == '}') {
                sb ??= new StringBuilder(template.Length);
                sb.Append("}}");
                i += 2;
                continue;
            }

            sb?.Append(ch);
            i++;
        }

        return sb is null ? template : sb.ToString();
    }

    private static string NormalizeRouteParameterInnerText(string inner) {
        string trimmed = inner.Trim();
        if (trimmed.Length == 0)
            return string.Empty;

        int start = 0;
        while (start < trimmed.Length && trimmed[start] == '*')
            start++;

        trimmed = trimmed[start..];

        int cutIndex = trimmed.Length;
        int colonIndex = trimmed.IndexOf(':');
        if (colonIndex >= 0)
            cutIndex = Math.Min(cutIndex, colonIndex);

        int questionIndex = trimmed.IndexOf('?');
        if (questionIndex >= 0)
            cutIndex = Math.Min(cutIndex, questionIndex);

        int equalsIndex = trimmed.IndexOf('=');
        if (equalsIndex >= 0)
            cutIndex = Math.Min(cutIndex, equalsIndex);

        return trimmed[..cutIndex].Trim();
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
