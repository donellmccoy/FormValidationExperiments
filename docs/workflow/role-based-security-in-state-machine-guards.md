# Role-Based Security in State Machine Guards

Your project already has the auth plumbing — JWT tokens, `JwtAuthStateProvider`,
`ClaimsIdentity`, `[Authorize]` controllers. Here's how to wire roles into the
guards.

## 1. Define Workflow Roles

Create an enum in `ECTSystem.Shared/Enums/` mapping to the LOD workflow
participants:

```csharp
public enum WorkflowRole
{
    MedicalTechnician,      // Steps 1-2: enters member info, clinical data
    MedicalOfficer,         // Step 3: reviews/signs medical assessment
    UnitCommander,          // Step 4: endorsement
    WingJudgeAdvocate,      // Step 5: legal review
    AppointingAuthority,    // Step 6: authority review
    WingCommander,          // Step 7: determination
    BoardMedicalTechnician, // Step 8: board-level med tech
    BoardMedicalOfficer,    // Step 9: board-level med officer
    BoardLegalReviewer,     // Step 10: board-level legal
    BoardAdministrator,     // Step 11: board admin
    Administrator           // Can cancel, override, etc.
}
```

## 2. Seed Identity Roles

On the API side, register these as ASP.NET Core Identity roles in your seeder:

```csharp
var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
foreach (var role in Enum.GetNames<WorkflowRole>())
{
    if (!await roleManager.RoleExistsAsync(role))
        await roleManager.CreateAsync(new IdentityRole(role));
}
```

Then assign roles to users via `UserManager.AddToRoleAsync()`. The roles will be
included as claims in the JWT token automatically (Identity's default
`UserClaimsPrincipalFactory` adds role claims).

## 3. Inject the User's Roles into the State Machine

Give `LodStateMachine` a way to know the current user's roles. The cleanest
approach is a simple interface:

```csharp
public interface ICurrentUser
{
    IReadOnlySet<string> Roles { get; }
    string UserId { get; }
    string DisplayName { get; }
}
```

Implement it by pulling from `AuthenticationStateProvider`:

```csharp
public class CurrentUser : ICurrentUser
{
    public IReadOnlySet<string> Roles { get; private set; } = new HashSet<string>();
    public string UserId { get; private set; } = string.Empty;
    public string DisplayName { get; private set; } = string.Empty;

    public async Task InitializeAsync(AuthenticationStateProvider authStateProvider)
    {
        var state = await authStateProvider.GetAuthenticationStateAsync();
        var user = state.User;
        UserId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? string.Empty;
        DisplayName = user.Identity?.Name ?? string.Empty;
        Roles = user.FindAll(ClaimTypes.Role)
                     .Select(c => c.Value)
                     .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }
}
```

## 4. Update the State Machine Constructor

Add `ICurrentUser` as a dependency:

```csharp
private readonly ICurrentUser _currentUser;

public LodStateMachine(
    LineOfDutyCase lineOfDutyCase,
    IDataService dataService,
    ICurrentUser currentUser)
{
    _lineOfDutyCase = lineOfDutyCase;
    _dataService = dataService;
    _currentUser = currentUser;
    // ... existing setup
}
```

## 5. Wire Roles into Guards

Map each state to its required role, then check in guards:

```csharp
// Central mapping: which role owns which state
private static readonly Dictionary<WorkflowState, WorkflowRole> _stateOwners = new()
{
    [WorkflowState.MemberInformationEntry]          = WorkflowRole.MedicalTechnician,
    [WorkflowState.MedicalTechnicianReview]          = WorkflowRole.MedicalTechnician,
    [WorkflowState.MedicalOfficerReview]             = WorkflowRole.MedicalOfficer,
    [WorkflowState.UnitCommanderReview]              = WorkflowRole.UnitCommander,
    [WorkflowState.WingJudgeAdvocateReview]           = WorkflowRole.WingJudgeAdvocate,
    [WorkflowState.AppointingAuthorityReview]         = WorkflowRole.AppointingAuthority,
    [WorkflowState.WingCommanderReview]              = WorkflowRole.WingCommander,
    [WorkflowState.BoardMedicalTechnicianReview]     = WorkflowRole.BoardMedicalTechnician,
    [WorkflowState.BoardMedicalOfficerReview]        = WorkflowRole.BoardMedicalOfficer,
    [WorkflowState.BoardLegalReview]                 = WorkflowRole.BoardLegalReviewer,
    [WorkflowState.BoardAdministratorReview]         = WorkflowRole.BoardAdministrator,
};

// Reusable check: does the current user own the current state?
private bool IsCurrentUserStateOwner()
{
    return _stateOwners.TryGetValue(_sm.State, out var requiredRole)
        && _currentUser.Roles.Contains(requiredRole.ToString());
}

// Admin override
private bool IsAdmin() => _currentUser.Roles.Contains(WorkflowRole.Administrator.ToString());
```

Then each guard becomes:

```csharp
private bool CanForwardToMedicalTechnicianReviewAsync()
{
    // Only the state owner (or admin) of the CURRENT state can fire a forward trigger
    return IsCurrentUserStateOwner() || IsAdmin();
}

private bool CanCancelAsync()
{
    // Only admins or the current state owner can cancel
    return IsAdmin() || IsCurrentUserStateOwner();
}
```

For return triggers, you might require a different role — the _destination_
state's owner:

```csharp
private bool CanReturnToMedicalTechnicianReviewAsync()
{
    // Can only return if you own the CURRENT state (you're sending it back)
    return IsCurrentUserStateOwner() || IsAdmin();
}
```

## 6. The UI Effect

Since EditCase already calls `_stateMachine.GetPermittedTriggersAsync()` to
determine which buttons are shown, **no UI changes are needed**. Guards that
return `false` because the user lacks the role will automatically filter that
trigger out of the permitted set. Buttons for unauthorized transitions simply
won't appear.

## Architecture Summary

```text
JWT Token (with role claims)
  ↓
JwtAuthStateProvider (parses claims)
  ↓
ICurrentUser.Roles (HashSet<string>)
  ↓
LodStateMachine guards (check role ownership)
  ↓
GetPermittedTriggersAsync() (only returns triggers passing guards)
  ↓
EditCase UI (only shows permitted action buttons)
```

The key principle: **authorization is enforced at the guard level, not the UI
level**. The UI is just a reflection of what the state machine permits. Even if
someone manipulates the client, the API controllers already have `[Authorize]` —
and you could add role-based `[Authorize(Roles = "...")]` on specific OData
endpoints as a second layer.
