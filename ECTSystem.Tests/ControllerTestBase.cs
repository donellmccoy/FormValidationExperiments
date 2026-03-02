using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using ECTSystem.Shared.Models;

namespace ECTSystem.Tests;

/// <summary>
/// Base class for controller unit tests. Provides a fake authenticated user context
/// and shared model-builder helpers so derived classes stay focused on assertion logic.
/// </summary>
public abstract class ControllerTestBase
{
    protected const string TestUserId = "test-user-id";
    private const int DefaultMemberId = 1;

    /// <summary>
    /// Creates a <see cref="ControllerContext"/> whose <c>User</c> is authenticated
    /// with the specified <paramref name="userId"/> as the NameIdentifier claim.
    /// </summary>
    protected static ControllerContext CreateControllerContext(string userId = TestUserId)
    {
        var claims   = new[] { new Claim(ClaimTypes.NameIdentifier, userId) };
        var identity = new ClaimsIdentity(claims, "TestScheme");
        return new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) }
        };
    }

    protected static ILogger<T> CreateLogger<T>() => NullLoggerFactory.Instance.CreateLogger<T>();

    /// <summary>
    /// Creates a minimal fully-populated <see cref="LineOfDutyCase"/> suitable for
    /// testing controller actions that deal with cases.
    /// </summary>
    protected static LineOfDutyCase BuildCase(int id = 1, string caseId = null) => new LineOfDutyCase
    {
        Id                  = id,
        CaseId              = caseId ?? $"CASE-{id:D4}",
        MemberId            = DefaultMemberId,
        MemberName          = "SSgt John Doe",
        MemberRank          = "SSgt",
        Unit                = "99 ABW",
        IncidentDescription = "Training injury",
        InitiationDate      = new DateTime(2025, 1, 15),
        MEDCON              = new MEDCONDetail(),
        INCAP               = new INCAPDetails(),
        Authorities         = [],
        Documents           = [],
        Appeals             = [],
        TimelineSteps       = [],
        Notifications       = [],
        WitnessStatements   = [],
        AuditComments       = []
    };
}
