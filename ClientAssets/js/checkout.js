function refreshCheckoutTotal() {
  var total = document.getElementById("checkoutTotal");
  if (!total) return;

  var subtotal = parseLocalizedNumber(total.dataset.subtotal, 0);
  var shippingFee = parseLocalizedNumber(total.dataset.shippingFee, 0);
  var discountInput = document.getElementById("DiscountAmount");
  var discount = discountInput ? parseLocalizedNumber(discountInput.value, 0) : 0;
  var nextTotal = Math.max(0, subtotal - discount) + shippingFee;

  total.textContent = formatVnd(nextTotal);
  total.classList.remove("bump");
  requestAnimationFrame(function () { total.classList.add("bump"); });
}

function initVoucher() {
  var button = document.getElementById("applyVoucher");
  if (!button) return;
  var savedCode = sessionStorage.getItem("techvoraVoucherCode");
  var savedInput = document.getElementById("voucherInput");
  if (savedCode && savedInput && !savedInput.value) savedInput.value = savedCode;
  function clearVoucher() {
    var code = document.getElementById("VoucherCode");
    var discount = document.getElementById("DiscountAmount");
    var line = document.getElementById("discountLine");
    if (code) code.value = "";
    if (discount) discount.value = "0";
    if (line) line.classList.remove("show");
    sessionStorage.removeItem("techvoraVoucherCode");
    refreshCheckoutTotal();
  }
  if (savedInput) savedInput.addEventListener("input", function () {
    clearVoucher();
    var message = document.getElementById("voucherMessage");
    if (message) message.textContent = "";
  });
  button.addEventListener("click", function () {
    if (button.disabled) return;
    var input = document.getElementById("voucherInput");
    var message = document.getElementById("voucherMessage");
    if (!input || !message) return;
    clearVoucher();
    if (!input.value.trim()) {
      message.textContent = "Nhập mã giảm giá trước khi áp dụng.";
      message.className = "voucher-message error";
      return;
    }
    button.disabled = true;
    input.disabled = true;
    message.textContent = "Đang kiểm tra mã giảm giá...";
    message.className = "voucher-message";
    var body = new URLSearchParams();
    body.append("code", input.value);
    var checkoutTotal = document.getElementById("checkoutTotal");
    if (checkoutTotal) body.append("subtotalOverride", checkoutTotal.dataset.subtotal || "0");
    fetch("/Voucher/Validate", {
      method: "POST",
      headers: {
        "Content-Type": "application/x-www-form-urlencoded",
        "RequestVerificationToken": antiForgeryToken()
      },
      body: body.toString()
    })
      .then(function (response) {
        if (response.redirected || response.status === 401) throw new Error("login-required");
        if (!response.ok) throw new Error("voucher-failed");
        return response.json();
      })
      .then(function (data) {
        message.textContent = data.message;
        message.className = "voucher-message " + (data.valid ? "success" : "error");
        if (!data.valid) return;
        var voucherCode = document.getElementById("VoucherCode");
        var discountAmount = document.getElementById("DiscountAmount");
        var discountText = document.getElementById("discountText");
        var discountLine = document.getElementById("discountLine");
        if (voucherCode) voucherCode.value = data.code;
        if (discountAmount) discountAmount.value = data.discountAmount;
        if (discountText) discountText.textContent = data.formattedDiscount || data.discountLabel;
        if (discountLine) discountLine.classList.add("show");
        refreshCheckoutTotal();
        showToast(data.message, "success");
        sessionStorage.removeItem("techvoraVoucherCode");
      })
      .catch(function (error) {
        message.textContent = error.message === "login-required"
          ? "Phiên đăng nhập đã hết hạn. Vui lòng đăng nhập lại."
          : "Chưa thể kiểm tra mã giảm giá. Vui lòng thử lại.";
        message.className = "voucher-message error";
      })
      .finally(function () {
        button.disabled = false;
        input.disabled = false;
      });
  });
  if (savedCode && savedInput) {
    setTimeout(function () {
      button.click();
    }, 0);
  }
}

var checkoutShippingRequestId = 0;
function updateCheckoutShippingFee() {
  var requestId = ++checkoutShippingRequestId;
  var total = document.getElementById("checkoutTotal");
  var feeText = document.getElementById("shippingFeeText");
  var feeNote = document.getElementById("shippingFeeNote");
  var province = document.querySelector("[data-province-select]") || document.getElementById("Province");
  var district = document.querySelector("[data-district-select]") || document.getElementById("District");
  if (!total || !feeText || !province) return;

  if (!province.value) {
    total.dataset.shippingFee = "0";
    feeText.textContent = formatVnd(0);
    if (feeNote) feeNote.textContent = "Chọn tỉnh/thành phố để tính phí.";
    refreshCheckoutTotal();
    return;
  }

  var query = new URLSearchParams();
  query.append("province", province.value);
  if (district && district.value) query.append("district", district.value);
  document.querySelectorAll('input[name="SelectedProductIds"]').forEach(function (input) {
    if (input.value) query.append("selectedProductIds", input.value);
  });
  var buyNowProductId = document.getElementById("BuyNowProductId");
  var buyNowQuantity = document.getElementById("BuyNowQuantity");
  if (buyNowProductId && buyNowProductId.value) query.append("buyNowProductId", buyNowProductId.value);
  if (buyNowQuantity && buyNowQuantity.value) query.append("buyNowQuantity", buyNowQuantity.value);

  fetch("/Order/ShippingFee?" + query.toString(), { headers: { Accept: "application/json" } })
    .then(function (response) {
      if (!response.ok || response.redirected) throw new Error("shipping-failed");
      return response.json();
    })
    .then(function (data) {
      if (requestId !== checkoutShippingRequestId) return;
      total.dataset.shippingFee = String(data.fee || 0);
      feeText.textContent = data.formattedFee || formatVnd(data.fee || 0);
      if (feeNote) {
        var meta = data.zone ? data.zone : "";
        feeNote.textContent = meta ? meta + " · " + (data.message || "") : (data.message || "");
      }
      refreshCheckoutTotal();
    })
    .catch(function () {
      if (requestId !== checkoutShippingRequestId) return;
      total.dataset.shippingFee = "0";
      feeText.textContent = "Chưa xác định";
      if (feeNote) feeNote.textContent = "Chưa tính được phí vận chuyển.";
      refreshCheckoutTotal();
    });
}

function initQuantitySteppers() {
  document.querySelectorAll(".quantity-stepper").forEach(function (stepper) {
    var input = stepper.querySelector("input");
    var minus = stepper.querySelector("[data-qty-minus]");
    var plus = stepper.querySelector("[data-qty-plus]");
    if (!input) return;
    if (minus) minus.addEventListener("click", function () {
      input.value = parseLocalizedNumber(input.value, parseLocalizedNumber(input.min, 1)) - 1;
      clampQuantity(input);
      input.dispatchEvent(new Event("change", { bubbles: true }));
    });
    if (plus) plus.addEventListener("click", function () {
      input.value = parseLocalizedNumber(input.value, parseLocalizedNumber(input.min, 1)) + 1;
      clampQuantity(input);
      input.dispatchEvent(new Event("change", { bubbles: true }));
    });
    input.addEventListener("change", updateProductSubtotal);
  });
  updateProductSubtotal();
}

function updateProductSubtotal() {
  var card = document.querySelector("[data-product-price]");
  var input = document.getElementById("qtyInput");
  var subtotal = document.getElementById("pdpSubtotal");
  if (!card || !input || !subtotal) return;
  var price = parseLocalizedNumber(card.dataset.productPrice, 0);
  var qty = clampQuantity(input);
  subtotal.textContent = formatVnd(price * qty);
}

function initShareTools() {
  document.querySelectorAll("[data-copy-link]").forEach(function (button) {
    button.addEventListener("click", function () {
      var link = window.location.href;
      if (navigator.clipboard && navigator.clipboard.writeText) {
        navigator.clipboard.writeText(link).then(function () {
          showToast("Đã copy link sản phẩm.", "success");
        }).catch(function () {
          fallbackCopy(link);
        });
      } else {
        fallbackCopy(link);
      }
    });
  });
}

function fallbackCopy(value) {
  var textarea = document.createElement("textarea");
  textarea.value = value;
  textarea.setAttribute("readonly", "");
  textarea.style.position = "fixed";
  textarea.style.opacity = "0";
  document.body.appendChild(textarea);
  textarea.select();
  document.execCommand("copy");
  textarea.remove();
  showToast("Đã copy link sản phẩm.", "success");
}

function initAuthHelpers() {
  document.querySelectorAll("[data-toggle-password]").forEach(function (button) {
    button.addEventListener("click", function () {
      var input = button.parentElement.querySelector("[data-password-input]");
      if (!input) return;
      input.type = input.type === "password" ? "text" : "password";
      button.textContent = input.type === "password" ? "Hiện" : "Ẩn";
    });
  });
  document.querySelectorAll("[data-password-strength]").forEach(function (input) {
    input.addEventListener("input", function () {
      var score = 0;
      if (input.value.length >= 6) score++;
      if (/[A-Z]/.test(input.value)) score++;
      if (/[0-9]/.test(input.value)) score++;
      if (/[^A-Za-z0-9]/.test(input.value)) score++;
      var meter = document.querySelector(".password-meter span");
      if (meter) meter.style.width = Math.max(15, score * 25) + "%";
      var bar = document.getElementById("strengthBar");
      var label = document.getElementById("strengthLabel");
      var labels = ["Chưa nhập", "Yếu", "Trung bình", "Mạnh", "Rất mạnh"];
      var colors = ["#e2e8f0", "#ef4444", "#f59e0b", "#22c55e", "#0066ff"];
      if (bar) {
        bar.style.width = score * 25 + "%";
        bar.style.background = colors[score];
      }
      if (label) label.textContent = labels[score];
    });
  });
}

function initPaymentChoice() {
  var safe = document.getElementById("vnpaySafe");
  var submit = document.getElementById("checkoutSubmit");
  document.querySelectorAll('input[name="PaymentMethod"]').forEach(function (radio) {
    radio.addEventListener("change", function () {
      var isVnpay = radio.value === "VNPAY" && radio.checked;
      if (safe) safe.classList.toggle("show", isVnpay);
      if (submit && isVnpay) submit.textContent = "Thanh toán VNPAY";
      if (submit && !isVnpay) submit.textContent = "Đặt hàng";
    });
  });
}

