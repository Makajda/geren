namespace Geren.Client.Generator.Tests.Unit.Emit;

public sealed class EmitClientTests {
    [Fact]
    public void Run_EmitsDiConstructorWithActivatorUtilitiesConstructor() {
        var endpoint = new Mapoint(
            Method: Givens.Get,
            Path: "/ping",
            SpaceName: "",
            ClassName: "RootClient",
            MethodName: "GetPing",
            ReturnType: string.Empty,
            BodyType: null,
            BodyMedia: null,
            Params: [],
            Queries: []);

        var group = new[] { endpoint }.GroupBy(static _ => (object)"k").First();
        var code = EmitClient.Run(group, "Acme", "Acme.Spec", "RootClient");

        code.Should().Contain("public RootClient(HttpClient http) : base(http) { }");
        code.Should().Contain("[global::Microsoft.Extensions.DependencyInjection.ActivatorUtilitiesConstructor]");
        code.Should().Contain("public RootClient(HttpClient http, System.IServiceProvider services) : base(http, services) { }");
    }

    [Fact]
    public void Run_GetVoid_EmitsGetAsyncAndEnsureSuccess() {
        var endpoint = new Mapoint(
            Method: Givens.Get,
            Path: "/ping",
            SpaceName: "",
            ClassName: "RootClient",
            MethodName: "GetPing",
            ReturnType: string.Empty,
            BodyType: null,
            BodyMedia: null,
            Params: [],
            Queries: []);

        var group = new[] { endpoint }.GroupBy(static _ => (object)"k").First();
        var code = EmitClient.Run(group, "Acme", "Acme.Spec", "RootClient");

        code.Should().Contain("new HttpRequestMessage(HttpMethod.Get");
        code.Should().Contain("PrepareRequest(request);");
        code.Should().Contain("await Http.SendAsync(request");
        code.Should().Contain("response.EnsureSuccessStatusCode();");
    }

    [Fact]
    public void Run_GetString_EmitsReadAsStringAsync() {
        var endpoint = new Mapoint(
            Method: Givens.Get,
            Path: "/ping",
            SpaceName: "",
            ClassName: "RootClient",
            MethodName: "GetPing",
            ReturnType: "string",
            BodyType: null,
            BodyMedia: null,
            Params: [],
            Queries: []);

        var group = new[] { endpoint }.GroupBy(static _ => (object)"k").First();
        var code = EmitClient.Run(group, "Acme", "Acme.Spec", "RootClient");

        code.Should().Contain("return await response.Content.ReadAsStringAsync");
    }

    [Fact]
    public void Run_GetDto_EmitsGetFromJsonAsync() {
        var endpoint = new Mapoint(
            Method: Givens.Get,
            Path: "/pets",
            SpaceName: "",
            ClassName: "RootClient",
            MethodName: "GetPets",
            ReturnType: "global::Dto.Pet",
            BodyType: null,
            BodyMedia: null,
            Params: [],
            Queries: []);

        var group = new[] { endpoint }.GroupBy(static _ => (object)"k").First();
        var code = EmitClient.Run(group, "Acme", "Acme.Spec", "RootClient");

        code.Should().Contain("return await response.Content.ReadFromJsonAsync<global::Dto.Pet>(JsonOptions");
    }

    [Fact]
    public void Run_WithQuery_BuildsRequestUriAndAddsParameters() {
        var endpoint = new Mapoint(
            Method: Givens.Get,
            Path: "/pets",
            SpaceName: "",
            ClassName: "RootClient",
            MethodName: "GetPets",
            ReturnType: "string",
            BodyType: null,
            BodyMedia: null,
            Params: [],
            Queries: [new Maparam("q", "q", "string?")]);

        var group = new[] { endpoint }.GroupBy(static _ => (object)"k").First();
        var code = EmitClient.Run(group, "Acme", "Acme.Spec", "RootClient");

        code.Should().Contain("BuildRequestUri");
        code.Should().Contain("AddQueryParameter(query, \"q\", q);");
    }

    [Fact]
    public void Run_PostJson_EmitsPostAsJsonAsyncAndBodyArgument() {
        var endpoint = new Mapoint(
            Method: Givens.Post,
            Path: "/pets",
            SpaceName: "",
            ClassName: "RootClient",
            MethodName: "CreatePet",
            ReturnType: string.Empty,
            BodyType: "Dto.CreatePet",
            BodyMedia: MediaTypes.Application_Json,
            Params: [],
            Queries: []);

        var group = new[] { endpoint }.GroupBy(static _ => (object)"k").First();
        var code = EmitClient.Run(group, "Acme", "Acme.Spec", "RootClient");

        code.Should().Contain("CreatePet(Dto.CreatePet body");
        code.Should().Contain("Content = JsonContent.Create(body, options: JsonOptions)");
        code.Should().Contain("await Http.SendAsync(request");
        code.Should().Contain("response.EnsureSuccessStatusCode();");
    }

    [Fact]
    public void Run_PutTextPlain_EmitsStringContentWithEncodingUtf8() {
        var endpoint = new Mapoint(
            Method: Givens.Put,
            Path: "/ping",
            SpaceName: "",
            ClassName: "RootClient",
            MethodName: "PutPing",
            ReturnType: "string",
            BodyType: "string",
            BodyMedia: MediaTypes.Text_Plain,
            Params: [],
            Queries: []);

        var group = new[] { endpoint }.GroupBy(static _ => (object)"k").First();
        var code = EmitClient.Run(group, "Acme", "Acme.Spec", "RootClient");

        code.Should().Contain("using System.Text;");
        code.Should().Contain("new StringContent(body, Encoding.UTF8, \"text/plain\")");
        code.Should().Contain("new HttpRequestMessage(HttpMethod.Put");
        code.Should().Contain("await Http.SendAsync(request");
    }
}
