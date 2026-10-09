// Legacy (stock Razor) admin pages render inside an iframe wrapper, never nesting a second
// Crest shell. Verifies the initial frame, an in-frame link navigation, and a location.href
// reassignment all preserve legacy-frame=1 and stay free of nested chrome. Uses the stock
// Deployment plans pages, which have no Blazor replacement yet.
module.exports = async function run(page, ctx) {
  const listPath = '/Admin/DeploymentPlan/Index';

  async function assertNoNestedCrestShell(frame) {
    const nestedFrames = await frame.locator('iframe.legacy-admin-frame').count();
    const nestedFrameWrapper = await frame.locator('.legacy-admin-frame-page').count();
    const nestedPrimaryNavMenu = await frame.locator('.primary-nav-menu').count();
    return nestedFrames === 0 && nestedFrameWrapper === 0 && nestedPrimaryNavMenu === 0;
  }

  const response = await page.goto(`${ctx.baseUrl}${listPath}`, { waitUntil: 'networkidle' });
  if (!response || response.status() >= 400) {
    return [{ name: 'deployment-plans-page-loads', pass: false, message: `status=${response?.status() ?? 'no response'}` }];
  }

  const frameElement = page.locator('iframe.legacy-admin-frame').first();
  await frameElement.waitFor({ timeout: 20000 });
  const frame = await (await frameElement.elementHandle()).contentFrame();
  if (!frame) {
    return [{ name: 'legacy-frame-available', pass: false, message: 'Legacy frame was not available.' }];
  }

  await frame.locator('body.crest-legacy-frame').waitFor({ timeout: 20000 });
  const initialNoNesting = await assertNoNestedCrestShell(frame);
  const initialFrameUrl = new URL(frame.url());
  const initialKeepsLegacyParam = initialFrameUrl.searchParams.get('legacy-frame') === '1';

  const createLink = frame.locator('a[href*="/Admin/DeploymentPlan/Create"]').first();
  await createLink.waitFor({ timeout: 20000 });
  const createHref = await createLink.getAttribute('href');
  if (!createHref) {
    return [{ name: 'create-link-has-href', pass: false, message: 'The create link did not include an href.' }];
  }

  // Navigate the way stock script does: a plain href without the legacy-frame parameter.
  await Promise.all([
    frame.waitForURL(/\/Admin\/DeploymentPlan\/Create/i, { timeout: 20000 }),
    frame.evaluate(href => {
      const url = new URL(href, window.location.href);
      url.searchParams.delete('legacy-frame');
      window.location.href = url.pathname + url.search + url.hash;
    }, createHref),
  ]);
  await frame.waitForLoadState('networkidle').catch(() => {});
  await frame.locator('body.crest-legacy-frame').waitFor({ timeout: 20000 });
  const linkNoNesting = await assertNoNestedCrestShell(frame);

  await Promise.all([
    frame.waitForURL(/\/Admin\/DeploymentPlan\/Index/i, { timeout: 20000 }),
    frame.evaluate(path => {
      window.location.href = path;
    }, listPath),
  ]);
  await frame.waitForLoadState('networkidle').catch(() => {});
  await frame.locator('body.crest-legacy-frame').waitFor({ timeout: 20000 });
  const reassignedNoNesting = await assertNoNestedCrestShell(frame);
  const reassignedUrl = new URL(frame.url());
  const reassignedKeepsLegacyParam = reassignedUrl.searchParams.get('legacy-frame') === '1';

  return [
    { name: 'initial-frame-no-nested-shell', pass: initialNoNesting, message: `noNesting=${initialNoNesting}` },
    { name: 'initial-frame-keeps-legacy-param', pass: initialKeepsLegacyParam, message: frame.url() },
    { name: 'link-frame-no-nested-shell', pass: linkNoNesting, message: `noNesting=${linkNoNesting}` },
    { name: 'location-reassign-no-nested-shell', pass: reassignedNoNesting, message: `noNesting=${reassignedNoNesting}` },
    {
      name: 'location-reassign-forced-back-to-legacy-frame',
      pass: reassignedKeepsLegacyParam,
      message: `Iframe navigation without query should be forced back into legacy frame mode: ${reassignedUrl.toString()}`,
    },
  ];
};
