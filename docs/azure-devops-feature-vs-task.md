# Azure DevOps (CMMI): Feature vs. Task

In Azure DevOps using the **CMMI** process template, **Feature** and **Task** sit at very different levels of the work-item hierarchy.

| | **Feature** | **Task** |
|---|---|---|
| **Level** | High — typically just under Epic | Lowest — under Requirement |
| **Purpose** | A shippable piece of business value or capability | A unit of technical work needed to complete a requirement |
| **Owner** | Product Manager / Product Owner | Individual developer/engineer |
| **Time horizon** | Weeks to months; spans multiple sprints | Hours to a couple of days; fits in one sprint |
| **Sizing** | Effort / Business Value / Time Criticality | Original Estimate, Remaining Work, Completed Work (hours) |
| **Children** | Requirements | None (leaf node) |
| **Tracked on** | Features backlog / Delivery Plans / roadmaps | Sprint Taskboard |

**Hierarchy:** Epic → Feature → Requirement → Task

**Rule of thumb:**
- A **Feature** answers *"what capability are we delivering?"*
- A **Task** answers *"what specific thing does a developer need to do today?"*

## Removing an existing feature

Treat the removal as planned, deliverable work — don't just delete the original work item.

**Pattern:** Create a **Requirement** *"Remove &lt;feature&gt;"* (Type = *Functional* or *Scenario*) parented to the existing **Feature** being retired, then decompose it into **Task** work items for the actual removal work (UI, API, data, tests, docs). Track defects discovered during removal with **Bug** work items, blockers with **Issue**, and uncertainty with **Risk**. Use **Change Request** or **Review** for governance follow-ups.

Guidance:
- Close the original **Feature** with state **Removed** once the removal **Requirement** is **Closed**, preserving links and history rather than deleting it.
- For larger deprecations spanning multiple sprints/teams, create a sibling **Feature** *"Deprecate &lt;X&gt;"* under the same Epic and break it into multiple Requirements. Reserve a new **Epic** only when the deprecation spans multiple features and releases.
- Use **Issue** only for blockers encountered during the removal, not for the removal work itself.

## Microsoft Learn references

1. **Define features and epics to organize your backlog** — <https://learn.microsoft.com/azure/devops/boards/backlogs/define-features-epics>

   Verbatim:
   > "Feature: A feature is a significant piece of functionality that delivers value to the user. It typically includes several user stories or backlog items and might take one or more sprints to complete."
   >
   > "Epic: An epic is a large body of work that can be broken down into multiple features. It represents a major initiative or goal and might span several sprints or even releases."
   >
   > "Generally, you should complete backlog items, such as user stories or tasks, within a sprint, while features and epics might take one or more sprints to complete."

2. **Add tasks to backlog items for sprint planning** (Task WIT usage) — <https://learn.microsoft.com/azure/devops/boards/sprints/add-tasks>

   Verbatim:
   > "Add tasks to backlog items to track the work needed to complete them and to estimate effort per team member."
   >
   > "Add as many tasks as needed to capture design, coding, testing, content, or sign-off work."
   >
   > "Tasks added from the sprint backlog or board are automatically linked to their parent backlog item and assigned to the sprint's Iteration Path."
   >
   > "Size tasks to take no more than a day. If a task is too large, break it down."

   Task form fields (Original Estimate, Remaining Work, Completed Work, Activity) are documented on the same page.

3. **CMMI process work item types** — <https://learn.microsoft.com/azure/devops/boards/work-items/guidance/cmmi-process>
   (Hierarchy: Epic → Feature → Requirement → Task. Supporting WITs: Bug, Issue, Risk, Change Request, Review.)

Pages retrieved from learn.microsoft.com on 2026-04-24.
