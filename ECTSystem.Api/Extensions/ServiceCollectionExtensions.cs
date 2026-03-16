using System.Threading.RateLimiting;
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
                .AddOpenApi()
                .AddApiRateLimiting();

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

    private static IServiceCollection AddApiRateLimiting(this IServiceCollection services)
    {
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
            {
                var userId = context.User.Identity?.Name ?? context.Connection.RemoteIpAddress?.ToString() ?? "anonymous";

                return RateLimitPartition.GetSlidingWindowLimiter(userId, _ => new SlidingWindowRateLimiterOptions
                {
                    PermitLimit = 100,
                    Window = TimeSpan.FromMinutes(1),
                    SegmentsPerWindow = 4,
                    QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                    QueueLimit = 0
                });
            });

            options.OnRejected = async (context, cancellationToken) =>
            {
                context.HttpContext.Response.Headers.RetryAfter = "60";

                await context.HttpContext.Response.WriteAsync(
                    "Rate limit exceeded. Try again later.", cancellationToken);
            };
        });

        return services;
    }

    private static IEdmModel BuildEdmModel()
    {
        var odataBuilder = new ODataConventionModelBuilder();

        var casesEntitySet = odataBuilder.EntitySet<LineOfDutyCase>("Cases");

        odataBuilder.EntitySet<Member>("Members");
        odataBuilder.EntitySet<Notification>("Notifications");
        odataBuilder.EntitySet<LineOfDutyAuthority>("Authorities");

        odataBuilder.EntitySet<LineOfDutyDocument>("Documents");
        odataBuilder.EntityType<LineOfDutyDocument>().MediaType();
        odataBuilder.EntityType<LineOfDutyDocument>().Ignore(d => d.Content);

        odataBuilder.EntitySet<LineOfDutyAppeal>("Appeals");
        odataBuilder.EntitySet<MEDCONDetail>("MEDCONDetails");
        odataBuilder.EntitySet<INCAPDetails>("INCAPDetails");

        odataBuilder.EntitySet<CaseBookmark>("CaseBookmarks");

        odataBuilder.EntitySet<WorkflowStateHistory>("WorkflowStateHistories");
        odataBuilder.EntitySet<WitnessStatement>("WitnessStatements");
        odataBuilder.EntitySet<AuditComment>("AuditComments");

        // Bound actions: POST /odata/Cases({key})/Checkout, /Checkin
        casesEntitySet.EntityType.Action("Checkout").ReturnsFromEntitySet<LineOfDutyCase>("Cases");
        casesEntitySet.EntityType.Action("Checkin").ReturnsFromEntitySet<LineOfDutyCase>("Cases");

        // Bound collection function: GET /odata/Cases/ByCurrentState(includeStates='...',excludeStates='...')
        var byCurrentState = casesEntitySet.EntityType.Collection.Function("ByCurrentState")
            .ReturnsFromEntitySet<LineOfDutyCase>("Cases");
        byCurrentState.Parameter<string>("includeStates").Optional();
        byCurrentState.Parameter<string>("excludeStates").Optional();

        return odataBuilder.GetEdmModel();
    }
}
