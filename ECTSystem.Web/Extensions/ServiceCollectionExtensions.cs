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
        var model = new EdmModel();
        var ns = "ECTSystem.Shared.Models";
        var enumNs = "ECTSystem.Shared.Enums";

        // ── Enum types ──────────────────────────────────────────────────

        var processTypeEnum = new EdmEnumType(enumNs, "ProcessType");
        processTypeEnum.AddMember("Informal", new EdmEnumMemberValue(0));
        processTypeEnum.AddMember("Formal", new EdmEnumMemberValue(1));
        model.AddElement(processTypeEnum);

        var serviceComponentEnum = new EdmEnumType(enumNs, "ServiceComponent");
        serviceComponentEnum.AddMember("RegularAirForce", new EdmEnumMemberValue(0));
        serviceComponentEnum.AddMember("UnitedStatesSpaceForce", new EdmEnumMemberValue(1));
        serviceComponentEnum.AddMember("AirForceReserve", new EdmEnumMemberValue(2));
        serviceComponentEnum.AddMember("AirNationalGuard", new EdmEnumMemberValue(3));
        model.AddElement(serviceComponentEnum);

        var incidentTypeEnum = new EdmEnumType(enumNs, "IncidentType");
        incidentTypeEnum.AddMember("Injury", new EdmEnumMemberValue(0));
        incidentTypeEnum.AddMember("Illness", new EdmEnumMemberValue(1));
        incidentTypeEnum.AddMember("Disease", new EdmEnumMemberValue(2));
        incidentTypeEnum.AddMember("Death", new EdmEnumMemberValue(3));
        incidentTypeEnum.AddMember("SexualAssault", new EdmEnumMemberValue(4));
        model.AddElement(incidentTypeEnum);

        var dutyStatusEnum = new EdmEnumType(enumNs, "DutyStatus");
        dutyStatusEnum.AddMember("Title10ActiveDuty", new EdmEnumMemberValue(0));
        dutyStatusEnum.AddMember("Title32ActiveDuty", new EdmEnumMemberValue(1));
        dutyStatusEnum.AddMember("InactiveDutyTraining", new EdmEnumMemberValue(2));
        dutyStatusEnum.AddMember("TravelToFromDuty", new EdmEnumMemberValue(3));
        dutyStatusEnum.AddMember("OtherQualifiedDuty", new EdmEnumMemberValue(4));
        dutyStatusEnum.AddMember("NotInDutyStatus", new EdmEnumMemberValue(5));
        model.AddElement(dutyStatusEnum);

        var substanceTypeEnum = new EdmEnumType(enumNs, "SubstanceType");
        substanceTypeEnum.AddMember("Alcohol", new EdmEnumMemberValue(0));
        substanceTypeEnum.AddMember("Drugs", new EdmEnumMemberValue(1));
        substanceTypeEnum.AddMember("Both", new EdmEnumMemberValue(2));
        model.AddElement(substanceTypeEnum);

        var lineOfDutyFindingEnum = new EdmEnumType(enumNs, "LineOfDutyFinding");
        lineOfDutyFindingEnum.AddMember("InLineOfDuty", new EdmEnumMemberValue(0));
        lineOfDutyFindingEnum.AddMember("NotInLineOfDutyDueToMisconduct", new EdmEnumMemberValue(1));
        lineOfDutyFindingEnum.AddMember("NotInLineOfDutyNotDueToMisconduct", new EdmEnumMemberValue(2));
        lineOfDutyFindingEnum.AddMember("ExistingPriorToServiceNotAggravated", new EdmEnumMemberValue(3));
        lineOfDutyFindingEnum.AddMember("ExistingPriorToServiceAggravated", new EdmEnumMemberValue(4));
        lineOfDutyFindingEnum.AddMember("PriorServiceCondition", new EdmEnumMemberValue(5));
        lineOfDutyFindingEnum.AddMember("EightYearRuleApplied", new EdmEnumMemberValue(6));
        lineOfDutyFindingEnum.AddMember("Undetermined", new EdmEnumMemberValue(7));
        model.AddElement(lineOfDutyFindingEnum);

        var workflowStateEnum = new EdmEnumType(enumNs, "WorkflowState");
        workflowStateEnum.AddMember("Draft", new EdmEnumMemberValue(0));
        workflowStateEnum.AddMember("MemberInformationEntry", new EdmEnumMemberValue(1));
        workflowStateEnum.AddMember("MedicalTechnicianReview", new EdmEnumMemberValue(2));
        workflowStateEnum.AddMember("MedicalOfficerReview", new EdmEnumMemberValue(3));
        workflowStateEnum.AddMember("UnitCommanderReview", new EdmEnumMemberValue(4));
        workflowStateEnum.AddMember("WingJudgeAdvocateReview", new EdmEnumMemberValue(5));
        workflowStateEnum.AddMember("AppointingAuthorityReview", new EdmEnumMemberValue(6));
        workflowStateEnum.AddMember("WingCommanderReview", new EdmEnumMemberValue(7));
        workflowStateEnum.AddMember("BoardMedicalTechnicianReview", new EdmEnumMemberValue(8));
        workflowStateEnum.AddMember("BoardMedicalOfficerReview", new EdmEnumMemberValue(9));
        workflowStateEnum.AddMember("BoardLegalReview", new EdmEnumMemberValue(10));
        workflowStateEnum.AddMember("BoardAdministratorReview", new EdmEnumMemberValue(11));
        workflowStateEnum.AddMember("Completed", new EdmEnumMemberValue(12));
        workflowStateEnum.AddMember("Closed", new EdmEnumMemberValue(13));
        workflowStateEnum.AddMember("Cancelled", new EdmEnumMemberValue(14));
        model.AddElement(workflowStateEnum);

        // ── Entity types ────────────────────────────────────────────────

        var caseType = new EdmEntityType(ns, "LineOfDutyCase");
        caseType.AddKeys(caseType.AddStructuralProperty("Id", EdmPrimitiveTypeKind.Int32, false));
        caseType.AddStructuralProperty("ProcessType", new EdmEnumTypeReference(processTypeEnum, false));
        caseType.AddStructuralProperty("Component", new EdmEnumTypeReference(serviceComponentEnum, false));
        caseType.AddStructuralProperty("IncidentType", new EdmEnumTypeReference(incidentTypeEnum, false));
        caseType.AddStructuralProperty("IncidentDutyStatus", new EdmEnumTypeReference(dutyStatusEnum, false));
        caseType.AddStructuralProperty("SubstanceType", new EdmEnumTypeReference(substanceTypeEnum, true));
        caseType.AddStructuralProperty("FinalFinding", new EdmEnumTypeReference(lineOfDutyFindingEnum, false));
        caseType.AddStructuralProperty("BoardFinding", new EdmEnumTypeReference(lineOfDutyFindingEnum, true));
        caseType.AddStructuralProperty("ApprovingFinding", new EdmEnumTypeReference(lineOfDutyFindingEnum, true));
        model.AddElement(caseType);

        var memberType = new EdmEntityType(ns, "Member");
        memberType.AddKeys(memberType.AddStructuralProperty("Id", EdmPrimitiveTypeKind.Int32, false));
        model.AddElement(memberType);

        var notificationType = new EdmEntityType(ns, "Notification");
        notificationType.AddKeys(notificationType.AddStructuralProperty("Id", EdmPrimitiveTypeKind.Int32, false));
        model.AddElement(notificationType);

        var authorityType = new EdmEntityType(ns, "LineOfDutyAuthority");
        authorityType.AddKeys(authorityType.AddStructuralProperty("Id", EdmPrimitiveTypeKind.Int32, false));
        model.AddElement(authorityType);

        var documentType = new EdmEntityType(ns, "LineOfDutyDocument");
        documentType.AddKeys(documentType.AddStructuralProperty("Id", EdmPrimitiveTypeKind.Int32, false));
        model.AddElement(documentType);

        var appealType = new EdmEntityType(ns, "LineOfDutyAppeal");
        appealType.AddKeys(appealType.AddStructuralProperty("Id", EdmPrimitiveTypeKind.Int32, false));
        model.AddElement(appealType);

        var medconType = new EdmEntityType(ns, "MEDCONDetail");
        medconType.AddKeys(medconType.AddStructuralProperty("Id", EdmPrimitiveTypeKind.Int32, false));
        model.AddElement(medconType);

        var incapType = new EdmEntityType(ns, "INCAPDetails");
        incapType.AddKeys(incapType.AddStructuralProperty("Id", EdmPrimitiveTypeKind.Int32, false));
        model.AddElement(incapType);

        var bookmarkType = new EdmEntityType(ns, "Bookmark");
        bookmarkType.AddKeys(bookmarkType.AddStructuralProperty("Id", EdmPrimitiveTypeKind.Int32, false));
        model.AddElement(bookmarkType);

        var historyType = new EdmEntityType(ns, "WorkflowStateHistory");
        historyType.AddKeys(historyType.AddStructuralProperty("Id", EdmPrimitiveTypeKind.Int32, false));
        historyType.AddStructuralProperty("WorkflowState", new EdmEnumTypeReference(workflowStateEnum, false));
        model.AddElement(historyType);

        var witnessType = new EdmEntityType(ns, "WitnessStatement");
        witnessType.AddKeys(witnessType.AddStructuralProperty("Id", EdmPrimitiveTypeKind.Int32, false));
        model.AddElement(witnessType);

        var auditType = new EdmEntityType(ns, "AuditComment");
        auditType.AddKeys(auditType.AddStructuralProperty("Id", EdmPrimitiveTypeKind.Int32, false));
        model.AddElement(auditType);

        // ── Entity container ────────────────────────────────────────────

        var container = new EdmEntityContainer(ns, "Default");
        container.AddEntitySet("Cases", caseType);
        container.AddEntitySet("Members", memberType);
        container.AddEntitySet("Notifications", notificationType);
        container.AddEntitySet("Authorities", authorityType);
        container.AddEntitySet("Documents", documentType);
        container.AddEntitySet("Appeals", appealType);
        container.AddEntitySet("MEDCONDetails", medconType);
        container.AddEntitySet("INCAPDetails", incapType);
        container.AddEntitySet("Bookmarks", bookmarkType);
        container.AddEntitySet("WorkflowStateHistory", historyType);
        container.AddEntitySet("WitnessStatements", witnessType);
        container.AddEntitySet("AuditComments", auditType);
        model.AddElement(container);

        return model;
    }

    private sealed class SingleHttpClientFactory(HttpClient httpClient) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => httpClient;
    }
}
