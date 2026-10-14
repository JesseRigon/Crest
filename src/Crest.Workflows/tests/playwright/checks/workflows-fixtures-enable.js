// Turns on Crest's own workflow fixtures (Crest.Workflows.Samples: a content-published
// registry trigger, a system flow, a shipped template) and the sample party role
// (Crest.Parties.SampleRole), so the workflow checks after it need no host module. Enabling an
// enabled feature is idempotent. Runs before every other workflows-* check that uses them.
module.exports = async function run(page, ctx) {
  const results = [];
  const tokenResponse = await page.request.get(`${ctx.baseUrl}/api/crest/antiforgery/token`);
  const token = tokenResponse.ok() ? await tokenResponse.json() : {};
  const headers = { [token.headerName || 'RequestVerificationToken']: token.requestToken };

  for (const feature of ['Crest.Parties.SampleRole', 'Crest.Workflows.Samples']) {
    const enable = await page.request.post(`${ctx.baseUrl}/api/crest/features/${feature}/enable`, { headers });
    results.push({ name: `enable-${feature}`, pass: enable.ok(), message: `HTTP ${enable.status()}` });
  }

  // The registry syncs shipped flows on activation; give the reloaded shell a moment.
  let registry = null;
  for (let attempt = 0; attempt < 20; attempt++) {
    const response = await page.request.get(`${ctx.baseUrl}/api/crest/workflows/registry`);
    registry = response.ok() ? await response.json() : null;
    const keys = (registry?.flows || []).map(f => f.key);
    if (keys.includes('samples.content-published') && keys.includes('samples.template')) break;
    await page.waitForTimeout(500);
  }

  const triggers = (registry?.triggers || []).map(t => t.key);
  const flows = registry?.flows || [];
  const system = flows.find(f => f.key === 'samples.content-published');
  const shipped = flows.find(f => f.key === 'samples.template');
  results.push({ name: 'registry-lists-the-sample-trigger', pass: triggers.includes('sample.content-published'), message: triggers.join(',') });
  results.push({ name: 'registry-lists-the-sample-flows', pass: system?.ownership === 'system' && system.published === true && shipped?.ownership === 'shipped' && shipped.published === false, message: JSON.stringify([system, shipped]).slice(0, 300) });
  return results;
};
