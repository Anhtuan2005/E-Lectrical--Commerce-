(function () {
  "use strict";

  var slotProducts = window.SLOT_PRODUCTS || {};
  var build = {};
  var currentSlot = null;

  var modal = document.getElementById("slotModal");
  var modalTitle = document.getElementById("modalTitle");
  var modalSearch = document.getElementById("modalSearch");
  var modalList = document.getElementById("modalProductList");
  var modalClose = document.getElementById("modalClose");
  var summaryList = document.getElementById("summaryList");
  var buildTotal = document.getElementById("buildTotal");
  var btnAddAll = document.getElementById("btnAddAll");
  var btnReset = document.getElementById("btnReset");
  var warningsEl = document.getElementById("compatWarnings");
  var wattageInfo = document.getElementById("wattageInfo");
  var wattageVal = document.getElementById("wattageVal");

  var wattageEstimate = { CPU: 95, VGA: 200, RAM: 10, SSD: 5, Mainboard: 50, PSU: 0, Case: 0, Cooling: 15 };

  document.querySelectorAll("[data-choose]").forEach(function (button) {
    button.addEventListener("click", function () {
      openModal(button.dataset.choose);
    });
  });

  document.querySelectorAll("[data-clear]").forEach(function (button) {
    button.addEventListener("click", function () {
      clearSlot(button.dataset.clear);
    });
  });

  document.querySelectorAll("[data-preset]").forEach(function (button) {
    button.addEventListener("click", function () {
      applyPreset(JSON.parse(button.dataset.preset));
    });
  });

  if (modalClose) modalClose.addEventListener("click", closeModal);
  if (modal) {
    modal.addEventListener("click", function (event) {
      if (event.target === modal) closeModal();
    });
  }

  if (modalSearch) {
    modalSearch.addEventListener("input", function () {
      if (currentSlot) renderModalProducts(currentSlot, modalSearch.value.toLowerCase());
    });
  }

  if (btnAddAll) btnAddAll.addEventListener("click", addAllToCart);
  if (btnReset) btnReset.addEventListener("click", resetBuild);

  function openModal(slot) {
    currentSlot = slot;
    modalTitle.textContent = "Chọn " + getSlotLabel(slot);
    modalSearch.value = "";
    renderModalProducts(slot, "");
    modal.hidden = false;
    document.body.style.overflow = "hidden";
    modalSearch.focus();
  }

  function closeModal() {
    modal.hidden = true;
    document.body.style.overflow = "";
    currentSlot = null;
  }

  function renderModalProducts(slot, filter) {
    var products = (slotProducts[slot] || []).filter(function (product) {
      return !filter || product.name.toLowerCase().indexOf(filter) >= 0;
    });

    if (!products.length) {
      modalList.innerHTML = '<p class="modal-empty">Chưa có sản phẩm phù hợp cho slot này.</p>';
      return;
    }

    modalList.innerHTML = products.map(function (product) {
      var selected = build[slot] && Number(build[slot].id) === Number(product.id);
      return '' +
        '<article class="modal-product-item ' + (selected ? "selected" : "") + '" data-product-id="' + product.id + '">' +
          '<img src="' + escapeHtml(product.imageUrl || "/images/placeholder.svg") + '" alt="' + escapeHtml(product.name) + '" />' +
          '<div class="modal-product-info">' +
            '<span>' + escapeHtml(product.name) + '</span>' +
            '<strong>' + escapeHtml(product.price) + '</strong>' +
          '</div>' +
          '<button class="btn-primary modal-select-btn" type="button">' + (selected ? "Đã chọn" : "Chọn") + '</button>' +
        '</article>';
    }).join("");

    modalList.querySelectorAll("[data-product-id]").forEach(function (item) {
      item.querySelector(".modal-select-btn").addEventListener("click", function () {
        var product = products.find(function (entry) { return String(entry.id) === String(item.dataset.productId); });
        if (product) selectProduct(currentSlot, product);
        closeModal();
      });
    });
  }

  function selectProduct(slot, product) {
    build[slot] = product;
    updateSlotRow(slot, product);
    updateSummary();
    checkCompatibility();
  }

  function updateSlotRow(slot, product) {
    var row = document.querySelector('[data-slot="' + cssEscape(slot) + '"]');
    if (!row) return;
    row.querySelector(".slot-selected-name").textContent = product.name;
    row.querySelector(".slot-price-display").textContent = product.price;
    row.querySelector(".slot-clear-btn").hidden = false;
    row.classList.add("has-product");
  }

  function clearSlot(slot) {
    delete build[slot];
    var row = document.querySelector('[data-slot="' + cssEscape(slot) + '"]');
    if (!row) return;
    row.querySelector(".slot-selected-name").textContent = "Chưa chọn";
    row.querySelector(".slot-price-display").textContent = "—";
    row.querySelector(".slot-clear-btn").hidden = true;
    row.classList.remove("has-product");
    updateSummary();
    checkCompatibility();
  }

  function updateSummary() {
    var entries = Object.entries(build);
    if (!entries.length) {
      summaryList.innerHTML = '<li class="summary-empty">Chưa chọn linh kiện nào</li>';
    } else {
      summaryList.innerHTML = entries.map(function (entry) {
        var slot = entry[0];
        var product = entry[1];
        var shortName = product.name.length > 30 ? product.name.substring(0, 30) + "..." : product.name;
        return '<li><span>' + getSlotLabel(slot) + '</span><span>' + escapeHtml(shortName) + '</span><strong>' + escapeHtml(product.price) + '</strong></li>';
      }).join("");
    }

    var total = entries.reduce(function (sum, entry) {
      return sum + Number(entry[1].priceRaw || 0);
    }, 0);
    buildTotal.textContent = total.toLocaleString("vi-VN") + " ₫";
    btnAddAll.disabled = entries.length === 0;
    updateWattage();
  }

  function updateWattage() {
    var total = Object.keys(build).reduce(function (sum, slot) {
      return sum + (wattageEstimate[slot] || 0);
    }, 0);

    wattageInfo.hidden = total <= 0;
    if (total > 0) wattageVal.textContent = total;
  }

  function checkCompatibility() {
    var warnings = [];
    var estimatedWatts = Object.keys(build).reduce(function (sum, slot) {
      return sum + (wattageEstimate[slot] || 0);
    }, 0);

    if (build.PSU && estimatedWatts > 0) {
      var match = build.PSU.name.toLowerCase().match(/(\d{3,4})\s*w/);
      if (match && parseInt(match[1], 10) < estimatedWatts * 1.2) {
        warnings.push("Nguồn " + build.PSU.name + " có thể chưa đủ công suất. Gợi ý tối thiểu khoảng " + Math.ceil(estimatedWatts * 1.2) + "W.");
      }
    }

    if (build.CPU && !build.Mainboard) warnings.push("Bạn đã chọn CPU, hãy chọn thêm Mainboard phù hợp.");
    if (build.VGA && !build.PSU) warnings.push("Bạn đã chọn VGA, hãy chọn nguồn có công suất phù hợp.");

    warningsEl.innerHTML = warnings.map(function (warning) {
      return '<div class="compat-warn">' + escapeHtml(warning) + '</div>';
    }).join("");
  }

  async function addAllToCart() {
    var items = Object.values(build);
    if (!items.length) return;

    btnAddAll.disabled = true;
    btnAddAll.textContent = "Đang thêm...";

    var successCount = 0;
    for (var i = 0; i < items.length; i++) {
      var product = items[i];
      try {
        var body = new URLSearchParams();
        body.append("productId", product.id);
        body.append("quantity", "1");
        var response = await fetch("/Cart/Add", {
          method: "POST",
          headers: {
            "Content-Type": "application/x-www-form-urlencoded",
            "RequestVerificationToken": antiForgeryToken()
          },
          body: body.toString()
        });
        var data = await response.json();
        if (data.success) {
          successCount++;
          var cartCount = document.getElementById("cart-count");
          if (cartCount) cartCount.textContent = data.itemCount;
        }
      } catch (error) {
        console.error("Lỗi thêm giỏ:", error);
      }
    }

    btnAddAll.textContent = "Đã thêm " + successCount + "/" + items.length + " sản phẩm";
    window.setTimeout(function () {
      btnAddAll.disabled = false;
      btnAddAll.textContent = "Thêm tất cả vào giỏ";
    }, 2200);
  }

  function resetBuild() {
    if (!Object.keys(build).length) return;
    if (!window.confirm("Đặt lại toàn bộ cấu hình?")) return;
    Object.keys(build).forEach(clearSlot);
    warningsEl.innerHTML = "";
  }

  function applyPreset(preset) {
    Object.entries(preset.slots || {}).forEach(function (entry) {
      var slot = entry[0];
      var keyword = String(entry[1] || "").toLowerCase();
      var product = (slotProducts[slot] || []).find(function (item) {
        return item.name.toLowerCase().indexOf(keyword) >= 0;
      });
      if (product) selectProduct(slot, product);
    });
  }

  function getSlotLabel(slot) {
    return {
      CPU: "CPU",
      VGA: "Card đồ họa",
      RAM: "RAM",
      SSD: "Ổ cứng SSD",
      Mainboard: "Mainboard",
      PSU: "Nguồn",
      Case: "Vỏ case",
      Cooling: "Tản nhiệt"
    }[slot] || slot;
  }

  function antiForgeryToken() {
    var token = document.querySelector('input[name="__RequestVerificationToken"]');
    return token ? token.value : "";
  }

  function escapeHtml(value) {
    return String(value || "").replace(/[&<>"']/g, function (ch) {
      return ({ "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;", "'": "&#039;" })[ch];
    });
  }

  function cssEscape(value) {
    return window.CSS && CSS.escape ? CSS.escape(value) : String(value).replace(/"/g, '\\"');
  }
})();
