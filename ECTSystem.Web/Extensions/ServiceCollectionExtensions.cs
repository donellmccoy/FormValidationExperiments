using System.Text.Json;
using System.Text.Json.Serialization;
using Blazored.LocalStorage;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.Http.Resilience;
using ECTSystem.Web.Services;
using PanoramicData.OData.Client;
using PanoramicData.OData.Client.Converters;
using Radzen;

namespace ECTSystem.Web.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services, IConfiguration configuration)
    {
        var apiBase = configuration["ApiBaseAddress"]
            ?? throw new InvalidOperationException("ApiBaseAddress is not configured in appsettings.json");
        var apiBaseAddress = new Uri(apiBase);
        var odataBaseAddress = new Uri(apiBaseAddress, "odata/");

        services.AddJsonSerializerOptions()
                .AddAuthenticationServices()
                .AddHttpClients(apiBaseAddress, odataBaseAddress)
                .AddODataClient(odataBaseAddress)
                .AddDomainServices();

        return services;
    }

    private static IServiceCollection AddJsonSerializerOptions(this IServiceCollection services)
    {
        // Shared JSON options — used for view model dirty tracking (snapshots)
        var jsonOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        jsonOptions.Converters.Add(new JsonStringEnumConverter());
        jsonOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
        services.AddSingleton(jsonOptions);

        return services;
    }

    private static IServiceCollection AddAuthenticationServices(this IServiceCollection services)
    {
        services.AddBlazoredLocalStorage();
        services.AddAuthorizationCore();
        services.AddScoped<JwtAuthStateProvider>();
        services.AddScoped<AuthenticationStateProvider>(sp => sp.GetRequiredService<JwtAuthStateProvider>());
        services.AddScoped<IAuthService, AuthService>();

        return services;
    }

    private static IServiceCollection AddHttpClients(this IServiceCollection services, Uri apiBaseAddress, Uri odataBaseAddress)
    {
        services.AddSingleton(new ApiEndpoints(apiBaseAddress));
        services.AddTransient<AuthorizationMessageHandler>();

        // Named HttpClient for general API calls (with auth + resilience)
        services.AddHttpClient("Api", client => client.BaseAddress = apiBaseAddress)
            .AddHttpMessageHandler<AuthorizationMessageHandler>()
            .AddStandardResilienceHandler();

        // Named HttpClient for OData calls (with auth + resilience)
        services.AddHttpClient("OData", client => client.BaseAddress = odataBaseAddress)
            .AddHttpMessageHandler<AuthorizationMessageHandler>()
            .AddStandardResilienceHandler();

        // Default HttpClient resolves to the "Api" named client
        services.AddScoped(sp => sp.GetRequiredService<IHttpClientFactory>().CreateClient("Api"));

        return services;
    }

    private static IServiceCollection AddODataClient(this IServiceCollection services, Uri odataBaseAddress)
    {
        // PanoramicData OData client — uses the "OData" named HttpClient (WASM-safe)
        services.AddScoped(sp =>
        {
            var factory = sp.GetRequiredService<IHttpClientFactory>();
            return new ODataClient(new ODataClientOptions
            {
                BaseUrl = odataBaseAddress.ToString(),
                HttpClient = factory.CreateClient("OData"),
                JsonSerializerOptions = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    PropertyNamingPolicy = null,
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

        return services;
    }

    private static IServiceCollection AddDomainServices(this IServiceCollection services)
    {
        services.AddRadzenComponents();
        services.AddScoped<BookmarkCountService>();
        services.AddScoped<IDataService, LineOfDutyCaseHttpService>();

        return services;
    }
}
