using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using ECTSystem.Persistence.Models;

namespace ECTSystem.Api.Controllers;

/// <summary>
/// Lightweight controller for returning the current authenticated user's identity.
/// Used by the Blazor WASM client to determine the user ID when opaque bearer tokens
/// are in use (ASP.NET Core Identity Data Protection tokens don't contain parseable claims).
/// </summary>
[ApiController]
[Route("api/[controller]")]
//[Authorize]
public class UserController(UserManager<ApplicationUser> userManager) : ControllerBase
{
    [HttpGet("me")]
    public IActionResult GetCurrentUser()
    {
        var userId = User?.FindFirstValue(ClaimTypes.NameIdentifier) ?? "test-user-id";
        var email = User?.FindFirstValue(ClaimTypes.Email);
        var name = User?.FindFirstValue(ClaimTypes.Name) ?? email ?? userId;

        return Ok(new { UserId = userId, Name = name });
    }

    [HttpGet("lookup")]
    public async Task<IActionResult> LookupUsers([FromQuery] string[] ids)
    {
        if (ids is null || ids.Length == 0)
            return Ok(new Dictionary<string, string>());

        var result = new Dictionary<string, string>(ids.Length);

        foreach (var id in ids.Distinct())
        {
            var user = await userManager.FindByIdAsync(id);
            result[id] = user?.UserName ?? user?.Email ?? id;
        }

        return Ok(result);
    }
}
