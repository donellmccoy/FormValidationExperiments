using System.ComponentModel.DataAnnotations;

namespace ECTSystem.Shared.ViewModels;

public class CreateCaseDialogueCommentDto
{
    [Required]
    [Range(1, int.MaxValue, ErrorMessage = "LineOfDutyCaseId is required.")]
    public int LineOfDutyCaseId { get; set; }

    [Required]
    [StringLength(4000)]
    public string Text { get; set; } = string.Empty;

    public int? ParentCommentId { get; set; }
}
