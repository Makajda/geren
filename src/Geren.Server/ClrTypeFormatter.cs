using System.Text;

namespace Geren.Server;

internal static class ClrTypeFormatter {
    public static string Format(Type type) {
        var sb = new StringBuilder();
        AppendType(sb, type);
        return sb.ToString();
    }

    private static void AppendType(StringBuilder sb, Type type) {
        if (Aliases.TryGetValue(type, out var alias)) {
            sb.Append(alias);
            return;
        }

        if (type.IsArray) {
            AppendType(sb, type.GetElementType()!);
            sb.Append('[');
            sb.Append(',', type.GetArrayRank() - 1);
            sb.Append(']');
            return;
        }

        if (type.IsGenericType) {
            var def = type.GetGenericTypeDefinition();
            if (def == typeof(Nullable<>)) {
                AppendType(sb, Nullable.GetUnderlyingType(type)!);
                sb.Append('?');
                return;
            }

            if (IsValueTuple(def)) {
                AppendValueTuple(sb, type);
                return;
            }

            AppendGeneric(sb, type);
        }
        else
            AppendNonGeneric(sb, type);
    }

    private static void AppendGeneric(StringBuilder sb, Type type) {
        if (type.IsNested) {
            AppendType(sb, type.DeclaringType!);
            sb.Append('.');
        }
        else if (!string.IsNullOrEmpty(type.Namespace)) {
            sb.Append("global::");
            sb.Append(type.Namespace);
            sb.Append('.');
        }

        var name = type.Name;
        var tick = name.IndexOf('`');
        if (tick >= 0)
            name = name[..tick];

        sb.Append(name);
        sb.Append('<');

        var args = type.GetGenericArguments();

        if (type.IsNested) {
            var parentArgs = type.DeclaringType!.GetGenericArguments().Length;
            args = args[parentArgs..];
        }

        for (int i = 0; i < args.Length; i++) {
            if (i > 0)
                sb.Append(", ");

            AppendType(sb, args[i]);
        }

        sb.Append('>');
    }

    private static void AppendNonGeneric(StringBuilder sb, Type type) {
        if (type.IsNested) {
            AppendNonGeneric(sb, type.DeclaringType!);
            sb.Append('.');
        }
        else {
            if (!string.IsNullOrEmpty(type.Namespace)) {
                sb.Append("global::");
                sb.Append(type.Namespace);
                sb.Append('.');
            }
        }

        sb.Append(type.Name);
    }

    private static bool IsValueTuple(Type def) =>
        def == typeof(ValueTuple<>) ||
        def == typeof(ValueTuple<,>) ||
        def == typeof(ValueTuple<,,>) ||
        def == typeof(ValueTuple<,,,>) ||
        def == typeof(ValueTuple<,,,,>) ||
        def == typeof(ValueTuple<,,,,,>) ||
        def == typeof(ValueTuple<,,,,,,>) ||
        def == typeof(ValueTuple<,,,,,,,>);

    private static void AppendValueTuple(StringBuilder sb, Type type) {
        sb.Append('(');

        var args = type.GetGenericArguments();

        for (int i = 0; i < args.Length; i++) {
            if (i > 0)
                sb.Append(", ");

            AppendType(sb, args[i]);
        }

        sb.Append(')');
    }

    public static readonly Dictionary<Type, string> Aliases = new() {
        { typeof(void), "void" },
        { typeof(bool), "bool" },
        { typeof(byte), "byte" },
        { typeof(sbyte), "sbyte" },
        { typeof(short), "short" },
        { typeof(ushort), "ushort" },
        { typeof(int), "int" },
        { typeof(uint), "uint" },
        { typeof(long), "long" },
        { typeof(ulong), "ulong" },
        { typeof(float), "float" },
        { typeof(double), "double" },
        { typeof(decimal), "decimal" },
        { typeof(string), "string" },
        { typeof(char), "char" },
        { typeof(object), "object" }
    };
}
