using System.Text.RegularExpressions;
using ECTSystem.Shared.Enums;
using ECTSystem.Shared.Models;
using PanoramicData.OData.Client;

#nullable enable

namespace ECTSystem.Web.Services;

/// <summary>
/// OData HTTP service for member search operations.
/// Implements <see cref="IMemberService"/> using the <c>Members</c> OData entity set.
/// Provides fuzzy search capabilities by expanding text queries to include:
/// <list type="bullet">
///   <item><description>Rank title/abbreviation to pay grade mapping (e.g., "SSgt" also matches "E-5").</description></item>
///   <item><description>Service component enum matching by raw name or display name (e.g., "Reserve" matches <c>AirForceReserve</c>).</description></item>
/// </list>
/// </summary>
public class MemberHttpService : ODataServiceBase, IMemberService
{
    /// <summary>
    /// Static lookup table mapping military rank titles and abbreviations to their corresponding
    /// pay grade strings (e.g., "Staff Sergeant" → "E-5", "Col" → "O-6").
    /// Multiple keys can map to the same pay grade to support both full titles and common abbreviations.
    /// Used during search to expand a rank-related query term into an additional <c>Rank eq '{payGrade}'</c>
    /// OData filter clause, since the database stores ranks as pay grade strings.
    /// </summary>
    private static readonly Dictionary<string, string> RankToPayGrade = new(StringComparer.OrdinalIgnoreCase)
    {
        // Enlisted
        ["Airman Basic"] = "E-1",
        ["AB"] = "E-1",
        ["Airman"] = "E-2",
        ["Amn"] = "E-2",
        ["Airman First Class"] = "E-3",
        ["A1C"] = "E-3",
        ["Senior Airman"] = "E-4",
        ["SrA"] = "E-4",
        ["Staff Sergeant"] = "E-5",
        ["SSgt"] = "E-5",
        ["Technical Sergeant"] = "E-6",
        ["TSgt"] = "E-6",
        ["Master Sergeant"] = "E-7",
        ["MSgt"] = "E-7",
        ["Senior Master Sergeant"] = "E-8",
        ["SMSgt"] = "E-8",
        ["Chief Master Sergeant"] = "E-9",
        ["CMSgt"] = "E-9",
        // Officer
        ["Second Lieutenant"] = "O-1",
        ["2d Lt"] = "O-1",
        ["SecondLt"] = "O-1",
        ["First Lieutenant"] = "O-2",
        ["1st Lt"] = "O-2",
        ["FirstLt"] = "O-2",
        ["Captain"] = "O-3",
        ["Capt"] = "O-3",
        ["Major"] = "O-4",
        ["Maj"] = "O-4",
        ["Lieutenant Colonel"] = "O-5",
        ["Lt Col"] = "O-5",
        ["LtCol"] = "O-5",
        ["Colonel"] = "O-6",
        ["Col"] = "O-6",
        ["Brigadier General"] = "O-7",
        ["Brig Gen"] = "O-7",
        ["BrigGen"] = "O-7",
        ["Major General"] = "O-8",
        ["Maj Gen"] = "O-8",
        ["MajGen"] = "O-8",
        ["Lieutenant General"] = "O-9",
        ["Lt Gen"] = "O-9",
        ["LtGen"] = "O-9",
        ["General"] = "O-10",
        ["Gen"] = "O-10",
    };

    /// <summary>
    /// Initializes a new instance of the <see cref="MemberHttpService"/> class.
    /// </summary>
    /// <param name="client">The typed OData client for query operations against the <c>Members</c> entity set.</param>
    /// <param name="httpClient">The raw HTTP client for any non-OData REST calls.</param>
    public MemberHttpService(ODataClient client, HttpClient httpClient)
        : base(client, httpClient) { }

    /// <inheritdoc />
    public async Task<List<Member>> SearchMembersAsync(string searchText, CancellationToken cancellationToken = default)
    {
        // OData string literals escape single quotes by doubling them ('').
        var literal = searchText.Replace("'", "''");
        var filter = $"contains(tolower(LastName),tolower('{literal}'))" +
                     $" or contains(tolower(FirstName),tolower('{literal}'))" +
                     $" or contains(tolower(Rank),tolower('{literal}'))" +
                     $" or contains(tolower(Unit),tolower('{literal}'))" +
                     $" or contains(tolower(ServiceNumber),tolower('{literal}'))";

        // Match rank titles/abbreviations to pay grade strings stored in DB
        var matchingPayGrades = RankToPayGrade
            .Where(r => r.Key.Contains(searchText, StringComparison.OrdinalIgnoreCase))
            .Select(r => r.Value)
            .Distinct()
            .ToList();

        if (matchingPayGrades.Count > 0)
        {
            filter += $" or {string.Join(" or ", matchingPayGrades.Select(pg => $"Rank eq '{pg}'"))}";
        }

        // Match Component enum values by raw name or display name (e.g. "Reserve" → AirForceReserve)
        var matchingComponents = Enum.GetValues<ServiceComponent>()
            .Where(c =>
            {
                var name = c.ToString();
                var display = Regex.Replace(name, @"(\B[A-Z])", " $1");
                return name.Contains(searchText, StringComparison.OrdinalIgnoreCase)
                       || display.Contains(searchText, StringComparison.OrdinalIgnoreCase);
            })
            .ToList();

        if (matchingComponents.Count > 0)
        {
            filter += $" or {string.Join(" or ", matchingComponents.Select(c => $"Component eq ECTSystem.Shared.Enums.ServiceComponent'{c}'"))}";
        }

        var query = Client.For<Member>("Members")
            .Filter(filter)
            .Top(25)
            .OrderBy("LastName,FirstName");

        var response = await Client.GetAsync(query, cancellationToken);

        return response.Value?.ToList().ToList().ToList() ?? [];
    }
}
