using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using ECTSystem.Web;
using ECTSystem.Web.Services;
using Radzen;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// Shared JSON options — must match the API's serialization settings
var jsonOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web);
jsonOptions.Converters.Add(new JsonStringEnumConverter());
jsonOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
builder.Services.AddSingleton(jsonOptions);

// HttpClient configured to call the Web API
builder.Services.AddScoped(sp => new HttpClient
{
    BaseAddress = new Uri("https://localhost:7173")
});

builder.Services.AddRadzenComponents();

// LOD case service — calls the API via OData + REST
builder.Services.AddScoped<ILineOfDutyCaseService, LineOfDutyCaseODataService>();

var host = builder.Build();
await host.RunAsync();
