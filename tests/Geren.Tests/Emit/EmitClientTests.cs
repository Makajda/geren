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

        code.Should().Contain("query.Add(\"includeArchived=\" + Uri.EscapeDataString(includeArchived ? \"true\" : \"false\"));");
        code.Should().Contain("Convert.ToString(page, CultureInfo.InvariantCulture) ?? string.Empty");
        code.Should().Contain("return $\"/pets/{petId}?{string.Join(\"&\", query)}\";");
    }

    private static IGrouping<object, EndpointSpec> Group(params EndpointSpec[] endpoints) =>
        endpoints.GroupBy(static endpoint => (object)new { endpoint.SpaceName, endpoint.ClassName }).Single();
}
