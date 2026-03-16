using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;
using System.Collections.Immutable;
using System.Globalization;

namespace Geren.Server.Exporter;

internal static class Extractor {
    internal sealed record EndpointModel(
        ImmutableArray<string> HttpMethods,
        string RouteTemplate,
        ImmutableArray<string> RouteParameters,
        string Handler,
        ImmutableArray<ParameterModel> Parameters,
        string? ReturnType);

    internal sealed record ParameterModel(string Name, string Type, string Source);

    private static readonly SymbolDisplayFormat FullyQualifiedMethodFormat =
        new(
            globalNamespaceStyle: SymbolDisplayGlobalNamespaceStyle.Included,
            typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces,
            genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters,
            memberOptions:
            SymbolDisplayMemberOptions.IncludeContainingType |
            SymbolDisplayMemberOptions.IncludeParameters |
            SymbolDisplayMemberOptions.IncludeType,
            parameterOptions:
            SymbolDisplayParameterOptions.IncludeType |
            SymbolDisplayParameterOptions.IncludeName,
            miscellaneousOptions:
            SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier |
            SymbolDisplayMiscellaneousOptions.EscapeKeywordIdentifiers);

    public static List<EndpointModel> Extract(Compilation compilation, List<string> warnings, CancellationToken cancellationToken) {
        List<EndpointModel> endpoints = [];
        var endpointRouteBuilder = compilation.GetTypeByMetadataName("Microsoft.AspNetCore.Routing.IEndpointRouteBuilder");
        if (endpointRouteBuilder is null) {
            warnings.Add("Unable to find Microsoft.AspNetCore.Routing.IEndpointRouteBuilder in compilation; no endpoints will be discovered.");
            return endpoints;
        }

        foreach (var tree in compilation.SyntaxTrees) {
            cancellationToken.ThrowIfCancellationRequested();

            var root = tree.GetRoot(cancellationToken);
            var semanticModel = compilation.GetSemanticModel(tree, ignoreAccessibility: true);

            foreach (var invocation in root.DescendantNodes().OfType<InvocationExpressionSyntax>()) {
                cancellationToken.ThrowIfCancellationRequested();

                if (!IsMapInvocationCandidate(invocation)) {
                    continue;
                }

                if (!TryBuildEndpoint(compilation, endpointRouteBuilder, semanticModel, invocation, cancellationToken, out var endpoint, out var warn)) {
                    if (warn is not null) {
                        warnings.Add(warn);
                    }
                    continue;
                }

                endpoints.Add(endpoint);
            }
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

        return endpoints;
    }

    private static bool IsMapInvocationCandidate(InvocationExpressionSyntax invocation) {
        var methodName = GetInvokedMethodName(invocation.Expression);
        return methodName is not null && methodName.StartsWith("Map", StringComparison.Ordinal);
    }

    private static string? GetInvokedMethodName(ExpressionSyntax expression) =>
        expression switch {
            MemberAccessExpressionSyntax m => m.Name.Identifier.ValueText,
            IdentifierNameSyntax i => i.Identifier.ValueText,
            GenericNameSyntax g => g.Identifier.ValueText,
            _ => null,
        };

    private static bool TryBuildEndpoint(
        Compilation compilation,
        INamedTypeSymbol endpointRouteBuilder,
        SemanticModel semanticModel,
        InvocationExpressionSyntax invocation,
        CancellationToken cancellationToken,
        out EndpointModel endpoint,
        out string? warning) {
        endpoint = null!;
        warning = null;

        var methodSymbol = semanticModel.GetSymbolInfo(invocation, cancellationToken).Symbol as IMethodSymbol;
        methodSymbol ??= semanticModel.GetSymbolInfo(invocation, cancellationToken).CandidateSymbols.OfType<IMethodSymbol>().FirstOrDefault();
        if (methodSymbol is null || !methodSymbol.Name.StartsWith("Map", StringComparison.Ordinal)) {
            return false;
        }

        if (!IsEndpointRouteBuilderExtension(methodSymbol, endpointRouteBuilder)) {
            return false;
        }

        var routeTemplateArgIndex = GetRouteTemplateArgumentIndex(methodSymbol);
        var handlerArgIndex = GetHandlerArgumentIndex(compilation, methodSymbol);
        if (routeTemplateArgIndex < 0 || handlerArgIndex < 0) {
            return false;
        }

        if (invocation.ArgumentList is null ||
            invocation.ArgumentList.Arguments.Count <= Math.Max(routeTemplateArgIndex, handlerArgIndex)) {
            return false;
        }

        var routeTemplateExpression = invocation.ArgumentList.Arguments[routeTemplateArgIndex].Expression;
        if (!TryGetConstantString(semanticModel, routeTemplateExpression, cancellationToken, out var routeTemplate)) {
            warning = FormatWarning(invocation, $"Skipped '{methodSymbol.Name}': route template is not a constant string.");
            return false;
        }

        if (invocation.Expression is MemberAccessExpressionSyntax memberAccess &&
            TryGetChainedMapGroupPrefix(endpointRouteBuilder, semanticModel, memberAccess.Expression, cancellationToken, out var prefix) &&
            prefix.Length != 0) {
            routeTemplate = CombineRouteTemplates(prefix, routeTemplate);
        }

        var routeParameterNames = ExtractRouteParameterNames(routeTemplate);

        var handlerExpression = invocation.ArgumentList.Arguments[handlerArgIndex].Expression;
        var handlerMethod = TryGetHandlerMethodSymbol(semanticModel, handlerExpression, cancellationToken);
        if (handlerMethod is null) {
            warning = FormatWarning(invocation, $"Skipped '{methodSymbol.Name}': unable to resolve handler method symbol.");
            return false;
        }

        var httpMethods = GetHttpMethods(methodSymbol, invocation, semanticModel, cancellationToken);
        var parameters = InferParameters(handlerMethod, routeParameterNames, httpMethods);
        var returnType = UnwrapReturnType(handlerMethod.ReturnType, compilation);

        endpoint = new EndpointModel(
            HttpMethods: httpMethods,
            RouteTemplate: routeTemplate,
            RouteParameters: routeParameterNames.OrderBy(static p => p, StringComparer.Ordinal).ToImmutableArray(),
            Handler: handlerMethod.ToDisplayString(FullyQualifiedMethodFormat),
            Parameters: parameters,
            ReturnType: returnType?.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat));

        return true;
    }

    private static bool TryGetChainedMapGroupPrefix(
        INamedTypeSymbol endpointRouteBuilder,
        SemanticModel semanticModel,
        ExpressionSyntax receiverExpression,
        CancellationToken cancellationToken,
        out string prefix) {
        prefix = string.Empty;

        List<string> prefixesReversed = [];
        ExpressionSyntax current = receiverExpression;

        while (current is InvocationExpressionSyntax invocation) {
            cancellationToken.ThrowIfCancellationRequested();

            var methodSymbol = semanticModel.GetSymbolInfo(invocation, cancellationToken).Symbol as IMethodSymbol;
            methodSymbol ??= semanticModel.GetSymbolInfo(invocation, cancellationToken).CandidateSymbols.OfType<IMethodSymbol>().FirstOrDefault();

            if (methodSymbol is null || !methodSymbol.Name.Equals("MapGroup", StringComparison.Ordinal))
                break;

            if (!IsEndpointRouteBuilderExtension(methodSymbol, endpointRouteBuilder))
                break;

            int routeTemplateArgIndex = GetRouteTemplateArgumentIndex(methodSymbol);
            if (routeTemplateArgIndex < 0 ||
                invocation.ArgumentList is null ||
                invocation.ArgumentList.Arguments.Count <= routeTemplateArgIndex) {
                break;
            }

            if (!TryGetConstantString(
                    semanticModel,
                    invocation.ArgumentList.Arguments[routeTemplateArgIndex].Expression,
                    cancellationToken,
                    out var groupPrefix)) {
                break;
            }

            prefixesReversed.Add(groupPrefix);

            if (invocation.Expression is MemberAccessExpressionSyntax ma)
                current = ma.Expression;
            else
                break;
        }

        if (prefixesReversed.Count == 0)
            return false;

        string combined = string.Empty;
        for (var i = prefixesReversed.Count - 1; i >= 0; i--)
            combined = CombineRouteTemplates(combined, prefixesReversed[i]);

        prefix = combined;
        return true;
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

    private static string FormatWarning(InvocationExpressionSyntax invocation, string message) {
        FileLinePositionSpan span = invocation.GetLocation().GetLineSpan();
        string file = span.Path.Length == 0 ? "<unknown>" : span.Path;
        int line = span.StartLinePosition.Line + 1;
        int col = span.StartLinePosition.Character + 1;
        return $"{file}({line.ToString(CultureInfo.InvariantCulture)},{col.ToString(CultureInfo.InvariantCulture)}): {message}";
    }

    private static bool IsEndpointRouteBuilderExtension(IMethodSymbol methodSymbol, INamedTypeSymbol endpointRouteBuilder) {
        IMethodSymbol original = methodSymbol.ReducedFrom ?? methodSymbol;
        if (!original.IsExtensionMethod || original.Parameters.Length == 0)
            return false;

        return SymbolEqualityComparer.Default.Equals(original.Parameters[0].Type, endpointRouteBuilder);
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

        trimmed = trimmed.Substring(start);

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

    private static IMethodSymbol? TryGetHandlerMethodSymbol(SemanticModel semanticModel, ExpressionSyntax expression, CancellationToken cancellationToken) {
        IOperation? operation = semanticModel.GetOperation(expression, cancellationToken);
        return operation is null ? null : TryGetHandlerMethodSymbol(operation);
    }

    private static IMethodSymbol? TryGetHandlerMethodSymbol(IOperation operation) {
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
        if (name.Equals("MapGet", StringComparison.Ordinal)) return ImmutableArray.Create("GET");
        if (name.Equals("MapPost", StringComparison.Ordinal)) return ImmutableArray.Create("POST");
        if (name.Equals("MapPut", StringComparison.Ordinal)) return ImmutableArray.Create("PUT");
        if (name.Equals("MapPatch", StringComparison.Ordinal)) return ImmutableArray.Create("PATCH");
        if (name.Equals("MapDelete", StringComparison.Ordinal)) return ImmutableArray.Create("DELETE");
        if (name.Equals("MapHead", StringComparison.Ordinal)) return ImmutableArray.Create("HEAD");
        if (name.Equals("MapOptions", StringComparison.Ordinal)) return ImmutableArray.Create("OPTIONS");

        if (name.Equals("MapMethods", StringComparison.Ordinal)) {
            int methodsArgIndex = FindHttpMethodsArgumentIndex(mapMethod);
            if (methodsArgIndex >= 0 &&
                invocation.ArgumentList is not null &&
                invocation.ArgumentList.Arguments.Count > methodsArgIndex &&
                TryGetConstantStringList(semanticModel, invocation.ArgumentList.Arguments[methodsArgIndex].Expression, cancellationToken, out var methods)) {
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

    private static ImmutableArray<ParameterModel> InferParameters(
        IMethodSymbol handlerMethod,
        HashSet<string> routeParameterNames,
        ImmutableArray<string> httpMethods) {
        var allowBody = httpMethods.Any(static m => m is "POST" or "PUT" or "PATCH");
        var bodyAssigned = false;

        var builder = ImmutableArray.CreateBuilder<ParameterModel>(handlerMethod.Parameters.Length);
        foreach (var parameter in handlerMethod.Parameters) {
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
            if (cls is null) continue;

            var metadataName = cls.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            if (metadataName == "global::Microsoft.AspNetCore.Mvc.FromRouteAttribute"
                || metadataName == "global::Microsoft.AspNetCore.Http.Metadata.FromRouteAttribute") {
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
            }

            break;
        }

        if (task is not null && SymbolEqualityComparer.Default.Equals(current, task))
            return null;

        if (valueTask is not null && SymbolEqualityComparer.Default.Equals(current, valueTask))
            return null;

        return current;
    }
}
