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

// Shared JSON options — used for view model dirty tracking (snapshots)
var jsonOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web);
jsonOptions.Converters.Add(new JsonStringEnumConverter());
jsonOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
builder.Services.AddSingleton(jsonOptions);

// HttpClient configured to call the Web API
builder.Services.AddScoped(sp => new HttpClient
{
    BaseAddress = new Uri("https://localhost:7173")
});

// OData client context — service root = API base + "odata/" route prefix
builder.Services.AddScoped(_ => new EctODataContext(new Uri("https://localhost:7173/odata/")));

builder.Services.AddRadzenComponents();

// LOD case service — uses Microsoft.OData.Client via EctODataContext
builder.Services.AddScoped<IDataService, LineOfDutyCaseODataService>();

var host = builder.Build();
await host.RunAsync();
