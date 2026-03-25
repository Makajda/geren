namespace Geren.Client.Generator.Tests.Unit.Emit;

public sealed class EmitClientTests {
    [Fact]
    public void Run_GetVoid_EmitsGetAsyncAndEnsureSuccess() {
        var endpoint = new Mapoint(
            Method: Given.Get,
            Path: "/ping",
            SpaceName: "",
            ClassName: "WebApiClient",
            MethodName: "GetPing",
            ReturnType: string.Empty,
            BodyType: null,
            BodyMediaType: null,
            Params: ImmutableArray<Maparam>.Empty,
            Queries: ImmutableArray<Maparam>.Empty);

        var group = new[] { endpoint }.GroupBy(static _ => (object)"k").First();
        var code = EmitClient.Run(group, "Acme", "Acme.Spec", "WebApiClient");

        code.Should().Contain("var response = await _http.GetAsync");
        code.Should().Contain("response.EnsureSuccessStatusCode();");
    }

    [Fact]
    public void Run_GetString_EmitsReadAsStringAsync() {
        var endpoint = new Mapoint(
            Method: Given.Get,
            Path: "/ping",
            SpaceName: "",
            ClassName: "WebApiClient",
            MethodName: "GetPing",
            ReturnType: "string",
            BodyType: null,
            BodyMediaType: null,
            Params: ImmutableArray<Maparam>.Empty,
            Queries: ImmutableArray<Maparam>.Empty);

        var group = new[] { endpoint }.GroupBy(static _ => (object)"k").First();
        var code = EmitClient.Run(group, "Acme", "Acme.Spec", "WebApiClient");

        code.Should().Contain("return await response.Content.ReadAsStringAsync");
    }

    [Fact]
    public void Run_GetDto_EmitsGetFromJsonAsync() {
        var endpoint = new Mapoint(
            Method: Given.Get,
            Path: "/pets",
            SpaceName: "",
            ClassName: "WebApiClient",
            MethodName: "GetPets",
            ReturnType: "global::Dto.Pet",
            BodyType: null,
            BodyMediaType: null,
            Params: ImmutableArray<Maparam>.Empty,
            Queries: ImmutableArray<Maparam>.Empty);

        var group = new[] { endpoint }.GroupBy(static _ => (object)"k").First();
        var code = EmitClient.Run(group, "Acme", "Acme.Spec", "WebApiClient");

        code.Should().Contain("_http.GetFromJsonAsync<global::Dto.Pet>");
    }

    [Fact]
    public void Run_WithQuery_BuildsRequestUriAndAddsParameters() {
        var endpoint = new Mapoint(
            Method: Given.Get,
            Path: "/pets",
            SpaceName: "",
            ClassName: "WebApiClient",
            MethodName: "GetPets",
            ReturnType: "string",
            BodyType: null,
            BodyMediaType: null,
            Params: ImmutableArray<Maparam>.Empty,
            Queries: ImmutableArray.Create(new Maparam("q", "q", "string?")));

        var group = new[] { endpoint }.GroupBy(static _ => (object)"k").First();
        var code = EmitClient.Run(group, "Acme", "Acme.Spec", "WebApiClient");

        code.Should().Contain("BuildRequestUri");
        code.Should().Contain("A(query, \"q\", q);");
    }

    [Fact]
    public void Run_PostJson_EmitsPostAsJsonAsyncAndBodyArgument() {
        var endpoint = new Mapoint(
            Method: Given.Post,
            Path: "/pets",
            SpaceName: "",
            ClassName: "WebApiClient",
            MethodName: "CreatePet",
            ReturnType: string.Empty,
            BodyType: "global::Dto.CreatePet",
            BodyMediaType: "application/json",
            Params: ImmutableArray<Maparam>.Empty,
            Queries: ImmutableArray<Maparam>.Empty);

        var group = new[] { endpoint }.GroupBy(static _ => (object)"k").First();
        var code = EmitClient.Run(group, "Acme", "Acme.Spec", "WebApiClient");

        code.Should().Contain("CreatePet(global::Dto.CreatePet body");
        code.Should().Contain("await _http.PostAsJsonAsync");
        code.Should().Contain("response.EnsureSuccessStatusCode();");
    }

    [Fact]
    public void Run_PutTextPlain_EmitsStringContentWithEncodingUtf8() {
        var endpoint = new Mapoint(
            Method: Given.Put,
            Path: "/ping",
            SpaceName: "",
            ClassName: "WebApiClient",
            MethodName: "PutPing",
            ReturnType: "string",
            BodyType: "string",
            BodyMediaType: "text/plain",
            Params: ImmutableArray<Maparam>.Empty,
            Queries: ImmutableArray<Maparam>.Empty);

        var group = new[] { endpoint }.GroupBy(static _ => (object)"k").First();
        var code = EmitClient.Run(group, "Acme", "Acme.Spec", "WebApiClient");

        code.Should().Contain("using System.Text;");
        code.Should().Contain("new StringContent(body, Encoding.UTF8, \"text/plain\")");
        code.Should().Contain("await _http.PutAsync");
    }

    [Fact]
    public void Run_DeleteJson_EmitsHttpRequestMessageWithJsonContent() {
        var endpoint = new Mapoint(
            Method: Given.Delete,
            Path: "/pets",
            SpaceName: "",
            ClassName: "WebApiClient",
            MethodName: "DeletePets",
            ReturnType: string.Empty,
            BodyType: "global::Dto.DeletePets",
            BodyMediaType: "application/json",
            Params: ImmutableArray<Maparam>.Empty,
            Queries: ImmutableArray<Maparam>.Empty);

        var group = new[] { endpoint }.GroupBy(static _ => (object)"k").First();
        var code = EmitClient.Run(group, "Acme", "Acme.Spec", "WebApiClient");

        code.Should().Contain("new HttpRequestMessage(HttpMethod.Delete");
        code.Should().Contain("Content = JsonContent.Create(body)");
        code.Should().Contain("await _http.SendAsync");
    }
}
