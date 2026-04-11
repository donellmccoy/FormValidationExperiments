using Xunit.Abstractions;
using Xunit.Sdk;

namespace ECTSystem.Tests.E2E;

/// <summary>
/// Orders test cases alphabetically by method name so T01, T02, … T10 run in sequence.
/// </summary>
public class AlphabeticalOrderer : ITestCaseOrderer
{
    public IEnumerable<TTestCase> OrderTestCases<TTestCase>(IEnumerable<TTestCase> testCases)
        where TTestCase : ITestCase
    {
        return testCases.OrderBy(tc => tc.TestMethod.Method.Name, StringComparer.OrdinalIgnoreCase);
    }
}
