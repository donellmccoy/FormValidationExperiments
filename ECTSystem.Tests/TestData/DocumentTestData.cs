using Xunit;

namespace ECTSystem.Tests.TestData;

/// <summary>
/// Provides file-size scenarios for document upload tests.
/// Columns: (int fileSizeBytes, bool shouldSucceed)
/// </summary>
public class DocumentUploadSizeTestData : TheoryData<int, bool>
{
    private const int OneMb = 1024 * 1024;

    public DocumentUploadSizeTestData()
    {
        Add(1_024,          true);  // 1 KB  – well within limit
        Add(5 * OneMb,      true);  // 5 MB  – within limit
        Add(10 * OneMb,     true);  // 10 MB – exactly at the limit (inclusive)
        Add(10 * OneMb + 1, false); // 10 MB + 1 byte – exceeds limit
        Add(20 * OneMb,     false); // 20 MB – clearly exceeds limit
    }
}

/// <summary>
/// Provides document-id + existence scenarios.
/// Columns: (int documentId, bool seedDocumentBeforeTest)
/// </summary>
public class DocumentKeyExistsTestData : TheoryData<int, bool>
{
    public DocumentKeyExistsTestData()
    {
        Add(1,   true);  // document exists
        Add(999, false); // document does not exist
    }
}
