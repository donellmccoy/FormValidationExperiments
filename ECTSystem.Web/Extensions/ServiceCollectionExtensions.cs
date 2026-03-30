using System.Text.Json;
using System.Text.Json.Serialization;
using Blazored.LocalStorage;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.OData.Client;
using Microsoft.OData.Edm;
using ECTSystem.Web.Factories;
using ECTSystem.Web.Handlers;
using ECTSystem.Web.Providers;
using ECTSystem.Web.StateMachines;
using ECTSystem.Web.Services;
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

        services.AddRadzenComponents()
                .AddJsonSerializerOptions()
                .AddAuthenticationServices()
                .AddHttpClients(apiBaseAddress, odataBaseAddress)
                .AddODataContext(odataBaseAddress)
                .AddDomainServices();

        return services;
    }

    private static IServiceCollection AddJsonSerializerOptions(this IServiceCollection services)
    {
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

        services.AddHttpClient("Api", client => client.BaseAddress = apiBaseAddress)
            .AddHttpMessageHandler<AuthorizationMessageHandler>()
            .AddStandardResilienceHandler(options =>
            {
                options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(30);      
                options.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(60);
                options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(120);
            });

        services.AddTransient<ODataLoggingHandler>();
        services.AddHttpClient("OData", client => client.BaseAddress = odataBaseAddress)
            .AddHttpMessageHandler<AuthorizationMessageHandler>()
            .AddHttpMessageHandler<ODataLoggingHandler>()
            .AddStandardResilienceHandler(options =>
            {
                options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(30);      
                options.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(60);
                options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(120);
            });

        services.AddScoped(sp => sp.GetRequiredService<IHttpClientFactory>().CreateClient("Api"));

        return services;
    }

    private static IServiceCollection AddDomainServices(this IServiceCollection services)
    {
        services.AddScoped<ICaseService, CaseService>();
        services.AddScoped<IAuthorityService, AuthorityService>();
        services.AddScoped<IMemberService, MemberService>();
        services.AddScoped<IDocumentService, DocumentService>();
        services.AddScoped<IWorkflowHistoryService, WorkflowHistoryService>();
        services.AddScoped<IBookmarkService, BookmarkService>();
        services.AddScoped<BookmarkCountService>();
        services.AddScoped<LineOfDutyStateMachineFactory>();

        return services;
    }

    private static IServiceCollection AddODataContext(this IServiceCollection services, Uri odataBaseAddress)
    {
        var clientEdmModel = BuildClientEdmModel();

        services.AddScoped(sp =>
        {
            var factory = sp.GetRequiredService<IHttpClientFactory>();
            var httpClient = factory.CreateClient("OData");

            var context = new EctODataContext(odataBaseAddress);

            context.Format.LoadServiceModel = () => clientEdmModel;

            context.Configurations.RequestPipeline.OnMessageCreating = args =>  
            {
                var requestArgs = new DataServiceClientRequestMessageArgs(      
                    args.Method,
                    args.RequestUri,
                    args.UsePostTunneling,
                    args.Headers,
                    new SingleHttpClientFactory(httpClient));

                return new HttpClientRequestMessage(requestArgs);
            };

            return context;
        });

        return services;
    }

    private static IEdmModel BuildClientEdmModel()
    {
        var assembly = typeof(ServiceCollectionExtensions).Assembly;
        using var stream = assembly.GetManifestResourceStream("ECTSystem.Web.ClientModel.xml");
        if (stream == null) throw new InvalidOperationException("Could not find ClientModel.xml resource.");
        using var reader = System.Xml.XmlReader.Create(stream);
        if (Microsoft.OData.Edm.Csdl.CsdlReader.TryParse(reader, out var model, out var errors))
        {
            return model;
        }
        throw new InvalidOperationException("Failed to parse EDM model from ClientModel.xml: " + string.Join(", ", errors.Select(e => e.ErrorMessage)));
    }

    private sealed class SingleHttpClientFactory(HttpClient httpClient) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => httpClient;
    }
}
