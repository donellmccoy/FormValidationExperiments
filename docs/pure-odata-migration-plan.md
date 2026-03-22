# Pure OData Architecture Migration Plan

This plan encompasses migrating the Blazor client and ASP.NET Core API to a strictly "Pure OData" architecture, based on evaluations of both the API/Client communication and EF Core entities.

---

## Phase 1: Domain & EF Core Model Refinements
*Prepare the shared models to map perfectly to the OData EDM parser without side effects.*

1. ✅ **Remove 1-to-1 Auto-Initializers**
   * **File:** `ECTSystem.Shared/Models/LineOfDutyCase.cs`
   * **Action:** Change `public MEDCONDetail MEDCON { get; set; } = new MEDCONDetail();` (and INCAPDetails) to implicitly be `null`.
   * **Why:** If the OData proxy sees an un-fetched object initialized locally, its change tracker will mistake it for a new record and attempt to push empty rows to the database.

2. ✅ **Explicit Concurrency Configurations**
   * **File:** `ECTSystem.Shared/Models/AuditableEntity.cs` (or via Fluent API)
   * **Action:** Ensure `RowVersion` is explicitly marked with `[ConcurrencyCheck]` alongside `[Timestamp]`. 
   * **Why:** This signals the OData EDM builder to enforce ETag checks automatically via the `If-Match: W/"{RowVersion}"` header.

---

## Phase 2: API & Controller Purification
*Ensure the server strictly respects standard EDM data shapes and OData routing conventions rather than REST/Web API workarounds.*

1. ✅ **Eliminate Custom Post DTOs**
   * **File:** `ECTSystem.Api/Controllers/CasesController.cs`
   * **Action:** Refactor `Post([FromBody] CreateCaseDto dto)` to consume the EDM entity `Post([FromBody] LineOfDutyCase lodCase)`.  
   * **Why:** Strictly adhering to OData means relying on the entity definition (metadata) for `POST`/`PATCH` rather than mapping custom DTOs at the endpoint layer.

2. ✅ **Strongly-Type the Collection Functions**
   * **File:** `ECTSystem.Api/Controllers/CasesController.cs` & `ECTSystem.Api/Extensions/ServiceCollectionExtensions.cs`
   * **Action:** Update `ByCurrentState` signature from `string includeStates` to `IEnumerable<WorkflowState> includeStates`. Update the `ODataConventionModelBuilder` to register `.CollectionParameter<WorkflowState>("includeStates")`.

3. ✅ **Move Relational Logic to the Server (Bound Functions)**
   * **File:** `ECTSystem.Api/Controllers/CasesController.cs` & `ECTSystem.Api/Extensions/ServiceCollectionExtensions.cs`
   * **Action:** Register and create a new GET endpoint for `/odata/Cases/Default.Bookmarked()`. Have this server-side endpoint perform the SQL `JOIN/WHERE` for bookmarks, returning the exact `LineOfDutyCase` subset.
   * **Why:** Eliminates the N+1 client-side HTTP calls where the client currently fetches IDs, then builds a massive `$filter=Id in(...)` string.

---

## Phase 3: Proxy Code Generation
*Replaces manual client wiring with the official, type-safe Microsoft OData generator.*

1. ✅ **Generate the OData Client**
   * **Action:** With the API running, use the `dotnet-odata` CLI tooling (or Visual Studio Connected Services) targeted at your API's `$metadata` endpoint.
   * **Why:** This outputs a generated C# file (e.g., `EctODataClient.g.cs`) containing the locked `DataServiceContext`, completely eliminating the need for you to establish "open types" or manually construct an EDM Model on the client.

---

## Phase 4: Client Refactoring & Cleanup
*Switching the Blazor client to strictly use the auto-generated proxy features.*

1. **Strip Hand-Rolled OData Core**
   * **Files to Delete/Modify:** Delete `ECTSystem.Web/Services/EctODataContext.cs`. In `ECTSystem.Web/Extensions/ServiceCollectionExtensions.cs`, completely remove `BuildClientEdmModel()`. 
   * **Action:** Register the newly generated `DataServiceContext` subclass instead.

2. **Eliminate Save Hacks via Native Change Tracking**
   * **File:** `ECTSystem.Web/Services/CaseHttpService.cs`
   * **Action:** In `SaveCaseAsync`, delete the ~30 lines of code caching navigation properties (like `documents`, `authorities`) before save and restoring them after. 
   * **Why:** The official generated client knows the structural EDM schema. By using `Context.UpdateObject(lodCase)`, the proxy performs a smart diff, sending a clean `PATCH` only for changed scalar properties, without wiping out navigation objects in memory.

3. **Replace String URLs with LINQ Expressions**
   * **File:** `ECTSystem.Web/Services/ODataServiceBase.cs` & derived services.
   * **Action:** Deprecate string-based building like `BuildNavigationPropertyUrl`. Replace explicit `HttpClient.GetAsync` strings and string interpolations with the generated `.ByKey(id)` and proxy-bound methods.
     * *Example:* Calling checkout changes from `ExecuteAsync("Cases(1)/Checkout")` to simply await `Context.Cases.ByKey(key).Checkout().ExecuteAsync()`.

4. **Bridge Radzen UI using Native Query Options**
   * **Action:** Since Radzen constructs string predicates (e.g., `"$skip=10"`), continue utilizing `AddQueryOption()` to attach them to the proxy's strongly typed `DataServiceQuery<LineOfDutyCase>` base. 

---

### Execution Strategy

The recommended path is to execute **Phase 1 and Phase 2** first. This aligns the API fully with pure OData expectations and carries zero UI impact, providing a clean foundation before shifting the client-side mechanism in Phases 3 and 4.