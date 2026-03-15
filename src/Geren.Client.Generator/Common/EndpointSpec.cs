namespace Geren.Client.Generator.Common;

internal sealed class EndpointSpec {
    public string Method { get; }
    public string Path { get; }
    public string SpaceName { get; }
    public string ClassName { get; }
    public string MethodName { get; }
    public string ReturnType { get; }
    public string? BodyType { get; }
    public string? BodyMediaType { get; }
    public ImmutableArray<ParamSpec> Params { get; }
    public ImmutableArray<ParamSpec> Queries { get; }

    public EndpointSpec(
        string method,
        string path,
        string spaceName,
        string className,
        string methodName,
        string returnType,
        string? bodyType,
        string? bodyMediaType,
        ImmutableArray<ParamSpec> @params,
        ImmutableArray<ParamSpec> queries) {
        Method = method;
        Path = path;
        SpaceName = spaceName;
        ClassName = className;
        MethodName = methodName;
        ReturnType = returnType;
        BodyType = bodyType;
        BodyMediaType = bodyMediaType;
        Params = @params;
        Queries = queries;
    }
}
