using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.OData;
using Microsoft.EntityFrameworkCore;
using Microsoft.OData.ModelBuilder;
using Microsoft.OData.Edm;
using ECTSystem.Persistence.Data;
using ECTSystem.Persistence.Models;
using ECTSystem.Api.Logging;
using ECTSystem.Shared.Models;

namespace ECTSystem.Api.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services, IConfiguration configuration)
    {
        // Entity Framework Core — SQL Server
        var connectionString = configuration.GetConnectionString("EctDatabase");

        services.AddDbContextFactory<EctDbContext>(options =>
            options.UseSqlServer(connectionString));

        // Dedicated Identity DbContext — shares the same database but keeps Identity
        // concerns separate from the application domain model.
        services.AddDbContext<EctIdentityDbContext>(options =>
            options.UseSqlServer(connectionString));

        // ASP.NET Core Identity with Bearer token authentication
        services.AddIdentityApiEndpoints<ApplicationUser>(options =>
        {
            options.SignIn.RequireConfirmedAccount = false;
            options.Password.RequireDigit = false;
            options.Password.RequireUppercase = false;
            options.Password.RequireNonAlphanumeric = false;
            options.Password.RequiredLength = 6;
        })
        .AddEntityFrameworkStores<EctIdentityDbContext>();

        services.AddAuthorization();

        // Logging
        services.AddSingleton<IApiLogService, ApiLogService>();

        // OData Entity Data Model
        var odataBuilder = new ODataConventionModelBuilder();
        var casesEntitySet = odataBuilder.EntitySet<LineOfDutyCase>("Cases");
        casesEntitySet.EntityType.Collection.Function("Bookmarked").ReturnsCollectionFromEntitySet<LineOfDutyCase>("Cases");
        odataBuilder.EntitySet<Member>("Members");
        odataBuilder.EntitySet<Notification>("Notifications");
        odataBuilder.EntitySet<LineOfDutyAuthority>("Authorities");
        var documentsEntitySet = odataBuilder.EntitySet<LineOfDutyDocument>("Documents");
        odataBuilder.EntityType<LineOfDutyDocument>().MediaType();
        odataBuilder.EntityType<LineOfDutyDocument>().Ignore(d => d.Content);
        var timelineStepsEntitySet = odataBuilder.EntitySet<TimelineStep>("TimelineSteps");
        timelineStepsEntitySet.EntityType.Action("Sign")
            .ReturnsFromEntitySet<TimelineStep>("TimelineSteps");
        timelineStepsEntitySet.EntityType.Action("Start")
            .ReturnsFromEntitySet<TimelineStep>("TimelineSteps");
        odataBuilder.EntitySet<LineOfDutyAppeal>("Appeals");
        odataBuilder.EntitySet<MEDCONDetail>("MEDCONDetails");
        odataBuilder.EntitySet<INCAPDetails>("INCAPDetails");
        var caseBookmarksEntitySet = odataBuilder.EntitySet<CaseBookmark>("CaseBookmarks");
        caseBookmarksEntitySet.EntityType.Collection.Action("DeleteByCaseId")
            .Parameter<int>("caseId");
        caseBookmarksEntitySet.EntityType.Collection.Function("IsBookmarked")
            .Returns<bool>()
            .Parameter<int>("caseId");
        odataBuilder.EntitySet<WorkflowStateHistory>("WorkflowStateHistories");
        var edmModel = odataBuilder.GetEdmModel();
        services.AddSingleton<IEdmModel>(edmModel);

        // Delta<LineOfDutyCase> uses the entity type directly — no ComplexType registration needed.

        // Controllers + OData
        services.AddControllers()
            .AddOData(options =>
            {
                options.RouteOptions.EnableUnqualifiedOperationCall = true;
                options.AddRouteComponents("odata", edmModel)
                       .Select()
                       .Filter()
                       .Expand()
                       .OrderBy()
                       .SetMaxTop(100)
                       .Count();
            })
            .AddJsonOptions(options =>
            {
                options.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
                options.JsonSerializerOptions.ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;
            });

        // CORS — allow the Blazor WASM client
        services.AddCors(options =>
        {
            options.AddPolicy("BlazorClient", policy =>
            {
                policy.WithOrigins("https://localhost:7240", "http://localhost:5101")
                      .AllowAnyHeader()
                      .AllowAnyMethod()
                      .AllowCredentials();
            });
        });

        services.AddOpenApi();

        return services;
    }
}
