using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ECTSystem.Tests.Integration;

/// <summary>
/// Regression tests verifying that the API route table does not contain duplicate
/// or accidentally re-introduced endpoints. Guards against §2.9 N2 — duplicate
/// <c>/me</c> endpoint where a Program.cs minimal API and the
/// <see cref="ECTSystem.Api.Controllers.UserController"/> previously coexisted with
/// divergent response shapes.
/// </summary>
public class RouteRegistrationTests : IClassFixture<EctSystemWebApplicationFactory>
{
    private readonly EctSystemWebApplicationFactory _factory;

    public RouteRegistrationTests(EctSystemWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public void Only_One_MeEndpoint_Is_Registered()
    {
        // Force the host to build so endpoints are available.
        _ = _factory.CreateClient();

        var endpointSources = _factory.Services.GetServices<EndpointDataSource>();
        var meRoutes = endpointSources
            .SelectMany(s => s.Endpoints)
            .OfType<RouteEndpoint>()
            .Where(e => e.RoutePattern.RawText is { } p &&
                        (p.Equals("/me", StringComparison.OrdinalIgnoreCase) ||
                         p.EndsWith("/User/me", StringComparison.OrdinalIgnoreCase)))
            .Select(e => e.RoutePattern.RawText!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        Assert.Single(meRoutes);
        Assert.EndsWith("User/me", meRoutes[0], StringComparison.OrdinalIgnoreCase);
    }
}
