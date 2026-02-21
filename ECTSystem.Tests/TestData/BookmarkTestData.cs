using Xunit;

namespace ECTSystem.Tests.TestData;

/// <summary>
/// Provides user-id + case-id pairs for bookmark operation tests.
/// Columns: (string userId, int caseId)
/// </summary>
public class BookmarkUserCaseTestData : TheoryData<string, int>
{
    public BookmarkUserCaseTestData()
    {
        Add("user-alice",   1);
        Add("user-bob",     2);
        Add("user-charlie", 1); // different user, same case
    }
}

/// <summary>
/// Provides bookmarked-state scenarios for IsBookmarkedAsync.
/// Columns: (bool bookmarkExists, bool expectedResult)
/// </summary>
public class BookmarkExistsTestData : TheoryData<bool, bool>
{
    public BookmarkExistsTestData()
    {
        Add(true,  true);  // bookmark seeded  → should return true
        Add(false, false); // no bookmark      → should return false
    }
}
