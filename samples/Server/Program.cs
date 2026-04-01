using Geren.Samples.Dto;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi(options => options.AddSchemaTransformer<Geren.Server.Transformer>());

var app = builder.Build();

app.MapGet("/your_namespace/your_type/hello", () => new SimpleDto("hello")).WithName("GetHello");
app.MapGet("/your_namespace/your_type/hello-generic/{value}", (int value) => new GenericDto<int>(value));

app.Run();

