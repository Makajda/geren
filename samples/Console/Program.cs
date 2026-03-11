using Geren.Sample;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddGerenClients(configureClient: http =>
{
    http.BaseAddress = new Uri("http://localhost:5000");
});

using var host = builder.Build();

var api = host.Services.GetRequiredService<Api>();
var message = await api.GetHello(CancellationToken.None);
Console.WriteLine(message);

