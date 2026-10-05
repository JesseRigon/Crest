// Theme compatibility (plans/shells-and-themes.md › Theme compatibility): the themes API
// reports one section per shell and each theme's shell, Crest Blazor capability and what it
// would break; a breaking theme change is refused unless acknowledged; and the themes page
// walks that refusal through the two-step confirmation. Nothing here applies a breaking
// change - the tenant's themes are left exactly as found.
const { clickForEffect } = require('../harness/interactive');

module.exports = async function run(page, ctx) {
  const results = [];
  const check = (name, pass, message) => results.push({ name, pass, message });

  async function api(method, url) {
    const token = await page.evaluate(async () => {
      const response = await fetch('/api/crest/antiforgery/token', { credentials: 'include' });
      return response.json();
    });
    return page.evaluate(
      async ({ method, url, token }) => {
        const response = await fetch(url, {
          method,
          credentials: 'include',
          headers: { [token.headerName || 'RequestVerificationToken']: token.requestToken },
        });
        const text = await response.text();
        let json = null;
        try {
          json = text ? JSON.parse(text) : null;
        } catch {}
        return { ok: response.ok, status: response.status, json, text };
      },
      { method, url, token },
    );
  }

  await page.goto(`${ctx.baseUrl}/Admin/Themes`, { waitUntil: 'domcontentloaded' });

  const listed = await api('GET', '/api/crest/themes');
  const state = listed.json ?? { shells: [], themes: [] };
  const shells = state.shells.map((shell) => shell.shell);
  check('themes-api-lists-shell-sections', listed.ok && shells[0] === 'site' && shells[1] === 'admin', `HTTP ${listed.status} shells=${JSON.stringify(shells)}`);

  const currentAdmin = state.themes.find((theme) => theme.shell === 'admin' && theme.isCurrent);
  check('current-admin-theme-is-crest-blazor', currentAdmin?.isCrestBlazor === true && currentAdmin.incompatibilities.length === 0, JSON.stringify(currentAdmin && { id: currentAdmin.id, isCrestBlazor: currentAdmin.isCrestBlazor, incompatibilities: currentAdmin.incompatibilities }));

  // A member theme belongs to the member shell, never the site: selecting it must not make
  // it the public site's theme.
  const memberThemesOnSite = state.themes.filter((theme) => theme.shell === 'site' && theme.id === 'Crest.Member');
  check('member-theme-is-not-a-site-theme', memberThemesOnSite.length === 0, JSON.stringify(memberThemesOnSite.map((theme) => theme.id)));

  const features = await api('GET', '/api/crest/features');
  const flagged = (features.json ?? []).filter((feature) => feature.incompatibility);
  check('no-enabled-feature-is-incompatible', features.ok && flagged.length === 0, JSON.stringify(flagged.map((feature) => `${feature.id}: ${feature.incompatibility}`)));

  // A classic (non-Crest) admin theme would break every admin module.
  const classicAdmin = state.themes.find((theme) => theme.shell === 'admin' && !theme.isCrestBlazor);
  if (!classicAdmin) {
    check('classic-admin-theme-available', true, 'no non-Crest admin theme installed; breaking-change probes skipped');
    return results;
  }

  check('classic-admin-theme-predicts-breakage', classicAdmin.incompatibilities.length > 0, `${classicAdmin.id} breaks ${classicAdmin.incompatibilities.length}`);

  const refused = await api('POST', `/api/crest/themes/${encodeURIComponent(classicAdmin.id)}/current`);
  check(
    'breaking-theme-change-refused-without-acknowledgement',
    refused.status === 409 && refused.json?.incompatibilities?.length > 0,
    `HTTP ${refused.status} ${refused.text?.slice(0, 200)}`,
  );

  const resetRefused = await api('POST', '/api/crest/themes/reset-admin');
  check('breaking-reset-refused-without-acknowledgement', resetRefused.status === 409, `HTTP ${resetRefused.status}`);

  // The page: Use on the classic theme opens step one with the server's report, Continue
  // opens step two, and Apply stays disabled until the acknowledgement is ticked. Cancelled.
  const adminSection = page.locator('[data-testid="theme-section"][data-shell="admin"]');
  await adminSection.waitFor({ timeout: 20000 });
  const card = adminSection.locator(`[data-testid="theme-card"][data-theme-id="${classicAdmin.id}"]`);
  const dialog = page.locator('[data-testid="theme-breaking-dialog"]');
  let stepOne = false;
  let stepTwoGated = false;
  let stepTwoUnlocks = false;
  let failure = '';
  try {
    // The prerendered button is visible before a runtime attaches its handler.
    await clickForEffect(card.locator('[data-testid="theme-use"]').first(), dialog, { attempts: 6, effectTimeout: 5000 });
    stepOne = (await dialog.getAttribute('data-step')) === '1' && (await dialog.locator('[data-testid="theme-breaking-features"] li').count()) > 0;
    await dialog.locator('[data-testid="theme-breaking-continue"]').click();
    await page.locator('[data-testid="theme-breaking-dialog"][data-step="2"]').waitFor({ timeout: 5000 });
    const confirm = page.locator('[data-testid="theme-breaking-confirm"]');
    stepTwoGated = await confirm.isDisabled();
    await page.locator('[data-testid="theme-breaking-acknowledge"]').check();
    stepTwoUnlocks = await confirm.isEnabled();
    await page.getByRole('button', { name: 'Cancel' }).click();
  } catch (error) {
    failure = error.message.split('\n')[0];
  }
  check('themes-page-two-step-confirmation', stepOne && stepTwoGated && stepTwoUnlocks, `stepOne=${stepOne} gated=${stepTwoGated} unlocks=${stepTwoUnlocks} ${failure}`);

  const after = await api('GET', '/api/crest/themes');
  const adminAfter = after.json?.themes?.find((theme) => theme.shell === 'admin' && theme.isCurrent);
  check('admin-theme-unchanged', adminAfter?.id === currentAdmin?.id, `before=${currentAdmin?.id} after=${adminAfter?.id}`);

  return results;
};
