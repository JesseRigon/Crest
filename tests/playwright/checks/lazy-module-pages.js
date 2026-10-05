const { createInstance } = require('../harness/instance');
const { loginAsAdmin } = require('../harness/auth');

// Module pages load on demand (Crest.LazyModules, in the host's WASM entry): a fresh
// browser on the dashboard downloads no module page library except one that registers JS
// components at startup; opening a module's page downloads that module and renders it.
// Crest's own opt-in module (Content Part Lists) is the subject, so this holds in any host.
module.exports = async function run(page, ctx) {
  const results = [];
  const { browser, page: fresh } = await createInstance();
  try {
    const modules = [];
    fresh.on('response', async response => {
      const url = response.url();
      if (!/\/_framework\/.*\.wasm(\?|$)/.test(url)) return;
      if (/\.BlazorWasm\.[^/]*\.wasm/.test(url)) modules.push(url.split('/').pop().split('.').slice(0, -2).join('.'));
    });

    await loginAsAdmin(fresh, ctx.baseUrl);
    await fresh.goto(`${ctx.baseUrl}/Admin/Dashboard`, { waitUntil: 'networkidle' });
    const atStart = [...new Set(modules)];
    results.push({
      name: 'dashboard-loads-no-lazy-module-page-library',
      pass: !atStart.includes('Crest.ContentPartLists.BlazorWasm'),
      message: `module libraries at start: ${atStart.join(', ') || 'none'}`,
    });

    await fresh.goto(`${ctx.baseUrl}/Admin/ContentTypes/ContentPartLists`, { waitUntil: 'networkidle' });
    const rendered = await fresh.locator('[data-testid="content-part-lists-page"]').waitFor({ timeout: 30000 }).then(() => true, () => false);
    const loaded = modules.includes('Crest.ContentPartLists.BlazorWasm');
    const onLegacyFrame = await fresh.locator('iframe.legacy-admin-frame').count();
    results.push({ name: 'opening-a-module-page-loads-its-module', pass: loaded && rendered && onLegacyFrame === 0, message: `loaded=${loaded} rendered=${rendered} legacyFrame=${onLegacyFrame}` });
  } finally {
    await browser.close().catch(() => {});
  }

  return results;
};
