# Controller Endpoint Inventory

## Global Serialization Config

Configured in `ServiceCollectionExtensions.cs`:

- `JsonStringEnumConverter` — enums serialize/deserialize as strings via STJ
- `ReferenceHandler.IgnoreCycles` — circular references write `null` instead of looping
- `MaxExpansionDepth = 3` on most `[EnableQuery]` attributes — except `CaseDialogueCommentsController.Get()` which uses `MaxExpansionDepth = 2`, and two `Post` methods (`BookmarksController`, `CaseDialogueCommentsController`) that use bare `[EnableQuery]` (OData default depth = 2)
- No child entity has a reverse navigation property back to `LineOfDutyCase` — the model is strictly one-directional (parent → children), so circular `$expand` is not possible

## 1. AuthoritiesController — All OData — ✅ No Issues

| Method | Route | OData? | Serialization | Client |
|--------|-------|--------|:---:|:---:|
| `Get()` | `GET /odata/Authorities` | ✅ `[EnableQuery]` | ✅ | ✅ |
| `Get(key)` | `GET /odata/Authorities({key})` | ✅ `[EnableQuery]`, `SingleResult` | ✅ | ❌ |
| `Post` | `POST /odata/Authorities` | ✅ `[EnableQuery]`, `Created()` | ✅ | ✅ |
| `Patch` | `PATCH /odata/Authorities({key})` | ✅ `Delta<T>`, `Updated()` | ✅ | ✅ |
| `Delete` | `DELETE /odata/Authorities({key})` | ✅ `FromODataUri` | ✅ | ✅ |

## 2. BookmarksController — All OData — ✅ No Issues

| Method | Route | OData? | Serialization | Client |
|--------|-------|--------|:---:|:---:|
| `Get()` | `GET /odata/Bookmarks` | ✅ `[EnableQuery]` | ✅ | ✅ |
| `Post` | `POST /odata/Bookmarks` | ✅ `[EnableQuery]`, `Created()` / `Ok()` | ✅ | ✅ |

> `Post` implements idempotent upsert — returns `Created(bookmark)` for new bookmarks and `Ok(existing)` when the bookmark already exists for the current user.
| `Delete` | `DELETE /odata/Bookmarks({key})` | ✅ `FromODataUri` | ✅ | ✅ |

## 3. CasesController — All OData — ✅ Fixed (was 🟡 2 Issues)

| Method | Route | OData? | Serialization | Client |
|--------|-------|--------|:---:|:---:|
| `Get()` | `GET /odata/Cases` | ✅ `[EnableQuery]` | ✅ | ✅ |
| `Get(key)` | `GET /odata/Cases({key})` | ✅ `[EnableQuery]`, `SingleResult` | ✅ | ❌ |
| `Post` | `POST /odata/Cases` | ✅ `[EnableQuery]`, `Created()` | ✅ | ✅ |
| `Patch` | `PATCH /odata/Cases({key})` | ✅ `Delta<T>`, `Updated()` | ✅ | ✅ |
| `Delete` | `DELETE /odata/Cases({key})` | ✅ `FromODataUri`, soft-delete | ✅ | ❌ |
| `Checkout` | `POST /odata/Cases({key})/Checkout` | ✅ OData bound action (`ODataActionParameters`) | ✅ | ✅ |
| `Checkin` | `POST /odata/Cases({key})/Checkin` | ✅ OData bound action (`ODataActionParameters`) | ✅ | ✅ |
| `Bookmarked` | `GET /odata/Cases/Default.Bookmarked()` | ✅ OData collection-bound function | ✅ | ✅ |
| `ByCurrentState` | `POST /odata/Cases/ByCurrentState` | ✅ OData collection-bound action | ✅ | ✅ |
| `GetDocuments` | `GET /odata/Cases({key})/Documents` | ✅ OData navigation property | ✅ | ❌ |
| `GetNotifications` | `GET /odata/Cases({key})/Notifications` | ✅ OData navigation property | ✅ | ❌ |
| `GetWorkflowStateHistories` | `GET /odata/Cases({key})/WorkflowStateHistories` | ✅ OData navigation property | ✅ | ❌ |
| `GetMember` | `GET /odata/Cases({key})/Member` | ✅ OData navigation property | ✅ | ❌ |
| `GetMEDCON` | `GET /odata/Cases({key})/MEDCON` | ✅ OData navigation property | ✅ | ❌ |
| `GetINCAP` | `GET /odata/Cases({key})/INCAP` | ✅ OData navigation property | ✅ | ❌ |

> ✅ **Checkout / Checkin** — previously returned `Ok(existing)` from `FindAsync`, serializing a full entity the client never reads. Fixed — both now return `NoContent()` (HTTP 204). The client (`CaseService.cs`) only checks `IsSuccessStatusCode` and returns `bool`.

## 4. CaseDialogueCommentsController — All OData — ✅ Fixed (was 🔴 1 Issue)

| Method | Route | OData? | Serialization | Client |
|--------|-------|--------|:---:|:---:|
| `Get()` | `GET /odata/CaseDialogueComments` | ✅ `[EnableQuery]` | ✅ | ✅ |
| `Post` | `POST /odata/CaseDialogueComments` | ✅ `[EnableQuery]`, `Created()` | ✅ | ✅ |
| `Patch` | `PATCH /odata/CaseDialogueComments({key})` | ✅ `Delta<T>`, `Updated()` | ✅ | ✅ |
| `Delete` | `DELETE /odata/CaseDialogueComments({key})` | ✅ `FromODataUri` | ✅ | ❌ |

> ✅ **Patch** — previously had `[FromBody]` on `Delta<CaseDialogueComment>`, which forced STJ deserialization. STJ cannot construct `Delta<T>`, so the endpoint was broken at runtime. Fixed — `[FromBody]` removed so the OData input formatter handles it.

## 5. DocumentsController — Mixed — 🟡 1 Issue (1 Fixed)

| Method | Route | OData? | Serialization | Client |
|--------|-------|--------|:---:|:---:|
| `Get()` | `GET /odata/Documents` | ✅ `[EnableQuery]` | ✅ | ✅ |
| `Get(key)` | `GET /odata/Documents({key})` | ✅ `[EnableQuery]`, `SingleResult` | ✅ | ❌ |
| `Patch` | `PATCH /odata/Documents({key})` | ✅ `Delta<T>`, `Updated()` | ✅ | ❌ |
| `Put` | `PUT /odata/Documents({key})` | ✅ `[EnableQuery]`, `Updated()` | ✅ | ❌ |
| `Delete` | `DELETE /odata/Documents({key})` | ✅ `FromODataUri` | ✅ | ✅ |
| `GetValue` | `GET /odata/Documents({key})/$value` | ❌ `[HttpGet]` explicit route, returns `File()` stream | ✅ | ✅ |
| `Upload` | `POST /odata/Cases({caseId})/Documents` | ❌ `[HttpPost]` explicit route, `IFormFile` multipart upload | 🟡 | ✅ |
| `GetForm348` | `GET /odata/Cases({caseId})/Form348` | ❌ `[HttpGet]` explicit route, returns PDF binary | ✅ | ✅ |

> ✅ **Put** — previously used `SetValues(document)` which copied all scalar properties from the client payload, including audit fields. Fixed — audit fields (`CreatedBy`, `CreatedDate`, `ModifiedBy`, `ModifiedDate`) are now marked `IsModified = false` after `SetValues()` to prevent over-posting.
>
> 🟡 **Upload** — returns `Ok(documents)` where `documents` is a raw `List<LineOfDutyDocument>`. Response is a plain JSON array instead of an OData-formatted envelope with `@odata.context` / `value` wrapper.

## 6. MembersController — All OData — ✅ Fixed (was 🟡 1 Issue)

| Method | Route | OData? | Serialization | Client |
|--------|-------|--------|:---:|:---:|
| `Get()` | `GET /odata/Members` | ✅ `[EnableQuery]` | ✅ | ✅ |
| `Get(key)` | `GET /odata/Members({key})` | ✅ `[EnableQuery]`, `SingleResult` | ✅ | ❌ |
| `Post` | `POST /odata/Members` | ✅ `[EnableQuery]`, `Created()` | ✅ | ❌ |
| `Put` | `PUT /odata/Members({key})` | ✅ `[EnableQuery]`, `Updated()` | ✅ | ❌ |
| `Patch` | `PATCH /odata/Members({key})` | ✅ `Delta<T>`, `Updated()` | ✅ | ❌ |
| `Delete` | `DELETE /odata/Members({key})` | ✅ `FromODataUri` | ✅ | ❌ |
| `GetLineOfDutyCases` | `GET /odata/Members({key})/LineOfDutyCases` | ✅ OData navigation property | ✅ | ❌ |

> ✅ **Put** — previously used `SetValues(member)` which copied all scalar properties from the client payload, including audit fields. Fixed — audit fields (`CreatedBy`, `CreatedDate`, `ModifiedBy`, `ModifiedDate`) are now marked `IsModified = false` after `SetValues()` to prevent over-posting.

## 7. WorkflowStateHistoryController — All OData — ✅ No Issues

| Method | Route | OData? | Serialization | Client |
|--------|-------|--------|:---:|:---:|
| `Get()` | `GET /odata/WorkflowStateHistory` | ✅ `[EnableQuery]` | ✅ | ✅ |
| `Get(key)` | `GET /odata/WorkflowStateHistory({key})` | ✅ `[EnableQuery]`, `SingleResult` | ✅ | ❌ |
| `Post` | `POST /odata/WorkflowStateHistory` | ✅ `[EnableQuery]`, `Created()` | ✅ | ✅ |
| `Patch` | `PATCH /odata/WorkflowStateHistory({key})` | ✅ `Delta<T>`, `Updated()` | ✅ | ✅ |

> `Post` uses `[FromBody]` intentionally — STJ with `JsonStringEnumConverter` handles the `WorkflowState` enum correctly. `Patch` manually extracts only `ExitDate` from the delta, ignoring all other properties.

## 8. UserController — No OData — ✅ No Issues

| Method | Route | OData? | Serialization | Client |
|--------|-------|--------|:---:|:---:|
| `GetCurrentUser` | `GET /api/User/me` | ❌ `[ApiController]`, `[Route("api/[controller]")]` | ✅ | ✅ |
| `LookupUsers` | `GET /api/User/lookup` | ❌ `[ApiController]`, `[FromQuery]` | ✅ | ✅ |

## Summary

| Controller | Total | OData | Non-OData | Client Used | Client Unused | Issues |
|------------|:-----:|:-----:|:---------:|:-----------:|:-------------:|:------:|
| AuthoritiesController | 5 | 5 | 0 | 4 | 1 | ✅ 0 |
| BookmarksController | 3 | 3 | 0 | 3 | 0 | ✅ 0 |
| CasesController | 15 | 15 | 0 | 8 | 7 | ✅ Fixed (was 2) |
| CaseDialogueCommentsController | 4 | 4 | 0 | 3 | 1 | ✅ Fixed (was 1) |
| DocumentsController | 8 | 5 | 3 | 5 | 3 | 🟡 1 (1 Fixed) |
| MembersController | 7 | 7 | 0 | 1 | 6 | ✅ Fixed (was 1) |
| WorkflowStateHistoryController | 4 | 4 | 0 | 3 | 1 | ✅ 0 |
| UserController | 2 | 0 | 2 | 2 | 0 | ✅ 0 |
| **Total** | **48** | **43** | **5** | **29** | **19** | **1 open (5 fixed)** |

### Issue Summary

| Severity | Count | Endpoints | Status |
|----------|:-----:|-----------|--------|
| ✅ Critical | 1 | CaseDialogueComments.Patch — `[FromBody]` on `Delta<T>` | **Fixed** |
| ✅ Medium | 2 | Cases.Checkout, Cases.Checkin — unnecessary response body | **Fixed** — now return `NoContent()` |
| ✅ Medium | 2 | Members.Put, Documents.Put — over-posting via `SetValues()` | **Fixed** — audit fields marked `IsModified = false` |
| 🟡 Medium | 1 | Documents.Upload — raw `List<T>` instead of OData envelope | Open |
| ✅ Clean | 42 | | |

The 5 non-OData endpoints are:
- `UserController.GetCurrentUser` — standard REST, returns current user identity
- `UserController.LookupUsers` — standard REST, batch user name lookup
- `DocumentsController.GetValue` — binary document download (`$value` stream)
- `DocumentsController.Upload` — multipart file upload (`IFormFile`)
- `DocumentsController.GetForm348` — PDF generation and download
