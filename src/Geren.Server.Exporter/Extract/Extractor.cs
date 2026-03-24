using Microsoft.CodeAnalysis.Operations;

namespace Geren.Server.Exporter.Extract;

internal static class Extractor {
    public static (List<Endpoint>, List<Dide.Warning>) Extract(Compilation compilation, CancellationToken cancellationToken) {
        List<Endpoint> endpoints = [];
        List<Dide.Warning> warnings = [];
        var endpointRouteBuilder = compilation.GetTypeByMetadataName("Microsoft.AspNetCore.Routing.IEndpointRouteBuilder");
        if (endpointRouteBuilder is null) {
            warnings.Add(Dide.Create("GERENEXP001", "Unable to find Microsoft.AspNetCore.Routing.IEndpointRouteBuilder in compilation; no endpoints will be discovered."));
            return (endpoints, warnings);
        }

        foreach (var tree in compilation.SyntaxTrees.Where(n => ValidSyntaxTree(n.FilePath))) {
            cancellationToken.ThrowIfCancellationRequested();

            var root = tree.GetRoot(cancellationToken);
            var semanticModel = compilation.GetSemanticModel(tree, ignoreAccessibility: true);

            foreach (var invocation in root.DescendantNodes().OfType<InvocationExpressionSyntax>())
                AddOne(compilation, endpointRouteBuilder, semanticModel, invocation, endpoints, warnings, cancellationToken);
        }

        endpoints.Sort(static (a, b) => {
            var c = StringComparer.Ordinal.Compare(a.RouteTemplate, b.RouteTemplate);
            if (c != 0) return c;

            var aMethod = a.HttpMethods.Length == 0 ? "" : a.HttpMethods[0];
            var bMethod = b.HttpMethods.Length == 0 ? "" : b.HttpMethods[0];
            c = StringComparer.Ordinal.Compare(aMethod, bMethod);
            if (c != 0) return c;

            return StringComparer.Ordinal.Compare(a.Handler, b.Handler);
        });

        return (endpoints, warnings);
    }

    private static void AddOne(
        Compilation compilation,
        INamedTypeSymbol endpointRouteBuilder,
        SemanticModel semanticModel,
        InvocationExpressionSyntax invocation,
        List<Endpoint> endpoints,
        List<Dide.Warning> warnings,
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

        var parameters = InferParameters.Run(compilation, handlerMethod, routeParameterNames, httpMethods);
        var returnType = UnwrapReturnType(handlerMethod.ReturnType, compilation);

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

    private static bool ValidSyntaxTree(string? filePath) {
        if (string.IsNullOrWhiteSpace(filePath))
            return true;

        if (filePath.EndsWith(".g.cs", StringComparison.OrdinalIgnoreCase))
            return false;

        return !filePath.Contains("\\obj\\", StringComparison.OrdinalIgnoreCase)
            && !filePath.Contains("/obj/", StringComparison.OrdinalIgnoreCase)
            && !filePath.Contains("\\bin\\", StringComparison.OrdinalIgnoreCase)
            && !filePath.Contains("/bin/", StringComparison.OrdinalIgnoreCase);
    }

    private static ITypeSymbol? UnwrapReturnType(ITypeSymbol returnType, Compilation compilation) {
        if (returnType.SpecialType == SpecialType.System_Void)
            return null;

        var taskOfT = compilation.GetTypeByMetadataName("System.Threading.Tasks.Task`1");
        var valueTaskOfT = compilation.GetTypeByMetadataName("System.Threading.Tasks.ValueTask`1");
        var task = compilation.GetTypeByMetadataName("System.Threading.Tasks.Task");
        var valueTask = compilation.GetTypeByMetadataName("System.Threading.Tasks.ValueTask");
        var actionResultOfT = compilation.GetTypeByMetadataName("Microsoft.AspNetCore.Mvc.ActionResult`1");
        var actionResult = compilation.GetTypeByMetadataName("Microsoft.AspNetCore.Mvc.ActionResult");
        var iActionResult = compilation.GetTypeByMetadataName("Microsoft.AspNetCore.Mvc.IActionResult");
        var iResult = compilation.GetTypeByMetadataName("Microsoft.AspNetCore.Http.IResult");
        const string HttpResultsNamespace = "Microsoft.AspNetCore.Http.HttpResults";
        const string ResultsUnionNamespace = "Microsoft.AspNetCore.Http.Results";

        static bool IsOrImplements(ITypeSymbol type, INamedTypeSymbol target) {
            if (SymbolEqualityComparer.Default.Equals(type, target))
                return true;

            if (type is not INamedTypeSymbol named)
                return false;

            if (target.TypeKind == TypeKind.Interface) {
                foreach (var iface in named.AllInterfaces)
                    if (SymbolEqualityComparer.Default.Equals(iface, target))
                        return true;

                return false;
            }

            for (INamedTypeSymbol? t = named; t is not null; t = t.BaseType)
                if (SymbolEqualityComparer.Default.Equals(t, target))
                    return true;

            return false;
        }

        ITypeSymbol current = returnType;
        while (true) {
            if (current is INamedTypeSymbol named && named.TypeArguments.Length == 1) {
                var original = named.OriginalDefinition;
                if (taskOfT is not null && SymbolEqualityComparer.Default.Equals(original, taskOfT)) {
                    current = named.TypeArguments[0];
                    continue;
                }

                if (valueTaskOfT is not null && SymbolEqualityComparer.Default.Equals(original, valueTaskOfT)) {
                    current = named.TypeArguments[0];
                    continue;
                }

                if (actionResultOfT is not null && SymbolEqualityComparer.Default.Equals(original, actionResultOfT)) {
                    current = named.TypeArguments[0];
                    continue;
                }

                // Minimal API TypedResults.* return wrappers under Microsoft.AspNetCore.Http.HttpResults (e.g., Ok<T>, Created<T>).
                if (named.ContainingNamespace?.ToDisplayString() == HttpResultsNamespace) {
                    current = named.TypeArguments[0];
                    continue;
                }
            }

            // Minimal API Results<...> union wrapper (e.g., Results<Ok<T>, NotFound>).
            if (current is INamedTypeSymbol resultsUnion
                && resultsUnion.Name.Equals("Results", StringComparison.Ordinal)
                && resultsUnion.TypeArguments.Length > 0
                && resultsUnion.ContainingNamespace?.ToDisplayString() == ResultsUnionNamespace) {

                HashSet<ITypeSymbol> payloadTypes = new(SymbolEqualityComparer.Default);
                foreach (var alt in resultsUnion.TypeArguments) {
                    if (alt is not INamedTypeSymbol altNamed || altNamed.TypeArguments.Length != 1)
                        continue;

                    if (altNamed.ContainingNamespace?.ToDisplayString() == HttpResultsNamespace) {
                        payloadTypes.Add(altNamed.TypeArguments[0]);
                        continue;
                    }

                    var altOriginal = altNamed.OriginalDefinition;
                    if (actionResultOfT is not null && SymbolEqualityComparer.Default.Equals(altOriginal, actionResultOfT)) {
                        payloadTypes.Add(altNamed.TypeArguments[0]);
                        continue;
                    }
                }

                if (payloadTypes.Count == 1) {
                    current = payloadTypes.Single();
                    continue;
                }

                return null;
            }

            break;
        }

        if (task is not null && SymbolEqualityComparer.Default.Equals(current, task))
            return null;

        if (valueTask is not null && SymbolEqualityComparer.Default.Equals(current, valueTask))
            return null;

        // "Zoo": returning IActionResult/IResult/etc. does not expose a stable DTO type.
        if (actionResult is not null && IsOrImplements(current, actionResult))
            return null;

        if (iActionResult is not null && IsOrImplements(current, iActionResult))
            return null;

        if (iResult is not null && IsOrImplements(current, iResult))
            return null;

        return current;
    }
}
