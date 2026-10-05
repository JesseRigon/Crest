# Approvals

Status: not started. A placeholder so the need is not lost; requirements to be worked out.

## Goal

One Crest approvals system that any feature can use when an action needs someone else's
agreement before it happens, not only workflows.

## What exists

Crest.Workflows already has approvals ([workflows.md](workflows.md), "Approvals"): the
`Request approval` activity records an `ApprovalTask` for a role or permission, the
`api/crest/workflows/approvals` endpoints list and decide them, and the Crest page
`/workflows/approvals` shows them. It is reachable only from inside a workflow.

The general system grows out of that rather than beside it: the task, the decision and the queue
move to a shared place, and the workflow activity becomes one way of requesting an approval.

## Known needs

- **Personal drive access when a notice fails.** Today the access proceeds and the failure is
  recorded ([media.md](media.md), notices). With approvals, a
  tenant could require one instead.
- **Overriding the personal drive notice delay**, as an alternative to entering a reason.
- Likely later: making content public, granting sensitive permissions, publishing changes to
  shipped definitions.

## To work out

- Who can decide: a role, a permission, a named user, the owner of the thing affected.
- One approver or several, and in what order.
- Expiry: what happens to a request nobody decides.
- How requesters and approvers are told (Orchard notifications, as personal drive notices are).
- What the requester sees while waiting, and how a decision resumes the action.
- Every request and decision recorded in the audit trail ([audit.md](audit.md)).
