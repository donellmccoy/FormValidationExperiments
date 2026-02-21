using Xunit;

namespace ECTSystem.Tests.TestData;

/// <summary>
/// Provides key + existence flag scenarios for case look-up and delete operations.
/// Columns: (int key, bool seedCaseBeforeTest)
/// </summary>
public class CaseKeyExistsTestData : TheoryData<int, bool>
{
    public CaseKeyExistsTestData()
    {
        Add(1, true);    // existing key → operation should succeed
        Add(999, false); // non-existing key → operation should return null / false
    }
}

/// <summary>
/// Provides multiple authority list sizes for authority-sync scenarios in UpdateCaseAsync.
/// Columns: (int authorityCount)
/// </summary>
public class AuthorityCountTestData : TheoryData<int>
{
    public AuthorityCountTestData()
    {
        Add(0);  // remove all authorities
        Add(1);  // single authority
        Add(3);  // multiple authorities
    }
}
