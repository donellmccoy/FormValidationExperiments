using System.Text.RegularExpressions;
using ECTSystem.Shared.Enums;
using ECTSystem.Shared.Models;
using Microsoft.Extensions.Logging;

#nullable enable

namespace ECTSystem.Web.Services;

/// <summary>
/// Client-side OData service for searching <see cref="Member"/> records.
/// </summary>
/// <remarks>
/// <para>
/// Search is implemented client-side: this service builds an OData <c>$filter</c> expression
/// composed of <c>contains()</c> predicates over <c>FirstName</c>, <c>LastName</c>, <c>Rank</c>,
/// <c>Unit</c>, and <c>ServiceNumber</c>, plus equality predicates synthesized from the
/// <see cref="RankToPayGrade"/> dictionary and from <see cref="ServiceComponent"/> enum
/// matching. The server has no dedicated search endpoint today.
/// </para>
/// <para>
/// User-supplied input is escaped for OData string literals by doubling single quotes
/// (<c>'</c> → <c>''</c>) before composition. This is the only sanitization layer; if a
/// server-side search action is added in the future, that input should still apply the same
/// escape (or use parameter binding) as defense in depth.
/// </para>
/// <para>
/// Result size is bounded by <c>$top=25</c> and ordered by <c>LastName,FirstName</c> so the
/// caller never receives an unbounded list. The search is intentionally limited to the
/// columns above; adding new searchable fields requires updating both the predicate list and
/// any server-side index strategy.
/// </para>
/// <para>
/// The <see cref="ServiceComponent"/> matcher uses a regex to derive a display name
/// (<c>"AirForceReserve"</c> → <c>"Air Force Reserve"</c>) so a user typing "Reserve" still
/// hits the right enum value. This is locale-insensitive but fragile against enum renames —
/// keep enum names PascalCase and do not introduce non-ASCII characters in member names.
/// </para>
/// </remarks>
public class MemberService : ODataServiceBase, IMemberService
{
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

    public MemberService(
        EctODataContext context,
        HttpClient httpClient,
        ILogger<MemberService> logger,
        [Microsoft.Extensions.DependencyInjection.FromKeyedServices(ECTSystem.Web.Extensions.ServiceCollectionExtensions.ODataJsonOptionsKey)] System.Text.Json.JsonSerializerOptions jsonOptions)
        : base(context, httpClient, logger, jsonOptions) { }

    /// <summary>
    /// Searches members across name, rank, unit, service number, pay grade (via rank lookup),
    /// and service component, returning at most 25 matches ordered by last name then first name.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The composed filter string is sent verbatim to OData; <paramref name="searchText"/> is
    /// escaped by doubling single quotes before interpolation. Pay-grade matches are derived
    /// from <see cref="RankToPayGrade"/> by substring (ordinal-ignore-case), and component
    /// matches expand to typed enum literals
    /// (<c>Component eq ECTSystem.Shared.Enums.ServiceComponent'AirForceReserve'</c>).
    /// </para>
    /// <para>
    /// All <c>contains()</c> calls are wrapped in <c>tolower()</c> on both sides for
    /// case-insensitive matching against arbitrary collation. Pay-grade and component
    /// predicates use exact equality (no <c>tolower</c>) because the values are normalized.
    /// </para>
    /// </remarks>
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

        var query = Context.Members
            .AddQueryOption("$filter", filter)
            .AddQueryOption("$top", 25)
            .AddQueryOption("$orderby", "LastName,FirstName");

        return await ExecuteQueryAsync(query, cancellationToken);
    }
}
