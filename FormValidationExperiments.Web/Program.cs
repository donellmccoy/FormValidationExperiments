using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using FormValidationExperiments.Web;
using FormValidationExperiments.Web.Services;
using Radzen;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// HttpClient configured to call the Web API
builder.Services.AddScoped(sp => new HttpClient
{
    BaseAddress = new Uri("https://localhost:7173")
});

builder.Services.AddRadzenComponents();

// LOD case service — calls the API via HttpClient
builder.Services.AddScoped<ILineOfDutyCaseService, LineOfDutyCaseHttpService>();

var host = builder.Build();
await host.RunAsync();
