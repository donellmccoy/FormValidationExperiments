using ECTSystem.Shared.Models;
using ECTSystem.Shared.ViewModels;

namespace ECTSystem.Shared.Mapping;

/// <summary>
/// Maps <see cref="CreateAuthorityDto"/> to <see cref="LineOfDutyAuthority"/>.
/// </summary>
public static class AuthorityDtoMapper
{
    public static LineOfDutyAuthority ToEntity(CreateAuthorityDto dto)
    {
        return new LineOfDutyAuthority
        {
            LineOfDutyCaseId = dto.LineOfDutyCaseId,
            Role = dto.Role,
            Name = dto.Name,
            Rank = dto.Rank,
            Title = dto.Title,
            ActionDate = dto.ActionDate,
            Recommendation = dto.Recommendation,
            Comments = dto.Comments,
        };
    }
}
