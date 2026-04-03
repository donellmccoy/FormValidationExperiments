using System.ComponentModel.DataAnnotations;

namespace ECTSystem.Shared.Models;

public abstract class AuditableEntity
{
    public int CreatedBy { get; set; }
    public DateTime CreatedDate { get; set; }
    public int ModifiedBy { get; set; }
    public DateTime ModifiedDate { get; set; }

    [Timestamp]
    [ConcurrencyCheck]
    public byte[] RowVersion { get; set; } = [];
}
