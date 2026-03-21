using ECTSystem.Shared.Models;
using ECTSystem.Shared.ViewModels;

namespace ECTSystem.Shared.Mapping;

/// <summary>
/// Maps <see cref="CreateMemberDto"/> and <see cref="UpdateMemberDto"/> to <see cref="Member"/>.
/// </summary>
public static class MemberDtoMapper
{
    public static Member ToEntity(CreateMemberDto dto)
    {
        return new Member
        {
            FirstName = dto.FirstName,
            MiddleInitial = dto.MiddleInitial,
            LastName = dto.LastName,
            Rank = dto.Rank,
            ServiceNumber = dto.ServiceNumber,
            Unit = dto.Unit,
            Component = dto.Component,
            DateOfBirth = dto.DateOfBirth,
        };
    }

    public static void ApplyUpdate(UpdateMemberDto dto, Member entity)
    {
        entity.FirstName = dto.FirstName;
        entity.MiddleInitial = dto.MiddleInitial;
        entity.LastName = dto.LastName;
        entity.Rank = dto.Rank;
        entity.ServiceNumber = dto.ServiceNumber;
        entity.Unit = dto.Unit;
        entity.Component = dto.Component;
        entity.DateOfBirth = dto.DateOfBirth;
        entity.RowVersion = dto.RowVersion;
    }
}
