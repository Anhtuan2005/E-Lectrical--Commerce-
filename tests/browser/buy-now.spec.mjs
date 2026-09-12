import { test, expect } from '@playwright/test';

async function login(page) {
  await page.goto('/Account/Login');
  await page.locator('#Email').fill('khachhang1@shop.vn');
  await page.locator('#Password').fill('User@123');
  await Promise.all([
    page.waitForURL(url => !url.pathname.includes('/Login')),
    page.locator('button[type=submit]').click()
  ]);
}

test('buy now opens a direct checkout without changing the cart count', async ({ page }) => {
  await login(page);
  await page.goto('/Cart');
  const cartProductIds = await page.locator('[name=selectedProductIds]').evaluateAll(inputs => inputs.map(input => input.value));
  const cartCount = await page.locator('#cart-count').textContent();

  await page.goto('/Product');
  const productId = await page.locator('[data-cart-product]').evaluateAll((buttons, existingIds) => {
    const button = buttons.find(item => !existingIds.includes(item.dataset.cartProduct) && !item.disabled);
    return button?.dataset.cartProduct;
  }, cartProductIds);
  expect(productId).toBeTruthy();

  await page.goto(`/Product/Detail/${productId}`);
  let releaseCheckout;
  let markCheckoutStarted;
  const checkoutStarted = new Promise(resolve => { markCheckoutStarted = resolve; });
  const checkoutReleased = new Promise(resolve => { releaseCheckout = resolve; });
  await page.route('**/Order/Checkout?**', async route => {
    markCheckoutStarted();
    await checkoutReleased;
    await route.continue();
  });
  const buttonState = await page.locator('#btnBuyNow').evaluate(button => {
    button.click();
    return { disabled: button.disabled, text: button.textContent.trim() };
  });
  await checkoutStarted;
  try {
    expect(buttonState).toEqual({ disabled: false, text: 'Mua ngay' });
  } finally {
    releaseCheckout();
  }
  await page.waitForURL(url => url.pathname === '/Order/Checkout');

  await expect(page.locator('#cart-count')).toHaveText(cartCount.trim());
  await expect(page.locator('.summary-item')).toHaveCount(1);
  await expect(page.locator('.summary-item small')).toContainText('Số lượng: 1');

  await page.goBack();
  await expect(page).toHaveURL(new RegExp(`/Product/Detail/${productId}$`));
  await expect(page.locator('#btnBuyNow')).toBeEnabled();
  await expect(page.locator('#btnBuyNow')).toHaveText('Mua ngay');
});

test('voucher input and apply button remain on one row on mobile', async ({ page }) => {
  await page.setViewportSize({ width: 390, height: 844 });
  await login(page);
  await page.goto('/Product');
  const productId = await page.locator('[data-cart-product]:not([disabled])').first().getAttribute('data-cart-product');
  expect(productId).toBeTruthy();
  await page.goto(`/Order/Checkout?buyNowProductId=${productId}&buyNowQuantity=1`);

  const input = await page.locator('#voucherInput').boundingBox();
  const button = await page.locator('#applyVoucher').boundingBox();
  expect(input).not.toBeNull();
  expect(button).not.toBeNull();
  expect(Math.abs(input.y - button.y)).toBeLessThan(2);
  await expect(page.locator('#applyVoucher')).toHaveText('Áp dụng');
  if (process.env.TECHVORA_CAPTURE_UI === 'true') {
    await page.locator('.voucher-box').screenshot({ path: 'artifacts/voucher-mobile-fixed.png' });
  }
});
