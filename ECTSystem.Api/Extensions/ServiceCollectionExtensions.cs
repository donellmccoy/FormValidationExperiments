using System.Threading.RateLimiting;
using Azure.Storage.Blobs;
using ECTSystem.Api.Logging;
using ECTSystem.Api.Services;
using ECTSystem.Persistence.Data;
using ECTSystem.Persistence.Models;
using ECTSystem.Shared.Models;
using ECTSystem.Shared.Enums;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.OData;
using Microsoft.AspNetCore.OData.Batch;
using Microsoft.EntityFrameworkCore;
using Microsoft.OData.Edm;
using Microsoft.OData.ModelBuilder;

namespace ECTSystem.Api.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddHttpContextAccessor();
        services.AddProblemDetails();
        services.AddSingleton(TimeProvider.System);

        services.AddDatabase(configuration)
                .AddIdentity()
                .AddApiLogging()
                .AddBlobStorage(configuration)
                .AddODataControllers()
                .AddPdfServices()
                .AddCorsPolicy()
                .AddOpenApi();
                //.AddApiRateLimiting();

        return services;
    }

    private static IServiceCollection AddDatabase(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("EctDatabase");

        services.AddSingleton<AuditSaveChangesInterceptor>();

        services.AddPooledDbContextFactory<EctDbContext>((sp, options) =>
        {
            options.UseSqlServer(connectionString, sql =>
            {
                sql.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery);
                sql.EnableRetryOnFailure(maxRetryCount: 5, maxRetryDelay: TimeSpan.FromSeconds(30), errorNumbersToAdd: null);
            });
            options.AddInterceptors(sp.GetRequiredService<AuditSaveChangesInterceptor>());
        }, poolSize: 32);

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

        services.AddAuthorization(options =>
        {
            options.AddPolicy("Admin", policy => policy.RequireRole("Admin"));
            options.AddPolicy("CaseManager", policy => policy.RequireRole("Admin", "CaseManager"));
            options.AddPolicy("CanManageDocuments", policy => policy.RequireRole("Admin", "CaseManager"));
        });

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
                options.AddRouteComponents("odata", edmModel, new DefaultODataBatchHandler())
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
                      .AllowCredentials()
                      .WithExposedHeaders("X-Case-IsBookmarked");
            });
        });

        return services;
    }

    private static IServiceCollection AddBlobStorage(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("BlobStorage");
        services.AddSingleton(_ => new BlobServiceClient(connectionString));
        services.AddSingleton<IBlobStorageService, AzureBlobStorageService>();
        services.AddHostedService<BlobContainerInitializer>();

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
        odataBuilder.EntityType<LineOfDutyDocument>()
            .Property(d => d.RowVersion).IsConcurrencyToken();

        odataBuilder.EntitySet<LineOfDutyAppeal>("Appeals");
        odataBuilder.EntitySet<MEDCONDetail>("MEDCONDetails");
        odataBuilder.EntitySet<INCAPDetails>("INCAPDetails");

        odataBuilder.EntitySet<Bookmark>("Bookmarks");

        odataBuilder.EntitySet<WorkflowStateHistory>("WorkflowStateHistory");
        odataBuilder.EntitySet<WitnessStatement>("WitnessStatements");
        odataBuilder.EntitySet<AuditComment>("AuditComments");
        odataBuilder.EntitySet<CaseDialogueComment>("CaseDialogueComments");

        // Bound actions: POST /odata/Cases({key})/Checkout, /Checkin
        var checkoutAction = casesEntitySet.EntityType.Action("Checkout").ReturnsFromEntitySet<LineOfDutyCase>("Cases");
        checkoutAction.Parameter<byte[]>("RowVersion").Optional();
        var checkinAction = casesEntitySet.EntityType.Action("Checkin").ReturnsFromEntitySet<LineOfDutyCase>("Cases");
        checkinAction.Parameter<byte[]>("RowVersion").Optional();

        // Bound collection action: POST /odata/Cases/ByCurrentState
        var byCurrentState = casesEntitySet.EntityType.Collection.Action("ByCurrentState")
            .ReturnsCollectionFromEntitySet<LineOfDutyCase>("Cases");
        byCurrentState.CollectionParameter<WorkflowState>("includeStates").Optional();
        byCurrentState.CollectionParameter<WorkflowState>("excludeStates").Optional();

        // Bound collection function: GET /odata/Cases/Default.Bookmarked()
        casesEntitySet.EntityType.Collection.Function("Bookmarked").ReturnsCollectionFromEntitySet<LineOfDutyCase>("Cases");

        return odataBuilder.GetEdmModel();
    }
}
