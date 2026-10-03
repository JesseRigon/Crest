const { fetchAntiforgeryToken } = require('../../../../../OrchardCore.Crest/tests/playwright/harness/antiforgery');
const { createInstance } = require('../../../../../OrchardCore.Crest/tests/playwright/harness/instance');
const { loginAsAdmin } = require('../../../../../OrchardCore.Crest/tests/playwright/harness/auth');

// The designer in the Crest shell (plans/workflows.md, 0c): the forked Studio, mounted as
// a Crest client module, in the Crest admin chrome, over the tenant's engine API with the
// Orchard cookie. The pass condition from the plan, driven through the real UI: open an
// existing definition, add a node from the palette, connect it, save, publish; run it; the
// instance page shows the journal. The API confirms what the designer did.
module.exports = async function run(page, ctx) {
  const results = [];
  const api = `${ctx.baseUrl}/crest-workflows/api`;
  const created = { definitions: [], connections: [] };
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

  const literal = value => ({ typeName: 'String', expression: { type: 'Literal', value } });
  const center = rect => ({ x: rect.x + rect.width / 2, y: rect.y + rect.height / 2 });
  const latest = async id => (await call('GET', `${api}/workflow-definitions/by-definition-id/${encodeURIComponent(id)}?versionOptions=Latest`)).json;

  try {
    // 0. Studio is loaded on demand (its assemblies are lazy, its services in its own
    //    container): a fresh browser on an ordinary admin page downloads none of it; the
    //    first workflow page does. (Assemblies only: a Debug build fetches every pdb at start.)
    const fresh = await createInstance();
    try {
      const downloads = [];
      fresh.page.on('request', request => { if (/\/_framework\/(MudBlazor|Crest\.Workflows\.Studio\.Host)\.[^/]*\.wasm/.test(request.url())) downloads.push(request.url()); });
      await loginAsAdmin(fresh.page, ctx.baseUrl);
      await fresh.page.goto(`${ctx.baseUrl}/Admin/Dashboard`, { waitUntil: 'networkidle' });
      const onDashboard = downloads.length;
      await fresh.page.goto(`${ctx.baseUrl}/Admin/workflows/definitions`, { waitUntil: 'networkidle' });
      const ready = await fresh.page.locator('[data-testid="workflows-studio"][data-ready="true"]').waitFor({ timeout: 60000 }).then(() => true, () => false);
      results.push({ name: 'studio-downloads-only-when-a-workflow-page-opens', pass: onDashboard === 0 && ready && downloads.length === 2, message: `dashboard=${onDashboard} afterWorkflowPage=${downloads.length} ready=${ready} ${downloads.map(u => u.split("/").pop()).join(",")}` });
    } finally {
      await fresh.browser.close().catch(() => {});
    }

    // An existing definition: one Write Line, placed left of centre.
    const name = `Playwright designer ${stamp}`;
    const saved = await call('POST', `${api}/workflow-definitions`, {
      model: {
        name, variables: [], inputs: [], outputs: [], outcomes: [], customProperties: {}, options: {},
        root: { type: 'Crest.Workflows.Flowchart', id: 'flow', version: 1, connections: [], activities: [
          { type: 'Crest.Workflows.WriteLine', id: 'first', version: 1, text: literal('first'), metadata: { designer: { position: { x: -200, y: 0 } } } } ] },
      },
      publish: false,
    });
    const definitionId = saved.json?.workflowDefinition?.definitionId;
    if (definitionId) created.definitions.push(definitionId);

    // 1. The definitions list renders inside the Crest admin - first reached in-app from
    //    another admin page, as a menu link does (that page may be running in a server
    //    circuit; the Studio pages hand themselves to the WASM client). The link is one the
    //    router intercepts like any menu link; the menu's own expand/collapse state is left
    //    alone (earlier checks leave it in different states).
    // Dashboard, not the bare admin path: that one redirects client-side, late enough to
    // hijack the navigation below.
    await page.goto(`${ctx.baseUrl}/Admin/Dashboard`, { waitUntil: 'networkidle' });
    await page.evaluate(() => {
      const link = document.createElement('a');
      link.href = 'workflows/definitions';
      link.textContent = 'workflows';
      document.body.appendChild(link);
      link.click();
    });
    // Studio adds its paging to the query string.
    const viaMenuUrl = await page.waitForURL(/\/workflows\/definitions(\?|$)/i, { timeout: 20000 }).then(() => true, () => false);
    const viaMenuReady = viaMenuUrl && await page.locator('[data-testid="workflows-studio"][data-ready="true"]').waitFor({ timeout: 60000 }).then(() => true, () => false);
    const hostState = await page.locator('[data-testid="workflows-studio"]').first().innerText().catch(() => '(no host)');
    results.push({ name: 'in-app-navigation-opens-the-studio', pass: viaMenuUrl && viaMenuReady, message: `url=${page.url()} ready=${viaMenuReady} host=${hostState.slice(0, 120).replace(/\s+/g, ' ')}` });

    if (!viaMenuReady) {
      await page.goto(`${ctx.baseUrl}/Admin/workflows/definitions`, { waitUntil: 'networkidle' });
    }
    const listReady = await page.locator('[data-testid="workflows-studio"][data-ready="true"]').waitFor({ timeout: 60000 }).then(() => true, () => false);
    // The list pages; search it for this run's definition.
    if (listReady) await page.getByPlaceholder(/Search/i).first().fill(name).catch(() => {});
    const listed = listReady && await page.getByText(name).first().waitFor({ timeout: 15000 }).then(() => true, () => false);
    const chrome = await page.locator('.primary-nav-menu').count();
    results.push({ name: 'definitions-list-renders-in-the-crest-shell', pass: listReady && listed && chrome > 0, message: `ready=${listReady} listed=${listed} crestNav=${chrome}` });

    await page.goto(`${ctx.baseUrl}/Admin/workflows/definitions/${definitionId}/edit`, { waitUntil: 'networkidle' });
    const designerReady = await page.locator('.x6-node').first().waitFor({ timeout: 60000 }).then(() => true, () => false);
    const palette = [];
    // The palette is an accordion (one category open at a time): open each, then look.
    for (const [category, label] of [['Connectors', 'Call connector'], ['Approvals', 'Request approval'], ['Accounting', 'Convert transaction']]) {
      await page.locator('.mud-expand-panel-header', { hasText: category }).first().click();
      const item = page.locator('div[draggable="true"]', { hasText: label }).first();
      if (await item.waitFor({ state: 'visible', timeout: 5000 }).then(() => true, () => false)) palette.push(label);
    }
    results.push({ name: 'designer-opens-the-definition-with-crest-activities', pass: designerReady && palette.length === 3, message: `canvas=${designerReady} palette=${palette.join(',')}` });

    // 2. Add a node from the palette and connect it.
    await page.locator('.mud-expand-panel-header', { hasText: 'Console' }).first().click();
    const writeLine = page.locator('div[draggable="true"]', { hasText: /^\s*Write Line\s*$/ }).first();
    await writeLine.waitFor({ state: 'visible', timeout: 10000 });
    const graph = page.locator('.x6-graph').first();
    const graphBox = await graph.boundingBox();
    await writeLine.dragTo(graph, { targetPosition: { x: graphBox.width * 0.75, y: graphBox.height * 0.5 } });
    const twoNodes = await waitFor(async () => (await page.locator('.x6-node').count()) === 2, { timeout: 10000 });
    results.push({ name: 'dragging-from-the-palette-adds-a-node', pass: Boolean(twoNodes), message: `nodes=${await page.locator('.x6-node').count()}` });

    const ports = await page.locator('.x6-port-body').evaluateAll(els => els.map(e => ({ port: e.getAttribute('port'), rect: e.getBoundingClientRect().toJSON() })));
    const from = ports.find(p => p.port === 'Done');
    const to = ports.filter(p => p.port === 'In')[1];
    if (from && to) {
      const a = center(from.rect);
      const b = center(to.rect);
      await page.mouse.move(a.x, a.y);
      await page.mouse.down();
      await page.mouse.move((a.x + b.x) / 2, (a.y + b.y) / 2, { steps: 5 });
      await page.mouse.move(b.x, b.y, { steps: 5 });
      await page.mouse.up();
    }
    const edge = await waitFor(async () => (await page.locator('.x6-edge').count()) === 1, { timeout: 5000 });
    results.push({ name: 'dragging-between-ports-connects-the-nodes', pass: Boolean(edge), message: `edges=${await page.locator('.x6-edge').count()}` });

    // 3. Save, then publish, through the toolbar; the engine has both.
    await page.locator('[data-testid="workflow-save"]').click();
    const stored = await waitFor(async () => {
      const model = await latest(definitionId);
      return model?.root?.activities?.length === 2 && model?.root?.connections?.length === 1 ? model : null;
    });
    results.push({ name: 'save-stores-the-node-and-the-connection', pass: Boolean(stored), message: stored ? `version=${stored.version}` : 'the latest version does not have two activities and a connection' });

    await page.locator('[data-testid="workflow-publish"]').click();
    const published = await waitFor(async () => (await latest(definitionId))?.isPublished === true);
    results.push({ name: 'publish-publishes-the-definition', pass: Boolean(published), message: `published=${Boolean(published)}` });

    // 4. Run it from the designer; the instance page shows the journal.
    await page.locator('[data-testid="workflow-run"]').click();
    const onInstance = await page.waitForURL(/\/workflows\/instances\/[^/]+\/view/i, { timeout: 20000 }).then(() => true, () => false);
    const instanceId = onInstance ? page.url().match(/instances\/([^/]+)\/view/i)?.[1] : null;
    const instance = instanceId ? await waitFor(async () => {
      const response = await call('GET', `${api}/workflow-instances/${encodeURIComponent(instanceId)}`);
      const status = response.json?.workflowState?.status ?? response.json?.status;
      return status === 'Finished' ? response.json : null;
    }) : null;
    results.push({ name: 'run-starts-an-instance-that-finishes', pass: Boolean(instance), message: `instancePage=${onInstance} id=${instanceId ?? '-'}` });

    const journalShown = onInstance && await page.getByText('Write Line').first().waitFor({ timeout: 20000 }).then(() => true, () => false);
    results.push({ name: 'instance-page-shows-the-journal', pass: journalShown, message: `journal=${journalShown}` });

    // 5. The Crest pages beside Studio: Connections lists the tenant's connections (a
    //    secret-bearing one shows no secret), the approval queue renders.
    const connectionKey = `pw-designer-${stamp}`;
    const secret = `designer-secret-${stamp}`;
    const connection = await call('POST', `${ctx.baseUrl}/api/crest/workflows/connections`, { key: connectionKey, displayName: `Designer check ${stamp}`, baseUrl: 'https://api.example.com/', authKind: 'bearer', secret });
    if (connection.ok) created.connections.push(connectionKey);
    await page.goto(`${ctx.baseUrl}/Admin/workflows/connections`, { waitUntil: 'networkidle' });
    const connectionsPage = await page.locator('[data-testid="workflow-connections"]').waitFor({ timeout: 30000 }).then(() => true, () => false);
    const connectionListed = connectionsPage && await page.getByText(`Designer check ${stamp}`).first().waitFor({ timeout: 15000 }).then(() => true, () => false);
    const secretShown = (await page.content()).includes(secret);
    results.push({ name: 'connections-page-lists-connections-without-secrets', pass: connectionsPage && connectionListed && !secretShown, message: `page=${connectionsPage} listed=${connectionListed} secretShown=${secretShown}` });

    await page.goto(`${ctx.baseUrl}/Admin/workflows/approvals`, { waitUntil: 'networkidle' });
    const approvalsPage = await page.locator('[data-testid="workflow-approvals"]').waitFor({ timeout: 30000 }).then(() => true, () => false);
    results.push({ name: 'approvals-page-renders', pass: approvalsPage, message: `page=${approvalsPage}` });
  } catch (error) {
    // Keep the checks that did run, and say where it stopped (the locator is in the call log).
    const shot = require('path').join(ctx.outputRoot || require('path').join(__dirname, '..', 'output'), 'new', 'workflows-designer-failure.png');
    await page.screenshot({ path: shot }).catch(() => {});
    results.push({ name: 'designer-run-completes', pass: false, message: `${error.message.split('\n').slice(0, 4).join(' | ')} (screenshot: ${shot})` });
  } finally {
    for (const id of created.definitions) await call('DELETE', `${api}/workflow-definitions/${encodeURIComponent(id)}`);
    for (const key of created.connections) await call('DELETE', `${ctx.baseUrl}/api/crest/workflows/connections/${encodeURIComponent(key)}`);
    // Leave the shared page where the next check expects an admin page, not on Studio.
    await page.goto(`${ctx.baseUrl}/Admin/Dashboard`, { waitUntil: 'networkidle' }).catch(() => {});
  }

  return results;
};
