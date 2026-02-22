using System.Text.Json;
using System.Text.Json.Serialization;
using Blazored.LocalStorage;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.Extensions.Http.Resilience;
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

// API base addresses
var apiBaseAddress = new Uri("https://localhost:7173");
var odataBaseAddress = new Uri(apiBaseAddress, "odata/");
builder.Services.AddSingleton(new ApiEndpoints(apiBaseAddress));
builder.Services.AddTransient<AuthorizationMessageHandler>();

// Named HttpClient for general API calls (with auth + resilience)
builder.Services.AddHttpClient("Api", client => client.BaseAddress = apiBaseAddress)
    .AddHttpMessageHandler<AuthorizationMessageHandler>()
    .AddStandardResilienceHandler();

// Named HttpClient for OData calls (with auth + resilience)
builder.Services.AddHttpClient("OData", client => client.BaseAddress = odataBaseAddress)
    .AddHttpMessageHandler<AuthorizationMessageHandler>()
    .AddStandardResilienceHandler();

// Default HttpClient resolves to the "Api" named client
builder.Services.AddScoped(sp => sp.GetRequiredService<IHttpClientFactory>().CreateClient("Api"));

builder.Services.AddRadzenComponents();
builder.Services.AddScoped<BookmarkCountService>();

// PanoramicData OData client — uses the "OData" named HttpClient
builder.Services.AddScoped(sp =>
{
    var factory = sp.GetRequiredService<IHttpClientFactory>();
    return new ODataClient(new ODataClientOptions
    {
        BaseUrl = odataBaseAddress.ToString(),
        HttpClient = factory.CreateClient("OData"),
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
