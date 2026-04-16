# Part 2: Entity Model Design Review

## Strengths

1. **Derived `CurrentWorkflowState`** — The computed property based on `MAX(WorkflowStateHistory)` eliminates a denormalized column and ensures consistency. The `OrderByDescending(CreatedDate).ThenByDescending(Id)` tiebreaker is correct for fast-insert scenarios.

2. **`AuditableEntity` base** — Centralizes `CreatedDate`, `ModifiedDate`, `CreatedBy`, `ModifiedBy`, `RowVersion` across all entities. Optimistic concurrency via `RowVersion` is applied correctly in PATCH.

3. **Proper collection initialization** — `ICollection<T> = new HashSet<T>()` prevents null reference issues and signals set semantics (no duplicates by reference).

4. **Document binary separation** — Binary `Content` is excluded from the OData EDM (`Ignore(d => d.Content)`) and served via REST endpoints. This prevents serializing large blobs in OData responses.

## Concerns

1. **Wide root entity (~80+ scalar properties)** — `LineOfDutyCase` has properties spanning 7+ form sections (Member Info, Medical Assessment, Commander Review, SJA, Wing CC, Board, Approving Authority). Every PATCH/GET serializes the full property bag even when only one section's fields changed.

   **Assessment**: This mirrors the AF Form 348's single-form structure. Splitting into separate tables would add complexity without clear benefit since the form is always loaded as a unit. The current approach is **acceptable** for this domain — the tradeoff of a wide table is offset by simpler queries, no JOINs for scalar data, and natural OData Delta patching.

2. **Witness fields as numbered properties** — `WitnessNameAddress1` through `WitnessNameAddress5` are denormalized scalar columns. The `WitnessStatements` collection also exists as a proper entity.

   **Recommendation**: Choose one pattern. If witnesses are truly fixed-count form fields (AF Form 348 Item 21 has 5 slots), the scalar approach is valid. If variable-length, use only the collection. Currently both exist, which creates ambiguity about which is authoritative.

3. **`List<string>` authority comments** — `LineOfDutyAuthority.Comments` as a serialized `List<string>` cannot be queried or indexed at the database level. For an audit trail, individual records with timestamps would be more appropriate.

4. **`MemberId` + `MemberName` + `MemberRank`** — Member data is stored both as a FK (`MemberId` → navigation `Member`) and as denormalized scalars. This is intentional (snapshot at time of case creation) but should be documented to prevent confusion about which is authoritative.
