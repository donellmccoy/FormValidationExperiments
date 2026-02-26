using Microsoft.EntityFrameworkCore;

namespace ECTSystem.Persistence.Data;

public static class ModelBuilderExtensions
{
    /// <summary>
    /// Converts all non-ownership Cascade delete behaviors to ClientCascade.
    /// This avoids SQL Server's "multiple cascade paths" error (Error 1785)
    /// while letting EF Core handle cascade deletes in memory for tracked entities.
    /// </summary>
    public static void DisableDatabaseCascadeDelete(this ModelBuilder modelBuilder)
    {
        foreach (var relationship in modelBuilder.Model.GetEntityTypes()
                     .SelectMany(e => e.GetForeignKeys()))
        {
            if (!relationship.IsOwnership && relationship.DeleteBehavior == DeleteBehavior.Cascade)
            {
                relationship.DeleteBehavior = DeleteBehavior.ClientCascade;
            }
        }
    }
}
