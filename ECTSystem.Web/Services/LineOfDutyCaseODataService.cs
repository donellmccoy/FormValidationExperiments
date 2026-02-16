using ECTSystem.Shared.Models;
using Microsoft.OData.Client;
using Radzen;

#nullable enable

namespace ECTSystem.Web.Services;

/// <summary>
/// OData Client-based service for all LOD case operations.
/// Uses <see cref="EctODataContext"/> (Microsoft.OData.Client) instead of
/// raw HttpClient — no manual URL construction, response DTOs, or
/// custom serialization options. Navigation/FK exclusion is handled
/// automatically by the OData Client's model-aware serializer.
/// </summary>
public class LineOfDutyCaseODataService : IDataService
{
    private readonly EctODataContext _ctx;

    public LineOfDutyCaseODataService(EctODataContext ctx)
    {
        _ctx = ctx;
    }

    public async Task<ODataServiceResult<LineOfDutyCase>> GetCasesAsync(
        string? filter = null,
        int? top = null,
        int? skip = null,
        string? orderby = null,
        bool? count = null,
        CancellationToken cancellationToken = default)
    {
        DataServiceQuery<LineOfDutyCase> query = _ctx.Cases;

        if (!string.IsNullOrEmpty(filter))
            query = query.AddQueryOption("$filter", filter);
        if (top.HasValue)
            query = query.AddQueryOption("$top", top.Value);
        if (skip.HasValue)
            query = query.AddQueryOption("$skip", skip.Value);
        if (!string.IsNullOrEmpty(orderby))
            query = query.AddQueryOption("$orderby", orderby);
        if (count == true)
            query = query.IncludeCount();

        var response = (QueryOperationResponse<LineOfDutyCase>)await query.ExecuteAsync();

        return new ODataServiceResult<LineOfDutyCase>
        {
            Value = response.ToList(),
            Count = count == true ? (int)response.Count : 0
        };
    }

    public async Task<LineOfDutyCase?> GetCaseAsync(
        string caseId,
        CancellationToken cancellationToken = default)
    {
        var query = _ctx.Cases
            .AddQueryOption("$filter", $"CaseId eq '{caseId}'")
            .AddQueryOption("$top", 1)
            .AddQueryOption("$expand",
                "Documents,Authorities," +
                "TimelineSteps($expand=ResponsibleAuthority)," +
                "Appeals($expand=AppellateAuthority)," +
                "Member,MEDCON,INCAP,Notifications");

        var response = await query.ExecuteAsync();
        return response.FirstOrDefault();
    }

    public async Task<LineOfDutyCase> SaveCaseAsync(
        LineOfDutyCase lodCase,
        CancellationToken cancellationToken = default)
    {
        if (lodCase.Id == 0)
        {
            // New entity — POST to entity set
            _ctx.AddObject("Cases", lodCase);
        }
        else
        {
            // Existing entity — PATCH
            if (_ctx.GetEntityDescriptor(lodCase) == null)
                _ctx.AttachTo("Cases", lodCase);

            _ctx.UpdateObject(lodCase);
        }

        try
        {
            await _ctx.SaveChangesAsync(SaveChangesOptions.None, cancellationToken);
            return lodCase;
        }
        finally
        {
            _ctx.Detach(lodCase);
        }
    }
}
