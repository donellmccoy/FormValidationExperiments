using System.ComponentModel.DataAnnotations;
using ECTSystem.Shared.Enums;

namespace ECTSystem.Shared.ViewModels;

public class CreateMemberDto
{
    [Required]
    [StringLength(100)]
    public string FirstName { get; set; } = string.Empty;

    [StringLength(5)]
    public string MiddleInitial { get; set; } = string.Empty;

    [Required]
    [StringLength(100)]
    public string LastName { get; set; } = string.Empty;

    [Required]
    [StringLength(20)]
    public string Rank { get; set; } = string.Empty;

    [Required]
    [StringLength(20)]
    public string ServiceNumber { get; set; } = string.Empty;

    [StringLength(100)]
    public string Unit { get; set; } = string.Empty;

    [Required]
    public ServiceComponent Component { get; set; }

    public DateTime? DateOfBirth { get; set; }
}
