// /Admin/Contents/ContentItems is a NATIVE Crest Blazor page (Crest.Admin wasm
// Pages/ContentItems.razor), not the legacy Orchard iframe an earlier version of this
// check guarded (the duplicated-shell-inside-iframe bug can no longer occur on this
// route). This verifies the native replacement: the page renders without any legacy
// frame, and filtering by a content type reloads the list without errors and shows only
// rows of that type (or the empty state). Whichever type the filter offers first is
// used, so the check depends on no particular module's content types.
module.exports = async function run(page, ctx) {
  await page.goto(`${ctx.baseUrl}/Admin/Contents/ContentItems`, { waitUntil: 'networkidle' });

  await page.locator('[data-testid="content-items-page"]').waitFor({ timeout: 20000 });
  const legacyFrames = await page.locator('iframe.legacy-admin-frame').count();

  // First CrestDropDown in the filter card is the content-type filter.
  const typeDropdown = page.locator('[data-testid="content-items-type-filter"]');
  await typeDropdown.waitFor({ timeout: 15000 });
  await typeDropdown.click();
  // Both filter dropdowns pre-render their panels hidden; target the one that is
  // actually open after the click.
  // Skip the "All content types" reset entry: filtering by it is not filtering, and
  // every row would then be "another type". Take the first REAL type the tenant offers,
  // so the check depends on no particular module's content types.
  const options = page.locator('.rz-dropdown-panel:visible .rz-dropdown-item:visible');
  await options.first().waitFor({ timeout: 10000 });
  const labels = (await options.allTextContents()).map(text => text.trim());
  const index = labels.findIndex(label => label && !/^all\b/i.test(label));
  if (index < 0) {
    return [
      { name: 'native-page-no-legacy-frame', pass: legacyFrames === 0, message: `legacy iframes: ${legacyFrames}` },
      { name: 'type-filter-refreshes-list', pass: false, message: `no concrete content type in the filter: ${JSON.stringify(labels).slice(0, 200)}` },
    ];
  }

  const chosenType = labels[index];
  await options.nth(index).click();
  await page.waitForLoadState('networkidle').catch(() => {});
  await page.waitForTimeout(1500);

  const dangerAlerts = await page.locator('.rz-alert-danger').count();
  const emptyState = await page.getByText(/No content items match/i).count();
  const rowCount = await page.locator('[data-testid="content-items-grid"] tbody tr').count();
  let otherTypeRows = 0;
  if (rowCount > 0) {
    const typeCells = await page.locator('[data-testid="content-items-grid"] tbody tr td:nth-child(2)').allTextContents();
    otherTypeRows = typeCells.filter(text => !text.includes(chosenType)).length;
  }

  return [
    { name: 'native-page-no-legacy-frame', pass: legacyFrames === 0, message: `legacy iframes: ${legacyFrames}` },
    {
      name: 'type-filter-refreshes-list',
      pass: dangerAlerts === 0 && (emptyState > 0 || (rowCount > 0 && otherTypeRows === 0)),
      message: `type=${chosenType} dangerAlerts=${dangerAlerts} emptyState=${emptyState} rows=${rowCount} otherTypeRows=${otherTypeRows}`,
    },
  ];
};
