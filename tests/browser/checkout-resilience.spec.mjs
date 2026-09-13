import { test, expect } from '@playwright/test';
import { signInDemo } from './auth.mjs';

let storageState;
let productId;
test.beforeAll(async ({ browser, baseURL }) => {
  const context = await browser.newContext({ baseURL });
  const page = await context.newPage();
  await signInDemo(page);
  await page.goto('/Product');
  productId = await page.locator('[data-cart-product]:not([disabled])').first().getAttribute('data-cart-product');
  storageState = await context.storageState();
  await context.close();
});

test.beforeEach(async ({ context, page }) => {
  await context.addCookies(storageState.cookies);
  await page.route('https://provinces.open-api.vn/**', route => route.fulfill({ json: [] }));
  await page.goto(`/Order/Checkout?buyNowProductId=${productId}&buyNowQuantity=1`);
  await expect(page.locator('#applyVoucher')).toBeVisible();
});

test('replacing a valid voucher with an invalid code clears the submitted discount', async ({ page }) => {
  await page.route('**/Voucher/Validate', async route => {
    const code = new URLSearchParams(route.request().postData()).get('code');
    await route.fulfill({ json: code === 'VALID' ? {
      valid: true, code, discountAmount: 10000, formattedDiscount: '10.000 ₫', message: 'Đã áp dụng mã.'
    } : { valid: false, message: 'Mã giảm giá không hợp lệ.' } });
  });
  await page.locator('#voucherInput').fill('VALID');
  await page.locator('#applyVoucher').click();
  await expect(page.locator('#VoucherCode')).toHaveValue('VALID');
  await page.locator('#voucherInput').fill('INVALID');
  await page.locator('#applyVoucher').click();
  await expect(page.locator('#voucherMessage')).toContainText('không hợp lệ');
  await expect(page.locator('#VoucherCode')).toHaveValue('');
  await expect(page.locator('#DiscountAmount')).toHaveValue('0');
  await expect(page.locator('#discountLine')).not.toHaveClass(/show/);
});

test('voucher request failure shows an error and permits retry without uncaught errors', async ({ page }) => {
  const errors = [];
  page.on('pageerror', error => errors.push(error.message));
  await page.route('**/Voucher/Validate', route => route.fulfill({ status: 429, contentType: 'text/plain', body: 'Bạn thao tác quá nhanh.' }));
  await page.locator('#voucherInput').fill('RETRY');
  await page.locator('#applyVoucher').click();
  await expect(page.locator('#voucherMessage')).toContainText('thử lại');
  await expect(page.locator('#applyVoucher')).toBeEnabled();
  expect(errors).toEqual([]);
});

test('an older shipping response cannot overwrite the latest selected address', async ({ page }) => {
  const requests = [];
  await page.route('**/Order/ShippingFee?**', route => { requests.push(route); });
  await page.evaluate(() => {
    const select = document.querySelector('[data-province-select]');
    select.innerHTML = '<option value="old">Old</option><option value="new">New</option>';
    select.value = 'old';
    updateCheckoutShippingFee();
  });
  await expect.poll(() => requests.length).toBe(1);
  await page.evaluate(() => {
    document.querySelector('[data-province-select]').value = 'new';
    updateCheckoutShippingFee();
  });
  await expect.poll(() => requests.length).toBe(2);
  await requests[1].fulfill({ json: { fee: 20000, formattedFee: '20.000 ₫', message: 'New' } });
  await expect(page.locator('#shippingFeeText')).toHaveText('20.000 ₫');
  await requests[0].fulfill({ json: { fee: 50000, formattedFee: '50.000 ₫', message: 'Old' } });
  await page.waitForTimeout(100);
  await expect(page.locator('#shippingFeeText')).toHaveText('20.000 ₫');
});
