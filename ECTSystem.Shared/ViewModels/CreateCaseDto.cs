using System.ComponentModel.DataAnnotations;
using ECTSystem.Shared.Enums;

namespace ECTSystem.Shared.ViewModels;

public class CreateCaseDto
{
    [Required]
    [Range(1, int.MaxValue, ErrorMessage = "MemberId is required.")]
    public int MemberId { get; set; }

    [Required]
    public ProcessType ProcessType { get; set; }

    [Required]
    public ServiceComponent Component { get; set; }

    [Required]
    [StringLength(200)]
    public string MemberName { get; set; } = string.Empty;

    [Required]
    [StringLength(50)]
    public string MemberRank { get; set; } = string.Empty;

    [Required]
    [StringLength(20)]
    public string ServiceNumber { get; set; } = string.Empty;

    public DateTime? MemberDateOfBirth { get; set; }

    [StringLength(100)]
    public string Unit { get; set; } = string.Empty;

    [StringLength(200)]
    public string FromLine { get; set; } = string.Empty;

    public IncidentType IncidentType { get; set; }

    public DateTime IncidentDate { get; set; }

    [StringLength(4000)]
    public string IncidentDescription { get; set; } = string.Empty;

    public DutyStatus IncidentDutyStatus { get; set; }
}
