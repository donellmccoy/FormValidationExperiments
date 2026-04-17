using ECTSystem.Shared.Models;
using ECTSystem.Shared.ViewModels;

namespace ECTSystem.Shared.Mapping;

/// <summary>
/// Maps <see cref="UpdateDocumentDto"/> to <see cref="LineOfDutyDocument"/>.
/// </summary>
public static class DocumentDtoMapper
{
    public static void ApplyUpdate(UpdateDocumentDto dto, LineOfDutyDocument entity)
    {
        entity.DocumentType = dto.DocumentType;
        entity.Description = dto.Description;
    }
}
