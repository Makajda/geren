using Geren.Sample;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddGerenClients(configureClient: http => {
    http.BaseAddress = new Uri("http://localhost:5000");
});

using var host = builder.Build();

var client = host.Services.GetRequiredService<Geren.Sample.Your_namespace.Your_type>();

var simpleMessage = await client.GetHello();
var genericMessage = await client.GetHello_generic(42);

Console.WriteLine(simpleMessage.Greeting);
Console.WriteLine(genericMessage.Data);

