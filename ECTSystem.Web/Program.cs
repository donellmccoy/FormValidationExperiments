using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using ECTSystem.Web;
using ECTSystem.Web.Services;
using PanoramicData.OData.Client;
using PanoramicData.OData.Client.Converters;
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

builder.Services.AddRadzenComponents();
builder.Services.AddScoped<BookmarkCountService>();

// PanoramicData OData client — uses its own HttpClient with the /odata/ base path.
// PanoramicData constructs relative URIs from the entity set name (e.g. "Cases?$top=10")
// and sends them through HttpClient, so BaseAddress MUST include the OData route prefix.
builder.Services.AddScoped(sp =>
{
    var odataHttpClient = new HttpClient
    {
        BaseAddress = new Uri("https://localhost:7173/odata/")
    };
    return new ODataClient(new ODataClientOptions
    {
        BaseUrl = "https://localhost:7173/odata/",
        HttpClient = odataHttpClient,
        JsonSerializerOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            PropertyNameCaseInsensitive = true,
            Converters =
            {
                new JsonStringEnumConverter(),
                new ODataDateTimeConverter(),
                new ODataNullableDateTimeConverter()
            },
            ReferenceHandler = ReferenceHandler.IgnoreCycles,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        }
    });
});

// LOD case service — uses PanoramicData.OData.Client (HttpClient-based, WASM-safe)
builder.Services.AddScoped<IDataService, LineOfDutyCaseHttpService>();

var host = builder.Build();
await host.RunAsync();
