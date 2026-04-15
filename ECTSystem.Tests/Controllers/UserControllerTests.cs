using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Moq;
using ECTSystem.Api.Controllers;
using ECTSystem.Persistence.Models;
using ECTSystem.Shared.ViewModels;
using Xunit;

namespace ECTSystem.Tests.Controllers;

/// <summary>
/// Unit tests for <see cref="UserController"/>, which returns the current
/// authenticated user's identity and performs user lookups by ID.
/// </summary>
public class UserControllerTests : ControllerTestBase
{
    private readonly Mock<UserManager<ApplicationUser>> _mockUserManager;

    public UserControllerTests()
    {
        var store = new Mock<IUserStore<ApplicationUser>>();
        _mockUserManager = new Mock<UserManager<ApplicationUser>>(
            store.Object, null, null, null, null, null, null, null, null);
    }

    // ──────────────────── GetCurrentUser ────────────────────

    [Fact]
    public void GetCurrentUser_ReturnsCurrentUserDto()
    {
        var sut = new UserController(_mockUserManager.Object);
        sut.ControllerContext = CreateControllerContext();

        var result = sut.GetCurrentUser();

        var ok = Assert.IsType<OkObjectResult>(result);
        var dto = Assert.IsType<CurrentUserDto>(ok.Value);
        Assert.Equal(TestUserId, dto.UserId);
        Assert.Equal(TestUserId, dto.Name); // no Name/Email claims → falls back to userId
    }

    [Fact]
    public void GetCurrentUser_UsesNameClaim_WhenPresent()
    {
        var sut = new UserController(_mockUserManager.Object);
        sut.ControllerContext = CreateControllerContextWithClaims(
            (ClaimTypes.NameIdentifier, TestUserId),
            (ClaimTypes.Name, "SSgt John Doe"));

        var result = sut.GetCurrentUser();

        var ok = Assert.IsType<OkObjectResult>(result);
        var dto = Assert.IsType<CurrentUserDto>(ok.Value);
        Assert.Equal("SSgt John Doe", dto.Name);
    }

    [Fact]
    public void GetCurrentUser_FallsBackToEmail_WhenNoNameClaim()
    {
        var sut = new UserController(_mockUserManager.Object);
        sut.ControllerContext = CreateControllerContextWithClaims(
            (ClaimTypes.NameIdentifier, TestUserId),
            (ClaimTypes.Email, "john.doe@us.af.mil"));

        var result = sut.GetCurrentUser();

        var ok = Assert.IsType<OkObjectResult>(result);
        var dto = Assert.IsType<CurrentUserDto>(ok.Value);
        Assert.Equal("john.doe@us.af.mil", dto.Name);
    }

    [Fact]
    public void GetCurrentUser_ThrowsUnauthorized_WhenNoNameIdentifierClaim()
    {
        var sut = new UserController(_mockUserManager.Object);
        sut.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity("TestScheme"))
            }
        };

        Assert.Throws<UnauthorizedAccessException>(() => sut.GetCurrentUser());
    }

    // ──────────────────── LookupUsers ────────────────────

    [Fact]
    public async Task LookupUsers_ReturnsEmptyDictionary_WhenIdsIsEmpty()
    {
        var sut = new UserController(_mockUserManager.Object);
        sut.ControllerContext = CreateControllerContext();

        var result = await sut.LookupUsers([], CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var dict = Assert.IsType<Dictionary<string, string>>(ok.Value);
        Assert.Empty(dict);
    }

    [Fact]
    public async Task LookupUsers_ReturnsEmptyDictionary_WhenIdsIsNull()
    {
        var sut = new UserController(_mockUserManager.Object);
        sut.ControllerContext = CreateControllerContext();

        var result = await sut.LookupUsers(null, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var dict = Assert.IsType<Dictionary<string, string>>(ok.Value);
        Assert.Empty(dict);
    }

    // ──────────────────── Helpers ────────────────────

    private static ControllerContext CreateControllerContextWithClaims(
        params (string type, string value)[] claims)
    {
        var claimsList = claims.Select(c => new Claim(c.type, c.value)).ToArray();
        var identity = new ClaimsIdentity(claimsList, "TestScheme");
        return new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) }
        };
    }
}
