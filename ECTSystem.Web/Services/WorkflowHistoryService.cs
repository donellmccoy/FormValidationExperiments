using ECTSystem.Shared.Models;
using Microsoft.OData.Client;
using Radzen;

#nullable enable

namespace ECTSystem.Web.Services;

public class WorkflowHistoryService : ODataServiceBase, IWorkflowHistoryService
{
    public WorkflowHistoryService(EctODataContext context, HttpClient httpClient)
        : base(context, httpClient) { }

    public async Task<ODataServiceResult<WorkflowStateHistory>> GetWorkflowStateHistoriesAsync(
        int caseId, string? filter = null, int? top = null, int? skip = null,
        string? orderby = null, bool? count = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(caseId);

        var caseFilter = $"LineOfDutyCaseId eq {caseId}";
        var combinedFilter = string.IsNullOrEmpty(filter) ? caseFilter : $"({caseFilter}) and ({filter})";

        var query = Context.WorkflowStateHistories
            .AddQueryOption("$filter", combinedFilter);

        if (top.HasValue)
            query = query.AddQueryOption("$top", top.Value);

        if (skip.HasValue)
            query = query.AddQueryOption("$skip", skip.Value);

        if (!string.IsNullOrEmpty(orderby))
            query = query.AddQueryOption("$orderby", orderby);

        if (count == true)
        {
            var (items, totalCount) = await ExecutePagedQueryAsync(query, cancellationToken);

            return new ODataServiceResult<WorkflowStateHistory>
            {
                Value = items,
                Count = totalCount
            };
        }

        var results = await ExecuteQueryAsync(query, cancellationToken);

        return new ODataServiceResult<WorkflowStateHistory>
        {
            Value = results,
            Count = results.Count
        };
    }

    public async Task<WorkflowStateHistory> AddHistoryEntryAsync(WorkflowStateHistory entry, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entry);

        Context.AddObject("WorkflowStateHistories", entry);
        await Context.SaveChangesAsync(cancellationToken);
        Context.Detach(entry);

        return entry;
    }

    public async Task<List<WorkflowStateHistory>> AddHistoryEntriesAsync(List<WorkflowStateHistory> entries, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entries);

        foreach (var entry in entries)
        {
            Context.AddObject("WorkflowStateHistories", entry);
        }

        var response = await Context.SaveChangesAsync(
            SaveChangesOptions.BatchWithSingleChangeset | SaveChangesOptions.UseJsonBatch,
            cancellationToken);

        var savedEntries = response
            .OfType<ChangeOperationResponse>()
            .Select(r => (r.Descriptor as EntityDescriptor)?.Entity as WorkflowStateHistory)
            .Where(e => e is not null)
            .Cast<WorkflowStateHistory>()
            .ToList();

        foreach (var entry in savedEntries)
        {
            Context.Detach(entry);
        }

        return savedEntries;
    }

    public async Task<WorkflowStateHistory> UpdateHistoryEndDateAsync(int entryId, DateTime endDate, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(entryId);

        var query = Context.WorkflowStateHistories
            .AddQueryOption("$filter", $"Id eq {entryId}")
            .AddQueryOption("$top", 1);

        var results = await ExecuteQueryAsync(query, cancellationToken);
        var entry = results.FirstOrDefault()
            ?? throw new InvalidOperationException($"Workflow state history entry {entryId} not found.");

        entry.ExitDate = endDate;

        if (Context.GetEntityDescriptor(entry) == null)
        {
            Context.AttachTo("WorkflowStateHistories", entry);
        }

        Context.UpdateObject(entry);
        await Context.SaveChangesAsync(cancellationToken);
        Context.Detach(entry);

        return entry;
    }
}
