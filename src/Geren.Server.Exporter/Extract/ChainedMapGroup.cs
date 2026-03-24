namespace Geren.Server.Exporter.Extract;

internal static class ChainedMapGroup {
    internal static string AddPrefix(
        string routeTemplate,
        INamedTypeSymbol endpointRouteBuilder,
        SemanticModel semanticModel,
        ExpressionSyntax receiverExpression,
        CancellationToken cancellationToken) {

        List<string> prefixesReversed = [];
        ExpressionSyntax current = UnwrapExpression(receiverExpression);
        HashSet<ISymbol> visitedReceiverSymbols = new(SymbolEqualityComparer.Default);

        while (true) {
            cancellationToken.ThrowIfCancellationRequested();

            if (current is not InvocationExpressionSyntax invocation) {
                if (TryResolveReceiverAliasExpression(semanticModel, current, visitedReceiverSymbols, cancellationToken, out var aliasValue)) {
                    current = UnwrapExpression(aliasValue);
                    continue;
                }

                break;
            }

            var methodSymbol = semanticModel.GetSymbolInfo(invocation, cancellationToken).Symbol as IMethodSymbol;
            methodSymbol ??= semanticModel.GetSymbolInfo(invocation, cancellationToken).CandidateSymbols.OfType<IMethodSymbol>().FirstOrDefault();

            if (methodSymbol is null)
                break;

            if (methodSymbol.Name.Equals("MapGroup", StringComparison.Ordinal)
                && Given.IsEndpointRouteBuilderExtension(methodSymbol, endpointRouteBuilder)
                && TryGetMapGroupPrefix(semanticModel, invocation, methodSymbol, cancellationToken, out var groupPrefix)) {

                prefixesReversed.Add(groupPrefix);

                if (invocation.Expression is MemberAccessExpressionSyntax ma) {
                    current = UnwrapExpression(ma.Expression);
                    continue;
                }

                break;
            }

            // Allow walking "builder configuration" calls that still return a route builder:
            // app.MapGroup(...).WithTags(...).RequireAuthorization(...).MapGet(...)
            if (Given.IsEndpointRouteBuilderType(methodSymbol.ReturnType, endpointRouteBuilder)
                && invocation.Expression is MemberAccessExpressionSyntax receiverAccess) {
                current = UnwrapExpression(receiverAccess.Expression);
                continue;
            }

            break;
        }

        if (prefixesReversed.Count > 0) {
            string prefix = string.Empty;
            for (var i = prefixesReversed.Count - 1; i >= 0; i--)
                prefix = CombineRouteTemplates(prefix, prefixesReversed[i]);

            return CombineRouteTemplates(prefix, routeTemplate);
        }

        return routeTemplate;
    }

    private static bool TryResolveReceiverAliasExpression(
        SemanticModel semanticModel,
        ExpressionSyntax expression,
        HashSet<ISymbol> visitedReceiverSymbols,
        CancellationToken cancellationToken,
        out ExpressionSyntax value) {

        expression = UnwrapExpression(expression);

        var symbolInfo = semanticModel.GetSymbolInfo(expression, cancellationToken);
        var symbol = symbolInfo.Symbol ?? symbolInfo.CandidateSymbols.FirstOrDefault();
        if (symbol is null) {
            value = null!;
            return false;
        }

        if (!visitedReceiverSymbols.Add(symbol)) {
            value = null!;
            return false;
        }

        if (symbol is ILocalSymbol local) {
            foreach (var syntaxRef in local.DeclaringSyntaxReferences) {
                if (syntaxRef.GetSyntax(cancellationToken) is not VariableDeclaratorSyntax declarator)
                    continue;

                if (declarator.Initializer?.Value is ExpressionSyntax initializer) {
                    value = initializer;
                    return true;
                }
            }
        }

        if (symbol is IFieldSymbol field) {
            foreach (var syntaxRef in field.DeclaringSyntaxReferences) {
                if (syntaxRef.GetSyntax(cancellationToken) is VariableDeclaratorSyntax declarator
                    && declarator.Initializer?.Value is ExpressionSyntax initializer) {
                    value = initializer;
                    return true;
                }
            }
        }

        if (symbol is IPropertySymbol property && property.GetMethod is not null) {
            foreach (var syntaxRef in property.GetMethod.DeclaringSyntaxReferences) {
                var syntax = syntaxRef.GetSyntax(cancellationToken);
                ExpressionSyntax? getterExpression = syntax switch {
                    AccessorDeclarationSyntax a => a.ExpressionBody?.Expression ?? TryGetSingleReturnExpression(a.Body),
                    _ => null
                };

                if (getterExpression is null)
                    continue;

                value = getterExpression;
                return true;
            }
        }

        value = null!;
        return false;

        static ExpressionSyntax? TryGetSingleReturnExpression(BlockSyntax? body) {
            if (body is null)
                return null;

            var returns = body.Statements.OfType<ReturnStatementSyntax>().ToArray();
            return returns.Length == 1 ? returns[0].Expression : null;
        }
    }

    private static bool TryGetMapGroupPrefix(
        SemanticModel semanticModel,
        InvocationExpressionSyntax invocation,
        IMethodSymbol methodSymbol,
        CancellationToken cancellationToken,
        out string prefix) {

        if (invocation.ArgumentList is null) {
            prefix = string.Empty;
            return false;
        }

        int stringArgIndex = Given.GetRouteTemplateArgumentIndex(methodSymbol);
        if (stringArgIndex >= 0 && invocation.ArgumentList.Arguments.Count > stringArgIndex) {
            return Given.TryGetConstantString(
                semanticModel,
                invocation.ArgumentList.Arguments[stringArgIndex].Expression,
                cancellationToken,
                out prefix);
        }

        prefix = string.Empty;
        return false;
    }

    private static ExpressionSyntax UnwrapExpression(ExpressionSyntax expression) {
        while (true) {
            switch (expression) {
                case ParenthesizedExpressionSyntax p:
                    expression = p.Expression;
                    continue;
                case CastExpressionSyntax c:
                    expression = c.Expression;
                    continue;
                case PostfixUnaryExpressionSyntax u
                    when u.IsKind(Microsoft.CodeAnalysis.CSharp.SyntaxKind.SuppressNullableWarningExpression):
                    expression = u.Operand;
                    continue;
                default:
                    return expression;
            }
        }
    }

    private static string CombineRouteTemplates(string left, string right) {
        if (left.Length == 0) return right;
        if (right.Length == 0) return left;

        bool leftEndsWithSlash = left[^1] == '/';
        bool rightStartsWithSlash = right[0] == '/';

        if (leftEndsWithSlash && rightStartsWithSlash)
            return string.Concat(left.AsSpan(0, left.Length - 1), right);

        if (!leftEndsWithSlash && !rightStartsWithSlash)
            return left + "/" + right;

        return left + right;
    }
}
