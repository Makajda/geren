var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi(options => options.AddSchemaTransformer<Geren.Server.Transformer>());

var app = builder.Build();

app.MapOpenApi();

app.MapGet("/api/hello", () => Results.Text("Hello from Geren sample server", "text/plain")).WithName("GetHello");

app.Run();

