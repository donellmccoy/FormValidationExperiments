using System.Text.Json;
using System.Text.Json.Serialization;
using Blazored.LocalStorage;
using Microsoft.AspNetCore.Components.Authorization;
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

// Authentication & authorization
builder.Services.AddBlazoredLocalStorage();
builder.Services.AddAuthorizationCore();
builder.Services.AddScoped<JwtAuthStateProvider>();
builder.Services.AddScoped<AuthenticationStateProvider>(sp => sp.GetRequiredService<JwtAuthStateProvider>());
builder.Services.AddScoped<IAuthService, AuthService>();

// HttpClient configured to call the Web API (with auth header)
builder.Services.AddScoped(sp =>
{
    var handler = new AuthorizationMessageHandler(
        sp.GetRequiredService<Blazored.LocalStorage.ILocalStorageService>())
    {
        InnerHandler = new HttpClientHandler()
    };
    return new HttpClient(handler)
    {
        BaseAddress = new Uri("https://localhost:7173")
    };
});

builder.Services.AddRadzenComponents();
builder.Services.AddScoped<BookmarkCountService>();

// PanoramicData OData client — uses its own HttpClient with the /odata/ base path (with auth header).
builder.Services.AddScoped(sp =>
{
    var odataHandler = new AuthorizationMessageHandler(
        sp.GetRequiredService<Blazored.LocalStorage.ILocalStorageService>())
    {
        InnerHandler = new HttpClientHandler()
    };
    var odataHttpClient = new HttpClient(odataHandler)
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
