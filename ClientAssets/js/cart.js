function initCartButtons() {
  document.querySelectorAll("[data-cart-product]").forEach(function (button) {
    button.addEventListener("click", function (event) {
      event.preventDefault();
      event.stopPropagation();
      if (button.disabled) return;
      postCartAdd(button.dataset.cartProduct, "1")
        .then(function (data) {
          if (!data.success) {
            showToast(data.message || "Không thể thêm sản phẩm vào giỏ.", "error");
            return;
          }
          updateCartBadges(data);
          if (button.dataset.cartReload === "true") {
            if (document.getElementById("checkoutForm")) {
              var checkoutUrl = new URL(window.location.href);
              checkoutUrl.searchParams.append("selectedProductIds", button.dataset.cartProduct);
              window.location.href = checkoutUrl.pathname + checkoutUrl.search + checkoutUrl.hash;
              return;
            }
            window.location.reload();
            return;
          }
          if (!showCartOffersAfterAdd(data)) {
            showToast(data.message, "success");
          }
        })
        .catch(function () {
          showToast("Không thể thêm sản phẩm vào giỏ. Vui lòng thử lại.", "error");
        });
    });
  });
}

function initDetailAddToCart() {
  var addButtons = document.querySelectorAll("[data-detail-add-cart]");
  var buyButtons = document.querySelectorAll("[data-detail-buy-now]");
  var input = document.getElementById("qtyInput");
  if (!input || (!addButtons.length && !buyButtons.length)) return;

  function setBusy(source, busy) {
    if (!source) return;
    source.disabled = busy || source.dataset.stockDisabled === "true";
    if (busy) {
      source.dataset.originalHtml = source.dataset.originalHtml || source.innerHTML;
      source.innerHTML = "Đang xử lý...";
    } else if (source.dataset.originalHtml) {
      source.innerHTML = source.dataset.originalHtml;
      refreshIcons();
    }
  }

  function addDetailProduct(redirectToCheckout, source) {
    if (!source || source.disabled) return;
    var qty = clampQuantity(input);
    setBusy(source, true);
    postCartAdd(source.dataset.productId, qty)
      .then(function (data) {
        if (!data.success) {
          showToast(data.message || "Không thể thêm sản phẩm vào giỏ.", "error");
          return;
        }
        updateCartBadges(data);
        if (redirectToCheckout) {
          window.location.href = "/Order/Checkout?selectedProductIds=" + encodeURIComponent(source.dataset.productId);
          return;
        }
        if (!showCartOffersAfterAdd(data)) {
          showToast(data.message, "success");
        }
      })
      .catch(function () {
        showToast("Không thể thêm sản phẩm vào giỏ. Vui lòng thử lại.", "error");
      })
      .finally(function () {
        setBusy(source, false);
      });
  }

  addButtons.forEach(function (button) {
    button.addEventListener("click", function () {
      addDetailProduct(false, button);
    });
  });

  buyButtons.forEach(function (button) {
    button.addEventListener("click", function () {
      addDetailProduct(true, button);
    });
  });
}

function parseLocalizedNumber(value, fallback) {
  fallback = typeof fallback === "number" && Number.isFinite(fallback) ? fallback : 0;
  if (typeof value === "number") return Number.isFinite(value) ? value : fallback;

  var text = String(value === null || value === undefined ? "" : value).trim();
  if (!text) return fallback;

  text = text.replace(/[^\d,.-]/g, "");
  if (!text) return fallback;

  var lastComma = text.lastIndexOf(",");
  var lastDot = text.lastIndexOf(".");
  if (lastComma >= 0 && lastDot >= 0) {
    var decimalSeparator = lastComma > lastDot ? "," : ".";
    var thousandsSeparator = decimalSeparator === "," ? "." : ",";
    text = text.replace(new RegExp("\\" + thousandsSeparator, "g"), "").replace(decimalSeparator, ".");
  } else if (lastComma >= 0) {
    text = text.length - lastComma - 1 <= 2 ? text.replace(/\./g, "").replace(",", ".") : text.replace(/,/g, "");
  } else if (lastDot >= 0 && text.length - lastDot - 1 > 2) {
    text = text.replace(/\./g, "");
  }

  var parsed = Number(text);
  return Number.isFinite(parsed) ? parsed : fallback;
}

function clampQuantity(input) {
  if (!input) return 1;
  var min = Math.max(1, Math.floor(parseLocalizedNumber(input.min, 1)));
  var max = Math.floor(parseLocalizedNumber(input.max, 99));
  if (max < min) max = min;
  var qty = Math.floor(parseLocalizedNumber(input.value, min));
  qty = Math.max(min, Math.min(max, qty));
  input.value = String(qty);
  return qty;
}

function formatVnd(value) {
  return new Intl.NumberFormat("vi-VN").format(parseLocalizedNumber(value, 0)) + " ₫";
}

function initPdpNotes() {
  document.querySelectorAll("[data-pdp-note]").forEach(function (button) {
    button.addEventListener("click", function () {
      showToast(button.dataset.pdpNote || "Thông tin này sẽ được chọn ở bước checkout.", "success");
    });
  });
}

function initDetailCoupon() {
  var button = document.querySelector("[data-save-detail-coupon]");
  var input = document.getElementById("pdpCouponInput");
  var message = document.getElementById("pdpCouponMessage");
  if (!button || !input) return;

  var saved = sessionStorage.getItem("techvoraVoucherCode");
  if (saved) input.value = saved;

  function setMessage(text, type) {
    if (!message) return;
    message.textContent = text;
    message.className = type ? "pdp-coupon-message " + type : "pdp-coupon-message";
  }

  function currentSubtotal() {
    var card = document.querySelector("[data-product-price]");
    var qtyInput = document.getElementById("qtyInput");
    var price = card ? parseLocalizedNumber(card.dataset.productPrice, 0) : 0;
    var qty = qtyInput ? clampQuantity(qtyInput) : 1;
    return price * qty;
  }

  function setBusy(busy) {
    button.disabled = busy;
    button.textContent = busy ? "Đang kiểm tra..." : "Áp dụng";
  }

  button.addEventListener("click", function () {
    var code = input.value.trim().toUpperCase();
    if (!code) {
      setMessage("Nhập mã giảm giá trước khi áp dụng.", "error");
      showToast("Nhập mã giảm giá trước khi áp dụng.", "error");
      return;
    }

    var body = new URLSearchParams();
    body.append("code", code);
    body.append("subtotalOverride", String(currentSubtotal()));
    setBusy(true);

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
        if (!response.ok) throw new Error("Voucher request failed");
        return response.json();
      })
      .then(function (data) {
        if (!data.valid) {
          sessionStorage.removeItem("techvoraVoucherCode");
          setMessage(data.message || "Mã giảm giá không hợp lệ.", "error");
          showToast(data.message || "Mã giảm giá không hợp lệ.", "error");
          return;
        }

        var savedCode = data.code || code;
        var discountText = data.formattedDiscount || data.discountLabel || "";
        sessionStorage.setItem("techvoraVoucherCode", savedCode);
        setMessage("Mã " + savedCode + " hợp lệ. " + discountText + " sẽ được áp dụng ở checkout.", "success");
        showToast(data.message || "Đã lưu mã giảm giá cho checkout.", "success");
      })
      .catch(function (error) {
        var loginRequired = error && error.message === "login-required";
        var text = loginRequired ? "Đăng nhập để dùng mã giảm giá." : "Chưa thể kiểm tra mã giảm giá. Vui lòng thử lại.";
        setMessage(text, "error");
        showToast(text, "error");
      })
      .finally(function () {
        setBusy(false);
      });
  });
}

function initCartPage() {
  document.querySelectorAll(".cart-qty").forEach(function (input) {
    input.addEventListener("change", function () {
      var row = input.closest(".cart-item");
      if (!row) return;
      updateCart("/Cart/Update", row.dataset.productId, input.value, row);
    });
  });
  document.querySelectorAll(".cart-remove").forEach(function (button) {
    button.addEventListener("click", function () {
      var row = button.closest(".cart-item");
      if (!row) return;
      updateCart("/Cart/Remove", row.dataset.productId, 0, row, true);
    });
  });
}

function initCartSelection() {
  var form = document.getElementById("cartCheckoutForm");
  if (!form) return;

  function itemInputs(scope) {
    return Array.from((scope || form).querySelectorAll("[data-cart-item-select]"));
  }

  function syncGroup(group) {
    if (!group) return;
    var groupInput = group.querySelector("[data-cart-group-select]");
    if (!groupInput) return;

    var inputs = itemInputs(group);
    var checked = inputs.filter(function (input) { return input.checked; }).length;
    groupInput.checked = inputs.length > 0 && checked === inputs.length;
    groupInput.indeterminate = checked > 0 && checked < inputs.length;
  }

  function syncAllGroups() {
    form.querySelectorAll("[data-cart-group]").forEach(syncGroup);
  }

  function syncSelection() {
    var checkedItems = itemInputs().filter(function (input) { return input.checked; });
    var count = checkedItems.length;
    var totalValue = checkedItems.reduce(function (sum, input) {
      return sum + parseLocalizedNumber(input.dataset.lineTotal, 0);
    }, 0);
    var selectedCount = document.getElementById("cart-selected-count");
    var total = document.querySelector("[data-cart-selected-total]") || document.getElementById("cart-total");
    var submit = document.getElementById("cartCheckoutSubmit");

    if (selectedCount) selectedCount.textContent = String(count);
    if (total) total.textContent = formatVnd(totalValue);
    if (submit) submit.disabled = count === 0;
    syncAllGroups();
  }

  form.querySelectorAll("[data-cart-group-toggle]").forEach(function (button) {
    button.addEventListener("click", function () {
      var targetId = button.getAttribute("aria-controls");
      var panel = targetId ? document.getElementById(targetId) : null;
      var group = button.closest("[data-cart-group]");
      var expanded = button.getAttribute("aria-expanded") === "true";
      var nextExpanded = !expanded;
      var label = button.querySelector("span");

      button.setAttribute("aria-expanded", String(nextExpanded));
      if (panel) panel.hidden = !nextExpanded;
      if (group) group.classList.toggle("is-collapsed", !nextExpanded);
      if (label) label.textContent = nextExpanded ? "Thu gọn" : "Xem linh kiện";
    });
  });

  form.querySelectorAll("[data-cart-group-select]").forEach(function (input) {
    input.addEventListener("change", function () {
      var group = input.closest("[data-cart-group]");
      itemInputs(group).forEach(function (itemInput) {
        itemInput.checked = input.checked;
      });
      input.indeterminate = false;
      syncSelection();
    });
  });

  itemInputs().forEach(function (input) {
    input.addEventListener("change", syncSelection);
  });

  form.addEventListener("submit", function (event) {
    if (itemInputs().some(function (input) { return input.checked; })) return;
    event.preventDefault();
    showToast("Vui lòng chọn ít nhất một sản phẩm để thanh toán.", "error");
  });

  window.syncCartSelection = syncSelection;
  syncSelection();
}

function updateCart(url, productId, quantity, row, removeRow) {
  var body = new URLSearchParams();
  body.append("productId", productId);
  body.append("quantity", quantity);
  var group = row ? row.closest("[data-cart-group]") : null;
  fetch(url, {
    method: "POST",
    headers: {
      "Content-Type": "application/x-www-form-urlencoded",
      "RequestVerificationToken": antiForgeryToken()
    },
    body: body.toString()
  })
    .then(function (response) { return response.json(); })
    .then(function (data) {
      if (!data.success) {
        showToast(data.message || "Không thể cập nhật giỏ hàng.", "error");
        return;
      }
      var cartCount = document.getElementById("cart-count");
      var cartTotal = document.getElementById("cart-total");
      if (cartCount) cartCount.textContent = data.itemCount;
      if (cartTotal && !document.querySelector("[data-cart-selected-total]")) cartTotal.textContent = data.total;
      var item = data.items.find(function (entry) { return String(entry.productId) === String(productId); });
      if (item && row) {
        var lineTotal = row.querySelector(".line-total");
        var select = row.querySelector("[data-cart-item-select]");
        if (lineTotal) lineTotal.textContent = item.lineTotal;
        if (select && item.lineTotalValue !== undefined) select.dataset.lineTotal = String(item.lineTotalValue);
      }
      if ((removeRow || Number(quantity) <= 0) && row) row.remove();
      syncCartGroup(group, data);
      if (window.syncCartSelection) window.syncCartSelection();
      showToast(data.message, "success");
      if (data.itemCount === 0) window.location.reload();
    })
    .catch(function () {
      showToast("Không thể cập nhật giỏ hàng. Vui lòng thử lại.", "error");
    });
}

function syncCartGroup(group, data) {
  if (!group) return;

  var remainingItems = group.querySelectorAll(".cart-item").length;
  if (remainingItems === 0) {
    group.remove();
    return;
  }

  var groupKey = group.dataset.cartGroup;
  var groups = data && Array.isArray(data.groups) ? data.groups : [];
  var updated = groups.find(function (entry) {
    return String(entry.key) === String(groupKey);
  });
  if (!updated) return;

  var total = group.querySelector("[data-cart-group-total]");
  var count = group.querySelector("[data-cart-group-count]");
  if (total) total.textContent = updated.total;
  if (count) count.textContent = updated.componentCount;

  var firstRow = group.querySelector(".cart-item");
  var firstImage = firstRow ? firstRow.querySelector("img") : null;
  var firstTitle = firstRow ? firstRow.querySelector("h3") : null;
  var leadImage = group.querySelector(".cart-build-cover");
  var leadTitle = group.querySelector(".cart-build-copy small strong");
  if (leadImage && firstImage) {
    leadImage.src = firstImage.currentSrc || firstImage.src;
    leadImage.alt = firstImage.alt || "";
  }
  if (leadTitle && firstTitle) leadTitle.textContent = firstTitle.textContent || "";
}

