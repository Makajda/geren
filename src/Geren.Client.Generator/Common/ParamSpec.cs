namespace Geren.Client.Generator.Common;

internal sealed class ParamSpec {
    public string Name { get; }
    public string Identifier { get; }
    public string TypeName { get; }

    public ParamSpec(string name, string identifier, string typeName) {
        Name = name;
        Identifier = identifier;
        TypeName = typeName;
    }
}
