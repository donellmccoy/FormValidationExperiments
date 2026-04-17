using ECTSystem.Shared.Models;
using ECTSystem.Shared.ViewModels;

namespace ECTSystem.Shared.Mapping;

/// <summary>
/// Maps <see cref="CreateCaseDialogueCommentDto"/> to <see cref="CaseDialogueComment"/>.
/// </summary>
public static class CaseDialogueCommentDtoMapper
{
    public static CaseDialogueComment ToEntity(CreateCaseDialogueCommentDto dto)
    {
        return new CaseDialogueComment
        {
            LineOfDutyCaseId = dto.LineOfDutyCaseId,
            Text = dto.Text,
            ParentCommentId = dto.ParentCommentId,
        };
    }
}
