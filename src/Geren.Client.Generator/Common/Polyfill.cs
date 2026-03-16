// It is needed because netstandard2.0 does not have the System.Runtime.CompilerServices.IsExternalInit type,
// and the C# compiler uses it for init setters that automatically appear in record.
// CS0518: Predefined type 'System.Runtime.CompilerServices.IsExternalInit' is not defined or imported

namespace System.Runtime.CompilerServices;

internal static class IsExternalInit { }
