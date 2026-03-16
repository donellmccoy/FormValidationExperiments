# Migration Complete: PanoramicData.OData.Client → Microsoft.OData.Client

## Summary

Replaced `PanoramicData.OData.Client` (v10.0.55) with `Microsoft.OData.Client` (v8.2.2) on the Blazor WASM client. The API project (`Microsoft.AspNetCore.OData` v9.4.1) was unchanged.

**Status:** All phases complete. Build passes with 0 errors.

**Benefits achieved:**
- Native `$batch` support (single HTTP round-trip for multi-entity operations)
- LINQ-to-OData queries via `DataServiceQuery<T>` with `AddQueryOption`
- `DataServiceContext` change tracking (eliminated manual scalar-only PATCH reflection hack)
- Eliminated custom `ODataResponse<T>` / `ODataCountResponse<T>` DTOs
- Proper `IHttpClientFactory` integration via `SingleHttpClientFactory` adapter

**Scope:** Client-side only. All changes in `ECTSystem.Web`.

---

## Completed Changes

| File | Change |
|------|--------|
| `ECTSystem.Web.csproj` | Package swap: PanoramicData → Microsoft.OData.Client 8.2.2 |
| `Services/EctODataContext.cs` | Manual `DataServiceContext` subclass with entity sets and resolvers |
| `Services/ODataServiceBase.cs` | Rewritten: `IncludeCount()` + `Count` property (v8.2.2 API) |
| `Services/CaseHttpService.cs` | Full rewrite: LINQ queries, native `$batch`, action/function invocation |
| `Services/AuthorityHttpService.cs` | Rewritten: batch upsert-prune via `$batch` (N+1 → 2 HTTP calls) |
| `Services/MemberHttpService.cs` | Rewritten: `DataServiceQuery` with `AddQueryOption` |
| `Services/DocumentHttpService.cs` | Rewritten: OData queries; kept `HttpClient` for file upload/download |
| `Services/WorkflowHistoryHttpService.cs` | Rewritten: queries + `BatchWithSingleChangeset` for bulk insert |
| `Services/BookmarkHttpService.cs` | Rewritten: two-phase bookmark query |
| `Extensions/ServiceCollectionExtensions.cs` | `AddDomainServices()` + `AddODataContext()` with `SingleHttpClientFactory` |

## Key Architecture Decisions

1. **Manual `DataServiceContext` (not proxy-generated)** — avoids duplicate model types since `ECTSystem.Shared.Models` is already shared between client and API.

2. **`AddQueryOption` (not LINQ)** — service interfaces accept raw OData filter/orderby strings from Radzen `DataGrid.LoadData` events. `AddQueryOption` passes them through directly.

3. **`SingleHttpClientFactory` adapter** — wraps the DI-managed `HttpClient` (with auth handler + resilience) for the v8.2.2 `DataServiceClientRequestMessageArgs` constructor which accepts `IHttpClientFactory`.

4. **PATCH is default** — `UpdateObject` sends HTTP PATCH by default in Microsoft.OData.Client v8.x. No `SaveChangesOptions.PatchOnUpdate` needed (doesn't exist in v8.2.2).

5. **`HttpClient` kept for non-OData operations** — file upload/download, parameterless OData actions (Checkout/Checkin), Identity endpoints.

## Known Warnings

| Warning | Source | Impact |
|---------|--------|--------|
| NU1608 | ECTSystem.Tests.csproj | Microsoft.OData.Client 8.2.2 requires OData.Core 8.2.2 but 8.4.0 resolved. Monitor for runtime issues. |

## Future Optimization (Optional)

- **LINQ-to-OData queries**: Convert `AddQueryOption("$filter", rawString)` to strongly-typed LINQ (e.g., `.Where(c => c.MemberName.Contains("Smith"))`) for compile-time safety. Requires changing service interfaces and all RadzenDataGrid callers.
- **Server-side `Cases/Bookmarked()` function**: Replace the 2-phase bookmark query (get bookmark IDs → get cases by IDs) with a single server-side OData function.
