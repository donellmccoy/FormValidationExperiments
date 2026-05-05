using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ECTSystem.Api.Extensions;
using ECTSystem.Persistence.Models;
using ECTSystem.Shared.ViewModels;

namespace ECTSystem.Api.Controllers;

/// <summary>
/// Lightweight controller for returning the current authenticated user's identity.
/// Used by the Blazor WASM client to determine the user ID when opaque bearer tokens
/// are in use (ASP.NET Core Identity Data Protection tokens don't contain parseable claims).
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class UserController(UserManager<ApplicationUser> userManager) : ControllerBase
{
    [HttpGet("me")]
    public async Task<IActionResult> GetCurrentUser()
    {
        var userId = User.GetRequiredUserId();
        var user = await userManager.FindByIdAsync(userId);

        var fullName = user is not null
            ? $"{user.FirstName} {user.LastName}".Trim()
            : string.Empty;

        var name = !string.IsNullOrWhiteSpace(fullName)
            ? fullName
            : User.FindFirstValue(ClaimTypes.Name)
                ?? User.FindFirstValue(ClaimTypes.Email)
                ?? userId;

        string role = null;
        if (user is not null)
        {
            var roles = await userManager.GetRolesAsync(user);
            role = roles.FirstOrDefault();
        }

        return Ok(new CurrentUserDto
        {
            UserId = userId,
            Name = name,
            FullName = fullName,
            Role = role
        });
    }

    [HttpGet("lookup")]
    public async Task<IActionResult> LookupUsers([FromQuery] string[] ids, CancellationToken ct = default)
    {
        if (ids is null || ids.Length == 0)
            return Ok(new Dictionary<string, string>());

        var distinctIds = ids.Distinct().Take(50).ToList();

        var users = await userManager.Users
            .Where(u => distinctIds.Contains(u.Id))
            .Select(u => new
            {
                u.Id,
                u.FirstName,
                u.LastName,
                u.UserName,
                u.Email
            })
            .ToDictionaryAsync(
                u => u.Id,
                u =>
                {
                    var fullName = $"{u.FirstName} {u.LastName}".Trim();
                    return !string.IsNullOrWhiteSpace(fullName)
                        ? fullName
                        : u.UserName ?? u.Email ?? u.Id;
                },
                ct);

        foreach (var id in distinctIds)
            users.TryAdd(id, id);

        return Ok(users);
    }
}
