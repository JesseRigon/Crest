const { createInstance } = require('../../../../../OrchardCore.Crest/tests/playwright/harness/instance');
const { ensureTestUser } = require('../../../../../OrchardCore.Crest/tests/playwright/harness/testUsers');
const { fetchAntiforgeryToken } = require('../../../../../OrchardCore.Crest/tests/playwright/harness/antiforgery');

// Approvals (plans/workflows.md, phase 4): Request approval parks a flow on a task for an
// Orchard role; the queue shows it to that role's members only; someone outside the role
// cannot decide (403), a member can, the flow resumes on the chosen port, and a second
// decision is refused (409). A task gated on a permission is decided by its holders.
module.exports = async function run(page, ctx) {
  const results = [];
  const api = `${ctx.baseUrl}/crest-workflows/api`;
  const approvalsApi = `${ctx.baseUrl}/api/crest/workflows/approvals`;
  const created = { definitions: [] };
  const stamp = Date.now();

  function client(target) {
    return async function call(method, url, body) {
      const token = await fetchAntiforgeryToken(target, ctx.baseUrl);
      return target.evaluate(async ({ method, url, body, token }) => {
        const headers = body === undefined ? {} : { 'Content-Type': 'application/json' };
        headers[token.headerName || 'RequestVerificationToken'] = token.requestToken;
        try {
          const response = await fetch(url, { method, credentials: 'include', headers, body: body === undefined ? undefined : JSON.stringify(body) });
          const text = await response.text();
          let json = null;
          try { json = text ? JSON.parse(text) : null; } catch {}
          return { ok: response.ok, status: response.status, text, json };
        } catch (error) {
          return { ok: false, status: 0, text: `${method} ${url}: ${error.message}`, json: null };
        }
      }, { method, url, body, token });
    };
  }
  const call = client(page);

  const literal = (value, typeName = 'String') => ({ typeName, expression: { type: 'Literal', value } });
  async function waitFor(predicate, { timeout = 15000, interval = 500 } = {}) {
    const deadline = Date.now() + timeout;
    let last;
    while (Date.now() < deadline) {
      last = await predicate();
      if (last) return last;
      await page.waitForTimeout(interval);
    }
    return last;
  }

  async function approvalFlow(name, gate) {
    const saved = await call('POST', `${api}/workflow-definitions`, {
      model: {
        name: `Playwright ${name} ${stamp}`, variables: [], inputs: [], outputs: [], outcomes: [], customProperties: {}, options: {},
        root: { type: 'Crest.Workflows.Flowchart', id: 'flow', version: 1, activities: [
          { type: 'Crest.Workflows.RequestApproval', id: 'ask', version: 1, title: literal(`${name} ${stamp}`), description: literal('Playwright approval'), ...gate },
          { type: 'Crest.Workflows.WriteLine', id: 'approved', version: 1, text: literal('approved') },
          { type: 'Crest.Workflows.WriteLine', id: 'rejected', version: 1, text: literal('rejected') } ],
          connections: [
            { source: { activity: 'ask', port: 'Approved' }, target: { activity: 'approved', port: 'In' } },
            { source: { activity: 'ask', port: 'Rejected' }, target: { activity: 'rejected', port: 'In' } }] },
      }, publish: true,
    });
    const id = saved.json?.workflowDefinition?.definitionId;
    if (id) created.definitions.push(id);
    const correlationId = `approval-${name.replace(/\s+/g, '-')}-${stamp}`;
    const run = id ? await call('POST', `${api}/workflow-definitions/${encodeURIComponent(id)}/execute`, { correlationId }) : null;
    return { id, saved, correlationId, instanceId: run?.json?.workflowState?.id, status: run?.json?.workflowState?.status };
  }

  async function finishedNodes(instanceId) {
    return waitFor(async () => {
      const instance = await call('GET', `${api}/workflow-instances/${encodeURIComponent(instanceId)}`);
      if (instance.json?.workflowState?.status !== 'Finished' && instance.json?.status !== 'Finished') return null;
      const journal = await call('GET', `${api}/workflow-instances/${encodeURIComponent(instanceId)}/journal`);
      return new Set((journal.json?.items || []).filter(e => e.eventName === 'Completed').map(e => e.activityId));
    });
  }

  try {
    // 1. A task for Editors: the flow waits.
    const editors = await approvalFlow('editor approval', { role: literal('Editor') });
    results.push({ name: 'request-approval-parks-the-flow', pass: Boolean(editors.id) && editors.status === 'Running', message: `HTTP ${editors.saved.status} status=${editors.status}` });

    // The pending state an object's page shows: the parked flow is pending for its object.
    const pending = await call('GET', `${ctx.baseUrl}/api/crest/workflows/pending?correlationId=${encodeURIComponent(editors.correlationId)}`);
    results.push({ name: 'the-pending-api-shows-the-parked-flow-for-its-object', pass: pending.ok && pending.json?.isPending === true && (pending.json?.instances || []).some(i => i.id === editors.instanceId && i.subStatus === 'Suspended'), message: `HTTP ${pending.status} ${pending.text.slice(0, 160)}` });

    const everything = await call('GET', `${approvalsApi}?all=true`);
    const task = (everything.json || []).find(t => t.workflowInstanceId === editors.instanceId);
    results.push({ name: 'the-task-is-recorded-for-the-role', pass: task?.role === 'Editor' && task?.status === 'pending' && task?.canDecide === false, message: JSON.stringify(task ?? null).slice(0, 200) });

    // The administrator is not an Editor: not in their queue, and deciding is 403.
    const mine = await call('GET', approvalsApi);
    const adminDecides = task ? await call('POST', `${approvalsApi}/${task.id}/decide`, { decision: 'approve' }) : { status: 0 };
    results.push({ name: 'outside-the-role-it-is-not-yours-to-decide', pass: !(mine.json || []).some(t => t.id === task?.id) && adminDecides.status === 403, message: `queue=${(mine.json || []).length} decide=${adminDecides.status}` });

    // An Editor sees it and approves it.
    const editor = await ensureTestUser(page, ctx.baseUrl, 'wfapprover', { roles: ['Editor'] });
    const session = await createInstance();
    try {
      await session.page.goto(`${ctx.baseUrl}/login`, { waitUntil: 'domcontentloaded' });
      const token = await fetchAntiforgeryToken(session.page, ctx.baseUrl);
      const login = await session.page.evaluate(async ({ url, body, token }) => {
        const response = await fetch(url, { method: 'POST', credentials: 'include', headers: { 'Content-Type': 'application/json', [token.headerName || 'RequestVerificationToken']: token.requestToken }, body: JSON.stringify(body) });
        return response.status;
      }, { url: `${ctx.baseUrl}/api/crest/auth/login`, body: { userName: editor.username, password: editor.password, rememberMe: false }, token });
      const callAsEditor = client(session.page);
      const queue = await callAsEditor('GET', approvalsApi);
      const listed = (queue.json || []).find(t => t.id === task?.id);
      results.push({ name: 'a-role-member-sees-it-in-their-queue', pass: login === 200 && listed?.canDecide === true, message: `login=${login} queue=${queue.status} listed=${Boolean(listed)}` });

      const decided = task ? await callAsEditor('POST', `${approvalsApi}/${task.id}/decide`, { decision: 'approve', comment: 'Looks right' }) : { status: 0 };
      const nodes = editors.instanceId ? await finishedNodes(editors.instanceId) : null;
      results.push({ name: 'approving-resumes-the-flow-on-approved', pass: decided.status === 200 && decided.json?.decidedBy === editor.username && nodes?.has('approved') && !nodes?.has('rejected'), message: `decide=${decided.status} nodes=${nodes ? [...nodes].join(',') : '-'}` });

      const again = task ? await callAsEditor('POST', `${approvalsApi}/${task.id}/decide`, { decision: 'reject' }) : { status: 0 };
      results.push({ name: 'a-task-is-decided-once', pass: again.status === 409, message: `HTTP ${again.status}` });
    } finally {
      await session.browser.close().catch(() => {});
    }

    // 2. A task gated on a permission: the administrator holds ManageWorkflows and rejects it.
    const managers = await approvalFlow('manager approval', { permission: literal('ManageWorkflows') });
    const managerTask = await waitFor(async () => ((await call('GET', approvalsApi)).json || []).find(t => t.workflowInstanceId === managers.instanceId) || null);
    const rejected = managerTask ? await call('POST', `${approvalsApi}/${managerTask.id}/decide`, { decision: 'reject', comment: 'Not now' }) : { status: 0 };
    const managerNodes = managers.instanceId ? await finishedNodes(managers.instanceId) : null;
    results.push({ name: 'permission-holders-decide-and-reject-resumes-on-rejected', pass: managerTask?.canDecide === true && rejected.status === 200 && managerNodes?.has('rejected') && !managerNodes?.has('approved'), message: `task=${Boolean(managerTask)} decide=${rejected.status} nodes=${managerNodes ? [...managerNodes].join(',') : '-'}` });

    const invalid = managerTask ? await call('POST', `${approvalsApi}/${managerTask.id}/decide`, { decision: 'maybe' }) : { status: 0 };
    results.push({ name: 'an-unknown-decision-is-400', pass: invalid.status === 400, message: `HTTP ${invalid.status}` });
  } finally {
    for (const id of created.definitions) await call('DELETE', `${api}/workflow-definitions/${encodeURIComponent(id)}`);
  }

  return results;
};
