using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using ECTSystem.Persistence.Models;

namespace ECTSystem.Persistence.Data;

/// <summary>
/// Dedicated DbContext for ASP.NET Core Identity tables.
/// Shares the same database as <see cref="EctDbContext"/> but keeps Identity
/// concerns separate from the application's domain model.
/// </summary>
public class EctIdentityDbContext : IdentityDbContext<ApplicationUser>
{
    public EctIdentityDbContext(DbContextOptions<EctIdentityDbContext> options) : base(options)
    {
    }
}
