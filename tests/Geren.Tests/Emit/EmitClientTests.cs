namespace Geren.Tests.Emit;

public sealed class EmitClientTests {
    [Fact]
    public void Run_should_emit_get_variants_for_void_string_and_json_returns() {
        var code = EmitClient.Run(Group(
            new EndpointSpec("Get", "/status", "", "StatusClient", "GetStatus", "", null, null, [], []),
            new EndpointSpec("Get", "/status/text", "", "StatusClient", "GetStatusText", "string", null, null, [], []),
            new EndpointSpec("Get", "/status/json", "", "StatusClient", "GetStatusJson", "global::Contracts.StatusDto", null, null, [], [])),
            "Generated.Status",
            "StatusClient");

        code.Should().Contain("public async Task GetStatus(");
        code.Should().Contain("return await response.Content.ReadAsStringAsync(cancellationToken);");
        code.Should().Contain("_http.GetFromJsonAsync<global::Contracts.StatusDto>");
    }

    [Fact]
    public void Run_should_emit_delete_post_and_put_transport_variants() {
        var code = EmitClient.Run(Group(
            new EndpointSpec("Delete", "/pets/{id}", "", "PetsClient", "DeleteJson", "", "global::Contracts.DeleteRequest", "application/json",
                [new ParamSpec("id", "id", "int")], []),
            new EndpointSpec("Delete", "/pets/{id}/note", "", "PetsClient", "DeleteText", "string", "string", "text/plain",
                [new ParamSpec("id", "id", "int")], []),
            new EndpointSpec("Post", "/pets", "", "PetsClient", "CreatePet", "global::Contracts.Pet", "global::Contracts.CreatePetRequest", "application/json", [], []),
            new EndpointSpec("Put", "/pets/{id}", "", "PetsClient", "ReplacePet", "", "string", "text/plain",
                [new ParamSpec("id", "id", "int")], [])),
            "Generated.Pets",
            "PetsClient");

        code.Should().Contain("new HttpRequestMessage(HttpMethod.Delete");
        code.Should().Contain("Content = JsonContent.Create(body)");
        code.Should().Contain("new StringContent(body, Encoding.UTF8, \"text/plain\")");
        code.Should().Contain("_http.PostAsJsonAsync");
        code.Should().Contain("_http.PutAsync");
    }

    [Fact]
    public void Run_should_emit_query_builder_for_bool_and_numeric_values() {
        var code = EmitClient.Run(Group(
            new EndpointSpec(
                "Get",
                "/pets/{petId}",
                "",
                "PetsClient",
                "SearchPets",
                "string",
                null,
                null,
                [new ParamSpec("petId", "petId", "int")],
                [new ParamSpec("includeArchived", "includeArchived", "bool"), new ParamSpec("page", "page", "long")])),
            "Generated.Pets",
            "PetsClient");

        code.Should().Contain("private static string BuildRequestUri(string path, Action<List<string>>? configureQuery = null)");
        code.Should().Contain("private static string FormatPathParameter(object? value)");
        code.Should().Contain("private static void AddQueryParameter(List<string> query, string name, object? value)");
        code.Should().Contain("AddQueryParameter(query, \"includeArchived\", includeArchived);");
        code.Should().Contain("AddQueryParameter(query, \"page\", page);");
        code.Should().Contain("BuildRequestUri($\"/pets/{FormatPathParameter(petId)}\", query =>");
        code.Should().Contain("IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture)");
        code.Should().Contain("return Uri.EscapeDataString(text);");
    }

    [Fact]
    public async Task Run_should_escape_path_parameters_and_use_invariant_culture_for_queries() {
        using var _ = new CultureScope("ru-RU");
        var endpoint = new EndpointSpec(
            "Get",
            "/pets/{slug}",
            "",
            "PetsClient",
            "GetPet",
            "string",
            null,
            null,
            [new ParamSpec("slug", "slug", "string")],
            [new ParamSpec("ratio", "ratio", "double?"), new ParamSpec("enabled", "enabled", "bool?")]);

        var uri = await GeneratedClientRuntimeHost.InvokeAsync(endpoint, "a/b c?#", 1.5d, true);

        uri.Should().NotBeNull();
        uri!.PathAndQuery.Should().Be("/pets/a%2Fb%20c%3F%23?ratio=1.5&enabled=true");
    }

    [Fact]
    public async Task Run_should_skip_null_queries_and_repeat_array_values() {
        var endpoint = new EndpointSpec(
            "Get",
            "/pets",
            "",
            "PetsClient",
            "SearchPets",
            "string",
            null,
            null,
            [],
            [
                new ParamSpec("tags", "tags", "System.Collections.Generic.IReadOnlyList<string>?"),
                new ParamSpec("page", "page", "int?"),
                new ParamSpec("search", "search", "string?")
            ]);

        var uri = await GeneratedClientRuntimeHost.InvokeAsync(endpoint, new[] { "red/blue", "white space" }, null, null);

        uri.Should().NotBeNull();
        uri!.PathAndQuery.Should().Be("/pets?tags=red%2Fblue&tags=white%20space");
    }

    private static IGrouping<object, EndpointSpec> Group(params EndpointSpec[] endpoints) =>
        endpoints.GroupBy(static endpoint => (object)new { endpoint.SpaceName, endpoint.ClassName }).Single();
}
