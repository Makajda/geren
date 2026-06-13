using Geren.Sample;
using Geren.Sample.Your_namespace;
using Geren.Samples.Dto;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);

builder.Services.AddGerenClients(c => c.BaseAddress = new Uri("http://localhost:5000"));

using IHost host = builder.Build();

Your_typeHttp client = host.Services.GetRequiredService<Your_typeHttp>();

SimpleDto simpleMessage = await client.GetHello();
GenericDto<int> genericMessage = await client.GetHello_generic(42);

Console.WriteLine(simpleMessage.Greeting);
Console.WriteLine(genericMessage.Data);

Console.ReadLine();
