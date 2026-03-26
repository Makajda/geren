namespace Geren.Client.Generator.Tests.Unit.Emit;

public sealed class EmitClientTests {
    [Fact]
    public void Run_GetVoid_EmitsGetAsyncAndEnsureSuccess() {
        var endpoint = new Mapoint(
            Method: Givens.Get,
            Path: "/ping",
            SpaceName: "",
            ClassName: "WebApiClient",
            MethodName: "GetPing",
            ReturnType: string.Empty,
            BodyType: null,
            BodyMedia: null,
            Params: [],
            Queries: []);

        var group = new[] { endpoint }.GroupBy(static _ => (object)"k").First();
        var code = EmitClient.Run(group, "Acme", "Acme.Spec", "WebApiClient");

        code.Should().Contain("var response = await _http.GetAsync");
        code.Should().Contain("response.EnsureSuccessStatusCode();");
    }

    [Fact]
    public void Run_GetString_EmitsReadAsStringAsync() {
        var endpoint = new Mapoint(
            Method: Givens.Get,
            Path: "/ping",
            SpaceName: "",
            ClassName: "WebApiClient",
            MethodName: "GetPing",
            ReturnType: "string",
            BodyType: null,
            BodyMedia: null,
            Params: [],
            Queries: []);

        var group = new[] { endpoint }.GroupBy(static _ => (object)"k").First();
        var code = EmitClient.Run(group, "Acme", "Acme.Spec", "WebApiClient");

        code.Should().Contain("return await response.Content.ReadAsStringAsync");
    }

    [Fact]
    public void Run_GetDto_EmitsGetFromJsonAsync() {
        var endpoint = new Mapoint(
            Method: Givens.Get,
            Path: "/pets",
            SpaceName: "",
            ClassName: "WebApiClient",
            MethodName: "GetPets",
            ReturnType: "global::Dto.Pet",
            BodyType: null,
            BodyMedia: null,
            Params: [],
            Queries: []);

        var group = new[] { endpoint }.GroupBy(static _ => (object)"k").First();
        var code = EmitClient.Run(group, "Acme", "Acme.Spec", "WebApiClient");

        code.Should().Contain("_http.GetFromJsonAsync<global::Dto.Pet>");
    }

    [Fact]
    public void Run_WithQuery_BuildsRequestUriAndAddsParameters() {
        var endpoint = new Mapoint(
            Method: Givens.Get,
            Path: "/pets",
            SpaceName: "",
            ClassName: "WebApiClient",
            MethodName: "GetPets",
            ReturnType: "string",
            BodyType: null,
            BodyMedia: null,
            Params: [],
            Queries: [new Maparam("q", "q", "string?")]);

        var group = new[] { endpoint }.GroupBy(static _ => (object)"k").First();
        var code = EmitClient.Run(group, "Acme", "Acme.Spec", "WebApiClient");

        code.Should().Contain("BuildRequestUri");
        code.Should().Contain("A(query, \"q\", q);");
    }

    [Fact]
    public void Run_PostJson_EmitsPostAsJsonAsyncAndBodyArgument() {
        var endpoint = new Mapoint(
            Method: Givens.Post,
            Path: "/pets",
            SpaceName: "",
            ClassName: "WebApiClient",
            MethodName: "CreatePet",
            ReturnType: string.Empty,
            BodyType: "Dto.CreatePet",
            BodyMedia: MediaTypes.Application_Json,
            Params: [],
            Queries: []);

        var group = new[] { endpoint }.GroupBy(static _ => (object)"k").First();
        var code = EmitClient.Run(group, "Acme", "Acme.Spec", "WebApiClient");

        code.Should().Contain("CreatePet(Dto.CreatePet body");
        code.Should().Contain("await _http.PostAsJsonAsync");
        code.Should().Contain("response.EnsureSuccessStatusCode();");
    }

    [Fact]
    public void Run_PutTextPlain_EmitsStringContentWithEncodingUtf8() {
        var endpoint = new Mapoint(
            Method: Givens.Put,
            Path: "/ping",
            SpaceName: "",
            ClassName: "WebApiClient",
            MethodName: "PutPing",
            ReturnType: "string",
            BodyType: "string",
            BodyMedia: MediaTypes.Text_Plain,
            Params: [],
            Queries: []);

        var group = new[] { endpoint }.GroupBy(static _ => (object)"k").First();
        var code = EmitClient.Run(group, "Acme", "Acme.Spec", "WebApiClient");

        code.Should().Contain("using System.Text;");
        code.Should().Contain("new StringContent(body, Encoding.UTF8, \"text/plain\")");
        code.Should().Contain("await _http.PutAsync");
    }
}
