using System.ComponentModel.DataAnnotations;

namespace ECTSystem.Shared.ViewModels;

public class CreateAuthorityDto
{
    [Required]
    [Range(1, int.MaxValue, ErrorMessage = "LineOfDutyCaseId is required.")]
    public int LineOfDutyCaseId { get; set; }

    [Required]
    [StringLength(100)]
    public string Role { get; set; } = string.Empty;

    [Required]
    [StringLength(200)]
    public string Name { get; set; } = string.Empty;

    [StringLength(50)]
    public string Rank { get; set; } = string.Empty;

    [StringLength(200)]
    public string Title { get; set; } = string.Empty;

    public DateTime? ActionDate { get; set; }

    [StringLength(500)]
    public string Recommendation { get; set; } = string.Empty;

    public List<string> Comments { get; set; } = new List<string>();
}
