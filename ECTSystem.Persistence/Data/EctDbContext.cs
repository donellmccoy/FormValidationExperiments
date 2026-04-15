using Microsoft.EntityFrameworkCore;
using ECTSystem.Shared.Models;

namespace ECTSystem.Persistence.Data;

/// <summary>
/// Entity Framework Core database context for the ECT (LOD) application domain model.
/// Identity is handled separately by <see cref="EctIdentityDbContext"/>.
/// </summary>
public class EctDbContext : DbContext
{
    public EctDbContext(DbContextOptions<EctDbContext> options) : base(options)
    {
    }

    public DbSet<LineOfDutyCase> Cases { get; set; }
    public DbSet<Member> Members { get; set; }
    public DbSet<LineOfDutyDocument> Documents { get; set; }
    public DbSet<LineOfDutyAppeal> Appeals { get; set; }
    public DbSet<LineOfDutyAuthority> Authorities { get; set; }
    public DbSet<MEDCONDetail> MEDCONDetails { get; set; }
    public DbSet<INCAPDetails> INCAPDetails { get; set; }
    public DbSet<Notification> Notifications { get; set; }
    public DbSet<Bookmark> Bookmarks { get; set; }
    public DbSet<WorkflowStateLookup> WorkflowStates { get; set; }
    public DbSet<WorkflowStateHistory> WorkflowStateHistories { get; set; }
    public DbSet<WitnessStatement> WitnessStatements { get; set; }
    public DbSet<AuditComment> AuditComments { get; set; }
    public DbSet<CaseDialogueComment> CaseDialogueComments { get; set; }
    public DbSet<WorkflowType> WorkflowTypes { get; set; }
    public DbSet<WorkflowModule> WorkflowModules { get; set; }

    // Audit fields are populated by AuditSaveChangesInterceptor (registered via DI).
    // The interceptor reads the current user from IHttpContextAccessor and sets
    // CreatedBy/ModifiedBy/CreatedDate/ModifiedDate on AuditableEntity entries.

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(EctDbContext).Assembly);
        modelBuilder.DisableDatabaseCascadeDelete();

        // Constrain AuditableEntity string columns across all entity types.
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (!typeof(AuditableEntity).IsAssignableFrom(entityType.ClrType))
                continue;

            modelBuilder.Entity(entityType.ClrType).Property(nameof(AuditableEntity.CreatedBy)).HasMaxLength(256);
            modelBuilder.Entity(entityType.ClrType).Property(nameof(AuditableEntity.ModifiedBy)).HasMaxLength(256);
        }

        // Normalize all DateTime/DateTime? properties to UTC.
        // Microsoft.OData.Client serializes DateTime as DateTimeOffset with "+00:00" offset.
        // System.Text.Json and the OData Delta formatter both deserialize "+00:00" strings
        // into DateTime with Kind=Local (converted to server-local time). This convention
        // ensures that all DateTime values are stored as UTC in the database and read back
        // with Kind=Utc, regardless of the deserialization path.
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            foreach (var property in entityType.GetProperties())
            {
                if (property.ClrType == typeof(DateTime))
                {
                    property.SetValueConverter(
                        new Microsoft.EntityFrameworkCore.Storage.ValueConversion.ValueConverter<DateTime, DateTime>(
                            v => v.Kind == DateTimeKind.Utc ? v : v.ToUniversalTime(),
                            v => DateTime.SpecifyKind(v, DateTimeKind.Utc)));
                }
                else if (property.ClrType == typeof(DateTime?))
                {
                    property.SetValueConverter(
                        new Microsoft.EntityFrameworkCore.Storage.ValueConversion.ValueConverter<DateTime?, DateTime?>(
                            v => v.HasValue
                                ? (v.Value.Kind == DateTimeKind.Utc ? v : v.Value.ToUniversalTime())
                                : v,
                            v => v.HasValue
                                ? DateTime.SpecifyKind(v.Value, DateTimeKind.Utc)
                                : v));
                }
            }
        }
    }
}
