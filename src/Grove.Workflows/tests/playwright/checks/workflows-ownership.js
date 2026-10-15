const { fetchAntiforgeryToken } = require('../../../../../tests/playwright/harness/antiforgery');
const { ensureTestUser } = require('../../../../../tests/playwright/harness/testUsers');
const { loginAsUser } = require('../../../../../tests/playwright/harness/auth');

// Ownership tiers, the permission set, per-flow access lists and the extension-point
// triggers, against Grove's own fixtures (workflows-fixtures-enable runs first):
//  - samples.content-published is a system flow: read-only in the API, and the admin (the
//    super user) cannot save, publish, retract or delete it;
//  - samples.template is a shipped template: the first save forks it, the registry says so,
//    a reset re-imports the shipped version and clears the mark; it cannot be deleted;
//  - a WorkflowEditor (View + Edit + Publish + Run; the roles ship with the feature) creates
//    and publishes a tenant flow, cannot manage connections and cannot edit a shipped flow; a
//    WorkflowViewer reads and changes nothing; a definition's Edit/Run lists veto the editor
//    and never the super user;
//  - the payload filter on sample.content-published: one node runs for the sample role and
//    is skipped for a base party;
//  - Raise trigger: a flow raises flow.raised, another flow hooks it.
// Cleans up what it creates; the shipped flow is reset to shipped.
module.exports = async function run(page, ctx) {
  const results = [];
  const api = `${ctx.baseUrl}/grove-workflows/api`;
  const registryApi = `${ctx.baseUrl}/api/grove/workflows/registry`;
  const definitionsApi = `${ctx.baseUrl}/api/grove/workflows/definitions`;
  const content = `${ctx.baseUrl}/api/grove/content-items`;
  const created = { definitions: [], items: [] };
  const stamp = Date.now();
  let editorContext = null;
  let viewerContext = null;

  async function callOn(target, method, url, body) {
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
  }
  const call = (method, url, body) => callOn(page, method, url, body);

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
  const flowModel = (name, activities, connections) => ({
    name: `Playwright ${name} ${stamp}`, variables: [], inputs: [], outputs: [], outcomes: [], customProperties: {}, options: {},
    root: { type: 'Grove.Workflows.Flowchart', id: 'flow', version: 1, activities, connections },
  });
  const trigger = (id, triggerKey, extra = {}) => ({ type: 'Grove.Workflows.RegistryTrigger', id, version: 1, triggerKey: literal(triggerKey), customProperties: { canStartWorkflow: true }, ...extra });
  const writeLine = (id, text) => ({ type: 'Grove.Workflows.WriteLine', id, version: 1, text: literal(text) });
  const edge = (from, to, port = 'Done') => ({ source: { activity: from, port }, target: { activity: to, port: 'In' } });
  async function registryFlow(key) {
    const registry = await call('GET', registryApi);
    return (registry.json?.flows || []).find(f => f.key === key) || null;
  }
  async function instancesOf(definitionId, correlationId) {
    const query = correlationId ? `&correlationId=${encodeURIComponent(correlationId)}` : '';
    const response = await call('GET', `${api}/workflow-instances?definitionId=${encodeURIComponent(definitionId)}${query}`);
    return response.json?.items || [];
  }
  async function journalOf(instanceId) {
    const journal = await call('GET', `${api}/workflow-instances/${encodeURIComponent(instanceId)}/journal`);
    return journal.json?.items || [];
  }

  try {
    // 1. Tiers as the registry reports them.
    const sample = await registryFlow('samples.content-published');
    const shipped = await registryFlow('samples.template');
    results.push({ name: 'registry-reports-ownership-tiers', pass: sample?.ownership === 'system' && sample.published === true && sample.installedVersion === 1 && sample.forked === false && shipped?.ownership === 'shipped' && shipped.forked === false && shipped.outdated === false, message: JSON.stringify([sample, shipped]).slice(0, 300) });

    // 2. The system flow: read-only in the API, and no user changes it - not the admin.
    if (sample?.definitionId) {
      const definition = await call('GET', `${api}/workflow-definitions/${encodeURIComponent(sample.definitionId)}`);
      const links = (definition.json?.links || []).map(l => l.rel);
      results.push({ name: 'system-flow-is-read-only-in-the-api', pass: definition.ok && definition.json?.isReadonly === true && !links.includes('publish') && !links.includes('delete'), message: `isReadonly=${definition.json?.isReadonly} links=${links.join(',')}` });

      const save = await call('POST', `${api}/workflow-definitions`, { model: { ...definition.json, description: 'tampered' }, publish: false });
      const publish = await call('POST', `${api}/workflow-definitions/${encodeURIComponent(sample.definitionId)}/publish`, {});
      const retract = await call('POST', `${api}/workflow-definitions/${encodeURIComponent(sample.definitionId)}/retract`, {});
      const remove = await call('DELETE', `${api}/workflow-definitions/${encodeURIComponent(sample.definitionId)}`);
      // The save is refused by the store's guard before the draft read (with the reason);
      // the engine's own endpoints refuse the rest for a read-only definition (no body).
      results.push({ name: 'system-flow-refuses-save-publish-retract-delete-for-the-super-user', pass: [save, publish, retract, remove].every(r => r.status === 403) && /system workflow/.test(save.text), message: `save=${save.status} publish=${publish.status} retract=${retract.status} delete=${remove.status} ${save.text.slice(0, 120)}` });
      const after = await registryFlow('samples.content-published');
      results.push({ name: 'system-flow-is-untouched', pass: after?.published === true && after.forked === false && after.definitionId === sample.definitionId, message: JSON.stringify(after) });
    }

    // 3. The shipped flow: forks on first save, resets, never deletes.
    if (shipped?.definitionId) {
      const definition = await call('GET', `${api}/workflow-definitions/${encodeURIComponent(shipped.definitionId)}`);
      const versionsBefore = (await call('GET', `${api}/workflow-definitions/${encodeURIComponent(shipped.definitionId)}/versions`)).json;
      const countBefore = (versionsBefore?.items || versionsBefore || []).length;
      const save = await call('POST', `${api}/workflow-definitions`, { model: { ...definition.json, description: `forked by playwright ${stamp}`, customProperties: { ...(definition.json?.customProperties || {}), 'Grove.Ownership': 'tenant' } }, publish: false });
      const forked = await registryFlow('samples.template');
      results.push({ name: 'shipped-flow-forks-on-first-save-and-keeps-its-tier', pass: save.ok && forked?.forked === true && forked.ownership === 'shipped' && forked.definitionId === shipped.definitionId, message: `save=${save.status} ${JSON.stringify(forked)}` });

      const remove = await call('DELETE', `${api}/workflow-definitions/${encodeURIComponent(shipped.definitionId)}`);
      results.push({ name: 'shipped-flow-refuses-delete', pass: remove.status === 403, message: `delete=${remove.status} ${remove.text.slice(0, 120)}` });

      const reset = await call('POST', `${registryApi}/flows/${encodeURIComponent(shipped.key)}/reset`, {});
      const afterReset = await registryFlow('samples.template');
      const versionsAfter = (await call('GET', `${api}/workflow-definitions/${encodeURIComponent(shipped.definitionId)}/versions`)).json;
      const countAfter = (versionsAfter?.items || versionsAfter || []).length;
      const latest = await call('GET', `${api}/workflow-definitions/${encodeURIComponent(shipped.definitionId)}`);
      // A draft that was never published has one version, and the fork and the reset both
      // land on it; a published copy would get a new version (the tenant's edit kept in history).
      results.push({ name: 'shipped-flow-resets-to-the-shipped-version', pass: reset.ok && afterReset?.forked === false && afterReset.published === false && afterReset.definitionId === shipped.definitionId && countAfter >= countBefore && !/forked by playwright/.test(latest.json?.description || ''), message: `reset=${reset.status} versions ${countBefore}->${countAfter} description=${JSON.stringify(latest.json?.description)} ${JSON.stringify(afterReset)}` });

      const systemReset = await call('POST', `${registryApi}/flows/${encodeURIComponent('samples.content-published')}/reset`, {});
      results.push({ name: 'system-flow-has-nothing-to-reset', pass: systemReset.status === 400, message: `reset=${systemReset.status}` });
    }

    // 4. The permission set, as the shipped roles see it.
    const editor = await ensureTestUser(page, ctx.baseUrl, 'wf', { roles: ['WorkflowEditor'] });
    editorContext = await page.context().browser().newContext({ baseURL: ctx.baseUrl });
    const editorPage = await editorContext.newPage();
    await loginAsUser(editorPage, ctx.baseUrl, editor);
    const asEditor = (method, url, body) => callOn(editorPage, method, url, body);

    const viewer = await ensureTestUser(page, ctx.baseUrl, 'wfv', { roles: ['WorkflowViewer'] });
    viewerContext = await page.context().browser().newContext({ baseURL: ctx.baseUrl });
    const viewerPage = await viewerContext.newPage();
    await loginAsUser(viewerPage, ctx.baseUrl, viewer);
    const asViewer = (method, url, body) => callOn(viewerPage, method, url, body);
    const viewerList = await asViewer('GET', `${api}/workflow-definitions?page=0&pageSize=5`);
    const viewerInstances = await asViewer('GET', `${api}/workflow-instances?page=0&pageSize=5`);
    const viewerSave = await asViewer('POST', `${api}/workflow-definitions`, { model: flowModel('viewer flow', [writeLine('log', 'viewer')], []), publish: false });
    const viewerRegistry = await asViewer('GET', registryApi);
    const viewerConnections = await asViewer('GET', `${ctx.baseUrl}/api/grove/workflows/connections`);
    if (viewerSave.json?.workflowDefinition?.definitionId) created.definitions.push(viewerSave.json.workflowDefinition.definitionId);
    results.push({ name: 'viewer-reads-and-changes-nothing', pass: viewerList.ok && viewerInstances.ok && viewerRegistry.ok && viewerSave.status === 403 && viewerConnections.status === 403, message: `definitions=${viewerList.status} instances=${viewerInstances.status} registry=${viewerRegistry.status} save=${viewerSave.status} connections=${viewerConnections.status}` });
    if (sample?.definitionId) {
      const viewerDefinition = await asViewer('GET', `${api}/workflow-definitions/${encodeURIComponent(shipped?.definitionId || sample.definitionId)}`);
      const viewerLinks = (viewerDefinition.json?.links || []).map(l => l.rel);
      results.push({ name: 'viewer-sees-no-write-links', pass: viewerDefinition.ok && !viewerLinks.includes('publish') && !viewerLinks.includes('delete'), message: `links=${viewerLinks.join(',')}` });
    }

    const editorList = await asEditor('GET', `${api}/workflow-definitions?page=0&pageSize=5`);
    const editorSaved = await asEditor('POST', `${api}/workflow-definitions`, { model: flowModel('editor flow', [trigger('on', 'sample.content-published', { payloadFilter: literal('ContentType = NoSuchType') }), writeLine('log', 'editor')], [edge('on', 'log')]), publish: false });
    const editorFlowId = editorSaved.json?.workflowDefinition?.definitionId;
    if (editorFlowId) created.definitions.push(editorFlowId);
    const editorPublished = editorFlowId ? await asEditor('POST', `${api}/workflow-definitions/${encodeURIComponent(editorFlowId)}/publish`, {}) : { status: 0 };
    results.push({ name: 'editor-can-view-create-and-publish-tenant-flows', pass: editorList.ok && editorSaved.ok && Boolean(editorFlowId) && editorPublished.ok, message: `list=${editorList.status} save=${editorSaved.status} publish=${editorPublished.status} ${editorSaved.text.slice(0, 100)}` });

    const editorConnections = await asEditor('GET', `${ctx.baseUrl}/api/grove/workflows/connections`);
    const editorRegistry = await asEditor('GET', registryApi);
    results.push({ name: 'editor-cannot-manage-connections-but-reads-the-registry', pass: editorConnections.status === 403 && editorRegistry.ok, message: `connections=${editorConnections.status} registry=${editorRegistry.status}` });

    if (shipped?.definitionId) {
      const shippedDefinition = await asEditor('GET', `${api}/workflow-definitions/${encodeURIComponent(shipped.definitionId)}`);
      const shippedLinks = (shippedDefinition.json?.links || []).map(l => l.rel);
      const editorShippedSave = await asEditor('POST', `${api}/workflow-definitions`, { model: { ...shippedDefinition.json, description: 'editor edit' }, publish: false });
      const stillUnforked = await registryFlow('samples.template');
      results.push({ name: 'editor-cannot-edit-a-shipped-flow', pass: shippedDefinition.ok && !shippedLinks.includes('delete') && editorShippedSave.status === 403 && stillUnforked?.forked === false, message: `links=${shippedLinks.join(',')} save=${editorShippedSave.status} ${editorShippedSave.text.slice(0, 120)}` });
    }

    // 5. Per-flow lists: the Editor's own flow, narrowed to a role they lack; the super user is never narrowed.
    if (editorFlowId) {
      const setAccess = await call('PUT', `${definitionsApi}/${encodeURIComponent(editorFlowId)}/access`, { edit: ['Finance'], run: ['Finance'] });
      const readAccess = await asEditor('GET', `${definitionsApi}/${encodeURIComponent(editorFlowId)}/access`);
      const editorDefinition = await asEditor('GET', `${api}/workflow-definitions/${encodeURIComponent(editorFlowId)}`);
      const editorLinks = (editorDefinition.json?.links || []).map(l => l.rel);
      const editorSave = await asEditor('POST', `${api}/workflow-definitions`, { model: { ...editorDefinition.json, description: 'narrowed' }, publish: false });
      const editorRun = await asEditor('POST', `${api}/workflow-definitions/${encodeURIComponent(editorFlowId)}/execute`, {});
      const editorSetAccess = await asEditor('PUT', `${definitionsApi}/${encodeURIComponent(editorFlowId)}/access`, { edit: [], run: [] });
      results.push({ name: 'access-lists-veto-an-unnamed-editor', pass: setAccess.ok && readAccess.ok && readAccess.json?.edit?.includes('Finance') && !editorLinks.includes('publish') && editorSave.status === 403 && editorRun.status === 403 && editorSetAccess.status === 403, message: `set=${setAccess.status} read=${readAccess.status} links=${editorLinks.join(',')} save=${editorSave.status} run=${editorRun.status} setByEditor=${editorSetAccess.status} ${editorSave.text.slice(0, 100)}` });

      const adminDefinition = await call('GET', `${api}/workflow-definitions/${encodeURIComponent(editorFlowId)}`);
      const adminSave = await call('POST', `${api}/workflow-definitions`, { model: { ...adminDefinition.json, description: 'admin still can' }, publish: true });
      const adminRun = await call('POST', `${api}/workflow-definitions/${encodeURIComponent(editorFlowId)}/execute`, {});
      const kept = await call('GET', `${definitionsApi}/${encodeURIComponent(editorFlowId)}/access`);
      results.push({ name: 'the-super-user-is-never-narrowed-and-a-save-keeps-the-lists', pass: adminSave.ok && adminRun.ok && kept.json?.edit?.includes('Finance') && kept.json?.run?.includes('Finance'), message: `save=${adminSave.status} run=${adminRun.status} access=${JSON.stringify(kept.json)}` });

      const named = await call('PUT', `${definitionsApi}/${encodeURIComponent(editorFlowId)}/access`, { edit: ['Finance', editor.username], run: [] });
      const namedDefinition = await asEditor('GET', `${api}/workflow-definitions/${encodeURIComponent(editorFlowId)}`);
      const namedSave = await asEditor('POST', `${api}/workflow-definitions`, { model: { ...namedDefinition.json, description: 'named user edits' }, publish: false });
      const namedRun = await asEditor('POST', `${api}/workflow-definitions/${encodeURIComponent(editorFlowId)}/execute`, {});
      results.push({ name: 'a-named-user-is-the-per-user-override', pass: named.ok && namedSave.ok && namedRun.ok, message: `set=${named.status} save=${namedSave.status} run=${namedRun.status} ${namedSave.text.slice(0, 100)}` });
    }

    // 6. Payload filter on sample.content-published, and Raise trigger.
    const filtered = await call('POST', `${api}/workflow-definitions`, { model: flowModel('one type', [trigger('on', 'sample.content-published', { payloadFilter: literal('ContentType = SampleRole') }), writeLine('log', 'sample role')], [edge('on', 'log')]), publish: true });
    const filteredId = filtered.json?.workflowDefinition?.definitionId;
    if (filteredId) created.definitions.push(filteredId);
    const raiser = await call('POST', `${api}/workflow-definitions`, { model: flowModel('raiser', [trigger('on', 'sample.content-published', { payloadFilter: literal('ContentType = SampleRole') }), { type: 'Grove.Workflows.RaiseTrigger', id: 'raise', version: 1, triggerKey: literal('flow.raised') }], [edge('on', 'raise')]), publish: true });
    const raiserId = raiser.json?.workflowDefinition?.definitionId;
    if (raiserId) created.definitions.push(raiserId);
    const hook = await call('POST', `${api}/workflow-definitions`, { model: flowModel('hook', [trigger('on', 'flow.raised'), writeLine('log', 'hooked')], [edge('on', 'log')]), publish: true });
    const hookId = hook.json?.workflowDefinition?.definitionId;
    if (hookId) created.definitions.push(hookId);
    results.push({ name: 'filter-raiser-and-hook-flows-publish', pass: Boolean(filteredId && raiserId && hookId), message: `${filtered.status} ${raiser.status} ${hook.status} ${[filtered, raiser, hook].map(r => r.text.slice(0, 80)).join(' | ')}` });

    // A base party and a sample role on it: both publish, both raise the sample trigger.
    const person = await call('POST', content, { contentType: 'Person', displayText: `Playwright ownership person ${stamp}`, publish: true });
    const personId = person.json?.contentItemId;
    if (personId) created.items.push(personId);
    const role = await call('POST', content, { contentType: 'SampleRole', displayText: `Playwright ownership role ${stamp}`, publish: true, content: { SampleRole: { Party: { ContentItemIds: [personId] } } } });
    const roleId = role.json?.contentItemId;
    if (roleId) created.items.push(roleId);
    results.push({ name: 'sample-items-publish', pass: person.ok && role.ok, message: `person=${person.status} role=${role.status}` });

    if (filteredId && personId && roleId) {
      const ranFor = async correlationId => {
        const runs = await waitFor(async () => {
          const items = await instancesOf(filteredId, correlationId);
          return items.length > 0 && items.every(i => i.status === 'Finished') ? items : null;
        });
        const journal = runs?.[0] ? await journalOf(runs[0].id) : [];
        return { runs: runs?.length ?? 0, ran: journal.some(e => e.activityType === 'Grove.Workflows.WriteLine' && e.eventName === 'Completed') };
      };
      const forRole = await ranFor(roleId);
      const forPerson = await ranFor(personId);
      results.push({ name: 'payload-filter-runs-the-flow-for-the-matching-type-only', pass: forRole.runs === 1 && forRole.ran && forPerson.runs === 1 && !forPerson.ran, message: `role=${JSON.stringify(forRole)} person=${JSON.stringify(forPerson)}` });
    }

    if (raiserId && hookId && roleId) {
      const hooked = await waitFor(async () => {
        const items = await instancesOf(hookId, roleId);
        return items.length > 0 && items.every(i => i.status === 'Finished') ? items : null;
      });
      const raiserRuns = await instancesOf(raiserId, roleId);
      const raiserJournal = raiserRuns[0] ? await journalOf(raiserRuns[0].id) : [];
      const raised = raiserJournal.some(e => e.activityType === 'Grove.Workflows.RaiseTrigger' && e.eventName === 'Completed');
      const hookedJournal = hooked?.[0] ? await journalOf(hooked[0].id) : [];
      results.push({ name: 'raise-trigger-starts-the-hooked-flow-for-the-same-object', pass: raiserRuns.length === 1 && raised && hooked?.length === 1 && hookedJournal.some(e => e.activityType === 'Grove.Workflows.WriteLine' && e.eventName === 'Completed'), message: `raiser=${raiserRuns.length} raised=${raised} hooked=${hooked?.length ?? 0}` });
    }

    const registry = await call('GET', registryApi);
    const triggerKeys = (registry.json?.triggers || []).map(t => t.key);
    results.push({ name: 'registry-lists-the-sample-and-flow-raised-triggers', pass: triggerKeys.includes('sample.content-published') && triggerKeys.includes('flow.raised'), message: triggerKeys.join(',') });
    // Sync is idempotent: run by hand it installs nothing new and answers Keep for every shipped flow.
    const sync = await call('POST', `${ctx.baseUrl}/api/grove/workflows/registry/sync`, {});
    const actions = Array.isArray(sync.json) ? sync.json : [];
    results.push({ name: 'registry-sync-is-idempotent', pass: sync.ok && actions.length > 0 && actions.every(a => a.action === 'Keep'), message: `HTTP ${sync.status} ${JSON.stringify(actions).slice(0, 200)}` });

  } finally {
    for (const id of created.definitions) await call('DELETE', `${api}/workflow-definitions/${encodeURIComponent(id)}`).catch(() => {});
    for (const id of created.items.reverse()) await call('DELETE', `${content}/${encodeURIComponent(id)}`).catch(() => {});
    if (editorContext) await editorContext.close().catch(() => {});
    if (viewerContext) await viewerContext.close().catch(() => {});
  }

  return results;
};
