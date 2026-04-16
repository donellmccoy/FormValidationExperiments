# Enable OData $batch and Remove PostBatch

## Background

The client already uses OData's native batch protocol — `SaveChangesOptions.BatchWithSingleChangeset | SaveChangesOptions.UseJsonBatch` in 3 places:
- `WorkflowHistoryService.AddHistoryEntriesAsync()`
- `CaseService.TransitionCaseAsync()`
- `AuthorityService` save

These send `POST /odata/$batch` automatically. The custom `PostBatch` action is **unused** — no code calls `/odata/WorkflowStateHistory/Batch` directly.

## Steps

| # | Action | File | Details |
|---|--------|------|---------|
| 1 | **Register `DefaultODataBatchHandler`** | `ServiceCollectionExtensions.cs` | In `AddODataControllers`, change `options.AddRouteComponents("odata", edmModel)` to pass a batch handler via the services overload. |
| 2 | **Remove `PostBatch` action** | `WorkflowStateHistoryController.cs` | Delete the entire `PostBatch` method. The existing `Post()` action handles each individual request within the batch. |
| 3 | **Remove unused logging methods** | `LoggingService.cs`, `ILoggingService.cs` | Remove `CreatingWorkflowStateHistoryBatch`, `WorkflowStateHistoryBatchCreated`, `WorkflowStateHistoryBatchEmpty` (orphaned after step 2). |
| 4 | **Rename test file** | `WorkflowStateHistoriesControllerTests.cs` | Rename to `WorkflowStateHistoryControllerTests.cs` to match the earlier controller rename. No `PostBatch` tests exist, so no test removal needed. |
| 5 | **Build & verify** | — | `dotnet build` the solution. |
| 6 | **Runtime smoke test** | — | Launch API + Web, trigger a state transition (which calls `TransitionCaseAsync` → `$batch`). Verify the batch request succeeds with 200. |

## What Changes at Runtime

**Before:** Client sends `POST /odata/$batch` → returns 404 (no handler registered).

**After:** Client sends `POST /odata/$batch` → `DefaultODataBatchHandler` unpacks the changeset → dispatches each `POST /odata/WorkflowStateHistory` to the existing `Post()` action → bundles responses back into a single batch response.

## No Client Changes Needed

The client code in `WorkflowHistoryService`, `CaseService`, and `AuthorityService` already uses the correct OData batch pattern. Zero changes required.
