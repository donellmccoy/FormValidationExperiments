using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using ECTSystem.Shared.Models;

namespace ECTSystem.Tests.Controllers;

/// <summary>
/// Base class for all OData controller unit tests in the ECT System API.
/// Provides a fake authenticated user context via <see cref="CreateControllerContext"/>
/// and shared model-builder helpers so derived test classes stay focused on assertion logic
/// rather than boilerplate setup.
/// </summary>
/// <remarks>
/// <para>
/// Every controller in the ECT System API requires an authenticated user (all controllers
/// are decorated with <c>[Authorize]</c>). This base class simulates authentication by
/// creating a <see cref="System.Security.Claims.ClaimsPrincipal"/> with a
/// <see cref="System.Security.Claims.ClaimTypes.NameIdentifier"/> claim, which controllers
/// use to identify the current user for operations like bookmark ownership and checkout tracking.
/// </para>
/// <para>
/// Derived classes include <see cref="CasesControllerTests"/>,
/// <see cref="CaseBookmarksControllerTests"/>, <see cref="DocumentsControllerTests"/>,
/// <see cref="MembersControllerTests"/>, and <see cref="WorkflowStateHistoriesControllerTests"/>.
/// </para>
/// </remarks>
public abstract class ControllerTestBase
{
    /// <summary>
    /// The default user identifier assigned to the fake authenticated principal in
    /// <see cref="CreateControllerContext"/>. Controllers that read
    /// <see cref="System.Security.Claims.ClaimTypes.NameIdentifier"/> from the request
    /// will see this value unless a custom <paramref name="userId"/> is provided.
    /// </summary>
    protected const string TestUserId = "test-user-id";

    /// <summary>
    /// Default foreign-key value used for <see cref="LineOfDutyCase.MemberId"/> when
    /// building test cases via <see cref="BuildCase"/>. Tests that seed an in-memory
    /// database should ensure a <see cref="ECTSystem.Shared.Models.Member"/> with this
    /// <c>Id</c> exists to satisfy EF Core navigation-property resolution.
    /// </summary>
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

    /// <summary>
    /// Creates a no-op <see cref="ILogger{T}"/> using <see cref="Microsoft.Extensions.Logging.Abstractions.NullLoggerFactory"/>
    /// so that controller constructors requiring a logger can be satisfied without capturing log output.
    /// </summary>
    /// <typeparam name="T">The category type for the logger, typically the controller under test.</typeparam>
    /// <returns>A <see cref="ILogger{T}"/> that discards all log entries.</returns>
    protected static ILogger<T> CreateLogger<T>() => NullLoggerFactory.Instance.CreateLogger<T>();

    /// <summary>
    /// Creates a minimal, fully-populated <see cref="LineOfDutyCase"/> suitable for seeding
    /// an in-memory database or passing directly to controller action methods under test.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The returned case includes empty-but-non-null collections for all navigation properties
    /// (<c>Authorities</c>, <c>Documents</c>, <c>Appeals</c>, <c>Notifications</c>,
    /// <c>WitnessStatements</c>, <c>AuditComments</c>) and default <see cref="MEDCONDetail"/>
    /// and <see cref="INCAPDetails"/> instances. This prevents null-reference exceptions in
    /// controller code that iterates or serializes these properties.
    /// </para>
    /// <para>
    /// The <paramref name="caseId"/> defaults to a date-stamped sequential ID matching the
    /// server's auto-generation pattern (<c>YYYYMMDD-NNN</c>) when not explicitly supplied.
    /// </para>
    /// </remarks>
    /// <param name="id">The primary key for the case entity. Defaults to <c>1</c>.</param>
    /// <param name="caseId">
    /// The human-readable case identifier (e.g., <c>"20250115-001"</c>). When <c>null</c>,
    /// a value is generated from the current UTC date and the <paramref name="id"/>.
    /// </param>
    /// <returns>A new <see cref="LineOfDutyCase"/> instance populated with realistic test data.</returns>
    protected static LineOfDutyCase BuildCase(int id = 1, string caseId = null) => new LineOfDutyCase
    {
        Id                  = id,
        CaseId              = caseId ?? $"{DateTime.UtcNow:yyyyMMdd}-{id:D3}",
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
        Notifications       = [],
        WitnessStatements   = [],
        AuditComments       = []
    };
}
