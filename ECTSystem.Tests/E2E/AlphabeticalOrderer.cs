using Xunit.Sdk;
using Xunit.v3;

namespace ECTSystem.Tests.E2E;

/// <summary>
/// Orders test cases alphabetically by method name so T01, T02, … T10 run in sequence.
/// </summary>
public class AlphabeticalOrderer : ITestCaseOrderer
{
    public IReadOnlyCollection<TTestCase> OrderTestCases<TTestCase>(IReadOnlyCollection<TTestCase> testCases)
        where TTestCase : notnull, ITestCase
    {
        return testCases
            .OrderBy(tc => tc.TestMethod?.MethodName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}
