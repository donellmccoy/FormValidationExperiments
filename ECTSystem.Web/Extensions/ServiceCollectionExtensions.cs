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
            .AddStandardResilienceHandler(options =>
            {
                options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(30);
                options.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(60);
                options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(120);
            });

        // Named HttpClient for OData calls (with auth + resilience)
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

        // Default HttpClient resolves to the "Api" named client
        services.AddScoped(sp => sp.GetRequiredService<IHttpClientFactory>().CreateClient("Api"));

        return services;
    }

    private static IServiceCollection AddDomainServices(this IServiceCollection services)
    {
        services.AddScoped<ICaseService, CaseHttpService>();
        services.AddScoped<IAuthorityService, AuthorityHttpService>();
        services.AddScoped<IMemberService, MemberHttpService>();
        services.AddScoped<IDocumentService, DocumentHttpService>();
        services.AddScoped<IWorkflowHistoryService, WorkflowHistoryHttpService>();
        services.AddScoped<IBookmarkService, BookmarkHttpService>();
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

            // Provide a minimal client-side EDM model for @odata.context URI parsing.
            // We cannot fetch $metadata from the server because the synchronous
            // Monitor.Wait in LoadServiceModelFromNetwork is not supported in
            // Blazor WASM's single-threaded runtime.
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
        var model = new EdmModel();
        var container = new EdmEntityContainer("Default", "Container");
        model.AddElement(container);

        AddEntitySet(model, container, "Cases", "LineOfDutyCase");
        AddEntitySet(model, container, "Members", "Member");
        AddEntitySet(model, container, "Authorities", "LineOfDutyAuthority");
        AddEntitySet(model, container, "Documents", "LineOfDutyDocument");
        AddEntitySet(model, container, "WorkflowStateHistories", "WorkflowStateHistory");
        AddEntitySet(model, container, "CaseBookmarks", "CaseBookmark");
        AddEntitySet(model, container, "Notifications", "Notification");
        AddEntitySet(model, container, "Appeals", "LineOfDutyAppeal");
        AddEntitySet(model, container, "WitnessStatements", "WitnessStatement");
        AddEntitySet(model, container, "AuditComments", "AuditComment");
        AddEntitySet(model, container, "MEDCONDetails", "MEDCONDetail");
        AddEntitySet(model, container, "INCAPDetails", "INCAPDetails");

        return model;
    }

    private static void AddEntitySet(EdmModel model, EdmEntityContainer container, string setName, string typeName)
    {
        // Mark types as open so the OData JSON reader treats undeclared properties
        // (everything beyond Id) as dynamic — allowing the materializer to map them
        // to CLR properties via reflection without type-mismatch errors.
        var entityType = new EdmEntityType("ECTSystem.Shared.Models", typeName, baseType: null, isAbstract: false, isOpen: true);
        entityType.AddKeys(entityType.AddStructuralProperty("Id", EdmPrimitiveTypeKind.Int32));
        model.AddElement(entityType);
        container.AddEntitySet(setName, entityType);
    }

    private sealed class SingleHttpClientFactory(HttpClient httpClient) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => httpClient;
    }
}
