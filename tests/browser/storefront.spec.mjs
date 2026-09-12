import { test, expect } from '@playwright/test';

test('storefront, catalog and PC builder render without application errors', async ({ page }) => {
  const errors = [];
  page.on('pageerror', error => errors.push(error.message));
  for (const path of ['/', '/Product', '/BuildPc']) {
    const response = await page.goto(path);
    expect(response.status()).toBe(200);
    await expect(page.locator('main')).toBeVisible();
    await expect(page.locator('body')).not.toContainText('InvalidOperationException');
  }
  await page.locator('#smartGenerate').click();
  await expect(page.locator('#smartResult')).toBeVisible();
  await expect(page.locator('#smartResultSlots')).not.toBeEmpty();
  expect(errors).toEqual([]);
});

test('mobile catalog fits the viewport', async ({ page }) => {
  await page.setViewportSize({ width: 390, height: 844 });
  await page.goto('/Product');
  await expect(page.locator('main')).toBeVisible();
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth + 1)).toBe(true);
});
