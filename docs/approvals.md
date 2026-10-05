# Approvals

Status: not started. A placeholder so the need is not lost; requirements to be worked out.
Covers one Crest approvals system that any feature can use when an action needs someone else's
agreement before it happens, not only workflows. What is built today is in
[workflows.md](workflows.md), "Approvals".

## Shared approvals system

Crest.Workflows already has approvals ([workflows.md](workflows.md), "Approvals"): the
`Request approval` activity records an `ApprovalTask` for a role or permission, the
`api/crest/workflows/approvals` endpoints list and decide them, and the Crest page
`/workflows/approvals` shows them. It is reachable only from inside a workflow.

- [ ] **Move approvals to a shared place.** The general system grows out of that rather than
  beside it: the task, the decision and the queue move to a shared place, and the workflow
  activity becomes one way of requesting an approval.

## Known needs

- [ ] **Personal drive access when a notice fails.** Today the access proceeds and the failure is
  recorded ([media.md](media.md), notices). With approvals, a
  tenant could require one instead.
- [ ] **Overriding the personal drive notice delay**, as an alternative to entering a reason.
- [ ] **Later uses.** Likely later: making content public, granting sensitive permissions,
  publishing changes to shipped definitions.

## Decisions needed

- [ ] **Who can decide.** A role, a permission, a named user, the owner of the thing affected.
- [ ] **How many approvers.** One approver or several, and in what order.
- [ ] **Expiry.** What happens to a request nobody decides.
- [ ] **Notifications.** How requesters and approvers are told (Orchard notifications, as
  personal drive notices are).
- [ ] **Waiting and resuming.** What the requester sees while waiting, and how a decision
  resumes the action.
- [ ] **Audit.** Every request and decision recorded in the audit trail ([audit.md](audit.md)).
