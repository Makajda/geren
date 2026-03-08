namespace Geren.Tests.Map;

public sealed class MapSessionTests {
    [Fact]
    public void BuildMap_should_resolve_names_and_supported_shapes() {
        var compilation = TestCompilationFactory.Create([
            """
            namespace Contracts;
            public sealed class Order;
            public sealed class CreateOrderRequest;
            """
        ]);

        var document = OpenApiDocumentFactory.Load("""
        {
          "openapi": "3.0.1",
          "info": { "title": "Orders", "version": "1.0" },
          "paths": {
            "/{tenantId}": {
              "get": {
                "responses": {
                  "200": { "description": "ok" }
                }
              }
            },
            "/status": {
              "get": {
                "responses": {
                  "200": {
                    "description": "ok",
                    "content": {
                      "text/plain": { "schema": { "type": "string" } }
                    }
                  }
                }
              }
            },
            "/orders/list": {
              "get": {
                "responses": {
                  "200": {
                    "description": "ok",
                    "content": {
                      "application/json": {
                        "schema": { "$ref": "#/components/schemas/Order" }
                      }
                    }
                  }
                }
              }
            },
            "/v1/orders/items/{orderId:int}": {
              "parameters": [
                { "name": "orderId", "in": "path", "required": true, "schema": { "type": "integer", "format": "int64" } }
              ],
              "post": {
                "operationId": "CreateOrderItem",
                "parameters": [
                  { "name": "include-archived", "in": "query", "schema": { "type": "boolean" } }
                ],
                "requestBody": {
                  "required": true,
                  "content": {
                    "application/json": {
                      "schema": { "$ref": "#/components/schemas/CreateOrderRequest" }
                    }
                  }
                },
                "responses": {
                  "201": {
                    "description": "ok",
                    "content": {
                      "application/json": {
                        "schema": { "$ref": "#/components/schemas/Order" }
                      }
                    }
                  }
                }
              }
            }
          },
          "components": {
            "schemas": {
              "Order": { "type": "object" },
              "CreateOrderRequest": { "type": "object" }
            }
          }
        }
        """);

        var result = new MapSession().BuildMap(compilation, document, @"specs\pet-store.v1.json");

        result.NamespaceFromFile.Should().Be("Pet_store_v1");
        result.HintFilePath.Should().StartWith("h");
        result.Diagnostics.Should().BeEmpty();
        result.Endpoints.Should().HaveCount(4);

        result.Endpoints.Should().ContainSingle(endpoint =>
            endpoint.ClassName == "WebApiClient" &&
            endpoint.MethodName == "GetRoot" &&
            endpoint.Path == "/{tenantId}");

        result.Endpoints.Should().ContainSingle(endpoint =>
            endpoint.ClassName == "WebApiClient" &&
            endpoint.MethodName == "GetStatus" &&
            endpoint.ReturnType == "string");

        result.Endpoints.Should().ContainSingle(endpoint =>
            endpoint.ClassName == "Orders" &&
            endpoint.MethodName == "GetList" &&
            endpoint.ReturnType == "global::Contracts.Order");

        result.Endpoints.Should().ContainSingle(endpoint =>
            endpoint.SpaceName == "V1" &&
            endpoint.ClassName == "Orders" &&
            endpoint.MethodName == "CreateOrderItem" &&
            endpoint.BodyType == "global::Contracts.CreateOrderRequest" &&
            endpoint.BodyMediaType == "application/json" &&
            endpoint.Params.Single().Identifier == "orderId" &&
            endpoint.Queries.Single().Identifier == "include_archived");
    }

    [Fact]
    public void BuildMap_should_merge_path_and_operation_parameters_with_override() {
        var compilation = TestCompilationFactory.Create();
        var document = OpenApiDocumentFactory.Load("""
        {
          "openapi": "3.0.1",
          "info": { "title": "Orders", "version": "1.0" },
          "paths": {
            "/orders": {
              "parameters": [
                { "name": "limit", "in": "query", "schema": { "type": "integer", "format": "int32" } }
              ],
              "get": {
                "parameters": [
                  { "name": "limit", "in": "query", "schema": { "type": "string" } }
                ],
                "responses": {
                  "200": { "description": "ok" }
                }
              }
            }
          }
        }
        """);

        var result = new MapSession().BuildMap(compilation, document, "orders.json");
        var endpoint = result.Endpoints.Should().ContainSingle().Subject;

        endpoint.Queries.Should().ContainSingle();
        endpoint.Queries.Single().TypeName.Should().Be("string");
    }

    [Fact]
    public void BuildMap_should_report_duplicate_generated_method_names() {
        var document = OpenApiDocumentFactory.Load("""
        {
          "openapi": "3.0.1",
          "info": { "title": "Orders", "version": "1.0" },
          "paths": {
            "/orders/items": {
              "get": {
                "responses": {
                  "200": { "description": "ok" }
                }
              }
            },
            "/orders/items/{id}": {
              "get": {
                "responses": {
                  "200": { "description": "ok" }
                }
              }
            }
          }
        }
        """);

        var result = new MapSession().BuildMap(TestCompilationFactory.Create(), document, "orders.json");

        result.Endpoints.Should().HaveCount(1);
        result.Diagnostics.Select(static diagnostic => diagnostic.Id).Should().Contain("GEREN006");
    }

    [Fact]
    public void BuildMap_should_report_parameter_and_body_diagnostics_and_skip_invalid_endpoints() {
        var document = OpenApiDocumentFactory.Load("""
        {
          "openapi": "3.0.1",
          "info": { "title": "Orders", "version": "1.0" },
          "paths": {
            "/header": {
              "get": {
                "parameters": [
                  { "name": "trace", "in": "header", "schema": { "type": "string" } }
                ],
                "responses": {
                  "200": { "description": "ok" }
                }
              }
            },
            "/missing": {
              "get": {
                "parameters": [
                  { "name": "trace", "schema": { "type": "string" } }
                ],
                "responses": {
                  "200": { "description": "ok" }
                }
              }
            },
            "/unsupported-query": {
              "get": {
                "parameters": [
                  { "name": "filter", "in": "query", "schema": { "type": "object" } }
                ],
                "responses": {
                  "200": { "description": "ok" }
                }
              }
            },
            "/body": {
              "post": {
                "requestBody": {
                  "content": {
                    "application/xml": {
                      "schema": { "type": "string" }
                    }
                  }
                },
                "responses": {
                  "200": { "description": "ok" }
                }
              }
            },
            "/broken/{id}": {
              "get": {
                "parameters": [
                  { "name": "otherId", "in": "path", "required": true, "schema": { "type": "string" } }
                ],
                "responses": {
                  "200": { "description": "ok" }
                }
              }
            }
          }
        }
        """);

        var result = new MapSession().BuildMap(TestCompilationFactory.Create(), document, "orders.json");

        result.Endpoints.Should().HaveCount(3);
        result.Diagnostics.Select(static diagnostic => diagnostic.Id)
            .Should()
            .BeEquivalentTo(["GEREN003", "GEREN008", "GEREN004", "GEREN005", "GEREN015"]);
    }
}
