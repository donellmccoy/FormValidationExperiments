using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ECTSystem.Api.Controllers;

/// <summary>
/// Lightweight controller for returning the current authenticated user's identity.
/// Used by the Blazor WASM client to determine the user ID when opaque bearer tokens
/// are in use (ASP.NET Core Identity Data Protection tokens don't contain parseable claims).
/// </summary>
[ApiController]
[Route("api/[controller]")]
//[Authorize]
public class UserController : ControllerBase
{
    [HttpGet("me")]
    public IActionResult GetCurrentUser()
    {
        var userId = User?.FindFirstValue(ClaimTypes.NameIdentifier) ?? "test-user-id";
        var email = User?.FindFirstValue(ClaimTypes.Email);
        var name = User?.FindFirstValue(ClaimTypes.Name) ?? email ?? userId;

        return Ok(new { UserId = userId, Name = name });
    }
}
