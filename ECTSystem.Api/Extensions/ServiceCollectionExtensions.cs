using ECTSystem.Api.Logging;
using ECTSystem.Api.Services;
using ECTSystem.Persistence.Data;
using ECTSystem.Persistence.Models;
using ECTSystem.Shared.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.OData;
using Microsoft.EntityFrameworkCore;
using Microsoft.OData.Edm;
using Microsoft.OData.ModelBuilder;

namespace ECTSystem.Api.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDatabase(configuration)
                .AddIdentity()
                .AddApiLogging()
                .AddODataControllers()
                .AddPdfServices()
                .AddCorsPolicy()
                .AddOpenApi();

        return services;
    }

    private static IServiceCollection AddDatabase(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("EctDatabase");

        services.AddPooledDbContextFactory<EctDbContext>(options =>
            options.UseSqlServer(connectionString, sql =>
            {
                sql.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery);
                sql.EnableRetryOnFailure(maxRetryCount: 5, maxRetryDelay: TimeSpan.FromSeconds(30), errorNumbersToAdd: null);
            }), poolSize: 32);

        services.AddPooledDbContextFactory<EctIdentityDbContext>(options =>
            options.UseSqlServer(connectionString, sql =>
            {
                sql.EnableRetryOnFailure(maxRetryCount: 5, maxRetryDelay: TimeSpan.FromSeconds(30), errorNumbersToAdd: null);
            }), poolSize: 32);

        return services;
    }

    private static IServiceCollection AddIdentity(this IServiceCollection services)
    {
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

        return services;
    }

    private static IServiceCollection AddApiLogging(this IServiceCollection services)
    {
        services.AddSingleton<ILoggingService, LoggingService>();

        return services;
    }

    private static IServiceCollection AddODataControllers(this IServiceCollection services)
    {
        var edmModel = BuildEdmModel();

        services.AddSingleton(edmModel);

        // Delta<LineOfDutyCase> uses the entity type directly — no ComplexType registration needed.
        // The project uses <Nullable>disable</Nullable>, so reference type properties are
        // nullable-oblivious. Without this flag, MVC treats them as implicitly [Required],
        // which breaks endpoints that receive entities with null navigation properties
        // (e.g., SaveAuthorities receiving LineOfDutyAuthority without LineOfDutyCase).
        services.AddControllers(options =>
            {
                options.SuppressImplicitRequiredAttributeForNonNullableReferenceTypes = true;
            })
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

        return services;
    }

    private static IServiceCollection AddPdfServices(this IServiceCollection services)
    {
        services.AddScoped<AF348PdfService>();

        return services;
    }

    private static IServiceCollection AddCorsPolicy(this IServiceCollection services)
    {
        services.AddCors(options =>
        {
            options.AddPolicy("BlazorClient", policy =>
            {
                policy.WithOrigins(
                          "https://localhost:7240",
                          "http://localhost:5101",
                          "https://thankful-tree-039497510.6.azurestaticapps.net",
                          "https://app-ectsystem-web-dev.azurewebsites.net")
                      .AllowAnyHeader()
                      .AllowAnyMethod()
                      .AllowCredentials();
            });
        });

        return services;
    }

    private static IEdmModel BuildEdmModel()
    {
        var odataBuilder = new ODataConventionModelBuilder();

        var casesEntitySet = odataBuilder.EntitySet<LineOfDutyCase>("Cases");

        var caseType = casesEntitySet.EntityType;
        caseType.HasMany(c => c.Documents).AutomaticallyExpand(true);
        caseType.HasMany(c => c.Authorities).AutomaticallyExpand(true);
        caseType.HasMany(c => c.Appeals).AutomaticallyExpand(true);
        caseType.HasMany(c => c.Notifications).AutomaticallyExpand(true);
        caseType.HasMany(c => c.WorkflowStateHistories).AutomaticallyExpand(true);
        caseType.HasMany(c => c.WitnessStatements).AutomaticallyExpand(true);
        caseType.HasMany(c => c.AuditComments).AutomaticallyExpand(true);
        caseType.HasOptional(c => c.Member).AutomaticallyExpand(true);
        caseType.HasOptional(c => c.MEDCON).AutomaticallyExpand(true);
        caseType.HasOptional(c => c.INCAP).AutomaticallyExpand(true);

        odataBuilder.EntitySet<Member>("Members");
        odataBuilder.EntitySet<Notification>("Notifications");
        odataBuilder.EntitySet<LineOfDutyAuthority>("Authorities");

        odataBuilder.EntitySet<LineOfDutyDocument>("Documents");
        odataBuilder.EntityType<LineOfDutyDocument>().MediaType();
        odataBuilder.EntityType<LineOfDutyDocument>().Ignore(d => d.Content);

        odataBuilder.EntitySet<LineOfDutyAppeal>("Appeals");
        odataBuilder.EntitySet<MEDCONDetail>("MEDCONDetails");
        odataBuilder.EntitySet<INCAPDetails>("INCAPDetails");

        var caseBookmarksEntitySet = odataBuilder.EntitySet<CaseBookmark>("CaseBookmarks");
        caseBookmarksEntitySet.EntityType.Collection.Action("DeleteByCaseId").Parameter<int>("caseId");
        caseBookmarksEntitySet.EntityType.Collection.Function("IsBookmarked").Returns<bool>().Parameter<int>("caseId");

        odataBuilder.EntitySet<WorkflowStateHistory>("WorkflowStateHistories");
        odataBuilder.EntityType<WorkflowStateHistory>().Collection.Action("Batch");
        odataBuilder.EntitySet<WitnessStatement>("WitnessStatements");
        odataBuilder.EntitySet<AuditComment>("AuditComments");

        return odataBuilder.GetEdmModel();
    }
}
