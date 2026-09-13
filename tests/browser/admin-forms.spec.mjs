import { test, expect } from '@playwright/test';
import { signInDemo } from './auth.mjs';

let storageState;
test.beforeAll(async ({ browser, baseURL }) => {
  const context = await browser.newContext({ baseURL });
  const page = await context.newPage();
  await signInDemo(page, 'Admin');
  storageState = await context.storageState();
  await context.close();
});

test.beforeEach(async ({ context }) => {
  await context.addCookies(storageState.cookies);
});

test('order CSV export preserves customer filters containing URL characters', async ({ page }) => {
  const customer = 'An & Bình + #1';
  await page.goto('/Admin/Order?customer=' + encodeURIComponent(customer));
  const href = await page.locator('a[href*="ExportCsv"]').getAttribute('href');
  expect(new URL(href, page.url()).searchParams.get('customer')).toBe(customer);
});

test('banner upload without a URL reaches the server and shows a file validation error', async ({ page }) => {
  await page.goto('/Admin/Banner/Create');
  await page.locator('#Title').fill('Upload validation test');
  await page.locator('#banner-image-file').setInputFiles({ name: 'invalid.gif', mimeType: 'image/gif', buffer: Buffer.from('invalid') });
  await Promise.all([
    page.waitForResponse(response => response.request().method() === 'POST' && response.url().includes('/Admin/Banner/Create')),
    page.getByRole('button', { name: 'Lưu banner' }).click()
  ]);
  await expect(page.locator('[data-valmsg-for=imageFile]')).toContainText('Chỉ hỗ trợ');
  await expect(page.locator('[data-valmsg-for=ImageUrl]')).toBeEmpty();
});

test('product upload without image URLs shows upload validation rather than blocking the form', async ({ page }) => {
  await page.goto('/Admin/Product/Create');
  await page.locator('#Name').fill('Upload validation test');
  await page.locator('#Description').fill('Test input with an intentionally invalid file.');
  await page.locator('#Price').fill('100000');
  await page.locator('#Stock').fill('1');
  await page.locator('#CategoryId').selectOption({ index: 0 });
  await page.locator('#product-image-files').setInputFiles({ name: 'invalid.gif', mimeType: 'image/gif', buffer: Buffer.from('invalid') });
  await Promise.all([
    page.waitForResponse(response => response.request().method() === 'POST' && response.url().includes('/Admin/Product/Create')),
    page.getByRole('button', { name: 'Lưu sản phẩm' }).click()
  ]);
  await expect(page.locator('[data-valmsg-for=imageFiles]')).toContainText('Chỉ hỗ trợ');
  await expect(page.locator('[data-valmsg-for=ImageUrls]')).toBeEmpty();
});
