namespace Geren.Server.Exporter.Extract;

internal static class ReturnType {
    internal static ITypeSymbol? Unwrap(ITypeSymbol returnType, Compilation compilation) {
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
