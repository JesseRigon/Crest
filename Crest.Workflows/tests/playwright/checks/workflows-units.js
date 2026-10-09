const { fetchAntiforgeryToken } = require('../../../../../Crest/tests/playwright/harness/antiforgery');

// Units of work and hooks (docs/workflows.md › Posting on workflows):
//  - a burst is one transaction: a host flow creates content, then runs a hook whose required
//    attachment fails the unit - the host faults and the content does not exist;
//  - the same host with a healthy attachment commits: content exists, child instances exist;
//  - a best-effort attachment's failure is journaled and the host commits;
//  - only atomic flows attach (a Delay is refused, naming it); a flow attached to a hook cannot
//    be re-published with a boundary in it;
//  - triggers fire after commit, never for a cancelled unit: a Raise trigger inside a failed
//    host starts nothing; inside a committed one it does.
module.exports = async function run(page, ctx) {
  const results = [];
  const api = `${ctx.baseUrl}/crest-workflows/api`;
  const hooksApi = `${ctx.baseUrl}/api/crest/workflows/hooks`;
  const content = `${ctx.baseUrl}/api/crest/content-items`;
  const created = { definitions: [], items: [], attachments: [] };
  const stamp = Date.now();

  async function call(method, url, body) {
    const token = await fetchAntiforgeryToken(page, ctx.baseUrl);
    return page.evaluate(async ({ method, url, body, token }) => {
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
  }

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
  const edge = (from, to, port = 'Done') => ({ source: { activity: from, port }, target: { activity: to, port: 'In' } });
  async function publish(name, activities, connections, { definitionId } = {}) {
    const saved = await call('POST', `${api}/workflow-definitions`, {
      model: {
        definitionId, name: `Playwright ${name} ${stamp}`, variables: [], inputs: [], outputs: [], outcomes: [], customProperties: {}, options: {},
        root: { type: 'Crest.Workflows.Flowchart', id: 'flow', version: 1, activities, connections },
      }, publish: true,
    });
    const id = saved.json?.workflowDefinition?.definitionId;
    if (id && !definitionId) created.definitions.push(id);
    return { response: saved, id };
  }
  const writeLine = (id, text) => ({ type: 'Crest.Workflows.WriteLine', id, version: 1, text: literal(text) });
  const failUnit = (id, reason) => ({ type: 'Crest.Workflows.FailUnit', id, version: 1, reason: literal(reason) });
  const hook = (id, slot = 'flow.hook') => ({ type: 'Crest.Workflows.Hook', id, version: 1, slot: literal(slot) });
  const createOrg = (id, title) => ({ type: 'Crest.Workflows.OrchardTask', id, version: 1, activityName: literal('CreateContentTask'), propertiesJson: literal(JSON.stringify({ ContentType: 'Organization', Publish: true, ContentProperties: { Expression: JSON.stringify({ DisplayText: title }) } })) });
  async function orgByTitle(title) {
    const response = await call('GET', `${content}?contentType=Organization&pageSize=100&search=${encodeURIComponent(title)}`);
    return (response.json?.items || []).find(i => i.displayText === title) || null;
  }
  async function execute(definitionId) {
    return call('POST', `${api}/workflow-definitions/${encodeURIComponent(definitionId)}/execute`, {});
  }
  async function instance(id) {
    return (await call('GET', `${api}/workflow-instances/${encodeURIComponent(id)}`)).json;
  }
  async function attach(definitionId, required = true) {
    const response = await call('POST', hooksApi, { slot: 'flow.hook', definitionId, required });
    if (response.json?.id) created.attachments.push(response.json.id);
    return response;
  }
  async function detachAll() {
    for (const id of created.attachments.splice(0)) await call('DELETE', `${hooksApi}/${encodeURIComponent(id)}`);
  }

  try {
    const registry = await call('GET', `${ctx.baseUrl}/api/crest/workflows/registry`);
    results.push({ name: 'registry-lists-the-generic-hook-slot', pass: (registry.json?.hookSlots || []).some(s => s.key === 'flow.hook' && s.allowTenantAttachments === true), message: JSON.stringify(registry.json?.hookSlots || []).slice(0, 200) });

    // Attachments: a failing one, a healthy one, a long-running one.
    const failing = await publish('failing attachment', [failUnit('fail', `nope ${stamp}`)], []);
    const healthy = await publish('healthy attachment', [writeLine('log', 'hooked')], []);
    const waiting = await publish('waiting attachment', [{ type: 'Crest.Workflows.Delay', id: 'wait', version: 1, timeSpan: literal('00:00:01', 'TimeSpan') }, writeLine('log', 'late')], [edge('wait', 'log')]);
    results.push({ name: 'attachment-flows-publish', pass: Boolean(failing.id && healthy.id && waiting.id), message: `${failing.response.status} ${healthy.response.status} ${waiting.response.status} ${waiting.response.text.slice(0, 120)}` });

    const refused = await attach(waiting.id);
    results.push({ name: 'a-long-running-flow-cannot-be-attached', pass: refused.status === 400 && /Delay/.test(refused.json?.detail || refused.text) && /long-running/.test(refused.json?.detail || refused.text), message: `HTTP ${refused.status} ${(refused.json?.detail || refused.text).slice(0, 200)}` });

    // The host: create content, run the hook, log.
    const hostTitle = `Playwright unit org ${stamp}`;
    const host = await publish('unit host', [createOrg('create', hostTitle), hook('hook'), writeLine('log', 'after hook')], [edge('create', 'hook', 'Done'), edge('hook', 'log')]);
    results.push({ name: 'host-flow-publishes', pass: Boolean(host.id), message: `HTTP ${host.response.status} ${host.response.text.slice(0, 120)}` });

    // 1. Required attachment fails: the host faults (the engine answers 500 for a faulted
    //    execution, with the state in the body), the content it created is gone.
    const attachedFailing = await attach(failing.id, true);
    const failedRun = await execute(host.id);
    const failedId = failedRun.json?.workflowState?.id;
    const failedInstance = failedId ? await waitFor(async () => { const i = await instance(failedId); return i?.subStatus === 'Faulted' ? i : null; }) : null;
    await page.waitForTimeout(500);
    const orgAfterFailure = await orgByTitle(hostTitle);
    const incident = JSON.stringify(failedInstance?.workflowState?.incidents || []);
    results.push({ name: 'a-failed-required-attachment-faults-the-host-and-discards-its-writes', pass: attachedFailing.ok && failedRun.status === 500 && failedInstance?.subStatus === 'Faulted' && /nope/.test(incident) && orgAfterFailure === null, message: `attach=${attachedFailing.status} run=${failedRun.status} status=${failedInstance?.status}/${failedInstance?.subStatus} org=${orgAfterFailure ? 'EXISTS' : 'absent'} incidents=${incident.slice(0, 160)}` });

    // 2. Healthy attachment: everything commits, child instance exists.
    await detachAll();
    const attachedHealthy = await attach(healthy.id, true);
    const okRun = await execute(host.id);
    const okId = okRun.json?.workflowState?.id;
    const okInstance = okId ? await instance(okId) : null;
    const orgAfterSuccess = await waitFor(() => orgByTitle(hostTitle));
    if (orgAfterSuccess?.contentItemId) created.items.push(orgAfterSuccess.contentItemId);
    const children = await call('GET', `${api}/workflow-instances?definitionId=${encodeURIComponent(healthy.id)}`);
    const child = (children.json?.items || []).find(i => !i.parentWorkflowInstanceId || i.parentWorkflowInstanceId === okId);
    results.push({ name: 'a-healthy-attachment-runs-inside-the-host-and-everything-commits', pass: attachedHealthy.ok && okInstance?.status === 'Finished' && Boolean(orgAfterSuccess) && Boolean(child) && child.status === 'Finished', message: `attach=${attachedHealthy.status} host=${okInstance?.status}/${okInstance?.subStatus} org=${orgAfterSuccess ? 'exists' : 'absent'} child=${child?.status ?? 'none'} (${children.json?.items?.length ?? 0} runs)` });

    // 3. Best effort: the failure is journaled, the host commits.
    await detachAll();
    const bestEffortTitle = `Playwright unit org best-effort ${stamp}`;
    const host2 = await publish('unit host best effort', [createOrg('create', bestEffortTitle), hook('hook'), writeLine('log', 'after hook')], [edge('create', 'hook', 'Done'), edge('hook', 'log')]);
    const attachedBestEffort = await attach(failing.id, false);
    const beRun = await execute(host2.id);
    const beId = beRun.json?.workflowState?.id;
    const beInstance = beId ? await instance(beId) : null;
    const orgBestEffort = await waitFor(() => orgByTitle(bestEffortTitle));
    if (orgBestEffort?.contentItemId) created.items.push(orgBestEffort.contentItemId);
    // The Hook's JournalData lands on its activity execution record (payload), which the
    // instance viewer shows per node.
    const hookRecords = beId ? (await call('GET', `${api}/activity-executions/list?workflowInstanceId=${encodeURIComponent(beId)}&activityNodeId=${encodeURIComponent('Workflow1:flow:hook')}`)).json?.items || [] : [];
    const journaled = hookRecords.some(r => JSON.stringify(r.payload || {}).includes('BestEffort'));
    results.push({ name: 'a-best-effort-attachment-failure-is-journaled-and-the-host-commits', pass: attachedBestEffort.ok && attachedBestEffort.json?.required === false && beInstance?.status === 'Finished' && Boolean(orgBestEffort) && journaled, message: `attach=${attachedBestEffort.status} required=${attachedBestEffort.json?.required} host=${beInstance?.status}/${beInstance?.subStatus} org=${orgBestEffort ? 'exists' : 'absent'} journaled=${journaled}` });

    // 4. An attached flow cannot be re-published with a boundary.
    await detachAll();
    await attach(healthy.id, true);
    const current = (await call('GET', `${api}/workflow-definitions/${encodeURIComponent(healthy.id)}`)).json;
    const republish = await call('POST', `${api}/workflow-definitions`, {
      model: { ...current, root: { type: 'Crest.Workflows.Flowchart', id: 'flow', version: 1, activities: [{ type: 'Crest.Workflows.Delay', id: 'wait', version: 1, timeSpan: literal('00:00:01', 'TimeSpan') }, writeLine('log', 'late')], connections: [edge('wait', 'log')] } },
      publish: true,
    });
    results.push({ name: 'an-attached-flow-cannot-gain-a-boundary', pass: republish.status === 400 && /atomic/.test(republish.text), message: `HTTP ${republish.status} ${republish.text.slice(0, 200)}` });
    await detachAll();

    // 5. Triggers fire after commit only.
    const listener = await publish('after-commit listener', [{ type: 'Crest.Workflows.CrestTrigger', id: 'on', version: 1, triggerKey: literal('flow.raised'), customProperties: { canStartWorkflow: true } }, writeLine('log', 'heard')], [edge('on', 'log')]);
    const raiser = await publish('raising host', [{ type: 'Crest.Workflows.RaiseTrigger', id: 'raise', version: 1, triggerKey: literal('flow.raised'), correlationId: literal(`unit-${stamp}`) }, hook('hook')], [edge('raise', 'hook')]);
    await attach(failing.id, true);
    const raisedFailed = await execute(raiser.id);
    await page.waitForTimeout(1500);
    const heardAfterFailure = (await call('GET', `${api}/workflow-instances?definitionId=${encodeURIComponent(listener.id)}&correlationId=${encodeURIComponent(`unit-${stamp}`)}`)).json?.items || [];
    await detachAll();
    const raisedOk = await execute(raiser.id);
    const heardAfterSuccess = await waitFor(async () => {
      const items = (await call('GET', `${api}/workflow-instances?definitionId=${encodeURIComponent(listener.id)}&correlationId=${encodeURIComponent(`unit-${stamp}`)}`)).json?.items || [];
      return items.length > 0 ? items : null;
    });
    results.push({ name: 'a-trigger-raised-in-a-failed-unit-never-fires-and-fires-after-a-committed-one', pass: raisedFailed.status === 500 && heardAfterFailure.length === 0 && raisedOk.ok && heardAfterSuccess?.length === 1, message: `failedRun=${raisedFailed.status} okRun=${raisedOk.status} afterFailure=${heardAfterFailure.length} afterSuccess=${heardAfterSuccess?.length ?? 0}` });

    const hooksList = await call('GET', hooksApi);
    results.push({ name: 'hooks-api-lists-slots-with-attachments', pass: hooksList.ok && Array.isArray(hooksList.json) && hooksList.json.some(s => s.slot?.key === 'flow.hook'), message: `HTTP ${hooksList.status}` });
  } finally {
    await detachAll();
    for (const id of created.items) await call('DELETE', `${content}/${encodeURIComponent(id)}`).catch(() => {});
    for (const id of created.definitions) await call('DELETE', `${api}/workflow-definitions/${encodeURIComponent(id)}`).catch(() => {});
  }

  return results;
};
