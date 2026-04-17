using System.ComponentModel.DataAnnotations;
using ECTSystem.Shared.Enums;

namespace ECTSystem.Shared.ViewModels;

public class UpdateDocumentDto
{
    [Required]
    public DocumentType DocumentType { get; set; }

    [StringLength(500)]
    public string Description { get; set; } = string.Empty;

    [Required]
    public byte[] RowVersion { get; set; } = [];
}
