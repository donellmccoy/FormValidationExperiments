namespace ECTSystem.Shared.Models;

public static class LineOfDutyCaseFactory
{
    public static LineOfDutyCase Create(int memberId)
    {
        var now = DateTime.UtcNow;

        return new LineOfDutyCase
        {
            MemberId = memberId,
            CreatedDate = now,
            ModifiedDate = now
        };
    }
}
