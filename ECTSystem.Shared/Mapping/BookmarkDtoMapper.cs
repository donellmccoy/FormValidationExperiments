using ECTSystem.Shared.Models;
using ECTSystem.Shared.ViewModels;

namespace ECTSystem.Shared.Mapping;

/// <summary>
/// Maps <see cref="CreateBookmarkDto"/> to <see cref="LineOfDutyBookmark"/>.
/// </summary>
public static class BookmarkDtoMapper
{
    public static LineOfDutyBookmark ToEntity(CreateBookmarkDto dto)
    {
        return new LineOfDutyBookmark
        {
            LineOfDutyCaseId = dto.LineOfDutyCaseId,
        };
    }
}
