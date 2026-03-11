using Geren.Sample;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddGerenClients(configureClient: http =>
{
    // Server runs locally in this sample.
    http.BaseAddress = new Uri("http://localhost:5000");
});

await builder.Build().RunAsync();

