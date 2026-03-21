using ECTSystem.Shared.Models;
using ECTSystem.Shared.ViewModels;

namespace ECTSystem.Shared.Mapping;

/// <summary>
/// Maps <see cref="CreateBookmarkDto"/> to <see cref="CaseBookmark"/>.
/// </summary>
public static class BookmarkDtoMapper
{
    public static CaseBookmark ToEntity(CreateBookmarkDto dto)
    {
        return new CaseBookmark
        {
            LineOfDutyCaseId = dto.LineOfDutyCaseId,
        };
    }
}
