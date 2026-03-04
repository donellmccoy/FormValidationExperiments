using ECTSystem.Shared.Enums;

namespace ECTSystem.Shared.Models;

public static class LineOfDutyCaseFactory
{
    public static LineOfDutyCase Create(int memberId)
    {
        var now = DateTime.UtcNow;

        return new LineOfDutyCase
        {
            CaseId = GenerateCaseId(now),
            MemberId = memberId,
            InitiationDate = now,
            IncidentDate = now,
            CreatedDate = now,
            ModifiedDate = now,
            TimelineSteps = []
        };
    }

    public static string GenerateCaseId(DateTime timestamp) => $"{timestamp:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..6].ToUpperInvariant()}";
}
