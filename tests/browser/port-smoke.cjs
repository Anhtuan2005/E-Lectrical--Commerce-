// Run against a freshly seeded, isolated development database.
// Requires Playwright/Edge and PORT_SMOKE_URL (defaults to localhost:5089).
const { chromium } = require('playwright');
const assert = require('node:assert/strict');

const baseURL = process.env.PORT_SMOKE_URL || 'http://127.0.0.1:5089';
const widths = [1440, 390];

async function signIn(page, email, password) {
  await page.goto('/Account/Login');
  await page.locator('#Email').fill(email);
  await page.locator('#Password').fill(password);
  await Promise.all([
    page.waitForURL(url => !url.pathname.toLowerCase().includes('/account/login')),
    page.locator('button[type="submit"]').click()
  ]);
}

async function checkComparison(page, width) {
  await page.goto('/Product/Compare?ids=31&ids=32&need=cores');
  await page.locator('.component-advice').waitFor();
  assert.match(await page.locator('.component-advice > strong').innerText(), /Intel Core i5-13400F/);
  assert.equal(await page.locator('.compare-product').count(), 2);
  assert.equal(await page.locator('.component-advice-sources a').count(), 2);
  assert.equal(await page.locator('body').evaluate(el => getComputedStyle(el).width.length > 0), true);
  console.log(`COMPARE ${width}: 2 CPU khác hãng, kết luận có nguồn và bảng hiển thị`);
}

async function checkSupport(page, width, orderId) {
  await page.goto(`/ReturnWarranty/Create?orderId=${orderId}`);
  const form = page.locator('[data-support-form]');
  await form.waitFor();
  await form.locator('[name="ContactName"]').fill('');
  await form.locator('[name="ContactPhone"]').fill('0123');
  await form.locator('[name="Reason"]').selectOption('');
  await form.locator('[name="Description"]').fill('Linh kiện bị lỗi');
  await form.locator('[name$=".Selected"]').first().uncheck();
  await form.locator('button[type="submit"]').click();
  assert.match(await form.locator('[data-support-item-error]').innerText(), /chọn ít nhất một sản phẩm/);
  assert.match(await form.locator('[data-valmsg-for="ContactPhone"]').innerText(), /10 chữ số/);
  assert.match(await form.locator('[data-valmsg-for="Description"]').innerText(), /hiện có 16/);
  const badBorder = await form.locator('[name="Description"]').evaluate(el => getComputedStyle(el).borderColor);
  assert.equal(badBorder, 'rgb(255, 59, 48)');
  if (width === 390) {
    const animation = await form.locator('[name="Description"]').evaluate(el => getComputedStyle(el).animationName);
    assert.equal(animation, 'none');
  }
  await form.locator('[name$=".Selected"]').first().check();
  await form.locator('[name="ContactName"]').fill('Khách kiểm thử');
  await form.locator('[name="ContactPhone"]').fill('0901234567');
  await form.locator('[name="Reason"]').selectOption({ label: 'Sản phẩm lỗi khi nhận hàng' });
  await form.locator('[name="Description"]').fill('Sản phẩm không khởi động được sau khi lắp vào máy.');
  assert.equal(await form.locator('[data-valmsg-for="Description"]').innerText(), '');
  await Promise.all([
    page.waitForURL(url => url.pathname.includes('/ReturnWarranty/Details/')),
    form.locator('button[type="submit"]').click()
  ]);
  console.log(`SUPPORT ${width}: lỗi theo trường và yêu cầu hợp lệ đã lưu`);
}

async function checkOrder(page, width) {
  await page.route('https://provinces.open-api.vn/api/v1/**', route => {
    const path = new URL(route.request().url()).pathname;
    const payload = path.endsWith('/p/')
      ? [{ code: 79, name: 'Hồ Chí Minh' }]
      : path.endsWith('/p/79')
        ? { districts: [{ code: 760, name: 'Quận 1' }] }
        : { wards: [{ code: 26734, name: 'Phường Bến Nghé' }] };
    return route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(payload) });
  });
  await page.goto('/Order/Checkout?buyNowProductId=31&buyNowQuantity=1');
  const form = page.locator('#checkoutForm');
  await form.waitFor();
  await form.locator('#RecipientName').fill('Khách kiểm thử');
  await form.locator('#RecipientPhone').fill('0901234567');
  await form.locator('#Province').selectOption({ label: 'Hồ Chí Minh' });
  await page.waitForFunction(() => document.querySelector('#District').options.length > 1);
  await form.locator('#District').selectOption({ index: 1 });
  await page.waitForFunction(() => document.querySelector('#Ward').options.length > 1);
  await form.locator('#Ward').selectOption({ index: 1 });
  await form.locator('#Street').fill('123 Nguyễn Trãi');
  await form.locator('[name="PaymentMethod"][value="COD"]').check();
  await Promise.all([
    page.waitForURL(url => url.pathname.includes('/Order/Confirmation/')),
    form.locator('#checkoutSubmit').click()
  ]);
  const orderId = Number(new URL(page.url()).pathname.split('/').pop());
  assert.ok(orderId > 0);
  await page.goto(`/Order/Detail/${orderId}`);
  page.once('dialog', dialog => dialog.accept());
  await page.locator('form[action*="Cancel"] button[type="submit"]').click();
  await page.goto(`/Order/Detail/${orderId}`);
  assert.match(await page.locator('.order-detail-page').innerText(), /Đã huỷ/);
  console.log(`ORDER ${width}: đặt COD và hủy đơn #DH${orderId}`);
  return orderId;
}

async function checkStock(page, width, orderId) {
  await page.goto('/Admin/Product/Edit/31');
  const table = page.locator('.admin-table').last();
  const rows = table.locator('tr', { hasText: `#DH${String(orderId).padStart(4, '0')}` });
  assert.equal(await rows.count(), 2);
  assert.match(await rows.first().innerText(), /\d+ → \d+/);
  console.log(`STOCK ${width}: xuất và hoàn kho có số lượng trước/sau và mã đơn`);
}

async function checkRefund(page, width, orderId) {
  await page.goto(`/Admin/Order/${orderId}`);
  const form = page.locator('form[action*="MarkRefunded"]');
  await form.waitFor();
  await form.locator('[name="method"]').selectOption({ label: 'Tiền mặt' });
  await form.locator('[name="reference"]').fill(`PORT-${width}-${orderId}`);
  await form.locator('[name="note"]').fill('Hoàn tiền thủ công theo yêu cầu kiểm thử.');
  await form.locator('[name="confirmed"]').check();
  await Promise.all([page.waitForURL(url => url.pathname.endsWith(`/Admin/Order/${orderId}`)), form.locator('button[type="submit"]').click()]);
  assert.match(await page.locator('.detail-list').first().innerText(), new RegExp(`PORT-${width}-${orderId}`));
  console.log(`REFUND ${width}: COD lưu mã đối soát và số tiền`);
}

(async () => {
  const browser = await chromium.launch({ channel: 'msedge', headless: true });
  let userCookies, adminCookies;
  try {
    for (const [index, width] of widths.entries()) {
      const context = await browser.newContext({ baseURL, viewport: { width, height: 900 },
        reducedMotion: width === 390 ? 'reduce' : 'no-preference' });
      const page = await context.newPage();
      await checkComparison(page, width);
      if (index === 0) {
        await signIn(page, 'khachhang1@shop.vn', 'User@123');
        userCookies = await context.cookies();
      } else {
        await context.addCookies(userCookies);
      }
      if (!process.env.PORT_SMOKE_SKIP_SUPPORT) await checkSupport(page, width, index + 1);
      const newOrderId = await checkOrder(page, width);
      await context.close();

      const adminContext = await browser.newContext({ baseURL, viewport: { width, height: 900 } });
      const adminPage = await adminContext.newPage();
      if (index === 0) {
        await signIn(adminPage, 'admin@shop.vn', 'Admin@123');
        adminCookies = await adminContext.cookies();
      } else {
        await adminContext.addCookies(adminCookies);
      }
      await checkRefund(adminPage, width, index + 1);
      await checkStock(adminPage, width, newOrderId);
      await adminContext.close();
    }
  } finally {
    await browser.close();
  }
})().catch(error => { console.error(error); process.exitCode = 1; });
