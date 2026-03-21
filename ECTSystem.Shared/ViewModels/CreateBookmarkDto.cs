using System.ComponentModel.DataAnnotations;

namespace ECTSystem.Shared.ViewModels;

public class CreateBookmarkDto
{
    [Required]
    [Range(1, int.MaxValue, ErrorMessage = "LineOfDutyCaseId is required.")]
    public int LineOfDutyCaseId { get; set; }
}
