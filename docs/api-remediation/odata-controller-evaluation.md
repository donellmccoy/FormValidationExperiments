# OData Controller Evaluation

> Generated: 2026-04-28  
> Scope: `ECTSystem.Api/Controllers/`  
> Reference: Microsoft OData documentation at <https://learn.microsoft.com/en-us/odata/>

## Summary

Overall, the API controllers are mostly aligned with Microsoft ASP.NET Core OData guidance. The strongest parts are the EDM registration, route component setup, bounded query options, convention-named entity controllers, `SingleResult` for key lookups, and scoped `DbContext` lifetime handling for deferred `IQueryable` serialization.

## Findings

### 1. Advertised EDM entity sets do not all have matching OData controllers

`ServiceCollectionExtensions.BuildEdmModel()` exposes the following entity sets in the EDM:

- `Cases`
- `Members`
- `Notifications`
- `Authorities`
- `Documents`
- `Appeals`
- `MEDCONDetails`
- `INCAPDetails`
- `Bookmarks`
- `WorkflowStateHistory`
- `WitnessStatements`
- `AuditComments`
- `CaseDialogueComments`

The attached controller folder contains controllers for only a subset of those OData entity sets:

- `CasesController`
- `MembersController`
- `AuthoritiesController`
- `DocumentsController`
- `BookmarksController`
- `WorkflowStateHistoryController`
- `CaseDialogueCommentsController`

Microsoft's OData guidance treats the EDM and service document as the set of addressable resources. Clients generated from `$metadata` can reasonably expect each advertised entity set to be routable.

**Recommendation:** Add read/write or read-only controllers for every exposed entity set, or remove entity sets that are not intended to be public OData resources.

## 2. `ByCurrentState` is modeled as an OData action even though it behaves like a composable query

`ByCurrentState` is registered as a collection-bound action and implemented as a POST endpoint that returns a queryable collection with `[EnableQuery]`.

Microsoft's OData guidance draws a clean line:

- Functions are side-effect-free GET operations and may support further composition.
- Actions are POST operations with possible side effects and are not meant to be further composed.

`ByCurrentState` appears to be read-only and query-like. Its body-based parameters make it convenient, but its behavior is closer to a function than an action.

**Recommendation:** If this is truly read-only, prefer a bound function or a normal OData collection query shape. If body-based filtering is required, keep it as an intentional pragmatic deviation and document it as non-standard OData behavior.

## 3. Some custom routes are REST escape hatches inside OData controllers

`DocumentsController` correctly marks `LineOfDutyDocument` as a media entity in the EDM and implements `$value` download. It also includes custom routes for multipart upload and Form 348 PDF generation.

These routes are architecturally reasonable, especially for multipart upload and generated PDFs, but they are not discoverable as OData operations through the EDM.

**Recommendation:** Keep these as REST companion endpoints, but consider moving them to a separate API controller or clearly documenting them as non-OData endpoints.

## 4. Unqualified operation calls are convenient but reduce portability

The API enables unqualified operation calls via `EnableUnqualifiedOperationCall`, and comments/routes use unqualified operation URLs such as:

- `POST /odata/Cases/ByCurrentState`
- `POST /odata/Bookmarks/AddBookmark`

Microsoft examples often show namespace-qualified operation calls unless unqualified calling is explicitly enabled.

**Recommendation:** This is acceptable for this app, but generated clients and external consumers should be guided by `$metadata`. Public documentation should include the qualified route form too.

## What Looks Good

The global OData setup follows Microsoft guidance:

- Registers the EDM with `AddRouteComponents("odata", edmModel, ...)`.
- Enables standard query options with `.Select()`, `.Filter()`, `.Expand()`, `.OrderBy()`, `.SetMaxTop(100)`, and `.Count()`.
- Uses `ODataConventionModelBuilder` for the EDM.
- Uses controller names that match entity sets for convention routing.
- Uses `EnableQuery` on GET/query endpoints.
- Bounds query risk with `MaxTop`, `PageSize`, `MaxExpansionDepth`, and `MaxNodeCount`.
- Uses `SingleResult.Create(...)` for key lookups while preserving OData `$select` and `$expand` composition.
- Keeps the EF Core context alive for deferred `IQueryable` serialization through `ODataControllerBase.CreateContextAsync()`.

CRUD routing is mostly conventional: `Get`, `Get(key)`, `Post`, `Patch`, `Put`, and `Delete` methods line up with OData entity routing expectations. The navigation property endpoints in `CasesController` and `MembersController` also follow convention naming.

## Source Areas Reviewed

- `ECTSystem.Api/Controllers/AuthoritiesController.cs`
- `ECTSystem.Api/Controllers/BookmarksController.cs`
- `ECTSystem.Api/Controllers/CaseDialogueCommentsController.cs`
- `ECTSystem.Api/Controllers/CasesController.cs`
- `ECTSystem.Api/Controllers/DocumentsController.cs`
- `ECTSystem.Api/Controllers/MembersController.cs`
- `ECTSystem.Api/Controllers/ODataControllerBase.cs`
- `ECTSystem.Api/Controllers/WorkflowStateHistoryController.cs`
- `ECTSystem.Api/Controllers/UserController.cs`
- `ECTSystem.Api/Extensions/ServiceCollectionExtensions.cs`

## Microsoft OData Guidance Referenced

- OData documentation root: <https://learn.microsoft.com/en-us/odata/>
- ASP.NET Core OData routing overview: <https://learn.microsoft.com/en-us/odata/webapi-8/fundamentals/routing-overview>
- ASP.NET Core OData query options: <https://learn.microsoft.com/en-us/odata/webapi-8/fundamentals/query-options>
- ASP.NET Core OData entity routing: <https://learn.microsoft.com/en-us/odata/webapi-8/fundamentals/entity-routing>
- ASP.NET Core OData actions and functions: <https://learn.microsoft.com/en-us/odata/webapi-8/fundamentals/actions-functions>

## Serialization Issues and Conflicts

### Summary

The shared model does not currently have `LineOfDutyCase -> child -> LineOfDutyCase` navigation cycles. Child entities mostly expose scalar foreign keys such as `LineOfDutyCaseId`, so classic EF navigation-loop serialization is not the main issue right now.

The bigger risks are response-shape mismatches, multiple JSON/EDM contract definitions, DateTime round-trip behavior, and inconsistent client expectations around OData mutation responses.

### 1. Document upload response shape conflicts with one client method

`DocumentsController.Upload` accepts `List<IFormFile>` and returns `Ok(documents)`, where `documents` is a collection of `LineOfDutyDocument` records.

One UI upload path already handles this flexibly by parsing collection, array, or single-object response shapes. However, `DocumentService.UploadDocumentAsync(...)` returns a single `LineOfDutyDocument` and deserializes the response as one object.

This can fail when the API returns an array response:

```text
The JSON value could not be converted to LineOfDutyDocument
```

**Recommendation:** Standardize the upload contract. Either make the service return `List<LineOfDutyDocument>`, or make the API return a single document for single-file upload and provide a separate multi-upload method.

### 2. Client JSON serialization options are inconsistent

The Blazor client has DI-registered JSON options using web defaults, string enums, and cycle ignoring. `ODataServiceBase` defines a separate static `JsonSerializerOptions` instance with:

```csharp
PropertyNameCaseInsensitive = true,
PropertyNamingPolicy = null,
Converters = { new JsonStringEnumConverter() }
```

That means some client calls use camelCase web defaults and `ReferenceHandler.IgnoreCycles`, while OData service calls use PascalCase output and no cycle handling.

This mostly works today because the server model binder is case-insensitive and the entity graph is mostly one-way, but it creates contract drift.

**Recommendation:** Use one shared `JsonSerializerOptions` definition for API service calls, injected where needed. Override per request only when an OData action payload requires exact PascalCase parameter names.

### 3. Mutation response body expectations are inconsistent

Some services assume `Updated(...)` responses include an entity body and immediately deserialize the response. Other services explicitly handle OData update responses that may return `204 No Content`.

This is a client-contract conflict. If the server returns `204`, or if a caller uses `Prefer: return=minimal`, service methods that always deserialize an entity can fail on an empty response body.

**Recommendation:** Standardize mutation response behavior. Either return explicit `Ok(entity)` when clients require the updated entity, or make all update clients tolerate `204 No Content` and reload the entity when needed.

### 4. DateTime serialization has known round-trip risk

The persistence layer includes UTC normalization because Microsoft.OData.Client can serialize `DateTime` values as `DateTimeOffset` strings with `+00:00`, and System.Text.Json/OData Delta can deserialize those strings with unexpected `DateTimeKind` behavior.

That mitigation is useful, but the domain includes both instant-like timestamps and date-like form fields. Date-only values can still shift if browser/user timezone behavior enters the round trip.

**Recommendation:** Keep `DateTime` for true instants such as audit timestamps, checkout timestamps, and workflow entry/exit times. For semantic date-only fields, consider `DateOnly` in DTO/view models or a clearly documented date-only string format.

### 5. Manual client EDM can drift from the server EDM

The Blazor client builds a hand-authored EDM model. This avoids a synchronous `$metadata` fetch and supports OData enum materialization, but it creates a second schema that must stay aligned with the server EDM.

Missing structural properties, enum definitions, concurrency tokens, or navigation definitions can cause runtime OData client materialization failures as usage grows.

**Recommendation:** Add focused tests that compare important server/client EDM facts, or fetch/cache `$metadata` instead of maintaining a manual duplicate model.

### 6. ProblemDetails parsing does not cover OData error envelopes

`ODataServiceBase` parses RFC 7807 `ProblemDetails` responses. OData query validation and formatter errors often use an OData error envelope instead:

```json
{
	"error": {
		"code": "",
		"message": "The query specified in the URI is not valid..."
	}
}
```

When this happens, the client can lose the useful OData parser or formatter message and surface a generic HTTP error.

**Recommendation:** Add an OData error parser fallback for `{ "error": { "message": ... } }` responses.

### Current Healthy Areas

- Child entities do not currently navigate back to `LineOfDutyCase`, so root-to-child serialization cycles are avoided by model shape.
- API JSON options include `ReferenceHandler.IgnoreCycles`, which is a useful defensive safety net if a navigation property is added later.
- API and client service JSON options both include `JsonStringEnumConverter`, and the client EDM explicitly defines OData enum types used by entity properties.
- `ODataControllerBase.CreateContextAsync()` keeps EF Core contexts alive through deferred `IQueryable` serialization.

### Recommended Serialization Test Coverage

- Document upload response shape for one file and multiple files.
- Update endpoints that return an entity body and update endpoints that return `204 No Content`.
- DateTime round trips for both instant timestamps and date-like form fields.
- Enum round trips through both System.Text.Json and Microsoft.OData.Client paths.
- OData validation errors using the `{ "error": { "message": ... } }` envelope.
- Server/client EDM alignment for entity sets, enum types, concurrency tokens, and key structural properties.
