# LOD Case Workflow State Machine

```mermaid
stateDiagram-v2
    direction TB

    %% State aliases
    state "Draft" as Draft
    state "Member Information\nEntry" as MIE
    state "Medical Technician\nReview" as MTR
    state "Medical Officer\nReview" as MOR
    state "Unit Commander\nReview" as UCR
    state "Wing Judge\nAdvocate Review" as WJAR
    state "Wing Commander\nReview" as WCR
    state "Appointing Authority\nReview" as AAR

    state "Board Reviews" as BoardReviews {
        direction LR
        state "Board Med Tech\nReview" as BMTR
        state "Board Med Officer\nReview" as BMOR
        state "Board Legal\nReview" as BLR
        state "Board Administrator\nReview" as BAR

        BMTR --> BMOR : Forward
        BMTR --> BLR : Forward
        BMTR --> BAR : Forward
        BMOR --> BMTR : Forward
        BMOR --> BLR : Forward
        BMOR --> BAR : Forward
        BLR --> BMTR : Forward
        BLR --> BMOR : Forward
        BLR --> BAR : Forward
        BAR --> BMTR : Forward
        BAR --> BMOR : Forward
        BAR --> BLR : Forward
    }

    state "Completed" as DONE
    state "Cancelled" as CANCEL

    %% Forward (sequential) transitions
    [*] --> Draft
    Draft --> MIE : Start LOD Case
    MIE --> MTR : Forward
    MTR --> MOR : Forward
    MOR --> UCR : Forward
    UCR --> WJAR : Forward
    WJAR --> WCR : Forward
    WCR --> AAR : Forward
    AAR --> BoardReviews : Forward to\nBoard Tech
    BAR --> DONE : Complete

    %% Return transitions (dynamic — back to any earlier stage)
    MOR --> MTR : Return
    UCR --> MOR : Return
    WJAR --> UCR : Return
    WCR --> WJAR : Return
    AAR --> WCR : Return
    BoardReviews --> AAR : Return

    %% Cancel transitions (all non-terminal → Cancelled)
    Draft --> CANCEL : Cancel
    MIE --> CANCEL : Cancel
    MTR --> CANCEL : Cancel
    MOR --> CANCEL : Cancel
    UCR --> CANCEL : Cancel
    WJAR --> CANCEL : Cancel
    WCR --> CANCEL : Cancel
    AAR --> CANCEL : Cancel
    BoardReviews --> CANCEL : Cancel

    %% Terminal states
    DONE --> [*]
    CANCEL --> [*]
```

## Workflow Overview

| Step | State | Forward To | Can Return? |
| --- | --- | --- | --- |
| 0 | Draft | Member Information Entry | No |
| 1 | Member Information Entry | Medical Technician Review | No |
| 2 | Med Tech Review | Medical Officer Review | Yes (Return trigger) |
| 3 | Medical Officer Review | Unit Commander Review | Yes |
| 4 | Unit Commander Review | Wing Judge Advocate Review | Yes |
| 5 | Wing Judge Advocate Review | Wing Commander Review | Yes |
| 6 | Wing Commander Review | Appointing Authority Review | Yes |
| 7 | Appointing Authority Review | Board Med Tech Review | Yes |
| 8 | Board Med Tech Review | Board Med/Legal/Admin (lateral) | Yes |
| 9 | Board Medical Officer Review | Board Tech/Legal/Admin (lateral) | Yes |
| 10 | Board Legal Review | Board Tech/Med/Admin (lateral) | Yes |
| 11 | Board Administrator Review | Completed (terminal) | Yes |

## Key Behaviors

- **Sequential pipeline (Steps 0–7):** Each step forwards to the next in order.
- **Board lateral routing (Steps 8–11):** Any board reviewer can forward to any
  other board reviewer.
- **Return trigger:** Dynamic — from Steps 2+ the case can be returned to _any_
  earlier stage via `PermitDynamicIf`.
- **Cancel trigger:** Available from all 12 non-terminal states; transitions to
  Cancelled.
- **Terminal states:** Completed and Cancelled silently ignore further Cancel
  triggers.
