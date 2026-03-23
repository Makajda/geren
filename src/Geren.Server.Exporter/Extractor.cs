using Microsoft.CodeAnalysis.Operations;

namespace Geren.Server.Exporter;

internal static class Extractor {
    public static (List<Endpoint>, List<Dide.WarningSpec>) Extract(Compilation compilation, CancellationToken cancellationToken) {
        List<Endpoint> endpoints = [];
        List<Dide.WarningSpec> warnings = [];
        var endpointRouteBuilder = compilation.GetTypeByMetadataName("Microsoft.AspNetCore.Routing.IEndpointRouteBuilder");
        if (endpointRouteBuilder is null) {
            warnings.Add(Dide.Create("GERENEXP001", "Unable to find Microsoft.AspNetCore.Routing.IEndpointRouteBuilder in compilation; no endpoints will be discovered."));
            return (endpoints, warnings);
        }

        foreach (var tree in compilation.SyntaxTrees) {
            cancellationToken.ThrowIfCancellationRequested();

            if (ShouldSkipSyntaxTree(tree.FilePath))
                continue;

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
        List<Dide.WarningSpec> warnings,
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
        if (methodSymbol is null || !methodSymbol.Name.StartsWith("Map", StringComparison.Ordinal))
            return;

        if (!IsEndpointRouteBuilderExtension(methodSymbol, endpointRouteBuilder))
            return;

        var routeTemplateArgIndex = GetRouteTemplateArgumentIndex(methodSymbol);
        var handlerArgIndex = GetHandlerArgumentIndex(compilation, methodSymbol);
        if (routeTemplateArgIndex < 0 || handlerArgIndex < 0)
            return;

        if (invocation.ArgumentList is null || invocation.ArgumentList.Arguments.Count <= Math.Max(routeTemplateArgIndex, handlerArgIndex))
            return;

        var routeTemplateExpression = invocation.ArgumentList.Arguments[routeTemplateArgIndex].Expression;
        if (!TryGetConstantString(semanticModel, routeTemplateExpression, cancellationToken, out var routeTemplate)) {
            warnings.Add(Dide.Create(invocation, "GERENEXP002", $"Skipped '{methodSymbol.Name}': route template is not a constant string."));
            return;
        }

        if (invocation.Expression is MemberAccessExpressionSyntax memberAccess)
            routeTemplate = AddChainedMapGroupPrefix(routeTemplate, endpointRouteBuilder, semanticModel, memberAccess.Expression, cancellationToken);

        routeTemplate = NormalizeRouteTemplate(routeTemplate);

        var routeParameterNames = ExtractRouteParameterNames(routeTemplate);

        var handlerExpression = invocation.ArgumentList.Arguments[handlerArgIndex].Expression;
        var operation = semanticModel.GetOperation(handlerExpression, cancellationToken);
        var handlerMethod = operation is null ? null : GetHandlerMethodSymbol(operation);
        if (handlerMethod is null) {
            warnings.Add(Dide.Create(invocation, "GERENEXP003", $"Skipped '{methodSymbol.Name}': unable to resolve handler method symbol."));
            return;
        }

        var httpMethods = GetHttpMethods(methodSymbol, invocation, semanticModel, cancellationToken);
        if (httpMethods.IsEmpty) {
            warnings.Add(Dide.Create(invocation, "GERENEXP004", $"Skipped '{methodSymbol.Name}': unknown HTTP method(s) (unable to infer from map call)."));
            return;
        }

        var parameters = InferParameters(compilation, handlerMethod, routeParameterNames, httpMethods);
        var returnType = UnwrapReturnType(handlerMethod.ReturnType, compilation);

        endpoints.Add(new(
            HttpMethods: httpMethods,
            RouteTemplate: routeTemplate,
            RouteParameters: [.. routeParameterNames.OrderBy(static p => p, StringComparer.Ordinal)],
            Handler: handlerMethod.ToDisplayString(Given.FullyQualifiedMethodFormat),
            Parameters: parameters,
            ReturnType: returnType?.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)));
    }

    private static string AddChainedMapGroupPrefix(
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
                && IsEndpointRouteBuilderExtension(methodSymbol, endpointRouteBuilder)
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
            if (IsEndpointRouteBuilderType(methodSymbol.ReturnType, endpointRouteBuilder)
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

        int stringArgIndex = GetRouteTemplateArgumentIndex(methodSymbol);
        if (stringArgIndex >= 0 && invocation.ArgumentList.Arguments.Count > stringArgIndex) {
            return TryGetConstantString(
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

    private static bool IsEndpointRouteBuilderExtension(IMethodSymbol methodSymbol, INamedTypeSymbol endpointRouteBuilder) {
        IMethodSymbol original = methodSymbol.ReducedFrom ?? methodSymbol;
        if (!original.IsExtensionMethod || original.Parameters.Length == 0)
            return false;

        return IsEndpointRouteBuilderType(original.Parameters[0].Type, endpointRouteBuilder);
    }

    private static bool IsEndpointRouteBuilderType(ITypeSymbol type, INamedTypeSymbol endpointRouteBuilder) {
        if (SymbolEqualityComparer.Default.Equals(type, endpointRouteBuilder))
            return true;

        if (type is INamedTypeSymbol named) {
            foreach (var iface in named.AllInterfaces) {
                if (SymbolEqualityComparer.Default.Equals(iface, endpointRouteBuilder))
                    return true;
            }
        }

        if (type is ITypeParameterSymbol typeParameter) {
            foreach (var constraint in typeParameter.ConstraintTypes) {
                if (IsEndpointRouteBuilderType(constraint, endpointRouteBuilder))
                    return true;
            }
        }

        return false;
    }

    private static int GetRouteTemplateArgumentIndex(IMethodSymbol methodSymbol) {
        for (var i = 0; i < methodSymbol.Parameters.Length; i++)
            if (methodSymbol.Parameters[i].Type.SpecialType == SpecialType.System_String)
                return i;

        return -1;
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

    private static bool TryGetConstantString(SemanticModel semanticModel, ExpressionSyntax expression, CancellationToken cancellationToken, out string value) {
        var constant = semanticModel.GetConstantValue(expression, cancellationToken);
        if (constant.HasValue && constant.Value is string s) {
            value = s;
            return true;
        }

        value = string.Empty;
        return false;
    }

    private static HashSet<string> ExtractRouteParameterNames(string routeTemplate) {
        HashSet<string> result = new(StringComparer.OrdinalIgnoreCase);
        if (routeTemplate.Length == 0)
            return result;

        for (var i = 0; i < routeTemplate.Length; i++) {
            if (routeTemplate[i] != '{')
                continue;

            var end = routeTemplate.IndexOf('}', i + 1);
            if (end < 0)
                break;

            string inner = routeTemplate.Substring(i + 1, end - i - 1);
            string name = NormalizeRouteParameterInnerText(inner);
            if (name.Length != 0)
                result.Add(name);

            i = end;
        }

        return result;
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

    private static ImmutableArray<string> GetHttpMethods(
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
            if (!TryGetConstantString(semanticModel, e, cancellationToken, out var s) || s.Length == 0) {
                values = [];
                return false;
            }

            builder.Add(s.ToUpperInvariant());
        }

        values = builder.ToImmutable();
        return values.Length != 0;
    }

    private static ImmutableArray<ParamSpec> InferParameters(
        Compilation compilation,
        IMethodSymbol handlerMethod,
        HashSet<string> routeParameterNames,
        ImmutableArray<string> httpMethods) {

        var allowBody = httpMethods.Any(static m => m is "POST" or "PUT" or "PATCH");
        var bodyAssigned = false;
        var efDbContext = compilation.GetTypeByMetadataName("Microsoft.EntityFrameworkCore.DbContext");

        var builder = ImmutableArray.CreateBuilder<ParamSpec>(handlerMethod.Parameters.Length);
        foreach (var parameter in handlerMethod.Parameters) {
            if (IsServicesOrInfrastructureParameter(parameter, efDbContext))
                continue;

            string source = InferParameterSource(parameter, routeParameterNames, allowBody, ref bodyAssigned);
            builder.Add(new(
                Name: parameter.Name,
                Type: parameter.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                Source: source));
        }

        return builder.ToImmutable();
    }

    private static string InferParameterSource(
        IParameterSymbol parameter,
        HashSet<string> routeParameterNames,
        bool allowBody,
        ref bool bodyAssigned) {

        if (HasFromRouteAttribute(parameter))
            return "route";

        if (routeParameterNames.Contains(parameter.Name))
            return "route";

        if (allowBody && !bodyAssigned && !IsSimpleType(parameter.Type)) {
            bodyAssigned = true;
            return "body";
        }

        return "query";
    }

    private static bool HasFromRouteAttribute(IParameterSymbol parameter) {
        foreach (var attribute in parameter.GetAttributes()) {
            var cls = attribute.AttributeClass;
            if (cls is null)
                continue;

            var metadataName = cls.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            if (metadataName == "global::Microsoft.AspNetCore.Mvc.FromRouteAttribute"
                || metadataName == "global::Microsoft.AspNetCore.Http.Metadata.FromRouteAttribute") {
                return true;
            }
        }

        return false;
    }

    private static bool IsServicesOrInfrastructureParameter(IParameterSymbol parameter, INamedTypeSymbol? efDbContext) {
        if (HasFromServicesAttribute(parameter))
            return true;

        return IsInfrastructureType(parameter.Type, efDbContext);
    }

    private static bool HasFromServicesAttribute(IParameterSymbol parameter) {
        foreach (var attribute in parameter.GetAttributes()) {
            var cls = attribute.AttributeClass;
            if (cls is null)
                continue;

            var metadataName = cls.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            if (metadataName == "global::Microsoft.AspNetCore.Mvc.FromServicesAttribute"
                || metadataName == "global::Microsoft.AspNetCore.Http.Metadata.FromServicesAttribute"
                || metadataName == "global::Microsoft.AspNetCore.Http.FromServicesAttribute") {
                return true;
            }
        }

        return false;
    }

    private static bool IsInfrastructureType(ITypeSymbol type, INamedTypeSymbol? efDbContext) {
        type = UnwrapNullable(type);

        if (type is IArrayTypeSymbol array)
            type = UnwrapNullable(array.ElementType);

        if (type is not INamedTypeSymbol named)
            return false;

        // Common binding-only parameters in Minimal APIs: not part of a client contract.
        if (named.Name == "CancellationToken" && named.ContainingNamespace?.ToDisplayString() == "System.Threading")
            return true;

        if (named.Name == "ClaimsPrincipal" && named.ContainingNamespace?.ToDisplayString() == "System.Security.Claims")
            return true;

        if (named.ContainingNamespace?.ToDisplayString() == "Microsoft.AspNetCore.Http") {
            if (named.Name is "HttpContext" or "HttpRequest" or "HttpResponse")
                return true;
        }

        // ILogger / ILogger<T>
        if (named.ContainingNamespace?.ToDisplayString() == "Microsoft.Extensions.Logging"
            && named.Name == "ILogger") {
            return true;
        }

        // Treat most Microsoft.Extensions.* as DI infrastructure by default.
        var ns = named.ContainingNamespace?.ToDisplayString() ?? string.Empty;
        if (ns.StartsWith("Microsoft.Extensions.", StringComparison.Ordinal)
            && !ns.StartsWith("Microsoft.Extensions.Primitives", StringComparison.Ordinal))
            return true;

        // DbContext or derived (best-effort; no hard dependency).
        if (named.Name.EndsWith("DbContext", StringComparison.Ordinal))
            return true;

        if (efDbContext is not null) {
            for (INamedTypeSymbol? cur = named; cur is not null; cur = cur.BaseType) {
                if (SymbolEqualityComparer.Default.Equals(cur, efDbContext))
                    return true;
            }
        }

        return false;
    }

    private static bool IsSimpleType(ITypeSymbol type) {
        type = UnwrapNullable(type);

        if (type is IArrayTypeSymbol array)
            type = UnwrapNullable(array.ElementType);

        if (type.TypeKind == TypeKind.Enum)
            return true;

        return type.SpecialType switch {
            SpecialType.System_Boolean or
            SpecialType.System_Byte or
            SpecialType.System_SByte or
            SpecialType.System_Int16 or
            SpecialType.System_UInt16 or
            SpecialType.System_Int32 or
            SpecialType.System_UInt32 or
            SpecialType.System_Int64 or
            SpecialType.System_UInt64 or
            SpecialType.System_Char or
            SpecialType.System_Decimal or
            SpecialType.System_Double or
            SpecialType.System_Single or
            SpecialType.System_String => true,
            _ => type.Name switch {
                "Guid" => type.ContainingNamespace?.ToDisplayString() == "System",
                "DateTime" => type.ContainingNamespace?.ToDisplayString() == "System",
                "DateTimeOffset" => type.ContainingNamespace?.ToDisplayString() == "System",
                "TimeSpan" => type.ContainingNamespace?.ToDisplayString() == "System",
                "Uri" => type.ContainingNamespace?.ToDisplayString() == "System",
                _ => false,
            },
        };
    }

    private static ITypeSymbol UnwrapNullable(ITypeSymbol type) {
        if (type is INamedTypeSymbol named
            && named.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T
            && named.TypeArguments.Length == 1) {
            return named.TypeArguments[0];
        }

        return type;
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

    private static bool ShouldSkipSyntaxTree(string? filePath) {
        if (string.IsNullOrWhiteSpace(filePath))
            return false;

        // Skip generated code and build outputs.
        if (filePath.EndsWith(".g.cs", StringComparison.OrdinalIgnoreCase))
            return true;

        return filePath.Contains("\\obj\\", StringComparison.OrdinalIgnoreCase)
            || filePath.Contains("/obj/", StringComparison.OrdinalIgnoreCase)
            || filePath.Contains("\\bin\\", StringComparison.OrdinalIgnoreCase)
            || filePath.Contains("/bin/", StringComparison.OrdinalIgnoreCase);
    }
}
